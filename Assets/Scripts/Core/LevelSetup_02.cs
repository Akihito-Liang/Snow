using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Snow2.Enemies;

namespace Snow2
{
    /// <summary>
    /// Level_02 场景装配脚本：
    /// - 运行时创建 Tilemap（错层型 Staggered Layout）
    /// - 在指定坐标 Instantiate 敌人
    /// - 顶层放置 ExitDoor
    ///
    /// 注意：这份脚本用于“快速原型/可复现布局”。
    /// 你也可以把 Tilemap/敌人直接在编辑器里摆好，然后删掉本脚本。
    /// </summary>
    public sealed class LevelSetup_02 : MonoBehaviour
    {
        [Header("Prefabs (Optional)")]
        [SerializeField] private GameObject redDemonPrefab;
        [SerializeField] private GameObject yellowFattyPrefab;
        [SerializeField] private GameObject exitDoorPrefab;

        [Header("Layout (Runtime)")]
        [SerializeField] private int halfWidth = 12;
        [SerializeField] private int groundY = 0;
        [SerializeField] private int topY = 10;

        // Level_02 旧参数（保留，避免你在 Inspector 里已经调整过的值丢失）
        [Header("Level_02 Preset")]
        [SerializeField] private int midY1 = 3;
        [SerializeField] private int midY2 = 6;
        [SerializeField] private int platformHalfLen = 4;
        [SerializeField] private int midPlatformInsetX = 6;

        private bool _built;

        private void Awake()
        {
            if (_built)
            {
                return;
            }
            _built = true;

            // 由于 Level_02 场景目前是从 Level_01 复制出来的（用于保留 Player/Camera/GameManager 等基础物体），
            // 这里先清理掉“第一关遗留”的环境/敌人，避免与第二关错层布局/刷怪产生叠加干扰。
            CleanupLevelArtifacts();

            var scene = SceneManager.GetActiveScene().name;
            if (string.Equals(scene, "Level_01", System.StringComparison.Ordinal))
            {
                BuildLevel01();
            }
            else
            {
                // 默认走 Level_02
                BuildLevel02();
            }
        }

        private static void CleanupLevelArtifacts()
        {
            // 1) 清理敌人（两套体系都清掉）
            var ecs = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            if (ecs != null)
            {
                for (var i = 0; i < ecs.Length; i++)
                {
                    if (ecs[i] != null) Destroy(ecs[i].gameObject);
                }
            }

            var ebs = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            if (ebs != null)
            {
                for (var i = 0; i < ebs.Length; i++)
                {
                    if (ebs[i] != null) Destroy(ebs[i].gameObject);
                }
            }

            // 2) 清理旧门
            var doors = FindObjectsByType<ExitDoor>(FindObjectsSortMode.None);
            if (doors != null)
            {
                for (var i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null) Destroy(doors[i].gameObject);
                }
            }

            // 3) 只清理“运行时生成的关卡物体”，不要动场景里手工摆的 Ground/Background/墙体等。
            //    （之前全删会导致玩家下坠、背景黑屏）
            var runtimeNames = new[]
            {
                "Level_01_Grid",
                "Level_01_Tilemap",
                "Level_02_Grid",
                "Level_02_Tilemap",
            };
            for (var i = 0; i < runtimeNames.Length; i++)
            {
                var go = GameObject.Find(runtimeNames[i]);
                if (go != null) Destroy(go);
            }
        }

