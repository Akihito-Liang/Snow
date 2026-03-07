using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Snow2;

namespace Snow2.Enemies
{
    /// <summary>
    /// 南瓜头 Boss（第 50 关）：
    /// - 屏幕顶部悬停
    /// - 随机选择 3 个点发射“全屏弹幕”
    /// - 瞬移到新位置
    ///
    /// 说明：此为通用框架示例，弹幕/瞬移/点位选择均做了可配置。
    /// 若你有自己的动画/特效系统，可绑定 UnityEvent Hook。
    /// </summary>
    public sealed class PumpkinBoss : EnemyBase
    {
        [Header("Boss (Hover)")]
        [SerializeField, Min(0f)] private float hoverYOffsetFromTop = 1.0f;
        [SerializeField, Min(0f)] private float hoverSmooth = 6f;

        [Header("Boss (Barrage)")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField, Min(1)] private int pointsPerCycle = 3;
        [SerializeField, Min(1)] private int bulletsPerPoint = 18;
        [SerializeField, Min(0.1f)] private float bulletSpeed = 6.5f;
        [SerializeField, Min(0.1f)] private float bulletLifeSeconds = 6f;
        [SerializeField, Min(0.1f)] private float timeBetweenPointsSeconds = 0.25f;

        [Header("Boss (Teleport)")]
        [SerializeField, Min(0.1f)] private float cycleIntervalSeconds = 1.2f;
        [SerializeField, Min(0f)] private float teleportMarginViewport = 0.12f;

        [Header("Hooks")]
        public UnityEvent OnCycleStarted;
        public UnityEvent OnTeleported;

        private Coroutine _loop;

        protected override void Awake()
        {
            base.Awake();

            // Boss 不走基础巡逻
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
            _loop = StartCoroutine(BossLoop());
        }

        private void LateUpdate()
        {
            // 顶部悬停（跟随相机顶部）
            var cam = UnityEngine.Camera.main;
            if (cam == null || rb == null)
            {
                return;
            }

            var topY = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f)).y;
            var targetY = topY - Mathf.Max(0f, hoverYOffsetFromTop);
            var p = rb.position;
            p.y = Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-Mathf.Max(0f, hoverSmooth) * Time.deltaTime));
            rb.MovePosition(p);
        }

        private IEnumerator BossLoop()
        {
            // 给一小段时间让相机/玩家初始化
            yield return null;
            yield return new WaitForSeconds(0.2f);

            while (true)
            {
                OnCycleStarted?.Invoke();

                // 1) 随机点位 + 弹幕
                for (var i = 0; i < Mathf.Max(1, pointsPerCycle); i++)
                {
                    FireAtRandomPoint();
                    yield return new WaitForSeconds(Mathf.Max(0.02f, timeBetweenPointsSeconds));
                }

                // 2) 瞬移
                TeleportToNewX();
                OnTeleported?.Invoke();

                yield return new WaitForSeconds(Mathf.Max(0.1f, cycleIntervalSeconds));
            }
        }

        private void FireAtRandomPoint()
        {
            Attack();

            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                return;
            }

            // 随机取屏幕内一点作为弹幕“发射源点”
            var m = Mathf.Clamp01(teleportMarginViewport);
            var vx = Random.Range(m, 1f - m);
            var vy = Random.Range(0.25f, 0.85f);
            var p = cam.ViewportToWorldPoint(new Vector3(vx, vy, 0f));
            p.z = 0f;

            SpawnBarrage((Vector2)p);
        }

        private void SpawnBarrage(Vector2 origin)
        {
            var n = Mathf.Max(1, bulletsPerPoint);
            for (var i = 0; i < n; i++)
            {
                // “全屏感”做法：从源点发射一圈弹（也可以替换成扇形/直线阵列）。
                var a = (i / (float)n) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                SpawnBullet(origin, dir * Mathf.Max(0f, bulletSpeed));
            }
        }

        private void SpawnBullet(Vector2 pos, Vector2 velocity)
        {
            GameObject go;
            if (bulletPrefab != null)
            {
                go = Instantiate(bulletPrefab, pos, Quaternion.identity);
            }
            else
            {
                // 兜底：动态创建一个圆形弹
                go = new GameObject("BossBullet");
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteLibrary.CircleSprite;
                sr.color = new Color(1f, 0.55f, 0.1f);
                sr.sortingOrder = 30;

                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.18f;
            }

            var rb2 = go.GetComponent<Rigidbody2D>();
            if (rb2 == null) rb2 = go.AddComponent<Rigidbody2D>();
            rb2.gravityScale = 0f;
            rb2.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            SetVelocity(rb2, velocity);

            Destroy(go, Mathf.Max(0.1f, bulletLifeSeconds));
        }

        private void TeleportToNewX()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                return;
            }

            var m = Mathf.Clamp01(teleportMarginViewport);
            var left = cam.ViewportToWorldPoint(new Vector3(m, 0f, 0f)).x;
            var right = cam.ViewportToWorldPoint(new Vector3(1f - m, 0f, 0f)).x;
            var x = Random.Range(Mathf.Min(left, right), Mathf.Max(left, right));

            var p = transform.position;
            p.x = x;
            p.z = 0f;
            transform.position = p;
        }
    }
}
