using UnityEngine;
using Snow2;
using Snow2.Player;
using Snow2.PowerUps;
using Snow2.Rewards;
using Snow2.Balance;

namespace Snow2.Enemies
{
    public sealed class EnemyController : MonoBehaviour
    {
        // RaycastNonAlloc 复用缓冲，避免频繁 GC。
        private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[8];

        public int HitsToFreeze = 3;
        public float KillSpeedThreshold = 4.2f;
        public float PatrolSpeed = 1.6f;
        public float PatrolDistance = 2.5f;

        [Header("Navigation")]
        [Min(0.02f)]
        public float WallCheckDistance = 0.12f;
        [Min(0.02f)]
        public float GroundAheadCheckDepth = 0.6f;
        [Min(0.0f)]
        public float GroundAheadInset = 0.03f;
        [Min(0f)]
        public float MaxStepDownHeight = 0f;

        [Header("Bump Bounce (Player)")]
        [Min(0f)]
        public float PlayerBumpBounceMultiplier = 0.9f;
        [Min(0f)]
        public float PlayerBumpMinSpeed = 3.0f;
        [Min(0f)]
        public float PlayerBumpDamping = 12f;
        [Min(0f)]
        public float PlayerBumpCooldownSeconds = 0.08f;

        // 数值集中在 Snow2.Balance.EnemyDropBalance

        private enum State
        {
            Normal,
            Frozen,
            Dead
        }

        private State _state = State.Normal;
        private int _snowHits;

        private SpriteRenderer _sr;
        private Rigidbody2D _rb;
        private BoxCollider2D _box;
        private CircleCollider2D _circle;

        // 冻结过渡：红方块 -> 白圆形（用一个叠加的圆形 SpriteRenderer 渐显）
        private SpriteRenderer _snowCircleOverlay;

        private float _dir = 1f;
        private bool _registered;

        private Vector2 _playerBumpVelocity;
        private float _nextPlayerBumpAt;

        private Collider2D _bodyCol;

        // Custom gravity (match PlayerController2D)
        private float _verticalVelocity;
        private bool _grounded;
        private float _gravityScale = 3f;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];

        private bool _deathDropDone;

        // 旧调试日志（Snow2DBG2）已停用：如需排查请在对应模块打开新的专用日志开关。
        [Header("Debug (Snow2DBG2) (Deprecated)")]
        public bool DebugLogs2;
        [Min(0.05f)]
        public float DebugLogIntervalSeconds2 = 0.8f;
        private float _nextDebugAt;

        private static readonly Color NormalColor = new Color(0.9f, 0.1f, 0.1f);

        public bool IsNormal => _state == State.Normal;
        public bool IsFrozen => _state == State.Frozen;
        public bool IsDead => _state == State.Dead;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _box = GetComponent<BoxCollider2D>();
            _circle = GetComponent<CircleCollider2D>();