        private void BuildLevel01()
        {
            // Level_01：阶梯攀登型（从左下爬到右上门）
            halfWidth = 14;
            groundY = 0;
            topY = 11;

            var tm = GetOrCreateRuntimeTilemap("Level_01");
            tm.ClearAllTiles();

            var tile = CreateRuntimeTile(new Color(0.72f, 0.86f, 1.0f, 1f));

            // 1) 底层地面
            for (var x = -halfWidth; x <= halfWidth; x++)
            {
                tm.SetTile(new Vector3Int(x, groundY, 0), tile);
            }

            // 2) 阶梯：run=2, rise=1
            var stepY = groundY + 1;
            var stepX = -halfWidth + 2;
            var run = 2;
            var treadHalf = 1; // 每级台阶宽度=3
            while (stepY <= topY && stepX + treadHalf <= halfWidth - 2)
            {
                FillPlatform(tm, tile, centerX: stepX, y: stepY, halfLen: treadHalf);
                stepX += run;
                stepY += 1;
            }

            // 3) 顶层平台（门口）
            var doorPlatformY = topY;
            var doorPlatformX = Mathf.Clamp(stepX - run, -halfWidth + 2, halfWidth - 2);
            FillPlatform(tm, tile, centerX: doorPlatformX, y: doorPlatformY, halfLen: 3);

            // 4) 中间岔路平台（让战斗形态不只是一路直上）
            FillPlatform(tm, tile, centerX: -6, y: 5, halfLen: 4);
            FillPlatform(tm, tile, centerX: +4, y: 7, halfLen: 3);

            // 5) 左右边界墙（防滚出）
            for (var y = groundY; y <= topY + 3; y++)
            {
                tm.SetTile(new Vector3Int(-halfWidth - 1, y, 0), tile);
                tm.SetTile(new Vector3Int(+halfWidth + 1, y, 0), tile);
            }

            // 6) 刷怪：分散在“底层/中层/高层”
            SpawnEnemy<RedDemon>(redDemonPrefab, new Vector2(-8, 1), Color.red);
            SpawnEnemy<RedDemon>(redDemonPrefab, new Vector2(-2, 1), Color.red);
            SpawnEnemy<YellowFatty>(yellowFattyPrefab, new Vector2(-6, 6), new Color(1f, 0.92f, 0.3f));
            SpawnEnemy<BlueMonkey>(null, new Vector2(+3, 8), new Color(0.35f, 0.65f, 1f));
            SpawnEnemy<FlyBat>(null, new Vector2(0, 9.5f), new Color(0.4f, 0.2f, 0.55f));

            // 7) 顶层门（黑色，按 W 进入）
            SpawnDoor(new Vector2(doorPlatformX, doorPlatformY + 1));
        }

        private void BuildLevel02()
        {
            // Level_02：错层回旋型（平台左右交替 + 右侧台阶上顶层）
            halfWidth = 12;
            groundY = 0;
            midY1 = 3;
            midY2 = 6;
            topY = 10;
            platformHalfLen = 4;
            midPlatformInsetX = 6;

            var tm = GetOrCreateRuntimeTilemap("Level_02");
            tm.ClearAllTiles();

            var tile = CreateRuntimeTile(new Color(0.82f, 0.84f, 0.9f, 1f));

            // 地面层：整条地面
            for (var x = -halfWidth; x <= halfWidth; x++)
            {
                tm.SetTile(new Vector3Int(x, groundY, 0), tile);
            }

            // 中间层：左右交替短平台（保持原本“Z 字滚落”味道）
            FillPlatform(tm, tile, centerX: +midPlatformInsetX, y: midY2, halfLen: platformHalfLen);
            FillPlatform(tm, tile, centerX: -midPlatformInsetX, y: midY1, halfLen: platformHalfLen);

            // 顶层：门平台
            FillPlatform(tm, tile, centerX: 0, y: topY, halfLen: 2);
            FillPlatform(tm, tile, centerX: +midPlatformInsetX + 3, y: topY, halfLen: 2);

            // 右侧台阶：从底层爬到顶层
            var stairX = +halfWidth - 2;
            for (var y = groundY + 1; y <= topY; y++)
            {
                FillPlatform(tm, tile, centerX: stairX, y: y, halfLen: 1);
                stairX = Mathf.Max(+midPlatformInsetX + 1, stairX - 1); // 轻微向左偏，形成“台阶”形态
            }

            // 左右边界墙
            for (var y = groundY; y <= topY + 3; y++)
            {
                tm.SetTile(new Vector3Int(-halfWidth - 1, y, 0), tile);
                tm.SetTile(new Vector3Int(+halfWidth + 1, y, 0), tile);
            }

            // 刷怪（与 Level_01 的组合不同）
            SpawnEnemy<RedDemon>(redDemonPrefab, new Vector2(+midPlatformInsetX - 2, midY2 + 1), Color.red);
            SpawnEnemy<RedDemon>(redDemonPrefab, new Vector2(-midPlatformInsetX + 2, midY1 + 1), Color.red);
            SpawnEnemy<YellowFatty>(yellowFattyPrefab, new Vector2(-halfWidth + 2, groundY + 1), new Color(1f, 0.92f, 0.3f));
            SpawnEnemy<BlueMonkey>(null, new Vector2(+midPlatformInsetX + 3, topY + 1), new Color(0.35f, 0.65f, 1f));
            SpawnEnemy<FlyBat>(null, new Vector2(-1, 7.5f), new Color(0.45f, 0.2f, 0.6f));

            SpawnDoor(new Vector2(0f, topY + 1));
        }

