########################################################################
# Snow2（Unity 2D）代码库说明（面向接手/协作）
########################################################################

> 目标：5 分钟内搞清楚“项目是什么、入口在哪、怎么跑、怎么改、怎么验证”。

本仓库是一个 Unity 2D 原型项目（URP 2D），玩法接近《雪人兄弟》：

1) 主角发射雪球命中敌人 → 2) 敌人逐步冻结 → 3) 敌人变成可推动雪球（`Snow2.Snowball`）
→ 4) 雪球滚动吸收/击杀敌人形成连击 → 5) 敌人死亡瞬间随机掉落药水/点心奖励
→ 6) 雪球碎裂时生成寿司 → 7) 清版触发寿司雨。

说明：仓库内未找到 `CLAUDE.md`（当前仅存在 `AGENTS.md` 与极简 `README.md`）。

## 1) 引擎与依赖

- Unity 版本：`6000.3.10f1`（见 `ProjectSettings/ProjectVersion.txt:1`）
- 渲染管线：URP 2D（`com.unity.render-pipelines.universal: 17.3.0`，见 `Packages/manifest.json:16`）
- 输入系统：Input System（`com.unity.inputsystem: 1.18.0`，见 `Packages/manifest.json:14`）
- Tilemap（第二关运行时生成用）：`com.unity.2d.tilemap: 1.0.0`（见 `Packages/manifest.json:8`）
- 测试框架：Unity Test Framework（`com.unity.test-framework: 1.6.0`，见 `Packages/manifest.json:17`）

## 2) 入口与运行方式

### 场景入口（按关卡）

- Build Settings 场景：
  - `Assets/Scenes/Level_01.unity`（见 `ProjectSettings/EditorBuildSettings.asset:9`）
  - `Assets/Scenes/Level_02.unity`（见 `ProjectSettings/EditorBuildSettings.asset:12`）

说明：关卡命名约定为 `Level_XX`（两位数）。`LevelManager` 会按命名自动切关（`Assets/Scripts/Core/LevelManager.cs:220`）。

### Editor 运行（推荐）

- 用 Unity Hub / Unity Editor 打开仓库根目录（即包含 `Assets/`、`Packages/`、`ProjectSettings/` 的目录）
- 打开 `Assets/Scenes/Level_01.unity` 并 Play

### 操作说明（当前实现）

- A/D 或 ←/→：移动（Input System + 键盘兜底）
- Space/W/↑：跳跃（Jump Buffer + Coyote Time）
- 鼠标左键 或 J：发射雪球（见 `Assets/Scripts/Player/PlayerShooter.cs:24`）
- 出口门交互：默认需要按 `W`（见 `Assets/Scripts/Core/ExitDoor.cs:20`；可在 Inspector 改键/改为触碰直接触发）

## 3) 关卡系统（Level System）

### LevelManager（自动常驻）

- `LevelManager` 通过 `RuntimeInitializeOnLoadMethod` 自动创建并 `DontDestroyOnLoad`（`Assets/Scripts/Core/LevelManager.cs:49`）。
- 胜利条件：`GetAliveEnemyCount()==0` 且所有启用的 `EnemySpawner` 已结束（`Assets/Scripts/Core/LevelManager.cs:124`、`Assets/Scripts/Core/LevelManager.cs:245`）。
- 胜利后：可选播放胜利音效，并按 `victoryDelaySeconds` 倒计时加载下一关（`Assets/Scripts/Core/LevelManager.cs:167`、`Assets/Scripts/Core/LevelManager.cs:200`）。
- 敌人数统计：优先复用 `GameManager.AliveEnemyCount`（旧体系 `EnemyController`），并额外兼容新体系 `EnemyBase`（`Assets/Scripts/Core/LevelManager.cs:267`）。

### ExitDoor（可选门）

- `ExitDoor`：玩家进入门的 Trigger 后，在门范围内按键触发切关（默认 `W`）；可配置“必须清怪后才能进门”（`Assets/Scripts/Core/ExitDoor.cs:14`、`Assets/Scripts/Core/ExitDoor.cs:116`）。
- 关卡里可手工摆门，也可由 `LevelSetup_02` 运行时生成（见 `Assets/Scripts/Core/LevelSetup_02.cs:287`）。

### EnemySpawner（波次刷怪，Inspector 可配）