            // 运行时把敌人放到独立 Layer（Enemy），便于与 Door 做层级隔离。
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                gameObject.layer = enemyLayer;
            }

            // 运行时兜底：保证敌人是 Dynamic + 可模拟，并锁 Z 旋转。
            // （如果场景里误改为 Static/不模拟，巡逻速度写入不会生效，看起来就“敌人不动”。）
            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.freezeRotation = true;

                // 与玩家一致：使用自定义重力（见 FixedUpdate），避免 MovePosition 影响 Unity 2D 重力积分。
                _rb.gravityScale = 0f;
            }

            // 优先用 Box（Normal 状态），否则用 Circle（Frozen 状态）
            _bodyCol = _box != null ? (Collider2D)_box : (Collider2D)_circle;
            if (_bodyCol == null)
            {
                _bodyCol = GetComponent<Collider2D>();
            }

            // Enemy 由运行时创建时，Start 可能晚于关卡初始化结算；
            // 这里在 Awake 里注册，确保初始敌人数量统计正确。
            TryRegister();
        }

        private void Start()
        {
            if (_sr != null)
            {
                if (_sr.sprite == null)
                {
                    _sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
                }
                _sr.color = NormalColor;
            }

            // 优先用 Box（Normal 状态），否则用 Circle（Frozen 状态）
            _bodyCol = _box != null ? (Collider2D)_box : (Collider2D)_circle;
            if (_bodyCol == null)
            {
                _bodyCol = GetComponent<Collider2D>();
            }

            // 重力倍率与玩家保持一致（若玩家不存在则用默认值）
            var player = FindAnyObjectByType<PlayerController2D>();
            if (player != null)
            {
                _gravityScale = Mathf.Max(0f, player.GravityScale);
            }

            // 奖励/药水不会提前展示，统一在死亡瞬间随机生成并掉落。
        }

        private void TryRegister()
        {
            if (_registered)
            {
                return;
            }
            if (GameManager.Instance == null)
            {
                return;
            }
            GameManager.Instance.RegisterEnemy(gameObject);
            _registered = true;
        }

        private void FixedUpdate()
        {
            if (_state != State.Normal)
            {
                return;
            }
            if (_rb == null)
            {
                return;
            }

            // 外力/反弹速度逐步衰减（用于敌人与玩家接触时的“弹开”）。
            if (_playerBumpVelocity.sqrMagnitude > 0.0001f)
            {
                var k = 1f - Mathf.Exp(-Mathf.Max(0f, PlayerBumpDamping) * Time.fixedDeltaTime);
                _playerBumpVelocity = Vector2.Lerp(_playerBumpVelocity, Vector2.zero, k);
            }
            else
            {
                _playerBumpVelocity = Vector2.zero;
            }

            UpdateGroundedFromContacts();
            if (_grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }

            // 自定义重力积分（与玩家一致）
            _verticalVelocity += Physics2D.gravity.y * Mathf.Max(0f, _gravityScale) * Time.fixedDeltaTime;

            var flipReason = 0; // 0=none 1=wall 2=edge
            if (ShouldFlipByWall()) flipReason = 1;
            else if (ShouldFlipByEdge()) flipReason = 2;

            if (flipReason != 0)
            {
                _dir = -_dir;
            }

            // 与 Player 保持一致：用 MovePosition 做水平移动。
            var rbPos = _rb.position;
            var beforeX = rbPos.x;
            rbPos.x += (_dir * PatrolSpeed + _playerBumpVelocity.x) * Time.fixedDeltaTime;
            rbPos.y += (_verticalVelocity + _playerBumpVelocity.y) * Time.fixedDeltaTime;
            _rb.MovePosition(rbPos);

            // (旧日志已移除) [Snow2DBG2][Enemy]
        }

        private bool ShouldFlipByWall()
        {
            if (_bodyCol == null)
            {
                return false;
            }

            var b = _bodyCol.bounds;
            var origin = new Vector2(b.center.x, b.center.y);
            var dir = Vector2.right * Mathf.Sign(_dir);
            // 预判“下一步”会不会撞到：把本帧位移也算进去。
            var dist = Mathf.Max(0.02f, b.extents.x + WallCheckDistance + Mathf.Abs(PatrolSpeed) * Time.fixedDeltaTime);

            // 关键：忽略 Trigger（例如出口门是 Trigger），否则在“起点位于 Trigger 内”时会被误判成无地/撞墙。
            return RaycastFirstNonTrigger(origin, dir, dist, ~0, out _);
        }

        private bool ShouldFlipByEdge()
        {
            if (_bodyCol == null)
            {
                return false;
            }

            var b = _bodyCol.bounds;
            var sign = Mathf.Sign(_dir);
            // 用“下一步的位置”做前方落脚点探测，尽量在走出边缘之前就反向。
            var nextStep = Mathf.Abs(PatrolSpeed) * Time.fixedDeltaTime;
            var footY = b.min.y + 0.03f;

            // 两条探测线：前脚 + 稍微内侧一点，任意一条无地/落差过大都反向。
            var originFront = new Vector2(b.center.x + sign * (b.extents.x + GroundAheadInset + nextStep), footY);
            var originInner = new Vector2(b.center.x + sign * Mathf.Max(0.02f, b.extents.x * 0.5f), footY);

            if (!HasGroundWithin(originFront) || !HasGroundWithin(originInner))
            {
                return true;
            }

            return false;
        }

        private bool HasGroundWithin(Vector2 origin)
        {
            if (!RaycastFirstNonTrigger(origin, Vector2.down, Mathf.Max(0.02f, GroundAheadCheckDepth), ~0, out var hit))
            {
                return false;
            }

            // 不允许“下台阶/下落”：如果前方地面比当前脚底低太多，视为悬崖。
            var drop = origin.y - hit.point.y;
            if (drop > Mathf.Max(0f, MaxStepDownHeight))
            {
                return false;
            }

            return true;
        }

        private bool RaycastFirstNonTrigger(Vector2 origin, Vector2 dir, float dist, int layerMask, out RaycastHit2D hit)
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = layerMask
            };

            var count = Physics2D.Raycast(origin, dir, filter, _rayHits, Mathf.Max(0.02f, dist));
            for (var i = 0; i < count; i++)
            {
                var h = _rayHits[i];
                if (h.collider == null) continue;
                if (h.collider.transform == transform) continue;
                hit = h;
                return true;
            }

            hit = default;
            return false;
        }

        public void ApplySnowHit()
        {
            if (_state != State.Normal)
            {
                return;
            }

            _snowHits++;
            var t = Mathf.Clamp01(_snowHits / Mathf.Max(1f, HitsToFreeze));
            if (_sr != null)
            {
                // 逐步冻结：底层方块逐渐变白并淡出；上层圆形逐渐淡入。
                EnsureSnowOverlay();

                var baseCol = Color.Lerp(NormalColor, Color.white, t);
                baseCol.a = 1f - t;
                _sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
                _sr.color = baseCol;

                if (_snowCircleOverlay != null)
                {
                    var c = Color.white;
                    c.a = t;
                    _snowCircleOverlay.sprite = RuntimeSpriteLibrary.CircleSprite;
                    _snowCircleOverlay.color = c;
                }
            }

            if (_snowHits >= HitsToFreeze)
            {
                FreezeFully();
            }
        }

        private void FreezeFully()
        {
            if (_state != State.Normal)
            {
                return;
            }

            _state = State.Frozen;

            // 切回真实物理：雪球应受 Unity 2D 重力影响
            _grounded = false;
            _verticalVelocity = 0f;

            // 冻结后变成“可推动雪球”。
            if (GetComponent<Snowball>() == null)
            {
                gameObject.AddComponent<Snowball>();
            }

            if (_rb != null)
            {
                var v = GetVelocity(_rb);
                v.x = 0f;
                SetVelocity(_rb, v);
                _rb.freezeRotation = false;
                _rb.linearDamping = 0.05f;
                _rb.angularDamping = 0.02f;
                _rb.mass = 2.0f;
                _rb.gravityScale = 1f;
            }

            if (_box != null)
            {
                _box.enabled = false;
            }
            if (_circle != null)
            {
                _circle.enabled = true;
            }

            _bodyCol = _circle != null ? (Collider2D)_circle : _bodyCol;

            // 变成“雪球”后外观为白色
            if (_sr != null)
            {
                _sr.sprite = RuntimeSpriteLibrary.CircleSprite;
                _sr.color = Color.white;
                if (_snowCircleOverlay != null)
                {
                    Destroy(_snowCircleOverlay.gameObject);
                    _snowCircleOverlay = null;
                }
            }

            // 给一点点旋转/滚动初速度，让推动更有“球”感（不会太强）
            if (_rb != null)
            {
                _rb.AddTorque(Random.Range(-0.6f, 0.6f), ForceMode2D.Impulse);
            }
        }

        private void EnsureSnowOverlay()
        {
            if (_snowCircleOverlay != null)
            {
                return;
            }
            if (_sr == null)
            {
                return;
            }

            var go = new GameObject("SnowCircleOverlay");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _snowCircleOverlay = go.AddComponent<SpriteRenderer>();
            _snowCircleOverlay.sprite = RuntimeSpriteLibrary.CircleSprite;
            _snowCircleOverlay.color = new Color(1f, 1f, 1f, 0f);
            _snowCircleOverlay.sortingLayerID = _sr.sortingLayerID;
            _snowCircleOverlay.sortingOrder = _sr.sortingOrder + 1;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            // Normal：与任何“非触发器”发生水平碰撞（包含敌人之间互撞）就反向。
            if (_state == State.Normal)
            {
                // 与玩家接触：按“入射速度”反射弹开，避免双方互相顶住卡死。
                if (collision.collider.GetComponent<PlayerController2D>() != null)
                {
                    TryBounceFromPlayer(collision);
                }

                if (!collision.collider.isTrigger)
                {
                    for (var i = 0; i < collision.contactCount; i++)
                    {
                        var n = collision.GetContact(i).normal;
                        if (Mathf.Abs(n.x) >= 0.5f)
                        {
                            _dir = -_dir;

                            // (旧日志已移除) [Snow2DBG2][Enemy][BumpFlip]

                            break;
                        }
                    }
                }

                return;
            }

            // Frozen：保持原有雪球逻辑
            if (_state != State.Frozen)
            {
                return;
            }

            // 若已挂载 Snowball 组件，则由 Snowball 负责反弹/连击击杀等玩法逻辑。
            if (GetComponent<Snowball>() != null)
            {
                return;
            }

            var otherEnemy = collision.collider.GetComponent<EnemyController>();
            if (otherEnemy != null && otherEnemy._state == State.Normal)
            {
                var speed = collision.relativeVelocity.magnitude;
                if (speed >= KillSpeedThreshold)
                {
                    otherEnemy.Kill(EnemyClearCause.SnowballImpact, null);
                }

                return;
            }

            // 冻结雪球撞到墙体：做一个“水平反弹”的最小实现（无需 PhysicsMaterial2D 资产）。
            if (_rb == null)
            {
                return;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                var n = collision.GetContact(i).normal;
                if (Mathf.Abs(n.x) > 0.5f)
                {
                    var v = GetVelocity(_rb);
                    v.x = -v.x;
                    SetVelocity(_rb, v);
                    break;
                }
            }
        }

        private void TryBounceFromPlayer(Collision2D collision)
        {
            if (_rb == null)
            {
                return;
            }
            if (Time.time < _nextPlayerBumpAt)
            {
                return;
            }
            if (collision.contactCount <= 0)
            {
                return;
            }

            // 以敌人当前“意图速度”（巡逻 + 既有外力）作为入射速度。
            var incident = new Vector2(_dir * PatrolSpeed + _playerBumpVelocity.x, _playerBumpVelocity.y);
            if (incident.sqrMagnitude < 0.0001f)
            {
                // 兜底：把相对速度反向，近似当作“自己撞上去的速度”。
                incident = -collision.relativeVelocity;
            }

            var n = collision.GetContact(0).normal;
            if (Vector2.Dot(incident, n) > 0f)
            {
                n = -n;
            }

            var reflected = Vector2.Reflect(incident, n);
            var speed = Mathf.Max(Mathf.Max(0f, PlayerBumpMinSpeed), incident.magnitude) * Mathf.Max(0f, PlayerBumpBounceMultiplier);
            if (reflected.sqrMagnitude < 0.0001f)
            {
                reflected = n;
            }

            _playerBumpVelocity = reflected.normalized * speed;
            _verticalVelocity = 0f;
            _grounded = false;
            _nextPlayerBumpAt = Time.time + Mathf.Max(0f, PlayerBumpCooldownSeconds);
        }

        private void UpdateGroundedFromContacts()
        {
            if (_bodyCol == null)
            {
                _grounded = false;
                return;
            }

            _grounded = false;
            var count = _bodyCol.GetContacts(_contacts);
            for (var i = 0; i < count; i++)
            {
                var c = _contacts[i];
                if (c.collider == null)
                {
                    continue;
                }
                if (c.collider.isTrigger)
                {
                    continue;
                }
                if (c.collider.GetComponent<PlayerController2D>() != null)
                {
                    continue;
                }
                if (c.collider.GetComponent<EnemyController>() != null)
                {
                    continue;
                }
                if (c.normal.y >= 0.6f)
                {
                    _grounded = true;
                    return;
                }
            }
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

        public void Kill(EnemyClearCause cause, Snowball sourceSnowball)
        {
            if (_state == State.Dead)
            {
                return;
            }

            Die(cause, sourceSnowball);
        }

        // 统一“死亡结算”入口：掉落、计分、注销、销毁。
        private void Die(EnemyClearCause cause, Snowball sourceSnowball)
        {
            _state = State.Dead;

            TryDropDeathItem();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEnemyCleared(transform.position, cause, sourceSnowball);
                GameManager.Instance.UnregisterEnemy(gameObject);
            }

            _registered = false;

            Destroy(gameObject);
        }

        public void AbsorbIntoSnowball(Snowball sourceSnowball, Transform snowballTransform)
        {
            if (_state == State.Dead)
            {
                return;
            }

            _state = State.Dead;

            // 卷入雪球也算被消灭：奖励在“死亡瞬间”原地掉落
            TryDropDeathItem();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEnemyCleared(transform.position, EnemyClearCause.SnowballImpact, sourceSnowball);
                GameManager.Instance.UnregisterEnemy(gameObject);
            }

            _registered = false;

            // 卷入雪球：禁用碰撞/刚体模拟/AI，让其跟随雪球移动。
            if (_box != null) _box.enabled = false;
            if (_circle != null) _circle.enabled = false;
            if (_bodyCol != null) _bodyCol.enabled = false;
            if (_rb != null) _rb.simulated = false;

            var sr = _sr;
            if (sr != null)
            {
                sr.color = new Color(0.8f, 0.9f, 1f, 0.65f);
                sr.sortingOrder = sr.sortingOrder - 1;
            }

            if (snowballTransform != null)
            {
                transform.SetParent(snowballTransform, worldPositionStays: true);
                // 轻微偏移，让“卷入雪球内部”更明显。
                transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.08f, 0.08f), UnityEngine.Random.Range(-0.08f, 0.08f), 0f);
                transform.localScale = transform.localScale * 0.65f;
            }

            enabled = false;
        }

        private void TryDropDeathItem()
        {
            if (_deathDropDone)
            {
                return;
            }
            _deathDropDone = true;

            if (!EnemyDropBalance.EnableDeathDrop)
            {
                return;
            }
            if (Random.value > Mathf.Clamp01(EnemyDropBalance.DeathDropChance))
            {
                return;
            }

            var pW = Mathf.Max(0f, EnemyDropBalance.PotionWeight);
            var cW = Mathf.Max(0f, EnemyDropBalance.CakeWeight);
            var sW = Mathf.Max(0f, EnemyDropBalance.SnackWeight);
            var sum = pW + cW + sW;
            if (sum <= 0.0001f)
            {
                return;
            }

            var pos = (Vector2)transform.position + new Vector2(0f, Mathf.Max(0f, EnemyDropBalance.DropYOffset));
            var roll = Random.value * sum;
            if (roll < pW)
            {
                SpawnRandomPotionAt(pos);
            }
            else if (roll < pW + cW)
            {
                SpawnScorePickupAt(pos, isCake: true);
            }
            else
            {
                SpawnScorePickupAt(pos, isCake: false);
            }
        }

        private static int GetPickupLayer()
        {
            var layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName1);
            if (layer == -1) layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName2);
            if (layer == -1) layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName3);
            // 兜底：用 Unity 内置的 Ignore Raycast 层（一定存在），至少保证和 Default/Enemy 分离。
            if (layer == -1) layer = EnemyDropBalance.FallbackPickupLayer;
            return layer;
        }

        private static void SpawnRandomPotionAt(Vector2 position)
        {
            var roll = Random.Range(0, 4);
            var color = Color.white;

            var go = new GameObject("PowerUp_Potion");
            go.layer = GetPickupLayer();
            go.transform.position = position;
            go.transform.localScale = Vector3.one * EnemyDropBalance.PotionScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSpriteLibrary.TriangleSprite;
            sr.sortingOrder = EnemyDropBalance.PotionSortingOrder;

            // 先加物理/碰撞体，再加具体道具脚本（确保 Awake 顺序）
            go.AddComponent<PowerUpDropBody>();

            switch (roll)
            {
                case 0:
                    go.AddComponent<RedPotion>();
                    color = PotionBalance.RedColor;
                    break;
                case 1:
                    go.AddComponent<BluePotion>();
                    color = PotionBalance.BlueColor;
                    break;
                case 2:
                    go.AddComponent<YellowPotion>();
                    color = PotionBalance.YellowColor;
                    break;
                default:
                    go.AddComponent<GreenPotion>();
                    color = PotionBalance.GreenColor;
                    break;
            }
            sr.color = color;
        }

        private void SpawnScorePickupAt(Vector2 position, bool isCake)
        {
            var go = new GameObject(isCake ? "Pickup_Cake" : "Pickup_Snack");
            go.layer = GetPickupLayer();
            go.transform.position = position;
            go.transform.localScale = Vector3.one * EnemyDropBalance.CakeSnackScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = isCake ? RuntimeSpriteLibrary.TriangleSprite : RuntimeSpriteLibrary.CircleSprite;
            sr.color = isCake ? EnemyDropBalance.CakeColor : EnemyDropBalance.SnackColor;
            sr.sortingOrder = EnemyDropBalance.RewardSortingOrder;

            var pickup = go.AddComponent<PickupItem>();
            pickup.Type = isCake ? PickupType.Cake : PickupType.Snack;
            pickup.RewardScore = isCake ? EnemyDropBalance.CakeScore : EnemyDropBalance.SnackScore;
            pickup.DespawnY = -12f;
        }

        private void OnDestroy()
        {
            // 如果由于场景卸载等原因被销毁，确保不留下注册。
            if (_registered && GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterEnemy(gameObject);
            }
        }
    }
}