        private static Tile CreateRuntimeTile(Color tint)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = RuntimeSpriteLibrary.WhiteSprite;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.color = tint;
            return tile;
        }

        private static Tilemap GetOrCreateRuntimeTilemap(string levelName)
        {
            // 放在 Environment 下，便于复用 GameManager 的“环境兜底修正”（可选）。
            var env = GameObject.Find("Environment");
            if (env == null)
            {
                env = new GameObject("Environment");
            }

            // Grid
            var gridGo = GameObject.Find($"{levelName}_Grid");
            if (gridGo == null)
            {
                gridGo = new GameObject($"{levelName}_Grid");
                gridGo.transform.SetParent(env.transform, worldPositionStays: true);
                gridGo.AddComponent<Grid>();
            }

            // Tilemap
            var tilemapGo = GameObject.Find($"{levelName}_Tilemap");
            Tilemap tm;
            if (tilemapGo == null)
            {
                tilemapGo = new GameObject($"{levelName}_Tilemap");
                tilemapGo.transform.SetParent(gridGo.transform, worldPositionStays: true);
                tm = tilemapGo.AddComponent<Tilemap>();

                var renderer = tilemapGo.AddComponent<TilemapRenderer>();
                renderer.sortingOrder = -5;

                var rb = tilemapGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
                rb.simulated = true;

                var col = tilemapGo.AddComponent<TilemapCollider2D>();
                col.compositeOperation = Collider2D.CompositeOperation.Merge;

                tilemapGo.AddComponent<CompositeCollider2D>();
            }
            else
            {
                tm = tilemapGo.GetComponent<Tilemap>();
                if (tm == null) tm = tilemapGo.AddComponent<Tilemap>();
            }

            return tm;
        }

        private static void FillPlatform(Tilemap tm, TileBase tile, int centerX, int y, int halfLen)
        {
            var len = Mathf.Max(1, halfLen);
            for (var x = centerX - len; x <= centerX + len; x++)
            {
                tm.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private void SpawnDoor(Vector2 pos)
        {
            GameObject go;
            if (exitDoorPrefab != null)
            {
                go = Instantiate(exitDoorPrefab, pos, Quaternion.identity);
            }
            else
            {
                go = new GameObject("ExitDoor");
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
                sr.color = Color.black;
                // 门应在角色后面渲染（角色/敌人默认在 0 以上）。
                sr.sortingOrder = -10;

                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.2f, 2.0f);
            }

            // 门放到独立 Layer（Door）。如果项目还没创建该层，这行不会生效。
            var doorLayer = LayerMask.NameToLayer("Door");
            if (doorLayer >= 0)
            {
                go.layer = doorLayer;
            }

            if (go.GetComponent<ExitDoor>() == null)
            {
                go.AddComponent<ExitDoor>();
            }
        }

        private static void SpawnEnemy<T>(GameObject prefab, Vector2 position, Color tint) where T : EnemyBase
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, position, Quaternion.identity);
            }
            else
            {
                go = new GameObject(typeof(T).Name);
                go.transform.position = position;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteLibrary.WhiteSprite;
                sr.sortingOrder = 10;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.freezeRotation = true;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.9f, 0.9f);
            }

            var enemy = go.GetComponent<T>();
            if (enemy == null)
            {
                enemy = go.AddComponent<T>();
            }

            // 运行时数据注入（避免必须创建 SO 资产）。
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.tintColor = tint;
            data.moveSpeed = 1.6f;
            data.attackRate = 0.33f;
            data.jumpForce = 7.5f;
            enemy.SetData(data);
        }
    }
}
