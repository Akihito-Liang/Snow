########################################################################
# Snow2（Unity 2D）代码库说明（面向接手/协作）
########################################################################

> 目标：5 分钟内搞清楚“项目是什么、入口在哪、怎么跑、怎么改、怎么验证”。

本仓库是一个 Unity 2D 原型项目（URP 2D），玩法接近《雪人兄弟》：

1) 主角发射雪球投射物命中敌人 → 2) 敌人逐步冻结（多次命中）
→ 3) 冻结完成后敌人变为可推动雪球（`Snow2.Snowball`）
→ 4) 雪球滚动卷入/击杀敌人形成“本次雪球连击”（`Snowball.ComboChanged`）
→ 5) 雪球碎裂时按卷入数量生成寿司（`RewardSystem` 监听 `Snowball.Broken`）
→ 6) 敌人死亡瞬间随机掉落药水/蛋糕/点心（`EnemyDropBalance` 控权重）
→ 7) 同一颗雪球清掉全场初始敌人时触发“寿司雨”（`GameManager`）。

说明：已在仓库内搜索，当前未包含 `CLAUDE.md`（仅有 `AGENTS.md` 与极简 `README.md`）。

## 1) 引擎与依赖

- Unity 版本：`6000.3.10f1`（见 `ProjectSettings/ProjectVersion.txt:1`）
- 渲染管线：URP（`com.unity.render-pipelines.universal: 17.3.0`，见 `Packages/manifest.json:16`）
- 输入系统：Input System（`com.unity.inputsystem: 1.18.0`，见 `Packages/manifest.json:14`；同时保留旧输入兜底）
- Tilemap（关卡原型脚本运行时生成用）：`com.unity.2d.tilemap: 1.0.0`（见 `Packages/manifest.json:8`）
- 测试框架：Unity Test Framework（`com.unity.test-framework: 1.6.0`，见 `Packages/manifest.json:17`）

## 2) 快速开始（跑起来）

### 场景入口（按关卡）

- Build Settings 场景：
  - `Assets/Scenes/Level_01.unity`（见 `ProjectSettings/EditorBuildSettings.asset:9`）
  - `Assets/Scenes/Level_02.unity`（见 `ProjectSettings/EditorBuildSettings.asset:12`）

关卡命名约定为 `Level_XX`（两位数）。`LevelManager` 会按 `Level_01 -> Level_02 -> ...` 自动计算下一关（`Assets/Scripts/Core/LevelManager.cs:220`）。

### Editor 运行（推荐）

- 用 Unity Hub / Unity Editor 打开仓库根目录（包含 `Assets/`、`Packages/`、`ProjectSettings/`）
- 打开 `Assets/Scenes/Level_01.unity` 并 Play

### 当前操作（实现口径）

- A/D 或 ←/→：移动（Input System + 键盘兜底，`Assets/Scripts/Player/PlayerController2D.cs:190`）
- Space/W/↑：跳跃（Jump Buffer + Coyote Time，`Assets/Scripts/Player/PlayerController2D.cs:29`）
- 鼠标左键 或 J：发射雪球（`Assets/Scripts/Player/PlayerShooter.cs:24`）
- 出口门交互：默认需要按 `W` 触发（可在 Inspector 配置 `requireKeyPress/interactKey`，`Assets/Scripts/Core/ExitDoor.cs:25`）

## 3) 关卡与流程（Level System）

### LevelManager（自动常驻）

- `LevelManager` 通过 `RuntimeInitializeOnLoadMethod` 自动创建并 `DontDestroyOnLoad`（`Assets/Scripts/Core/LevelManager.cs:49`）
- 胜利条件：`GetAliveEnemyCount()==0` 且（若存在）所有启用的 `EnemySpawner` 已完成（`Assets/Scripts/Core/LevelManager.cs:124`、`Assets/Scripts/Core/LevelManager.cs:245`）
- 胜利后：
  - 若场景存在 `ExitDoor`，会按配置解锁门（`Assets/Scripts/Core/LevelManager.cs:176`）
  - 默认会延迟 `victoryDelaySeconds` 自动进下一关（`Assets/Scripts/Core/LevelManager.cs:160`、`Assets/Scripts/Core/LevelManager.cs:200`）
- 敌人数统计：优先复用 `GameManager.AliveEnemyCount`（`EnemyController` 体系），并兼容 `EnemyBase`（`Assets/Scripts/Core/LevelManager.cs:267`）

### ExitDoor（可选门）

- `ExitDoor`：玩家进入 Trigger 后，在门范围内按键触发切关；支持“必须清怪后才能进门”（`lockUntilClear`，`Assets/Scripts/Core/ExitDoor.cs:20`）
- 门会强制把自身 Collider2D 设为 Trigger，避免与玩家/敌人产生实体阻挡（`Assets/Scripts/Core/ExitDoor.cs:31`）

### EnemySpawner（波次刷怪，Inspector 可配）

- `EnemySpawner` 支持配置 `spawnPoints`、`waves` 与每波 `SpawnEntry`（`Assets/Scripts/Core/EnemySpawner.cs:23`）
- `LevelManager` 把“所有 spawner 完成 + 场上敌人清空”作为通关门控（`Assets/Scripts/Core/LevelManager.cs:245`）

