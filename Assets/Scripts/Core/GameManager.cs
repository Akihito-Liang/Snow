using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Snow2.Rewards;
using Snow2.Player;

namespace Snow2
{
    public enum EnemyClearCause
    {
        SnowballImpact,
        Other
    }

    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private readonly HashSet<int> _aliveEnemyIds = new HashSet<int>();
        private int _initialEnemyCount;

        public int Score { get; private set; }
        public int Potions { get; private set; }

        public int Combo { get; private set; }
        private float _comboExpireAt;
        private int _clearedSinceComboStart;

        // Perfect Clear：同一个雪球清掉所有敌人
        private Snowball _perfectClearCandidate;
        private int _clearedByPerfectClearCandidate;

        // 雪球连击文案（用于屏幕提示）
        private int _lastSnowballComboCount;
        private float _snowballComboTextExpireAt;

        [Header("Combo")]
        public float ComboWindowSeconds = 2.0f;

        [Header("Reward")]
        public int BaseSnowballKillScore = 50;

        [Header("Sushi Rain")]
        public float SushiRainDurationSeconds = 3.0f;
        public float SushiRainRatePerSecond = 30f;
        public int SushiScore = 10;

        private PlayerController2D _hudPlayer;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 运行时兜底：确保奖励系统存在（未在场景中挂载时也能工作）。
            if (GetComponent<RewardSystem>() == null)
            {
                gameObject.AddComponent<RewardSystem>();
            }
        }

        private void OnEnable()
        {
            Snowball.RollStarted += OnSnowballRollStarted;
            Snowball.ComboChanged += OnSnowballComboChanged;
        }

        private void OnDisable()
        {
            Snowball.RollStarted -= OnSnowballRollStarted;
            Snowball.ComboChanged -= OnSnowballComboChanged;
        }

        private IEnumerator Start()
        {
            ForceRuntimeSimulationDefaults();

            // 运行时兜底：防止 Environment 下的地面/台阶被误设为 Dynamic，导致开局“台阶歪了/乱动”。
            FixEnvironmentRigidbodies();

            // 等一帧，确保场景里的 EnemyController 都完成 Awake 注册。
            yield return null;
            _initialEnemyCount = _aliveEnemyIds.Count;
        }

        private static void ForceRuntimeSimulationDefaults()
        {
            // 一些项目会把 TimeScale 或 2D 物理模拟模式改掉，导致 Rigidbody2D 看起来“完全不动”。
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }

            // 另一个高频坑：Time.fixedDeltaTime 被改得极小，会导致刚体位置每帧只移动“肉眼几乎看不见”的距离。
            // 正常默认一般是 0.02（50Hz）。这里做兜底纠正。
            if (Time.fixedDeltaTime < 0.005f || Time.fixedDeltaTime > 0.05f)
            {
                Time.fixedDeltaTime = 0.02f;
            }

