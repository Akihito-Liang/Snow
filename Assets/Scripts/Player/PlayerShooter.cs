using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Snow2.Projectiles;

namespace Snow2.Player
{
    [RequireComponent(typeof(PlayerController2D))]
    public sealed class PlayerShooter : MonoBehaviour
    {
        public float ProjectileSpeed = 16f;
        public float FireCooldownSeconds = 0.22f;

        private PlayerController2D _player;
        private float _nextFireAt;

        private void Awake()
        {
            _player = GetComponent<PlayerController2D>();
        }

        private void Update()
        {
            if (Time.time < _nextFireAt)
            {
                return;
            }

            var fire = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                fire = true;
            }
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            {
                fire = true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                fire = true;
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                fire = true;
            }
#endif

            if (!fire)
            {
                return;
            }

            var dir = GetAimDirection();
            SpawnProjectile(dir);
            _nextFireAt = Time.time + FireCooldownSeconds;
        }

        private Vector2 GetAimDirection()
        {
            var cam = UnityEngine.Camera.main;

#if ENABLE_INPUT_SYSTEM
            if (cam != null && Mouse.current != null)
            {
                var mousePos = Mouse.current.position.ReadValue();
                var world = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
                var dir = (Vector2)(world - transform.position);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    return dir.normalized;
                }
            }
#else
            if (cam != null)
            {
                var mousePos = Input.mousePosition;
                var world = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
                var dir = (Vector2)(world - transform.position);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    return dir.normalized;
                }
            }
#endif

            return new Vector2(Mathf.Sign(_player.FacingX), 0f);
        }

        private void SpawnProjectile(Vector2 dir)
        {
            var go = new GameObject("SnowballProjectile");
            go.transform.position = transform.position + (Vector3)(dir * 0.8f);
            go.transform.localScale = Vector3.one * 0.3f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
            sr.color = Color.white;
            sr.sortingOrder = 30;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var proj = go.AddComponent<SnowballProjectile>();
            proj.LifetimeSeconds = 2.0f;
            proj.Speed = ProjectileSpeed;
            proj.Direction = dir;
        }
    }
}
