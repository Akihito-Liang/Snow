using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Snow2.Enemies;

namespace Snow2.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        public float MoveSpeed = 7f;
        public float JumpImpulse = 11f;

        [Header("Ground Check")]
        [Range(0.4f, 0.95f)]
        public float GroundNormalThreshold = 0.6f;

        [Header("Jump")]
        [Min(0f)]
        public float JumpBufferSeconds = 0.12f;
        [Min(0f)]
        public float CoyoteTimeSeconds = 0.10f;

        [Header("Gravity (Custom)")]
        [Min(0f)]
        public float GravityScale = 3f;

        [Header("Bump Bounce (Enemy)")]
        [Min(0f)]
        public float EnemyBumpBounceMultiplier = 1.0f;
        [Min(0f)]
        public float EnemyBumpMinSpeed = 6f;
        [Min(0f)]
        public float EnemyBumpDamping = 10f;
        [Min(0f)]
        public float EnemyBumpCooldownSeconds = 0.08f;

        [Header("Debug (Snow2DBG2)")]
        public bool DebugLogs2;
        [Min(0.05f)]
        public float DebugLogIntervalSeconds2 = 0.5f;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private float _moveX;
        private bool _grounded;

        private float _jumpBufferedUntil;
        private float _lastGroundedAt = -999f;
        private float _jumpBlockLoggedUntil;

        private float _verticalVelocity;

        private Vector2 _enemyBumpVelocity;
        private float _nextEnemyBumpAt;

        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private int _lastContactCount;
        private float _lastMaxGroundNormalY;
        private float _nextDebugAt;

#if ENABLE_INPUT_SYSTEM
        private InputAction _moveAction;
        private InputAction _jumpAction;
#endif

        public float FacingX { get; private set; } = 1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();

            // 运行时兜底：保证玩家是 Dynamic + 可模拟，并锁 Z 旋转。
            // 这样即使场景里 Rigidbody2D 被误改（例如变成 Static/不模拟），也能正常移动。
            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.freezeRotation = true;

                // 使用自定义重力（见 FixedUpdate），避免 MovePosition 影响 Unity 2D 重力积分。
                _rb.gravityScale = 0f;
            }
        }

        private void Start()
        {
            DebugLogs2 = true;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            EnsureActions();
            _moveAction?.Enable();
            _jumpAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
        }

        private void EnsureActions()
        {
            if (_moveAction != null && _jumpAction != null)
            {
                return;
            }

            // Move: 键盘 A/D/←/→ + 手柄左摇杆 X
            // 用 1DAxis（而不是 2DVector）避免某些配置下未绑定 Up/Down 导致读值恒为 0 的情况。
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
            var compositeWasd = _moveAction.AddCompositeBinding("1DAxis");
            compositeWasd.With("Negative", "<Keyboard>/a");
            compositeWasd.With("Positive", "<Keyboard>/d");

            var compositeArrows = _moveAction.AddCompositeBinding("1DAxis");
            compositeArrows.With("Negative", "<Keyboard>/leftArrow");
            compositeArrows.With("Positive", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick/x");

            // Jump: Space/W/↑ + 手柄 A(南键)
            _jumpAction = new InputAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Keyboard>/w");
            _jumpAction.AddBinding("<Keyboard>/upArrow");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }
#endif

        private void Update()
        {
            _moveX = 0f;

            float rawMove = 0f;
            var usedFallback = false;

            var jumpPressedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
            EnsureActions();

            rawMove = _moveAction != null ? _moveAction.ReadValue<float>() : 0f;
            _moveX = Mathf.Clamp(rawMove, -1f, 1f);

            // 兜底：有些项目把 Input System 更新模式改到 FixedUpdate 或出现焦点问题时，ReadValue 可能一直为 0。
            // 这里直接用 Keyboard 当前态再补一次。
            if (Mathf.Abs(_moveX) < 0.01f && Keyboard.current != null)
            {
                usedFallback = true;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) _moveX = -1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) _moveX = 1f;
            }

            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                jumpPressedThisFrame = true;
            }
#endif