- `EnemySpawner` 支持配置 `spawnPoints`、`waves` 与每波的 `SpawnEntry`（`Assets/Scripts/Core/EnemySpawner.cs:23`）。
- `LevelManager` 会把“所有 spawner 完成 + 场上敌人清空”作为通关门控（`Assets/Scripts/Core/LevelManager.cs:245`）。

### Level_02（第二关）

- `Level_02` 目前是从 `Level_01` 复制的基础场景（保留 Player/Camera/GameManager 等基础物体），实际关卡内容由 `LevelSetup_02` 运行时重建：
  - 进入时先清理上一关遗留的敌人/门/运行时生成的 Tilemap（`Assets/Scripts/Core/LevelSetup_02.cs:62`）
  - 运行时生成 Grid + Tilemap，并绘制布局（`Assets/Scripts/Core/LevelSetup_02.cs:166`、`Assets/Scripts/Core/LevelSetup_02.cs:230`）
  - 运行时按坐标生成敌人，并生成顶层 ExitDoor（`Assets/Scripts/Core/LevelSetup_02.cs:211`、`Assets/Scripts/Core/LevelSetup_02.cs:287`）

补充：`LevelSetup_02` 也支持在 `Level_01` 场景里构建“阶梯攀登型”布局（用于快速原型对比，见 `Assets/Scripts/Core/LevelSetup_02.cs:109`）。

## 4) 目录结构（当前仓库）

### 关键目录

- `Assets/Scenes/`：关卡场景（`Level_01.unity`、`Level_02.unity`）
- `Assets/Scripts/`：运行时代码
- `Assets/Resources/`：运行时 `Resources.Load` 资源（`Block16.png` 用于 `RuntimeSpriteLibrary`）
- `Assets/Settings/`：URP/2D Renderer/模板场景等资产
- `Packages/`：Unity Package 依赖与锁定
- `ProjectSettings/`：项目设置（Unity 版本、Build Settings、输入系统开关等）

### 脚本模块（按目录）

- `Assets/Scripts/Core/`
  - `Balance/EnemyDropBalance.cs`：敌人死亡掉落权重/分数/排序层等常量（`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`）
  - `Balance/PotionBalance.cs`：药水持续时间/倍率/染色常量（`Assets/Scripts/Core/Balance/PotionBalance.cs:5`）
  - `GameManager.cs`：分数/连击/敌人登记、环境物理兜底、Perfect Clear 寿司雨
  - `LevelManager.cs`：关卡切换与胜利检测（`Assets/Scripts/Core/LevelManager.cs:22`）
  - `EnemySpawner.cs`：波次刷怪（`Assets/Scripts/Core/EnemySpawner.cs:15`）
  - `ExitDoor.cs`：出口门（`Assets/Scripts/Core/ExitDoor.cs:12`）
  - `LevelSetup_02.cs`：关卡运行时布局与刷怪（Level_01/02 都可用，`Assets/Scripts/Core/LevelSetup_02.cs:17`）
  - `RuntimeSpriteLibrary.cs`：运行时通用 Sprite（`Resources.Load("Block16")` + 运行时生成圆/三角，见 `Assets/Scripts/Core/RuntimeSpriteLibrary.cs:21`）
- `Assets/Scripts/Enemies/`
  - `EnemyController.cs`：旧敌人体系（计数/冻结/死亡掉落）；由 `GameManager` 统计敌人存活数（`Assets/Scripts/Enemies/EnemyController.cs:145`）
  - `Snowball.cs`：核心雪球玩法（推动/撞墙碎裂/连击事件，见 `Assets/Scripts/Enemies/Snowball.cs:24`）
  - `EnemyBase.cs`：新敌人 AI 基类（数据驱动 + 巡逻 + 受伤变雪球，`Assets/Scripts/Enemies/EnemyBase.cs:19`）
  - `EnemyDataSO.cs`：新敌人数据（`Assets/Scripts/Enemies/EnemyDataSO.cs:14`）
  - `RedDemon.cs`：红怪（随机跳跃，`Assets/Scripts/Enemies/RedDemon.cs:11`）
  - `YellowFatty.cs`：吐火怪（持续伤害区域原型，`Assets/Scripts/Enemies/YellowFatty.cs:14`）
  - `BlueMonkey.cs`/`FlyBat.cs`/`SumoBody.cs`/`PumpkinBoss.cs`：其余扩展示例敌人
- `Assets/Scripts/Rewards/`：寿司/蛋糕/点心拾取与奖励生成
- `Assets/Scripts/PowerUps/`：药水掉落与拾取
- `Assets/Scripts/Camera/`：相机跟随（注意命名空间为 `Snow2.Camera`，见 `Assets/Scripts/Camera/CameraFollow2D.cs:3`）

