using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Snow2.Player;
using Snow2.Enemies;

namespace Snow2
{
    /// <summary>
    /// 《雪人兄弟》核心雪球：
    /// - 玩家接触后推动（水平初速度）
    /// - 物理滚动（Rigidbody2D + 重力）
    /// - 撞 Wall 反弹并计数，两次后碎裂
    /// - 滚动速度超过阈值时开启 Trigger，用于连击击杀
    ///
    /// 注意：本组件通过事件抛出“击杀/碎裂/连击变化”，不直接负责奖励生成。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Snowball : MonoBehaviour
    {
        public static event Action<Snowball> RollStarted;
        public static event Action<Snowball, int> ComboChanged;
        public static event Action<Snowball, Vector2> EnemyKilled;
        // 破碎/消失：breakPosition 为雪球消失点（用于在原地生成奖励）。
        public static event Action<Snowball, Vector2, IReadOnlyList<Vector2>, int> Broken;

        [Header("Push")]
        [SerializeField, Min(0f)] private float pushSpeed = 14f;
        [SerializeField] private bool pushOnContact = true;
        [SerializeField] private bool requireAttackToPush = false;
        [SerializeField, Min(0f)] private float pushCooldownSeconds = 0.10f;
        [SerializeField, Min(0f)] private float attackBufferSeconds = 0.15f;

        [Header("Rolling")]
        [SerializeField, Min(0f)] private float rollingSpeedThreshold = 3.5f;
        [SerializeField, Min(0f)] private float rollingDisableSpeedThreshold = 2.2f;
        [SerializeField, Min(0f)] private float triggerRadiusMultiplier = 1.10f;

        [Header("Absorb")]
        [SerializeField] private bool absorbEnemies = true;

        [Header("Wall Bounce")]
        // 不依赖 Tag/名字：用碰撞法线判断“是否撞到垂直墙面”。
        [SerializeField, Range(0.2f, 0.95f)] private float wallNormalXThreshold = 0.55f;
        [SerializeField] private bool playerActsAsWall = true;
        [SerializeField] private bool countPlayerHitAsWallBounce = false;
        [SerializeField, Range(0.1f, 1f)] private float wallBounceDamping = 0.92f;
        [SerializeField, Min(1)] private int maxWallBounces = 2;

        [Header("Physics (Anti-Slow)")]
        [SerializeField] private bool applyLowFrictionMaterial = true;
        [SerializeField, Range(0f, 2f)] private float maxLinearDamping = 0.01f;

        [Header("Despawn")]
        [SerializeField] private float despawnY = -12f;
        [SerializeField, Min(0f)] private float despawnViewportMargin = 0.25f;

        private Rigidbody2D _rb;
        private Collider2D _solidCol;
        private CircleCollider2D _triggerCol;

        private readonly List<Vector2> _killedEnemyPositions = new List<Vector2>(8);
        private int _comboCount;
        private int _wallBounceCount;

        private bool _runActive;
        private bool _rolling;

        private float _nextPushAt;
        private float _lastPushAt;
        private float _attackBufferedUntil;
        private bool _broken;

        private PlayerController2D _touchingPlayer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _solidCol = GetComponent<Collider2D>();

            // 兜底：保证雪球使用真实物理（受重力/可滚落平台）。
            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                // 冻结时 EnemyController 会把阻尼设大一点，这里兜底压低，避免雪球很快“刹停”。
                _rb.linearDamping = Mathf.Min(_rb.linearDamping, Mathf.Max(0f, maxLinearDamping));
            }

            if (applyLowFrictionMaterial)
            {
                ApplyLowFrictionToSolidColliders();
            }

