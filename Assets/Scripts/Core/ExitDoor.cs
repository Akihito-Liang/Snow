using UnityEngine;
using Snow2.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Snow2
{
    /// <summary>
    /// 出口门：
    /// - 场景可选放置
    /// - 当玩家触碰且敌人已清空（LevelManager.CanProceed）时，调用 LevelManager.NextLevel()
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExitDoor : MonoBehaviour
    {
        private const string DoorLayerName = "Door";

        [Header("Gate")]
        [Tooltip("为 true 时：必须清怪（LevelManager.CanProceed）后才能进门。\n为 false 时：允许直接进门跳关（满足‘清怪 或 进门’）。")]
        [SerializeField] private bool lockUntilClear = false;

        [SerializeField] private bool locked = false;

        [Header("Interaction")]
        [SerializeField] private bool requireKeyPress = true;
        [SerializeField] private KeyCode interactKey = KeyCode.W;

        private bool _playerInRange;

        private void Awake()
        {
            // 需求：门不应与玩家/敌人发生“实体碰撞阻挡”，应可穿过。
            // 做法：
            // 1) 强制门（含子物体）所有 Collider2D 都是 Trigger
            // 2) 可选：把门放到单独的物理 Layer（Door），便于在项目设置里统一关闭碰撞
            EnsureAllCollidersAreTriggers();
            TryApplyDoorLayer();
        }

        private void EnsureAllCollidersAreTriggers()
        {
            var cols = GetComponentsInChildren<Collider2D>(includeInactive: true);
            if (cols == null || cols.Length == 0)
            {
                var added = gameObject.AddComponent<BoxCollider2D>();
                added.isTrigger = true;
                return;
            }

            for (var i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                c.isTrigger = true;
            }
        }

        private void TryApplyDoorLayer()
        {
            var doorLayer = LayerMask.NameToLayer(DoorLayerName);
            if (doorLayer < 0)
            {
                return;
            }

            SetLayerRecursively(gameObject, doorLayer);

            // 仅忽略 Door 与 Enemy 的交互（不影响 Player 进入 Trigger 进门）。
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(doorLayer, enemyLayer, true);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;

            root.layer = layer;

            var t = root.transform;
            if (t == null) return;

            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child == null) continue;
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public bool IsLocked => locked;

        public bool LockUntilClear => lockUntilClear;

        public void SetLocked(bool isLocked)
        {
            locked = isLocked;
        }

        private void Update()
        {
            if (!_playerInRange)
            {
                return;
            }

            if (requireKeyPress && !IsInteractPressedThisFrame())
            {
                return;
            }

            TryProceed();
        }

        private bool IsInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            // 仅映射项目里常用的按键；需要更多按键时再补。
            switch (interactKey)
            {
                case KeyCode.W: return kb.wKey.wasPressedThisFrame;
                case KeyCode.A: return kb.aKey.wasPressedThisFrame;
                case KeyCode.S: return kb.sKey.wasPressedThisFrame;
                case KeyCode.D: return kb.dKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.J: return kb.jKey.wasPressedThisFrame;
                case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
                default:
                    return false;
            }
#else
            return Input.GetKeyDown(interactKey);
#endif
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            // 优先用组件判断；没有则再用 Tag 兜底。
            if (other.GetComponent<PlayerController2D>() == null)
            {
                if (!other.CompareTag("Player"))
                {
                    return;
                }
            }

            _playerInRange = true;

            // 兼容旧行为：不需要按键时，触碰立即触发
            if (!requireKeyPress)
            {
                TryProceed();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            if (other.GetComponent<PlayerController2D>() == null)
            {
                if (!other.CompareTag("Player"))
                {
                    return;
                }
            }

            _playerInRange = false;
        }

        private void TryProceed()
        {
            if (locked)
            {
                return;
            }

            if (LevelManager.Instance == null)
            {
                return;
            }

            if (lockUntilClear && !LevelManager.Instance.CanProceed)
            {
                return;
            }

            // 满足两种通关方式：
            // - 清怪：由 LevelManager 自动触发
            // - 进门：在门范围内按 W 直接进入下一关（可选择是否要求清怪）
            LevelManager.Instance.RequestNextLevel(ignoreVictory: !lockUntilClear);
        }
    }
}