#if !ENABLE_INPUT_SYSTEM
            // 旧输入系统兜底（当项目未启用 Input System 或编译宏不可用时）。
            usedFallback = true;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) _moveX = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) _moveX = 1f;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                jumpPressedThisFrame = true;
            }
#endif

            if (jumpPressedThisFrame)
            {
                _jumpBufferedUntil = Time.time + Mathf.Max(0f, JumpBufferSeconds);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugLogs2)
                {
                    Debug.Log($"[Snow2DBG2][Player][JumpPress] t={Time.time:0.###} grounded={_grounded} bufferUntil={_jumpBufferedUntil:0.###}", this);
                }
#endif
            }

            if (Mathf.Abs(_moveX) > 0.01f)
            {
                FacingX = Mathf.Sign(_moveX);
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null)
            {
                return;
            }

            // 外力/反弹速度逐步衰减（用于玩家与敌人接触时的“弹开”）。
            if (_enemyBumpVelocity.sqrMagnitude > 0.0001f)
            {
                var k = 1f - Mathf.Exp(-Mathf.Max(0f, EnemyBumpDamping) * Time.fixedDeltaTime);
                _enemyBumpVelocity = Vector2.Lerp(_enemyBumpVelocity, Vector2.zero, k);
            }
            else
            {
                _enemyBumpVelocity = Vector2.zero;
            }

            UpdateGroundedFromContacts();

            if (_grounded)
            {
                _lastGroundedAt = Time.time;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = 0f;
                }
            }

            var buffered = Time.time <= _jumpBufferedUntil;
            var withinCoyote = (Time.time - _lastGroundedAt) <= Mathf.Max(0f, CoyoteTimeSeconds);
            if (buffered && withinCoyote)
            {
                // 只允许“借力跳”：地面接触（或 CoyoteTime 内）才会赋予向上初速度。
                _verticalVelocity = JumpImpulse;
                _jumpBufferedUntil = -999f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugLogs2)
                {
                    Debug.Log($"[Snow2DBG2][Player][JumpDo] t={Time.time:0.###} lastGroundedAgo={(Time.time - _lastGroundedAt):0.###} maxNy={_lastMaxGroundNormalY:0.###} contacts={_lastContactCount}", this);
                }
#endif
            }

            // 自定义重力积分（Physics2D.gravity.y 默认约为 -9.81）
            _verticalVelocity += Physics2D.gravity.y * Mathf.Max(0f, GravityScale) * Time.fixedDeltaTime;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugLogs2 && buffered && !withinCoyote && _jumpBufferedUntil > _jumpBlockLoggedUntil)
            {
                _jumpBlockLoggedUntil = _jumpBufferedUntil;
                var vNow = GetVelocity(_rb);
                Debug.Log($"[Snow2DBG2][Player][JumpBlocked] t={Time.time:0.###} grounded={_grounded} groundedAgo={(Time.time - _lastGroundedAt):0.###} maxNy={_lastMaxGroundNormalY:0.###} contacts={_lastContactCount} v=({vNow.x:0.###},{vNow.y:0.###})", this);
            }
#endif

            // 自定义速度 -> 位移，并用 MovePosition 推进
            var rbPos = _rb.position;
            rbPos.x += (_moveX * MoveSpeed + _enemyBumpVelocity.x) * Time.fixedDeltaTime;
            rbPos.y += (_verticalVelocity + _enemyBumpVelocity.y) * Time.fixedDeltaTime;
            _rb.MovePosition(rbPos);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugLogs2 && Time.unscaledTime >= _nextDebugAt)
            {
                _nextDebugAt = Time.unscaledTime + Mathf.Max(0.05f, DebugLogIntervalSeconds2);
                var rbPosAfter = _rb.position;
                var vNow = GetVelocity(_rb);
                var bufferLeft = Mathf.Max(0f, _jumpBufferedUntil - Time.time);
                var groundedAgo = Time.time - _lastGroundedAt;
                Debug.Log($"[Snow2DBG2][Player] pos=({rbPosAfter.x:0.###},{rbPosAfter.y:0.###}) rbV=({vNow.x:0.###},{vNow.y:0.###}) vY={_verticalVelocity:0.###} moveX={_moveX:0.###} grounded={_grounded} maxNy={_lastMaxGroundNormalY:0.###} contacts={_lastContactCount} bufferLeft={bufferLeft:0.###} groundedAgo={groundedAgo:0.###} dt={Time.fixedDeltaTime:0.#####} gScale={GravityScale:0.###}", this);
            }