            EnsureTriggerCollider();
        }

        private void Update()
        {
            if (!requireAttackToPush)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            {
                _attackBufferedUntil = Time.time + Mathf.Max(0f, attackBufferSeconds);
            }
#else
            if (Input.GetKeyDown(KeyCode.J))
            {
                _attackBufferedUntil = Time.time + Mathf.Max(0f, attackBufferSeconds);
            }
#endif
        }

        private void FixedUpdate()
        {
            if (_rb == null || _broken)
            {
                return;
            }

            var v = GetVelocity(_rb);
            var speed = v.magnitude;
            var enableT = Mathf.Max(0f, rollingSpeedThreshold);
            var disableT = Mathf.Clamp(Mathf.Max(0f, rollingDisableSpeedThreshold), 0f, enableT);

            var shouldRoll = _rolling ? (speed > disableT) : (speed > enableT);

            if (_rolling != shouldRoll)
            {
                _rolling = shouldRoll;
                if (_triggerCol != null)
                {
                    _triggerCol.enabled = _rolling;
                }
            }

            // 飞出屏幕/跌落销毁（触发奖励生成）。
            if (transform.position.y < despawnY)
            {
                BreakAndDestroy();
                return;
            }

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(transform.position);
                var m = Mathf.Max(0f, despawnViewportMargin);
                if (vp.x < -m || vp.x > 1f + m || vp.y < -m)
                {
                    BreakAndDestroy();
                }
            }
        }

        private void EnsureTriggerCollider()
        {
            // 需求：滚动速度 > 阈值时开启 Trigger 检测。
            // 同一个物体上叠一个 trigger collider，半径略大于实体 collider，减少“擦边漏判”。
            var cols = GetComponents<CircleCollider2D>();
            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].isTrigger)
                {
                    _triggerCol = cols[i];
                    break;
                }
            }

            if (_triggerCol == null)
            {
                _triggerCol = gameObject.AddComponent<CircleCollider2D>();
                _triggerCol.isTrigger = true;
            }

            // 参考一个实体圆形碰撞体的半径。
            var solidCircle = default(CircleCollider2D);
            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && !cols[i].isTrigger)
                {
                    solidCircle = cols[i];
                    break;
                }
            }

            if (solidCircle != null)
            {
                _triggerCol.radius = solidCircle.radius * Mathf.Max(1f, triggerRadiusMultiplier);
            }
            else
            {
                _triggerCol.radius = 0.55f;
            }

            _triggerCol.enabled = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_broken || collision == null || collision.collider == null)
            {
                return;
            }

            // 玩家接触推动（可选：需要攻击键）。
            var player = collision.collider.GetComponent<PlayerController2D>();
            if (player != null)
            {
                _touchingPlayer = player;
                if (pushOnContact && !requireAttackToPush)
                {
                    TryPushByPlayer(player);
                }

                // 雪球滚动回来撞到玩家：玩家也当作墙面处理（可选）。
                // 注意：刚推的一瞬间不应反弹，否则会把推动速度抵消，体感变“推不动/变慢”。
                if (playerActsAsWall && _runActive)
                {
                    if (Time.time - _lastPushAt > 0.06f)
                    {
                        if (TryBounceFromSurface(collision, countBounce: countPlayerHitAsWallBounce))
                        {
                            return;
                        }
                    }
                }

                // 与玩家发生碰撞时，不继续做“墙面反弹”检测。
                return;
            }

            // 兜底：即使 Trigger 因速度阈值未开启，雪球高速撞到敌人也应击杀/同化。
            var hitEnemy = collision.collider.GetComponentInParent<EnemyController>();
            if (hitEnemy != null && hitEnemy.IsNormal)
            {
                var vNow = GetVelocity(_rb);
                if (vNow.magnitude > Mathf.Max(0f, rollingDisableSpeedThreshold))
                {
                    HandleHitEnemy(hitEnemy);
                }
                return;
            }

            // 撞墙反弹：用碰撞法线判断是否撞到“垂直墙面”。
            if (TryBounceFromSurface(collision, countBounce: true))
            {
                return;
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_broken || collision == null || collision.collider == null)
            {
                return;
            }

            var player = collision.collider.GetComponent<PlayerController2D>();
            if (player == null)
            {
                return;
            }
            _touchingPlayer = player;

            if (!pushOnContact)
            {
                return;
            }
            if (!requireAttackToPush)
            {
                return;
            }

            if (Time.time <= _attackBufferedUntil)
            {
                TryPushByPlayer(player);
                _attackBufferedUntil = -999f;
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }
            if (_touchingPlayer != null && collision.collider.GetComponent<PlayerController2D>() == _touchingPlayer)
            {
                _touchingPlayer = null;
            }
        }

        private void TryPushByPlayer(PlayerController2D player)
        {
            if (_rb == null)
            {
                return;
            }
            if (Time.time < _nextPushAt)
            {
                return;
            }

            var facing = Mathf.Sign(player != null ? player.FacingX : 1f);
            if (Mathf.Abs(facing) < 0.01f)
            {
                facing = 1f;
            }

            if (!_runActive)
            {
                StartRun();
            }

            var v = GetVelocity(_rb);
            // 推动时不要把已经更快的雪球“推慢”。
            var target = Mathf.Max(Mathf.Abs(v.x), Mathf.Max(0f, pushSpeed));
            v.x = facing * target;
            SetVelocity(_rb, v);
            _nextPushAt = Time.time + Mathf.Max(0f, pushCooldownSeconds);
            _lastPushAt = Time.time;
        }

        private void StartRun()
        {
            _runActive = true;
            _comboCount = 0;
            _wallBounceCount = 0;
            _killedEnemyPositions.Clear();

            RollStarted?.Invoke(this);
            ComboChanged?.Invoke(this, _comboCount);
        }

        private bool TryBounceFromSurface(Collision2D collision, bool countBounce)
        {
            if (_rb == null)
            {
                return false;
            }

            if (collision.contactCount <= 0)
            {
                return false;
            }

            // 只在“左右墙面”反弹：法线 x 分量足够大。
            var hitWall = false;
            var n = Vector2.zero;
            for (var i = 0; i < collision.contactCount; i++)
            {
                var cn = collision.GetContact(i).normal;
                if (Mathf.Abs(cn.x) >= Mathf.Clamp01(wallNormalXThreshold))
                {
                    hitWall = true;
                    n = cn;
                    break;
                }
            }
            if (!hitWall)
            {
                return false;
            }

            // 忽略敌人本体（避免同化/击杀逻辑与反弹互相打架）
            if (collision.collider != null && collision.collider.GetComponentInParent<EnemyController>() != null)
            {
                return false;
            }

            var v = GetVelocity(_rb);
            var reflected = Vector2.Reflect(v, n) * Mathf.Clamp01(wallBounceDamping);

            // 避免反弹后速度过小导致“贴墙停住”。
            if (reflected.sqrMagnitude < 0.01f && v.sqrMagnitude > 0.01f)
            {
                reflected = v * -Mathf.Clamp01(wallBounceDamping);
            }

            SetVelocity(_rb, reflected);

            if (countBounce)
            {
                _wallBounceCount++;
                if (_wallBounceCount >= Mathf.Max(1, maxWallBounces))
                {
                    BreakAndDestroy();
                }
            }

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_broken || other == null)
            {
                return;
            }
            if (!_rolling)
            {
                return;
            }

            // 连击击杀：Trigger 命中 EnemyController（不依赖 Tag，避免 Tag 未定义报错）。
            var enemy = other.GetComponentInParent<EnemyController>();
            if (enemy == null)
            {
                return;
            }
            if (!enemy.IsNormal)
            {
                return;
            }

            HandleHitEnemy(enemy);
        }

        private void HandleHitEnemy(EnemyController enemy)
        {
            if (enemy == null || !enemy.IsNormal)
            {
                return;
            }

            var pos = (Vector2)enemy.transform.position;

            // 同化/击杀：默认把敌人卷入雪球内部随雪球移动。
            if (absorbEnemies)
            {
                enemy.AbsorbIntoSnowball(this, transform);
            }
            else
            {
                enemy.Kill(EnemyClearCause.SnowballImpact, this);
            }

            _comboCount++;
            _killedEnemyPositions.Add(pos);
            EnemyKilled?.Invoke(this, pos);
            ComboChanged?.Invoke(this, _comboCount);
        }

        private void ApplyLowFrictionToSolidColliders()
        {
            var mat = new PhysicsMaterial2D("Snowball_LowFriction")
            {
                friction = 0f,
                bounciness = 0f
            };

            var cols = GetComponents<Collider2D>();
            for (var i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || c.isTrigger)
                {
                    continue;
                }
                c.sharedMaterial = mat;
            }
        }

        // 旧的“按 Tag/名字识别墙”已移除：CompareTag 在 Tag 未配置时会抛异常，
        // 而名字识别容易受子节点/Collider 名称影响导致失效。

        private void BreakAndDestroy()
        {
            if (_broken)
            {
                return;
            }
            _broken = true;

            var breakPos = (Vector2)transform.position;

            Broken?.Invoke(this, breakPos, _killedEnemyPositions, _comboCount);
            Destroy(gameObject);
        }

        private static Vector2 GetVelocity(Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        private static void SetVelocity(Rigidbody2D rb, Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }
    }
}
