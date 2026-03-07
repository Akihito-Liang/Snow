using UnityEngine;

namespace Snow2.Enemies
{
    /// <summary>
    /// 小红怪：最基础 AI
    /// - 平台上持续左右巡逻
    /// - 每隔 Random.Range(2, 5) 秒随机跳跃一次
    /// - 颜色：由 EnemyDataSO.tintColor 决定（建议设为 Color.red）
    /// </summary>
    public sealed class RedDemon : EnemyBase
    {
        [Header("Random Jump")]
        [SerializeField] private Vector2 jumpIntervalRange = new Vector2(2f, 5f);

        private float _nextJumpAt;

        protected override void Start()
        {
            base.Start();
            ScheduleNextJump();
        }

        protected override void Tick(float dt)
        {
            if (rb == null)
            {
                return;
            }

            if (Time.time >= _nextJumpAt)
            {
                TryJump();
                ScheduleNextJump();
            }
        }

        private void ScheduleNextJump()
        {
            var min = Mathf.Max(0.1f, jumpIntervalRange.x);
            var max = Mathf.Max(min, jumpIntervalRange.y);
            _nextJumpAt = Time.time + Random.Range(min, max);
        }

        private void TryJump()
        {
            if (!IsGrounded())
            {
                return;
            }

            var jf = Data != null ? Mathf.Max(0f, Data.jumpForce) : 0f;
            if (jf <= 0.0001f)
            {
                return;
            }

            // 用“设定向上速度”的方式跳跃，控制更稳定。
            var v = GetVelocity(rb);
            v.y = jf;
            SetVelocity(rb, v);
        }
    }
}