#endif
        }

        private void UpdateGroundedFromContacts()
        {
            if (_col == null)
            {
                _grounded = false;
                return;
            }

            _grounded = false;
            _lastMaxGroundNormalY = -1f;
            var count = _col.GetContacts(_contacts);
            _lastContactCount = count;
            for (var i = 0; i < count; i++)
            {
                var c = _contacts[i];
                if (c.collider == null)
                {
                    continue;
                }
                if (c.collider.isTrigger)
                {
                    continue;
                }
                if (c.collider.GetComponent<EnemyController>() != null)
                {
                    continue;
                }
                if (c.normal.y > _lastMaxGroundNormalY)
                {
                    _lastMaxGroundNormalY = c.normal.y;
                }
                if (c.normal.y >= GroundNormalThreshold)
                {
                    _grounded = true;
                    return;
                }
            }
        }

        private static Vector2 GetVelocity(Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        private static void SetVelocity(Rigidbody2D rb, Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // 只要与“非敌人”碰撞并且法线朝上，就认为在地面/台阶上。
            if (collision.collider == null)
            {
                return;
            }

            // 与敌人接触：按“入射速度”做反弹，避免持续挤压导致双方卡死。
            var enemy = collision.collider.GetComponent<EnemyController>();
            if (enemy != null)
            {
                // Frozen 敌人会变成“可推动雪球”，不要把玩家弹开，否则无法推进。
                if (!enemy.IsFrozen)
                {
                    TryBounceFromEnemy(collision);
                }
                return;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                var n = collision.GetContact(i).normal;
                if (n.y > 0.5f)
                {
                    _grounded = true;
                    return;
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            // 首次触碰敌人时立刻弹开。
            var enemy = collision.collider.GetComponent<EnemyController>();
            if (enemy != null)
            {
                if (!enemy.IsFrozen)
                {
                    TryBounceFromEnemy(collision);
                }
            }
        }

        private void TryBounceFromEnemy(Collision2D collision)
        {
            if (_rb == null)
            {
                return;
            }
            if (Time.time < _nextEnemyBumpAt)
            {
                return;
            }
            if (collision.contactCount <= 0)
            {
                return;
            }

            // 以“玩家当前意图速度”（水平输入 + 自定义竖直速度 + 既有外力）作为入射速度。
            var incident = new Vector2(_moveX * MoveSpeed + _enemyBumpVelocity.x, _verticalVelocity + _enemyBumpVelocity.y);
            if (incident.sqrMagnitude < 0.0001f)
            {
                // 兜底：如果输入/自定义速度很小，就用物理解算的相对速度。
                incident = collision.relativeVelocity;
            }

            // 法线方向需“迎着入射速度”，否则反射方向会出错。
            var n = collision.GetContact(0).normal;
            if (Vector2.Dot(incident, n) > 0f)
            {
                n = -n;
            }

            var reflected = Vector2.Reflect(incident, n);
            var speed = Mathf.Max(Mathf.Max(0f, EnemyBumpMinSpeed), incident.magnitude) * Mathf.Max(0f, EnemyBumpBounceMultiplier);
            if (reflected.sqrMagnitude < 0.0001f)
            {
                // 极端情况下 Reflect 结果接近 0，就直接沿法线弹开。
                reflected = n;
            }

            _enemyBumpVelocity = reflected.normalized * speed;
            _verticalVelocity = 0f; // 避免自定义重力在下一帧立刻把反弹抵消成“继续往下挤”。
            _grounded = false;
            _nextEnemyBumpAt = Time.time + Mathf.Max(0f, EnemyBumpCooldownSeconds);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            // 简化处理：离开任何碰撞就先当作不在地面，下一次 Stay 会再置回 true。
            // 现在主要依赖 UpdateGrounded()，这里不再强制清空，避免偶发漏判导致“按不出跳跃”。
        }
    }
}