#if UNITY_2020_1_OR_NEWER
            if (Physics2D.simulationMode != SimulationMode2D.FixedUpdate)
            {
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            }
#endif

        }

        private static void FixEnvironmentRigidbodies()
        {
            var env = GameObject.Find("Environment");
            if (env == null)
            {
                return;
            }

            var rbs = env.GetComponentsInChildren<Rigidbody2D>(true);
            for (var i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (rb == null)
                {
                    continue;
                }

                rb.simulated = true;
                rb.bodyType = RigidbodyType2D.Static;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }

            // 运行时兜底：把关卡里的台阶（Step1/Step2/...）变成“可从下穿过、从上站住”的单向平台。
            // 约定：只处理名称以 "Step" 开头的物体，避免影响 Ground/墙体等。
            SetupOneWaySteps(env);
        }

        private static void SetupOneWaySteps(GameObject env)
        {
            if (env == null)
            {
                return;
            }

            var colliders = env.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                var go = col.gameObject;
                if (go == null)
                {
                    continue;
                }

                // 只把 Step* 当作“台阶”。地面（Ground）按需求不做单向处理。
                if (!go.name.StartsWith("Step", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 确保台阶本身是非 Trigger（单向平台依赖实体碰撞）。
                col.isTrigger = false;
                col.usedByEffector = true;

                var effector = go.GetComponent<PlatformEffector2D>();
                if (effector == null)
                {
                    effector = go.AddComponent<PlatformEffector2D>();
                }

                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.rotationalOffset = 0f;
                effector.surfaceArc = 180f;
            }
        }

        private void Update()
        {
            if (Combo > 0 && Time.time > _comboExpireAt)
            {
                Combo = 0;
                _clearedSinceComboStart = 0;
            }
        }

        public void RegisterEnemy(GameObject enemyGo)
        {
            _aliveEnemyIds.Add(enemyGo.GetInstanceID());
        }

        public void UnregisterEnemy(GameObject enemyGo)
        {
            _aliveEnemyIds.Remove(enemyGo.GetInstanceID());

            if (_aliveEnemyIds.Count == 0)
            {
                // Perfect Clear：同一个雪球清掉所有初始敌人。
                var clearedAllByOneSnowball = (_perfectClearCandidate != null) && (_initialEnemyCount > 0) && (_clearedByPerfectClearCandidate >= _initialEnemyCount);
                if (clearedAllByOneSnowball)
                {
                    StartCoroutine(SushiRain());
                }
            }
        }

        public int AliveEnemyCount => _aliveEnemyIds.Count;

        public void OnEnemyCleared(Vector2 position, EnemyClearCause cause)
        {
            OnEnemyCleared(position, cause, null);
        }

        public void OnEnemyCleared(Vector2 position, EnemyClearCause cause, Snowball sourceSnowball)
        {
            if (cause == EnemyClearCause.SnowballImpact)
            {
                AdvanceCombo();
                _clearedSinceComboStart++;
                AddScore(BaseSnowballKillScore * Mathf.Max(1, Combo));

                // Perfect Clear 只认同一个雪球：一旦出现“非候选雪球”的击杀，就直接失效。
                if (_perfectClearCandidate != null)
                {
                    if (sourceSnowball != null && sourceSnowball == _perfectClearCandidate)
                    {
                        _clearedByPerfectClearCandidate++;
                    }
                    else
                    {
                        _perfectClearCandidate = null;
                    }
                }
            }
            else
            {
                // 其他方式清敌：不满足“同一雪球清版”。
                _perfectClearCandidate = null;
            }
        }

        private void OnSnowballRollStarted(Snowball snowball)
        {
            _perfectClearCandidate = snowball;
            _clearedByPerfectClearCandidate = 0;
            _lastSnowballComboCount = 0;
        }

        private void OnSnowballComboChanged(Snowball snowball, int comboCount)
        {
            _lastSnowballComboCount = Mathf.Max(0, comboCount);
            if (_lastSnowballComboCount > 0)
            {
                _snowballComboTextExpireAt = Time.time + 1.0f;
            }
        }

        public void AddScore(int delta)
        {
            Score += Mathf.Max(0, delta);
        }

        public void AddPotion(int delta)
        {
            Potions += Mathf.Max(0, delta);
        }

        private void AdvanceCombo()
        {
            var now = Time.time;
            if (now <= _comboExpireAt)
            {
                Combo += 1;
            }
            else
            {
                Combo = 1;
                _clearedSinceComboStart = 0;
            }
            _comboExpireAt = now + ComboWindowSeconds;
        }

        private IEnumerator SushiRain()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                yield break;
            }

            var start = Time.time;
            var interval = 1f / Mathf.Max(1f, SushiRainRatePerSecond);
            while (Time.time - start < SushiRainDurationSeconds)
            {
                SpawnSushiFromTop(cam);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnSushiFromTop(UnityEngine.Camera cam)
        {
            var topY = cam.ViewportToWorldPoint(new Vector3(0, 1, 0)).y + 1.0f;
            var leftX = cam.ViewportToWorldPoint(new Vector3(0, 0, 0)).x;
            var rightX = cam.ViewportToWorldPoint(new Vector3(1, 0, 0)).x;

            var x = Random.Range(leftX, rightX);
            var pos = new Vector2(x, topY);

            var go = new GameObject("SushiRain_Sushi");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.45f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
            sr.color = new Color(1f, 0.85f, 0.1f);
            sr.sortingOrder = 20;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var pickup = go.AddComponent<PickupItem>();
            pickup.Type = PickupType.Sushi;
            pickup.SushiScore = SushiScore;
            pickup.DespawnY = -12f;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18
            };
            GUI.Label(new Rect(10, 10, 480, 30), $"Score: {Score}", style);
            GUI.Label(new Rect(10, 35, 480, 30), $"Combo: {Combo}", style);
            GUI.Label(new Rect(10, 60, 480, 30), $"Potions: {Potions}", style);
            GUI.Label(new Rect(10, 85, 480, 30), $"Enemies Left: {AliveEnemyCount}", style);
            GUI.Label(new Rect(10, 110, 720, 30), "操作：A/D 移动，Space 跳跃，鼠标左键/ J 发射雪球", style);

            // 雪球连击提示（独立于全局 Combo，按本次雪球滚动计数）
            if (_lastSnowballComboCount > 0 && Time.time <= _snowballComboTextExpireAt)
            {
                var big = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 34,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(0, 140, Screen.width, 50), $"Combo x{_lastSnowballComboCount}", big);
            }

            // 右上角：药水倒计时（每种药水独立）
            if (_hudPlayer == null)
            {
                _hudPlayer = FindAnyObjectByType<PlayerController2D>();
            }
            if (_hudPlayer != null)
            {
                DrawPotionCountdownHUD(_hudPlayer);
            }
        }

        private static void DrawPotionCountdownHUD(PlayerController2D player)
        {
            var rightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperRight
            };

            const float width = 260f;
            const float lineH = 22f;
            var x = Screen.width - 10f - width;
            var y = 10f;

            // 仅当有任意药水在生效时才显示
            var r = player.GetPotionRemainingSeconds(PlayerController2D.PotionType.Red);
            var b = player.GetPotionRemainingSeconds(PlayerController2D.PotionType.Blue);
            var yel = player.GetPotionRemainingSeconds(PlayerController2D.PotionType.Yellow);
            var g = player.GetPotionRemainingSeconds(PlayerController2D.PotionType.Green);

            if (r <= 0.0001f && b <= 0.0001f && yel <= 0.0001f && g <= 0.0001f)
            {
                return;
            }

            GUI.Label(new Rect(x, y, width, lineH), "药水倒计时", rightStyle);
            y += lineH;

            if (r > 0.0001f) { GUI.Label(new Rect(x, y, width, lineH), $"红药水：{r:0.0}s", rightStyle); y += lineH; }
            if (b > 0.0001f) { GUI.Label(new Rect(x, y, width, lineH), $"蓝药水：{b:0.0}s", rightStyle); y += lineH; }
            if (yel > 0.0001f) { GUI.Label(new Rect(x, y, width, lineH), $"黄药水：{yel:0.0}s", rightStyle); y += lineH; }
            if (g > 0.0001f) { GUI.Label(new Rect(x, y, width, lineH), $"绿药水：{g:0.0}s", rightStyle); }
        }
    }
}
