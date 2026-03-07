using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using Snow2.Enemies;

namespace Snow2
{
    /// <summary>
    /// 关卡管理器（单例 + 常驻 DontDestroyOnLoad）。
    ///
    /// 核心职责：
    /// - 胜利条件：EnemyCount == 0（并考虑是否有刷怪器尚未结束）
    /// - 触发胜利音效
    /// - 延时 3 秒切换到下一关（Level_01 -> Level_02 ...）
    /// - 可选 ExitDoor：当玩家触碰且清怪后，手动触发切关
    ///
    /// 说明：
    /// - 本项目已有 GameManager 的敌人注册计数（EnemyController 体系）。
    /// - 这里同时兼容 EnemyBase 体系（以防关卡里使用了新敌人）。
    /// </summary>
    public sealed class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Flow")]
        [SerializeField, Min(0.1f)] private float victoryDelaySeconds = 3.0f;
        [SerializeField] private bool requireExitDoorToProceed = false;

        [Header("Scene Naming")]
        [SerializeField] private string levelNamePrefix = "Level_";
        [SerializeField, Min(1)] private int levelNumberDigits = 2;
        [Tooltip("当前场景名无法解析编号时，使用该场景名作为 NextLevel。")]
        [SerializeField] private string fallbackNextSceneName = "Level_02";

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip victorySfx;

        private AudioSource _audio;
        private bool _victoryTriggered;
        private bool _countdownRunning;
        private float _loadAt;
        private string _nextSceneName;

        private ExitDoor _door;

        private static readonly Regex LevelRegex = new Regex(@"^Level_(\d+)$", RegexOptions.Compiled);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("LevelManager");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _victoryTriggered = false;
            _countdownRunning = false;
            _nextSceneName = null;
            _door = FindAnyObjectByType<ExitDoor>();
            if (_door != null)
            {
                // 默认：门可用作“清怪 或 进门”两种通关方式。
                // 若门配置为必须清怪（LockUntilClear）或本关要求必须清怪后进门（requireExitDoorToProceed），才锁门。
                var shouldLock = _door.LockUntilClear || requireExitDoorToProceed;
                _door.SetLocked(shouldLock);
            }

            // 关卡形态由 Scene（手工摆放 Sprite/Collider）决定；这里不再用代码自动生成布局。
        }

        private void Update()
        {
            if (_countdownRunning)
            {
                if (Time.time >= _loadAt)
                {
                    LoadNextLevelInternal();
                }
                return;
            }

            if (_victoryTriggered)
            {
                return;
            }

            // 胜利条件：敌人清空 + （若有刷怪器）刷怪流程已结束。
            if (GetAliveEnemyCount() == 0 && AreAllSpawnersCompleted())
            {
                TriggerVictory();
            }
        }

        /// <summary>
        /// Door 或调试按键可直接调用：尝试进入下一关。
        /// 只有在“已经满足胜利条件”时才会生效。
        /// </summary>
        public void NextLevel()
        {
            RequestNextLevel(ignoreVictory: false);
        }

        /// <summary>
        /// 请求进入下一关。
        /// - ignoreVictory=false：必须先满足胜利条件（清怪/刷怪结束）
        /// - ignoreVictory=true：直接进入下一关（用于“门口按键跳关”）
        /// </summary>
        public void RequestNextLevel(bool ignoreVictory)
        {
            if (!ignoreVictory && !_victoryTriggered)
            {
                return;
            }

            if (_countdownRunning)
            {
                return;
            }

            _nextSceneName = ComputeNextLevelSceneName();

            // 门交互：尽量即时切关；清怪胜利：保留胜利延时。
            var delay = ignoreVictory ? 0.05f : Mathf.Max(0.1f, victoryDelaySeconds);
            _loadAt = Time.time + delay;
            _countdownRunning = true;
        }

        public bool CanProceed => _victoryTriggered;

        private void TriggerVictory()
        {
            _victoryTriggered = true;

            if (_audio != null && victorySfx != null)
            {
                _audio.PlayOneShot(victorySfx);
            }

            if (_door != null)
            {
                _door.SetLocked(false);

                if (requireExitDoorToProceed)
                {
                    // 等玩家进门再触发倒计时
                    return;
                }
            }

            StartCountdownToNextLevel();
        }

        private void StartCountdownToNextLevel()
        {
            if (_countdownRunning)
            {
                return;
            }

            RequestNextLevel(ignoreVictory: false);
        }

        private void LoadNextLevelInternal()
        {
            _countdownRunning = false;

            var next = string.IsNullOrWhiteSpace(_nextSceneName) ? fallbackNextSceneName : _nextSceneName;
            if (string.IsNullOrWhiteSpace(next))
            {
                Debug.LogWarning("[LevelManager] Next scene name is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(next))
            {
                Debug.LogWarning($"[LevelManager] Scene '{next}' is not in Build Settings (or cannot be loaded). Please add it.");
                return;
            }

            SceneManager.LoadScene(next);
        }

        private string ComputeNextLevelSceneName()
        {
            var current = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(current))
            {
                return fallbackNextSceneName;
            }

            // 仅严格匹配 Level_XX 格式；若不匹配则 fallback。
            var m = LevelRegex.Match(current);
            if (!m.Success)
            {
                return fallbackNextSceneName;
            }

            if (!int.TryParse(m.Groups[1].Value, out var idx))
            {
                return fallbackNextSceneName;
            }

            idx = Mathf.Max(0, idx) + 1;
            var fmt = new string('0', Mathf.Max(1, levelNumberDigits));
            return $"{levelNamePrefix}{idx.ToString(fmt)}";
        }

        private static bool AreAllSpawnersCompleted()
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            if (spawners == null || spawners.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < spawners.Length; i++)
            {
                var s = spawners[i];
                if (s == null) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                if (!s.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetAliveEnemyCount()
        {
            var count = 0;

            // 1) 复用现有计数（EnemyController 体系）
            if (GameManager.Instance != null)
            {
                count += Mathf.Max(0, GameManager.Instance.AliveEnemyCount);
            }
            else
            {
                // 没有 GameManager 时：兜底直接数 EnemyController
                var ecs = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                count += ecs != null ? ecs.Length : 0;
            }

            // 2) 兼容 EnemyBase 体系（新 AI 敌人）。
            //    注意：EnemyBase 在变雪球时会 disabled，但对象仍应算“敌人未清除”。
            var ebs = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            count += ebs != null ? ebs.Length : 0;

            return Mathf.Max(0, count);
        }
    }
}
