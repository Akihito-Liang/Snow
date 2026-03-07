using UnityEngine;
using UnityEngine.Events;
using Snow2;

namespace Snow2.Enemies
{
    /// <summary>
    /// 吐火怪：
    /// - 巡逻时每隔 3 秒停下攻击
    /// - 攻击时实例化 FirePrefab（若不配置则运行时生成一个简易火焰）
    /// - 火焰在地面停留 2 秒，形成持续伤害区域（本项目暂无玩家血量系统，此处仅提供 DamageZone 回调）
    /// - 颜色：由 EnemyDataSO.tintColor 决定（建议设为 Color.yellow）
    /// </summary>
    public sealed class YellowFatty : EnemyBase
    {
        [Header("Fire")]
        [SerializeField] private GameObject firePrefab;
        [SerializeField, Min(0.1f)] private float attackEverySeconds = 3.0f;
        [SerializeField, Min(0f)] private float attackWindupSeconds = 0.35f;
        [SerializeField, Min(0.1f)] private float fireDurationSeconds = 2.0f;
        [SerializeField] private Vector2 fireOffset = new Vector2(0.65f, 0.0f);
        [SerializeField, Min(0.1f)] private float fireRadius = 0.55f;

        [Header("Hooks")]
        public UnityEvent OnFireSpawned;

        private float _nextAttackAt;
        private bool _attacking;

        protected override void Start()
        {
            base.Start();
            _nextAttackAt = Time.time + Mathf.Max(0.1f, attackEverySeconds);
        }

        protected override void Tick(float dt)
        {
            if (_attacking)
            {
                return;
            }

            if (Time.time >= _nextAttackAt)
            {
                StartCoroutine(AttackRoutine());
            }
        }

        protected override void Patrol()
        {
            // 攻击时停下。
            if (_attacking)
            {
                return;
            }
            base.Patrol();
        }

        private System.Collections.IEnumerator AttackRoutine()
        {
            _attacking = true;

            // 预留：动画/音效/特效
            Attack();

            // 小停顿：模拟“抬手/蓄力”。
            var windup = Mathf.Max(0f, attackWindupSeconds);
            if (windup > 0f)
            {
                yield return new WaitForSeconds(windup);
            }

            SpawnFire();
            _nextAttackAt = Time.time + Mathf.Max(0.1f, attackEverySeconds);

            _attacking = false;
        }

        private void SpawnFire()
        {
            var pos = (Vector2)transform.position + new Vector2(Mathf.Sign(Dir) * Mathf.Abs(fireOffset.x), fireOffset.y);

            // 尽量把火焰贴地：往下找一下地面命中点。
            var hit = Physics2D.Raycast(pos + Vector2.up * 0.5f, Vector2.down, 3f, groundMask);
            if (hit.collider != null)
            {
                pos.y = hit.point.y + 0.02f;
            }

            GameObject go;
            if (firePrefab != null)
            {
                go = Instantiate(firePrefab, pos, Quaternion.identity);
            }
            else
            {
                // 无 prefab 时的兜底：动态创建一个可见的火焰区域（原型友好）。
                go = new GameObject("FireZone");
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteLibrary.CircleSprite;
                sr.color = new Color(1f, 0.35f, 0.05f, 0.75f);
                sr.sortingOrder = 15;
            }

            // 持续伤害区域：用 Trigger 圆形碰撞体表示。
            var col = go.GetComponent<CircleCollider2D>();
            if (col == null) col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.05f, fireRadius);

            var zone = go.GetComponent<DamageZone2D>();
            if (zone == null) zone = go.AddComponent<DamageZone2D>();
            zone.LifeSeconds = Mathf.Max(0.1f, fireDurationSeconds);
            zone.DamageTickSeconds = 0.25f;
            zone.TargetMask = ~0;
            zone.DebugTag = "Fire";

            OnFireSpawned?.Invoke();
        }
    }

    /// <summary>
    /// 通用持续伤害区域（简化版）。
    ///
    /// 说明：当前项目未实现玩家 HP/受伤系统，因此此组件默认只做“节流触发 + Hook 入口”。
    /// 你可以：
    /// - 给玩家脚本加一个 public void ApplyDamage(int amount) 并在这里调用
    /// - 或在 Inspector 里把 OnDamageTick 绑定到你自己的扣血逻辑
    /// </summary>
    public sealed class DamageZone2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifeSeconds = 2f;
        [SerializeField, Min(0.05f)] private float damageTickSeconds = 0.25f;
        [SerializeField] private LayerMask targetMask = ~0;

        [Header("Hook")]
        public UnityEvent OnDamageTick;

        // 仅用于调试/识别
        public string DebugTag;

        private float _dieAt;
        private float _nextTickAt;

        public float LifeSeconds { get => lifeSeconds; set => lifeSeconds = value; }
        public float DamageTickSeconds { get => damageTickSeconds; set => damageTickSeconds = value; }
        public LayerMask TargetMask { get => targetMask; set => targetMask = value; }

        private void OnEnable()
        {
            _dieAt = Time.time + Mathf.Max(0.1f, lifeSeconds);
            _nextTickAt = Time.time;
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null) return;
            if (other.isTrigger) return;
            if (((1 << other.gameObject.layer) & targetMask.value) == 0) return;

            if (Time.time < _nextTickAt)
            {
                return;
            }

            _nextTickAt = Time.time + Mathf.Max(0.05f, damageTickSeconds);
            OnDamageTick?.Invoke();
        }
    }
}
