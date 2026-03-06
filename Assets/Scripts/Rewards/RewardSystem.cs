using System.Collections.Generic;
using UnityEngine;

namespace Snow2.Rewards
{
    /// <summary>
    /// 奖励系统：监听 Snowball 事件，在雪球碎裂/消失时生成掉落。
    ///
    /// 规则：
    /// - 每卷入（击杀）的敌人位置生成 1 个寿司
    /// - 若本次雪球连击 comboCount > 3，额外生成 1 个药水
    ///
    /// 说明：
    /// - 若 SushiPrefab/PotionPrefab 未设置，会回退到“运行时动态创建 PickupItem”的最小实现，便于原型阶段调参。
    /// </summary>
    public sealed class RewardSystem : MonoBehaviour
    {
        [Header("Prefabs (Optional)")]
        [SerializeField] private GameObject sushiPrefab;
        [SerializeField] private GameObject potionPrefab;

        [Header("Fallback Pickup (When Prefab is null)")]
        [SerializeField] private int sushiScore = 10;
        [SerializeField] private float spawnYOffset = 0.6f;
        [SerializeField] private float pickupDespawnY = -12f;

        private void OnEnable()
        {
            Snowball.Broken += OnSnowballBroken;
        }

        private void OnDisable()
        {
            Snowball.Broken -= OnSnowballBroken;
        }

        private void OnSnowballBroken(Snowball snowball, Vector2 breakPosition, IReadOnlyList<Vector2> enemyPositions, int comboCount)
        {
            if (snowball == null)
            {
                return;
            }

            // 雪球消失点生成奖励（同一位置轻微散开）。
            var center = breakPosition + new Vector2(0f, spawnYOffset);

            // 每个被卷入敌人生成 1 个寿司。
            if (enemyPositions != null)
            {
                for (var i = 0; i < enemyPositions.Count; i++)
                {
                    var offset = UnityEngine.Random.insideUnitCircle * 0.35f;
                    SpawnSushi(center + offset);
                }
            }

            // 连击奖励：comboCount > 3 额外掉 1 个药水。
            if (comboCount > 3)
            {
                SpawnPotion(center + UnityEngine.Random.insideUnitCircle * 0.25f);
            }
        }

        private void SpawnSushi(Vector2 position)
        {
            if (sushiPrefab != null)
            {
                Instantiate(sushiPrefab, position, Quaternion.identity);
                return;
            }

            SpawnFallbackPickup(position, PickupType.Sushi);
        }

        private void SpawnPotion(Vector2 position)
        {
            if (potionPrefab != null)
            {
                Instantiate(potionPrefab, position, Quaternion.identity);
                return;
            }

            SpawnFallbackPickup(position, PickupType.Potion);
        }

        private void SpawnFallbackPickup(Vector2 position, PickupType type)
        {
            var go = new GameObject(type == PickupType.Sushi ? "Pickup_Sushi" : "Pickup_Potion");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            // 对应外观：寿司=方块，药水=圆形（便于一眼区分）。
            sr.sprite = type == PickupType.Potion ? RuntimeSpriteLibrary.CircleSprite : RuntimeSpriteLibrary.WhiteSprite;
            sr.color = type == PickupType.Sushi ? new Color(1f, 0.85f, 0.1f) : new Color(0.2f, 0.9f, 0.35f);
            sr.sortingOrder = 20;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var pickup = go.AddComponent<PickupItem>();
            pickup.Type = type;
            pickup.SushiScore = sushiScore;
            pickup.DespawnY = pickupDespawnY;
        }
    }
}
