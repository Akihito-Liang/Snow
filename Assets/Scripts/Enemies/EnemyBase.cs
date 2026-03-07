using UnityEngine;
using UnityEngine.Events;
using Snow2.Player;

namespace Snow2.Enemies
{
    /// <summary>
    /// 《雪人兄弟》通用敌人 AI 基类：
    /// - 数据驱动（EnemyDataSO）
    /// - 基础巡逻（左右移动，遇墙反转）
    /// - 受伤入口（TakeDamage -> 默认变雪球）
    /// - 为子类“特殊攻击/行为”预留 Hook（虚函数 + UnityEvent）
    ///
    /// 使用建议：
    /// - 把 EnemyBase 派生脚本挂到敌人 Prefab 根对象
    /// - 根对象应包含：SpriteRenderer + Rigidbody2D + Collider2D（Box/Circle 均可）
    /// - 将 EnemyDataSO 拖到 Data 字段，运行时会按 tintColor 自动上色
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        // RaycastNonAlloc 复用缓冲，避免频繁 GC。
        private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[8];

        [Header("Data")]
        [SerializeField] private EnemyDataSO data;

        [Header("Refs")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected Collider2D bodyCollider;

        [Header("Patrol")]
        [SerializeField] protected bool enablePatrol = true;
        [SerializeField] protected bool startFacingRight = true;
        [SerializeField, Min(0.02f)] protected float wallCheckDistance = 0.12f;
        [SerializeField] protected LayerMask obstacleMask = ~0;

        [Header("Ground Check (Jump 用)")]
        [SerializeField, Min(0.02f)] protected float groundCheckDistance = 0.08f;
        [SerializeField] protected LayerMask groundMask = ~0;

        [Header("Events")]
        /// <summary>子类发起攻击时可 Invoke（用于动画/音效/可视化）。</summary>
        public UnityEvent OnAttack;
        /// <summary>受击时可 Invoke（用于受击闪烁/音效）。</summary>
        public UnityEvent OnDamaged;
        /// <summary>变雪球时可 Invoke（用于特效）。</summary>
        public UnityEvent OnBecameSnowball;

        protected float Dir { get; private set; } = 1f;
        protected bool IsSnowball { get; private set; }

        protected EnemyDataSO Data => data;

        /// <summary>
        /// 运行时注入/覆盖数据（便于关卡脚本动态生成敌人时使用）。
        /// 注意：若你在 Inspector 里已配置 data，一般不需要调用。
        /// </summary>
        public void SetData(EnemyDataSO newData)
        {
            data = newData;

            // 若已经有渲染器，立即刷新颜色（便于运行时生成时可见）。
            if (spriteRenderer != null && data != null)
            {
                spriteRenderer.color = data.tintColor;
            }
        }

        protected virtual void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
        }

        protected virtual void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();

