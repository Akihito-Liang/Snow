using UnityEngine;

namespace Snow2.Enemies
{
    public sealed class EnemyController : MonoBehaviour
    {
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

        private float _dir = 1f;
        private bool _registered;

        private Collider2D _bodyCol;

        [Header("Debug (Snow2DBG2)")]
        public bool DebugLogs2;
        [Min(0.05f)]
        public float DebugLogIntervalSeconds2 = 0.8f;
        private float _nextDebugAt;

        private static readonly Color NormalColor = new Color(0.9f, 0.1f, 0.1f);

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _box = GetComponent<BoxCollider2D>();
            _circle = GetComponent<CircleCollider2D>();

            // 运行时兜底：保证敌人是 Dynamic + 可模拟，并锁 Z 旋转。
            // （如果场景里误改为 Static/不模拟，巡逻速度写入不会生效，看起来就“敌人不动”。）
            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.freezeRotation = true;
            }

            // Enemy 由运行时创建时，Start 可能晚于关卡初始化结算；
            // 这里在 Awake 里注册，确保初始敌人数量统计正确。
            TryRegister();
        }

        private void Start()
        {
            if (_sr != null)
            {
                _sr.color = NormalColor;
            }

            // 优先用 Box（Normal 状态），否则用 Circle（Frozen 状态）
            _bodyCol = _box != null ? (Collider2D)_box : (Collider2D)_circle;
            if (_bodyCol == null)
            {
                _bodyCol = GetComponent<Collider2D>();
            }
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
            rbPos.x += _dir * PatrolSpeed * Time.fixedDeltaTime;
            _rb.MovePosition(rbPos);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugLogs2 && Time.unscaledTime >= _nextDebugAt)
            {
                _nextDebugAt = Time.unscaledTime + Mathf.Max(0.05f, DebugLogIntervalSeconds2);
                var reason = flipReason == 1 ? "wall" : (flipReason == 2 ? "edge" : "-");
                Debug.Log($"[Snow2DBG2][Enemy] name={name} x={beforeX:0.###}->{rbPos.x:0.###} dir={_dir:0.###} flip={reason} dt={Time.fixedDeltaTime:0.#####}", this);
            }
#endif
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

            var hit = Physics2D.Raycast(origin, dir, dist);
            if (hit.collider == null)
            {
                return false;
            }
            if (hit.collider.isTrigger)
            {
                return false;
            }
            if (hit.collider.transform == transform)
            {
                return false;
            }
            return true;
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
            var hit = Physics2D.Raycast(origin, Vector2.down, Mathf.Max(0.02f, GroundAheadCheckDepth));
            if (hit.collider == null)
            {
                return false;
            }
            if (hit.collider.isTrigger)
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
                _sr.color = Color.Lerp(NormalColor, Color.white, t);
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

            if (_rb != null)
            {
                var v = GetVelocity(_rb);
                v.x = 0f;
                SetVelocity(_rb, v);
                _rb.freezeRotation = false;
                _rb.linearDamping = 0.05f;
                _rb.angularDamping = 0.02f;
                _rb.mass = 2.0f;
            }

            if (_box != null)
            {
                _box.enabled = false;
            }
            if (_circle != null)
            {
                _circle.enabled = true;
            }

            // 变成“雪球”后外观为白色
            if (_sr != null)
            {
                _sr.color = Color.white;
            }

            // 给一点点旋转/滚动初速度，让推动更有“球”感（不会太强）
            if (_rb != null)
            {
                _rb.AddTorque(Random.Range(-0.6f, 0.6f), ForceMode2D.Impulse);
            }
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
                if (!collision.collider.isTrigger)
                {
                    for (var i = 0; i < collision.contactCount; i++)
                    {
                        var n = collision.GetContact(i).normal;
                        if (Mathf.Abs(n.x) >= 0.5f)
                        {
                            _dir = -_dir;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (DebugLogs2)
                            {
                                Debug.Log($"[Snow2DBG2][Enemy][BumpFlip] name={name} other={collision.collider.name} nx={n.x:0.###}", this);
                            }
#endif

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

            var otherEnemy = collision.collider.GetComponent<EnemyController>();
            if (otherEnemy != null && otherEnemy._state == State.Normal)
            {
                var speed = collision.relativeVelocity.magnitude;
                if (speed >= KillSpeedThreshold)
                {
                    otherEnemy.Kill(EnemyClearCause.SnowballImpact);
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnEnemyCleared(otherEnemy.transform.position, EnemyClearCause.SnowballImpact);
                    }
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

        private void Kill(EnemyClearCause cause)
        {
            if (_state == State.Dead)
            {
                return;
            }
            _state = State.Dead;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterEnemy(gameObject);
            }

            _registered = false;

            Destroy(gameObject);
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