### LevelSetup_02（运行时关卡装配脚本，原型用）

- `LevelSetup_02` 会在 `Awake()` 清理“上一关遗留敌人/门/运行时 Tilemap”，然后根据当前场景名构建布局：
  - `Level_01`：阶梯攀登型（`Assets/Scripts/Core/LevelSetup_02.cs:109`）
  - `Level_02`：错层回旋型（`Assets/Scripts/Core/LevelSetup_02.cs:166`）
- 该脚本用于“可复现布局的快速原型”。如果改为纯编辑器摆放（Tilemap/敌人/门），可以移除本脚本（见注释 `Assets/Scripts/Core/LevelSetup_02.cs:14`）。

## 4) 目录结构（当前仓库）

### 关键目录

- `Assets/Scenes/`：关卡场景（`Level_01.unity`、`Level_02.unity`）
- `Assets/Scripts/`：运行时代码
- `Assets/Resources/`：运行时 `Resources.Load` 资源（`Block16.png` 给 `RuntimeSpriteLibrary.WhiteSprite` 使用，`Assets/Scripts/Core/RuntimeSpriteLibrary.cs:21`）
- `Assets/Settings/`：URP/2D Renderer/模板场景等资产
- `Packages/`：Unity Package 依赖与锁定
- `ProjectSettings/`：项目设置（Unity 版本、Build Settings、输入系统等）

### 脚本模块（按目录）

- `Assets/Scripts/Core/`
  - `GameManager.cs`：分数/Combo/敌人登记，运行时物理兜底，寿司雨（`Assets/Scripts/Core/GameManager.cs:15`）
  - `LevelManager.cs`：关卡切换与胜利检测（`Assets/Scripts/Core/LevelManager.cs:22`）
  - `ExitDoor.cs`：出口门（`Assets/Scripts/Core/ExitDoor.cs:15`）
  - `EnemySpawner.cs`：波次刷怪（`Assets/Scripts/Core/EnemySpawner.cs:15`）
  - `LevelSetup_02.cs`：运行时生成 Tilemap/敌人/门（`Assets/Scripts/Core/LevelSetup_02.cs:17`）
  - `RuntimeSpriteLibrary.cs`：资源加载 + 运行时生成圆/三角（`Assets/Scripts/Core/RuntimeSpriteLibrary.cs:5`）
  - `Balance/EnemyDropBalance.cs`：敌人死亡掉落权重/分数/排序层等常量（`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`）
  - `Balance/PotionBalance.cs`：药水持续时间/倍率/染色常量（`Assets/Scripts/Core/Balance/PotionBalance.cs:5`）
- `Assets/Scripts/Player/`
  - `PlayerController2D.cs`：移动/跳跃/药水增益/巨大化（`Assets/Scripts/Player/PlayerController2D.cs:12`）
  - `PlayerShooter.cs`：发射雪球投射物（`Assets/Scripts/Player/PlayerShooter.cs:10`）
- `Assets/Scripts/Projectiles/`
  - `SnowballProjectile.cs`：投射物命中敌人触发冻结（`Assets/Scripts/Projectiles/SnowballProjectile.cs:7`）
- `Assets/Scripts/Enemies/`
  - `EnemyController.cs`：旧敌人体系（冻结/变雪球/死亡掉落；并向 `GameManager` 注册计数，`Assets/Scripts/Enemies/EnemyController.cs:10`）
  - `Snowball.cs`：核心雪球玩法（推动/撞墙碎裂/连击事件，`Assets/Scripts/Enemies/Snowball.cs:24`）
  - `EnemyBase.cs`：新敌人 AI 基类（数据驱动 + 巡逻 + 受伤变雪球，`Assets/Scripts/Enemies/EnemyBase.cs:19`）
  - `EnemyDataSO.cs` + `RedDemon.cs`/`YellowFatty.cs`/`BlueMonkey.cs`/`FlyBat.cs`/`SumoBody.cs`/`PumpkinBoss.cs`：扩展示例敌人
- `Assets/Scripts/Rewards/`
  - `RewardSystem.cs`：监听 `Snowball.Broken` 生成寿司（`Assets/Scripts/Rewards/RewardSystem.cs:16`）
  - `PickupItem.cs`：寿司/蛋糕/点心拾取计分（实体落地 + trigger 拾取，`Assets/Scripts/Rewards/PickupItem.cs:16`）
- `Assets/Scripts/PowerUps/`
  - `PowerUpItem.cs`：药水拾取基类（`Assets/Scripts/PowerUps/PowerUpItem.cs:14`）
  - `PowerUpDropBody.cs`：药水落地/碰撞设置（`Assets/Scripts/PowerUps/PowerUpDropBody.cs:15`）
- `Assets/Scripts/Camera/`
  - `CameraFollow2D.cs`：相机跟随（注意命名空间为 `Snow2.Camera`，`Assets/Scripts/Camera/CameraFollow2D.cs:3`）

## 5) 玩法/数据流（关键链路）