            // 运行时把敌人放到独立 Layer（Enemy），便于与 Door 做层级隔离。
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                gameObject.layer = enemyLayer;
            }

            Dir = startFacingRight ? 1f : -1f;

            // 兜底：避免场景误配置导致“敌人完全不动”。
            if (rb != null)
            {
                rb.simulated = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        protected virtual void Start()
        {
            // 外观控制：按 ScriptableObject 的 tintColor 动态上色，便于区分不同敌人。
            if (spriteRenderer != null && data != null)
            {
                spriteRenderer.color = data.tintColor;
            }
        }

        protected virtual void Update()
        {
            // 子类可 override Update 写特殊逻辑；建议仍然调用 base.Update() 保留 Tick。
            Tick(Time.deltaTime);
        }

        protected virtual void FixedUpdate()
        {
            if (!enablePatrol || IsSnowball)
            {
                return;
            }

            Patrol();
        }

        /// <summary>
        /// 每帧逻辑入口（默认空实现）。子类可在此做更细粒度的 AI Tick。
        /// </summary>
        protected virtual void Tick(float dt) { }

        /// <summary>
        /// 通用巡逻：按 Data.moveSpeed 左右移动，遇墙反转。
        /// 子类可 override 以定制“停下/加速/冲刺”等。
        /// </summary>
        protected virtual void Patrol()
        {
            if (rb == null)
            {
                return;
            }

            if (ShouldFlipByWall())
            {
                Flip();
            }

            var speed = data != null ? Mathf.Max(0f, data.moveSpeed) : 0f;
            var pos = rb.position;
            pos.x += Dir * speed * Time.fixedDeltaTime;
            rb.MovePosition(pos);
        }

        /// <summary>
        /// 受伤入口：默认直接变雪球（与《雪人兄弟》核心规则一致）。
        /// 若你希望“多段受击才变雪球”，可在子类里累积命中次数后再调用 base.TakeDamage。
        /// </summary>
        public virtual void TakeDamage(int amount = 1)
        {
            if (IsSnowball)
            {
                return;
            }

            OnDamaged?.Invoke();

            BecomeSnowball();
        }

        /// <summary>
        /// 变雪球：挂载现有 Snow2.Snowball 玩法组件并停止 AI。
        /// 说明：本项目已有 Snowball 玩法脚本（用于推/撞墙碎裂/连击等），此处直接复用。
        /// </summary>
        protected virtual void BecomeSnowball()
        {
            IsSnowball = true;
            enablePatrol = false;

            if (rb != null)
            {
                rb.simulated = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 1f;
                rb.freezeRotation = false;
            }

            // 复用现有雪球玩法组件（namespace 为 Snow2）。
            if (GetComponent<Snow2.Snowball>() == null)
            {
                gameObject.AddComponent<Snow2.Snowball>();
            }

            // AI 自身关闭，避免与雪球脚本冲突。
            enabled = false;

            OnBecameSnowball?.Invoke();
        }

        /// <summary>
        /// 子类特殊攻击入口（可在 Update/Tick 中按需要调用）。
        /// </summary>
        protected virtual void Attack()
        {
            OnAttack?.Invoke();
        }

        /// <summary>
        /// 供子类检测：玩家引用（未找到则为 null）。
        /// </summary>
        protected PlayerController2D FindPlayer()
        {
            // FindAnyObjectByType 在 Unity 2022+ 可用；项目当前 Unity 6 可用。
            return FindAnyObjectByType<PlayerController2D>();
        }

        protected void Flip()
        {
            Dir = -Dir;

            // 视觉翻转（可选）：用 localScale.x 表达朝向。
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (Dir >= 0f ? 1f : -1f);
            transform.localScale = s;
        }

        protected bool IsGrounded()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            var b = bodyCollider.bounds;
            var origin = new Vector2(b.center.x, b.min.y + 0.02f);
            var dist = Mathf.Max(0.02f, groundCheckDistance);
            // 忽略 Trigger（例如出口门/拾取物），避免误判“没踩地”。
            return RaycastFirstNonTrigger(origin, Vector2.down, dist, groundMask, out _);
        }

        protected bool ShouldFlipByWall()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            var b = bodyCollider.bounds;
            var origin = new Vector2(b.center.x, b.center.y);
            var dir = Vector2.right * Mathf.Sign(Dir);
            var dist = Mathf.Max(0.02f, b.extents.x + wallCheckDistance);

            // 忽略 Trigger（例如出口门是 Trigger），只用实体碰撞体判断“墙”。
            return RaycastFirstNonTrigger(origin, dir, dist, obstacleMask, out var hit) && hit.collider != null;
        }

        private bool RaycastFirstNonTrigger(Vector2 origin, Vector2 dir, float dist, LayerMask mask, out RaycastHit2D hit)
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = mask
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

        protected static Vector2 GetVelocity(Rigidbody2D body)
        {
            if (body == null) return Vector2.zero;
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        protected static void SetVelocity(Rigidbody2D body, Vector2 v)
        {
            if (body == null) return;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = v;
#else
            body.velocity = v;
#endif
        }

        /// <summary>
        /// 工具：由 attackRate（次/秒）换算出的最小攻击间隔（秒）。
        /// </summary>
        protected float GetAttackIntervalSeconds(float fallbackSeconds = 3f)
        {
            if (data == null)
            {
                return Mathf.Max(0.02f, fallbackSeconds);
            }

            var rate = Mathf.Max(0f, data.attackRate);
            if (rate <= 0.0001f)
            {
                return Mathf.Max(0.02f, fallbackSeconds);
            }

            return Mathf.Max(0.02f, 1f / rate);
        }
    }
}
