using UnityEngine;

namespace Snow2.Enemies
{
    /// <summary>
    /// 飞行怪（蝙蝠）：
    /// - 不使用重力（Rigidbody2D.gravityScale = 0）
    /// - 支持两种模式：
    ///   1) 正弦波飞行（沿 X 方向前进 + Y 方向 Sin 摆动）
    ///   2) 追踪玩家（MoveTowards）
    /// - 颜色：由 EnemyDataSO.tintColor 决定（建议设为紫色/黑色）
    /// </summary>
    public sealed class FlyBat : EnemyBase
    {
        public enum FlyMode
        {
            SineWave,
            ChasePlayer
        }

        [Header("Fly")]
        [SerializeField] private FlyMode mode = FlyMode.SineWave;
        [SerializeField, Min(0f)] private float sineAmplitude = 0.8f;
        [SerializeField, Min(0.1f)] private float sineFrequency = 1.5f;
        [SerializeField, Min(0f)] private float chaseStopDistance = 0.2f;

        private Vector2 _startPos;

        protected override void Awake()
        {
            base.Awake();

            // 飞行怪默认不做地面巡逻逻辑。
            enablePatrol = false;

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }
        }

        protected override void Start()
        {
            base.Start();
            _startPos = transform.position;
        }

        protected override void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            var speed = Data != null ? Mathf.Max(0f, Data.moveSpeed) : 0f;
            var pos = rb.position;

            if (mode == FlyMode.SineWave)
            {
                // 沿面向方向前进 + Sin 波动
                pos.x += Dir * speed * Time.fixedDeltaTime;
                var t = Time.time * Mathf.Max(0.01f, sineFrequency);
                pos.y = _startPos.y + Mathf.Sin(t) * Mathf.Max(0f, sineAmplitude);

                // 避免飞出太远：简单在遇墙时反向
                if (ShouldFlipByWall())
                {
                    Flip();
                }
            }
            else
            {
                var player = FindPlayer();
                if (player != null)
                {
                    var target = (Vector2)player.transform.position;
                    var next = Vector2.MoveTowards(pos, target, speed * Time.fixedDeltaTime);
                    if ((target - pos).sqrMagnitude > chaseStopDistance * chaseStopDistance)
                    {
                        pos = next;
                    }

                    // 同步朝向
                    if (Mathf.Abs(target.x - pos.x) > 0.05f)
                    {
                        var wantRight = (target.x - pos.x) > 0f;
                        if (wantRight && Dir < 0f) Flip();
                        else if (!wantRight && Dir > 0f) Flip();
                    }
                }
            }

            rb.MovePosition(pos);
        }
    }
}

