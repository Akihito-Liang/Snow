using UnityEngine;
using Snow2.Player;
using Snow2.Enemies;
using Snow2.Balance;

namespace Snow2.PowerUps
{
    /// <summary>
    /// Power-Up 物理/碰撞设置：
    /// - 一个实体 Collider（非 Trigger）负责落地/停在台阶上
    /// - 一个 Trigger Collider 负责拾取判定（由 PowerUpItem 处理）
    /// - 实体 Collider 与玩家忽略碰撞，避免把玩家顶飞/卡住
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerUpDropBody : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField, Min(0f)] private float gravityScale = 2f;
        [SerializeField, Min(0.01f)] private float solidRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float triggerRadiusMultiplier = 1.15f;

        private Collider2D _solidCollider;
        private Collider2D _triggerCollider;

        private static PhysicsMaterial2D _lowFrictionMat;

        private void Awake()
        {
            EnsureSetup();

            // 让药水尽量在独立图层（若项目已配置 Pickup/PowerUp 图层则使用；否则退化到 Ignore Raycast）。
            var layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName1);
            if (layer == -1) layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName2);
            if (layer == -1) layer = LayerMask.NameToLayer(EnemyDropBalance.PickupLayerName3);
            if (layer == -1) layer = EnemyDropBalance.FallbackPickupLayer;
            gameObject.layer = layer;
        }

        private void Start()
        {
            IgnoreSolidCollisionWithPlayer();
            IgnoreSolidCollisionWithEnemies();
        }

        private void EnsureSetup()
        {
            // Rigidbody2D（用于掉落/落地）
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = Mathf.Max(0f, gravityScale);
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 实体碰撞体
            var solid = FindCircle(isTrigger: false);
            if (solid == null)
            {
                solid = gameObject.AddComponent<CircleCollider2D>();
            }
            solid.isTrigger = false;
            solid.radius = Mathf.Max(0.01f, solidRadius);
            _solidCollider = solid;

            // 拾取触发器
            var trigger = FindCircle(isTrigger: true);
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<CircleCollider2D>();
            }
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(trigger.radius, solid.radius * Mathf.Max(1f, triggerRadiusMultiplier));
            _triggerCollider = trigger;

            // 低摩擦，避免卡台阶/乱弹
            if (_lowFrictionMat == null)
            {
                _lowFrictionMat = new PhysicsMaterial2D("PowerUp_LowFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
            solid.sharedMaterial = _lowFrictionMat;
        }

        private CircleCollider2D FindCircle(bool isTrigger)
        {
            var circles = GetComponents<CircleCollider2D>();
            for (var i = 0; i < circles.Length; i++)
            {
                var c = circles[i];
                if (c != null && c.isTrigger == isTrigger)
                {
                    return c;
                }
            }
            return null;
        }

        private void IgnoreSolidCollisionWithPlayer()
        {
            if (_solidCollider == null)
            {
                return;
            }

            var players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null)
                {
                    continue;
                }
                var cols = p.GetComponentsInChildren<Collider2D>(includeInactive: true);
                for (var j = 0; j < cols.Length; j++)
                {
                    var pc = cols[j];
                    if (pc == null)
                    {
                        continue;
                    }
                    Physics2D.IgnoreCollision(_solidCollider, pc, true);
                }
            }
        }

        private void IgnoreSolidCollisionWithEnemies()
        {
            if (_solidCollider == null)
            {
                return;
            }

            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (var i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null)
                {
                    continue;
                }
                var cols = e.GetComponentsInChildren<Collider2D>(includeInactive: true);
                for (var j = 0; j < cols.Length; j++)
                {
                    var ec = cols[j];
                    if (ec == null)
                    {
                        continue;
                    }
                    Physics2D.IgnoreCollision(_solidCollider, ec, true);
                }
            }
        }
    }
}
