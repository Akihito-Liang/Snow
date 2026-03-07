using UnityEngine;
using Snow2;
using Snow2.Player;

namespace Snow2.Enemies
{
    /// <summary>
    /// 投掷怪（蓝猴）：
    /// - 巡逻逻辑不变
    /// - 当检测到玩家在下方时（Physics2D.Raycast 向下），投掷一个受重力影响的 IceBlockPrefab
    /// - 颜色：由 EnemyDataSO.tintColor 决定（建议设为 Color.blue）
    /// </summary>
    public sealed class BlueMonkey : EnemyBase
    {
        [Header("Throw")]
        [SerializeField] private GameObject iceBlockPrefab;
        [SerializeField, Min(0.5f)] private float detectDownDistance = 6f;
        [SerializeField, Min(0.1f)] private float throwCooldownSeconds = 1.6f;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0.45f, 0.55f);
        [SerializeField] private Vector2 throwVelocity = new Vector2(2.2f, 5.2f);

        private float _nextThrowAt;

        protected override void Start()
        {
            base.Start();
            // 默认用 Data.attackRate 控制（若未配则 fallback 1.6 秒）。
            throwCooldownSeconds = Mathf.Max(0.1f, GetAttackIntervalSeconds(throwCooldownSeconds));
            _nextThrowAt = Time.time + 0.2f;
        }

        protected override void Tick(float dt)
        {
            if (Time.time < _nextThrowAt)
            {
                return;
            }

            var player = FindPlayer();
            if (player == null)
            {
                return;
            }

            if (!IsPlayerBelow(player))
            {
                return;
            }

            ThrowIceBlockAt(player.transform.position);
            _nextThrowAt = Time.time + throwCooldownSeconds;
        }

        private bool IsPlayerBelow(PlayerController2D player)
        {
            var origin = (Vector2)transform.position + Vector2.up * 0.2f;
            var hit = Physics2D.Raycast(origin, Vector2.down, Mathf.Max(0.1f, detectDownDistance));
            if (hit.collider == null) return false;

            return hit.collider.GetComponent<PlayerController2D>() != null;
        }

        private void ThrowIceBlockAt(Vector3 targetPos)
        {
            Attack();

            var spawnPos = (Vector2)transform.position + new Vector2(Mathf.Sign(Dir) * Mathf.Abs(spawnOffset.x), spawnOffset.y);

            GameObject go;
            if (iceBlockPrefab != null)
            {
                go = Instantiate(iceBlockPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // 兜底：动态生成一个方块投掷物（原型友好）。
                go = new GameObject("IceBlock");
                go.transform.position = spawnPos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
                sr.color = new Color(0.5f, 0.85f, 1f);
                sr.sortingOrder = 15;

                var box = go.AddComponent<BoxCollider2D>();
                box.size = Vector2.one * 0.6f;
            }

            var rb2 = go.GetComponent<Rigidbody2D>();
            if (rb2 == null) rb2 = go.AddComponent<Rigidbody2D>();
            rb2.gravityScale = 1f;
            rb2.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 简化投掷：按面向方向给一个初速度（可调），不做复杂弹道预测。
            var v = throwVelocity;
            v.x = Mathf.Abs(v.x) * Mathf.Sign(Dir);
            SetVelocity(rb2, v);

            Destroy(go, 6f);
        }
    }
}
