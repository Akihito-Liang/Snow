using UnityEngine;
using Snow2.Enemies;

namespace Snow2.Projectiles
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SnowballProjectile : MonoBehaviour
    {
        public Vector2 Direction = Vector2.right;
        public float Speed = 16f;
        public float LifetimeSeconds = 2f;

        private Rigidbody2D _rb;
        private float _dieAt;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            SetVelocity(_rb, Direction.normalized * Speed);
            _dieAt = Time.time + LifetimeSeconds;
        }

        private static void SetVelocity(Rigidbody2D rb, Vector2 v)
        {
            if (rb == null)
            {
                return;
            }
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
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

            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.ApplySnowHit();
                Destroy(gameObject);
                return;
            }

            // 命中任意非触发碰撞体（墙/地/台阶）也消失
            if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
