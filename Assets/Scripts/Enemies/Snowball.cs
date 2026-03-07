using System;
using System.Collections.Generic;
using System.Text;
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
        // 需求变更：雪球不应因为“离开屏幕/相机视口”而平白无故消失。
        // 如需旧行为（离开视口后自动碎裂并销毁），可手动打开该开关。
        [SerializeField] private bool enableAutoDespawn = false;
        [SerializeField] private float despawnY = -12f;
        [SerializeField, Min(0f)] private float despawnViewportMargin = 0.25f;

        [Header("Debug (Snow2Bounce)")]
        // 仅用于排查：默认关闭，避免刷屏。
        [SerializeField] private bool bounceDebugLogs;
        [SerializeField, Min(0f)] private float bounceDebugCooldownSeconds = 0.05f;
        private float _nextBounceDebugAt;

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

            if (enableAutoDespawn)
            {
                // 可选：飞出屏幕/跌落后碎裂并销毁（会触发奖励生成）。
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

            // 说明：这里不要用“每次 BounceDbg() 打一行”的方式。
            // 我们有 cooldown（防刷屏），如果第一行打完就进入冷却，后续关键日志会丢失。
            // 因此：单次碰撞收集信息，最后汇总输出 1 行。
            var sb = default(StringBuilder);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var doLog = bounceDebugLogs && (Time.unscaledTime >= _nextBounceDebugAt);
            if (doLog)
            {
                sb = new StringBuilder(512);
                var v0 = GetVelocity(_rb);
                sb.Append($"name={name} other={collision.collider.name} otherTrig={collision.collider.isTrigger} contacts={collision.contactCount}");
                sb.Append($" rbV0=({v0.x:0.###},{v0.y:0.###}) relV=({collision.relativeVelocity.x:0.###},{collision.relativeVelocity.y:0.###})");
                sb.Append($" thrNx={Mathf.Clamp01(wallNormalXThreshold):0.###} damping={Mathf.Clamp01(wallBounceDamping):0.###}");
                sb.Append($" runActive={_runActive} rolling={_rolling} wallBounces={_wallBounceCount}");
            }
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (sb != null)
                {
                    sb.Append(" | hitWall=false normals=");
                    for (var i = 0; i < collision.contactCount; i++)
                    {
                        var c = collision.GetContact(i);
                        sb.Append($"[{i}]n=({c.normal.x:0.###},{c.normal.y:0.###})sep={c.separation:0.###}p=({c.point.x:0.###},{c.point.y:0.###}) ");
                    }

                    Debug.Log($"[Snow2Bounce] {sb}", this);
                    _nextBounceDebugAt = Time.unscaledTime + Mathf.Max(0f, bounceDebugCooldownSeconds);
                }
#endif
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (sb != null)
            {
                sb.Append($" | hitWall=true n0=({n.x:0.###},{n.y:0.###})");
            }
#endif

            // 忽略敌人本体（避免同化/击杀逻辑与反弹互相打架）
            if (collision.collider != null && collision.collider.GetComponentInParent<EnemyController>() != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (sb != null)
                {
                    sb.Append(" | ignoreReason=enemyCollider");
                    Debug.Log($"[Snow2Bounce] {sb}", this);
                    _nextBounceDebugAt = Time.unscaledTime + Mathf.Max(0f, bounceDebugCooldownSeconds);
                }
#endif
                return false;
            }

            // 关键修复：用“入射速度”而不是当前 rb 速度。
            // Unity 的 2D 物理解算有时会在回调触发前把 rb 速度解算成接近 0，
            // 此时直接 Reflect(rb.velocity) 会导致“撞墙后停住”。
            // 入射速度优先用 rb 当前速度；如果它已被物理解算“清零”，再回退用 relativeVelocity。
            // 注意：对静态墙来说，relativeVelocity 近似等于 (other - self) = -selfVel，所以需要取反。
            var rbV = GetVelocity(_rb);
            var incident = rbV;
            var usedRelative = false;
            if (incident.sqrMagnitude < 0.0004f)
            {
                usedRelative = true;
                incident = -collision.relativeVelocity;
            }

            // 确保法线朝向正确：incident 应该指向墙面（与法线点积为负）。
            if (Vector2.Dot(incident, n) > 0f)
            {
                n = -n;
            }

            var damping = Mathf.Clamp01(wallBounceDamping);
            var reflected = Vector2.Reflect(incident, n) * damping;

            // 避免反弹后速度过小导致“贴墙停住”。
            // 若 Reflect 结果太小，则退化为“仅翻转 X 速度”的墙面反弹。
            if (reflected.sqrMagnitude < 0.01f && incident.sqrMagnitude > 0.01f)
            {
                reflected = incident;
                reflected.x = -reflected.x;
                reflected *= damping;
            }

            // 最终兜底：如果仍然太小，给一个最小水平反弹速度（保留原有 y 方向）。
            if (reflected.sqrMagnitude < 0.01f && incident.sqrMagnitude > 0.01f)
            {
                var minSpeed = Mathf.Max(1.5f, incident.magnitude * 0.35f);
                var dirX = -Mathf.Sign(Mathf.Abs(incident.x) > 0.01f ? incident.x : n.x);
                reflected = new Vector2(dirX * minSpeed, incident.y * damping);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (sb != null)
            {
                sb.Append($" | rbV=({rbV.x:0.###},{rbV.y:0.###})");
                sb.Append($" incident=({incident.x:0.###},{incident.y:0.###}) usedRel={usedRelative}");
                sb.Append($" n=({n.x:0.###},{n.y:0.###}) dot={Vector2.Dot(incident, n):0.###}");
                sb.Append($" reflected=({reflected.x:0.###},{reflected.y:0.###}) countBounce={countBounce}");
            }
#endif

            SetVelocity(_rb, reflected);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (sb != null)
            {
                var vAfter = GetVelocity(_rb);
                sb.Append($" | rbVAfter=({vAfter.x:0.###},{vAfter.y:0.###})");
            }
#endif

            if (countBounce)
            {
                _wallBounceCount++;
                if (_wallBounceCount >= Mathf.Max(1, maxWallBounces))
                {
                    BreakAndDestroy();
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (sb != null)
            {
                Debug.Log($"[Snow2Bounce] {sb}", this);
                _nextBounceDebugAt = Time.unscaledTime + Mathf.Max(0f, bounceDebugCooldownSeconds);
            }
#endif

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
