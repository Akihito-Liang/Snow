using UnityEngine;
using Snow2.Player;
using Snow2.Enemies;
using Snow2.Balance;

namespace Snow2.Rewards
{
    public enum PickupType
    {
        Sushi,
        Potion,
        Cake,
        Snack,
    }

    public sealed class PickupItem : MonoBehaviour
    {
        public PickupType Type;
        public int SushiScore = 10;
        public int RewardScore = 15;
        public float DespawnY = -12f;

        // 需求：掉落物应该能“落在地面/台阶上”，而不是穿透。
        // 做法：
        // - 一个实体 Collider（非 Trigger）负责与地形/台阶发生物理碰撞
        // - 一个 Trigger Collider 仅用于玩家拾取判定
        // - 实体 Collider 与玩家 Collider 忽略碰撞，避免把玩家顶飞/卡住

        private Collider2D _solidCollider;
        private Collider2D _pickupTrigger;

        private static PhysicsMaterial2D _pickupMaterial;

        private void Awake()
        {
            EnsurePhysicsSetup();

            // 让拾取物尽量在独立图层（若项目已配置 Pickup/PowerUp 图层则使用；否则退化到 Ignore Raycast）。
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

        private void EnsurePhysicsSetup()
        {
            // Rigidbody2D
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = Mathf.Max(0f, rb.gravityScale);
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = Mathf.Max(rb.linearDamping, 0.8f);
            rb.angularDamping = Mathf.Max(rb.angularDamping, 2.0f);

            // 实体碰撞体：优先复用已存在的 CircleCollider2D（生成逻辑里通常会先加一个 trigger）
            var circles = GetComponents<CircleCollider2D>();
            CircleCollider2D solidCircle = null;
            for (var i = 0; i < circles.Length; i++)
            {
                var c = circles[i];
                if (c != null && !c.isTrigger)
                {
                    solidCircle = c;
                    break;
                }
            }

            if (solidCircle == null)
            {
                // 复用第一个 CircleCollider2D（无论是否 trigger），否则新增
                for (var i = 0; i < circles.Length; i++)
                {
                    if (circles[i] != null)
                    {
                        solidCircle = circles[i];
                        break;
                    }
                }
            }

            if (solidCircle == null)
            {
                solidCircle = gameObject.AddComponent<CircleCollider2D>();
                solidCircle.radius = 0.5f;
            }

            solidCircle.isTrigger = false;
            _solidCollider = solidCircle;

            // 拾取触发器：确保存在一个 isTrigger=true 的 CircleCollider2D（半径略大，避免擦边漏判）
            CircleCollider2D triggerCircle = null;
            circles = GetComponents<CircleCollider2D>();
            for (var i = 0; i < circles.Length; i++)
            {
                var c = circles[i];
                if (c != null && c.isTrigger)
                {
                    triggerCircle = c;
                    break;
                }
            }
            if (triggerCircle == null)
            {
                triggerCircle = gameObject.AddComponent<CircleCollider2D>();
                triggerCircle.isTrigger = true;
            }

            triggerCircle.radius = Mathf.Max(triggerCircle.radius, solidCircle.radius * 1.15f);
            _pickupTrigger = triggerCircle;

            // 降低摩擦/弹性，避免掉落物与台阶“黏住”或乱弹
            if (_pickupMaterial == null)
            {
                _pickupMaterial = new PhysicsMaterial2D("Pickup_LowFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
            solidCircle.sharedMaterial = _pickupMaterial;
        }

        private void IgnoreSolidCollisionWithPlayer()
        {
            if (_solidCollider == null)
            {
                return;
            }

            // 只忽略“实体 collider”与玩家的碰撞；拾取触发器保留
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

        private void Update()
        {
            if (transform.position.y < DespawnY)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            // 玩家 Collider 可能在子节点上，组件在父节点上。
            if (other.GetComponentInParent<PlayerController2D>() == null)
            {
                return;
            }

            if (GameManager.Instance != null)
            {
                switch (Type)
                {
                    case PickupType.Potion:
                        GameManager.Instance.AddPotion(1);
                        break;
                    case PickupType.Sushi:
                        GameManager.Instance.AddScore(SushiScore);
                        break;
                    case PickupType.Cake:
                    case PickupType.Snack:
                        GameManager.Instance.AddScore(Mathf.Max(0, RewardScore));
                        break;
                }
            }

            Destroy(gameObject);
        }
    }
}