- 发射：`PlayerShooter` 运行时创建 `SnowballProjectile`（`Assets/Scripts/Player/PlayerShooter.cs:94`）
- 冻结：投射物命中 `EnemyController` 后调用 `ApplySnowHit()`，累计命中到 `HitsToFreeze` 后进入 Frozen 并挂载 `Snowball`（`Assets/Scripts/Enemies/EnemyController.cs:301`、`Assets/Scripts/Enemies/EnemyController.cs:335`）
- 滚动击杀：雪球速度超过阈值开启 Trigger，触发卷入/击杀并更新本次雪球连击（`Assets/Scripts/Enemies/Snowball.cs:134`、`Assets/Scripts/Enemies/Snowball.cs:521`）
- 奖励：雪球碎裂时触发 `Snowball.Broken`；`RewardSystem` 按卷入数量生成寿司（`Assets/Scripts/Rewards/RewardSystem.cs:36`）
- 掉落：敌人死亡瞬间随机掉落药水/蛋糕/点心（`Assets/Scripts/Enemies/EnemyController.cs:667`；权重见 `Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`）

补充：雪球“离开相机视口自动销毁”的行为默认关闭；如需旧行为可打开 `enableAutoDespawn`（`Assets/Scripts/Enemies/Snowball.cs:59`）。

## 6) 构建与验证命令（CLI）

仓库内没有自定义 `-executeMethod` 构建脚本；命令行主要用于“导入/编译校验”和“跑测试”。

注意：同一个 Unity 工程同一时间只能被一个 Unity 实例打开；若 Editor 正在打开项目，batchmode 会报错并退出。

- 仅导入/编译校验（macOS）：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`
- 仅导入/编译校验（Windows，路径按实际 Unity Hub 安装调整）：
  - `"C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.10f1\\Editor\\Unity.exe" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`
- 跑测试（项目当前未提供 `Assets/Tests`，但 Test Framework 依赖已在）：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -runTests -testPlatform PlayMode -testResults "<repo>/TestResults_PlayMode.xml" -logFile -`
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -runTests -testPlatform EditMode -testResults "<repo>/TestResults_EditMode.xml" -logFile -`
- 快速脚本编译烟测（非 Unity，仅用于本地快速校验 C# 语法/引用）：
  - `dotnet build "<repo>/Assembly-CSharp.csproj"`
  - 说明：`Assembly-CSharp.csproj`/`.sln` 通常由 Unity/IDE 生成，可能受本机 Unity/包解析影响；最终仍以 Unity 导入编译结果为准。

## 7) 代码规范与约定（以当前实现为准）

- 命名空间：统一 `Snow2.*`（例如 `Snow2.Player`、`Snow2.Enemies`、`Snow2.Camera`）
- Unity 6 兼容：读写速度使用编译宏兼容 `Rigidbody2D.linearVelocity`（例如 `Assets/Scripts/Player/PlayerController2D.cs:624`、`Assets/Scripts/Enemies/Snowball.cs:608`）
- 避免命名冲突：因为存在 `namespace Snow2.Camera`，在 `Snow2.*` 内引用相机建议写 `UnityEngine.Camera.main`（例如 `Assets/Scripts/Core/GameManager.cs:247`）
- 输入系统：用 `#if ENABLE_INPUT_SYSTEM` 兼容 Input System；并保留“键盘/旧输入”兜底（例如 `Assets/Scripts/Player/PlayerController2D.cs:208`）
- 运行时兜底优先：关键组件在 `Awake/Start` 中会修正 Rigidbody2D/Collider2D 配置，避免场景误配导致“完全不动/乱弹/卡死”（例如 `Assets/Scripts/Core/GameManager.cs:92`、`Assets/Scripts/Enemies/EnemyController.cs:103`）
- 数值集中：掉落/药水参数集中在 `Snow2.Balance`，避免散落在多个 MonoBehaviour 里（`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`、`Assets/Scripts/Core/Balance/PotionBalance.cs:5`）

（可选但推荐）Layer/Tag 约定：

- Tag：Player 物体建议设置 `Player`（相机/门有 Tag 兜底路径，见 `Assets/Scripts/Camera/CameraFollow2D.cs:18`、`Assets/Scripts/Core/ExitDoor.cs:159`）
- Layer：项目若配置了 `Player`/`Enemy`/`Door`/`Pickup`，脚本会在运行时尝试自动赋层；若未配置则安全跳过（例如 `Assets/Scripts/Player/PlayerController2D.cs:111`、`Assets/Scripts/Core/ExitDoor.cs:59`）

## 8) 仓库健康/注意事项

- 缓存目录：`.gitignore` 已忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等（见 `.gitignore:4`），排查问题时优先确认它们没有被误加入版本控制
- 包依赖目录：`.gitignore` 会忽略 `Packages/` 下除 `manifest.json`/`packages-lock.json` 以外内容（见 `.gitignore:83`），改包版本时以这两个文件为准
- `Resources/` 资源会进入打包体积；`RuntimeSpriteLibrary` 依赖 `Resources.Load<Sprite>("Block16")`（`Assets/Scripts/Core/RuntimeSpriteLibrary.cs:21`），如改名/移动需同步更新