## 5) 玩法/数据流（关键事件链）

- 发射：`PlayerShooter` 运行时创建 `SnowballProjectile`（`Assets/Scripts/Player/PlayerShooter.cs:94`）
- 冻结（旧敌人体系）：投射物命中后调用 `EnemyController.ApplySnowHit()`（`Assets/Scripts/Projectiles/SnowballProjectile.cs:48`）
- 变雪球（新敌人体系）：`EnemyBase.TakeDamage()` 默认直接挂载 `Snow2.Snowball` 并关闭自身（`Assets/Scripts/Enemies/EnemyBase.cs:148`、`Assets/Scripts/Enemies/EnemyBase.cs:164`）
- 奖励：`RewardSystem` 监听 `Snowball.Broken`，按“卷入敌人数量”生成寿司（`Assets/Scripts/Rewards/RewardSystem.cs:26`）
- 药水：当前改为“仅敌人死亡时随机掉落”，并由 `EnemyDropBalance` 控制权重（`Assets/Scripts/Enemies/EnemyController.cs:37`、`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`）

补充：雪球“离开相机视口自动销毁”的行为默认已关闭，避免雪球在屏幕外平白消失；如需旧行为可打开 `enableAutoDespawn`（`Assets/Scripts/Enemies/Snowball.cs:62`）。

## 6) 构建与验证命令（CLI）

仓库内没有自定义 `-executeMethod` 构建脚本；命令行主要用于“导入/编译校验”和“跑测试”。

注意：同一个 Unity 工程同一时间只能被一个 Unity 实例打开；若 Editor 正在打开项目，batchmode 会报错并退出。

- 仅导入/编译校验（macOS）：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`
- 仅导入/编译校验（Windows，路径按实际 Unity Hub 安装调整）：
  - `"C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.10f1\\Editor\\Unity.exe" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`
- 跑测试（项目当前未提供 `Assets/Tests`，但框架依赖已在，可按需补充后使用）：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -runTests -testPlatform PlayMode -testResults "<repo>/TestResults.xml" -logFile -`
- 快速脚本编译烟测（非 Unity，适合本地快速校验 C# 语法/引用）：
  - `dotnet build "<repo>/Assembly-CSharp.csproj"`
  - 说明：`Assembly-CSharp.csproj` 通常由 Unity/IDE 生成，可能受本机 Unity/包解析影响；最终仍以 Unity 导入编译结果为准。

## 7) 代码规范与约定（以当前实现为准）

- 命名空间：统一 `Snow2.*`（例如 `Snow2.Player`、`Snow2.Enemies`、`Snow2.Camera`）
- Unity 6 兼容：速度写入使用编译宏兼容 `Rigidbody2D.linearVelocity`（可参考 `Assets/Scripts/Enemies/EnemyBase.cs:249`）
- 避免命名冲突：由于项目存在 `namespace Snow2.Camera`，在 `Snow2.*` 命名空间内引用相机请用 `UnityEngine.Camera.main`（例如 `Assets/Scripts/Enemies/PumpkinBoss.cs` 已按此处理）
- 场景约定：每关一个 Scene，命名 `Level_XX`，并确保加入 Build Settings（见 `ProjectSettings/EditorBuildSettings.asset:9`）
- 运行时兜底优先：关键组件在 `Awake` 中修正 Rigidbody2D 配置，避免场景误配导致“完全不动”

补充约定：

- 平衡参数集中：掉落/药水数值优先放在 `Snow2.Balance`（见 `Assets/Scripts/Core/Balance/EnemyDropBalance.cs:5`、`Assets/Scripts/Core/Balance/PotionBalance.cs:5`），避免散落在多个 MonoBehaviour 里。

## 8) 仓库健康/注意事项

- 缓存目录：`.gitignore` 已忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等；排查问题时优先确认它们没有被误加入版本控制
- 包依赖目录：`.gitignore` 会忽略 `Packages/` 目录下除 `manifest.json`/`packages-lock.json` 以外的内容（见 `.gitignore:83`），改包版本时以这两个文件为准
- `Resources` 下资源会进入打包体积；当前 `RuntimeSpriteLibrary` 依赖 `Resources.Load<Sprite>("Block16")`（`Assets/Scripts/Core/RuntimeSpriteLibrary.cs:21`），如资源改名/移动需同步更新
