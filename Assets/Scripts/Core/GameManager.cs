using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Snow2.Rewards;

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

        [Header("Combo")]
        public float ComboWindowSeconds = 2.0f;

        [Header("Reward")]
        public int BaseSnowballKillScore = 50;
        public float PotionDropChance = 0.2f;

        [Header("Sushi Rain")]
        public float SushiRainDurationSeconds = 3.0f;
        public float SushiRainRatePerSecond = 30f;
        public int SushiScore = 10;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
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
                // “一次性清版”判定：在同一连击窗口内清掉所有初始敌人。
                var clearedAllInOneCombo = (_initialEnemyCount > 0) && (_clearedSinceComboStart >= _initialEnemyCount);
                if (clearedAllInOneCombo)
                {
                    StartCoroutine(SushiRain());
                }
            }
        }

        public int AliveEnemyCount => _aliveEnemyIds.Count;

        public void OnEnemyCleared(Vector2 position, EnemyClearCause cause)
        {
            if (cause == EnemyClearCause.SnowballImpact)
            {
                AdvanceCombo();
                _clearedSinceComboStart++;
                AddScore(BaseSnowballKillScore * Mathf.Max(1, Combo));
                SpawnKillDrop(position);
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

        private void SpawnKillDrop(Vector2 position)
        {
            var type = (Random.value < PotionDropChance) ? PickupType.Potion : PickupType.Sushi;
            var go = new GameObject(type == PickupType.Sushi ? "Pickup_Sushi" : "Pickup_Potion");
            go.transform.position = position + new Vector2(0, 0.6f);
            go.transform.localScale = Vector3.one * 0.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
            sr.color = type == PickupType.Sushi ? new Color(1f, 0.85f, 0.1f) : new Color(0.2f, 0.9f, 0.35f);
            sr.sortingOrder = 20;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var pickup = go.AddComponent<PickupItem>();
            pickup.Type = type;
            pickup.SushiScore = SushiScore;
            pickup.DespawnY = -12f;
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
        }
    }
}
