using UnityEngine;
using UnityEngine.Events;

namespace Snow2.Enemies
{
    /// <summary>
    /// 相扑怪：
    /// - 仅上半身 Sprite（不影响逻辑）
    /// - 无法跳跃
    /// - 平时巡逻速度很快
    /// - 当玩家处于同一水平线（Y 差距小）时，发起高速冲撞（Dash）
    /// - 颜色：由 EnemyDataSO.tintColor 决定（建议设为肉色/棕色）
    /// </summary>
    public sealed class SumoBody : EnemyBase
    {
        [Header("Move")]
        [SerializeField, Min(0f)] private float minPatrolSpeed = 3.5f;

        [Header("Dash")]
        [SerializeField, Min(0.1f)] private float dashSpeed = 10f;
        [SerializeField, Min(0.05f)] private float dashDurationSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float dashCooldownSeconds = 2.0f;
        [SerializeField, Min(0f)] private float sameLineYThreshold = 0.35f;

        [Header("Hooks")]
        public UnityEvent OnDash;

        private float _dashEndAt;
        private float _nextDashAt;
        private bool _dashing;

        protected override void Start()
        {
            base.Start();
        }

        protected override void Tick(float dt)
        {
            if (_dashing)
            {
                if (Time.time >= _dashEndAt)
                {
                    _dashing = false;
                }
                return;
            }

            if (Time.time < _nextDashAt)
            {
                return;
            }

            var player = FindPlayer();
            if (player == null)
            {
                return;
            }

            var dy = Mathf.Abs(player.transform.position.y - transform.position.y);
            if (dy > sameLineYThreshold)
            {
                return;
            }

            StartDashTowards(player.transform.position);
        }

        protected override void Patrol()
        {
            // 冲刺时不巡逻。
            if (_dashing)
            {
                return;
            }

            if (rb == null)
            {
                return;
            }

            if (ShouldFlipByWall())
            {
                Flip();
            }

            var baseSpeed = Data != null ? Mathf.Max(0f, Data.moveSpeed) : 0f;
            var speed = Mathf.Max(baseSpeed, Mathf.Max(0f, minPatrolSpeed));
            var pos = rb.position;
            pos.x += Dir * speed * Time.fixedDeltaTime;
            rb.MovePosition(pos);
        }

        private void StartDashTowards(Vector3 playerPos)
        {
            if (rb == null)
            {
                return;
            }

            // 面向玩家
            var wantRight = (playerPos.x - transform.position.x) >= 0f;
            if (wantRight && Dir < 0f) Flip();
            else if (!wantRight && Dir > 0f) Flip();

            _dashing = true;
            _dashEndAt = Time.time + Mathf.Max(0.05f, dashDurationSeconds);
            _nextDashAt = Time.time + Mathf.Max(0.1f, dashCooldownSeconds);

            Attack();
            OnDash?.Invoke();

            // 冲刺用“设定水平速度”，不影响重力。
            var v = GetVelocity(rb);
            v.x = Mathf.Abs(dashSpeed) * Mathf.Sign(Dir);
            SetVelocity(rb, v);
        }
    }
}
