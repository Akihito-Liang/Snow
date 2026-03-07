#+#+#+#+############################################################
# Snow2（Unity 2D）代码库说明（面向接手/协作）
########################################################################

> 目标：5 分钟内搞清楚“项目是什么、入口在哪、怎么跑、怎么改、怎么验证”。

本仓库是一个 Unity 2D 原型项目（URP 2D）。当前玩法主循环是：

1) 主角发射雪球命中敌人 → 2) 敌人逐步冻结 → 3) 敌人变成可推动雪球（Snowball）→ 4) 雪球滚动吸收/击杀敌人形成连击 → 5) 敌人死亡瞬间随机掉落药水/点心奖励；雪球碎裂时生成寿司 → 6) 清版触发寿司雨。

说明：当前仓库未找到 `CLAUDE.md`（仅存在 `AGENTS.md` 与 `README.md`）。

## 1) 引擎与依赖

- Unity 版本：`6000.3.10f1`（见 `ProjectSettings/ProjectVersion.txt:1`）
- 渲染管线：URP 2D（`com.unity.render-pipelines.universal: 17.3.0`，见 `Packages/manifest.json:16`）
- 输入系统：Input System（`com.unity.inputsystem: 1.18.0`，见 `Packages/manifest.json:14`；项目启用见 `ProjectSettings/ProjectSettings.asset:683`）
- 测试框架：Unity Test Framework（`com.unity.test-framework: 1.6.0`，见 `Packages/manifest.json:17`）

## 2) 入口与运行方式

### 场景入口

- Build Settings 场景：`Assets/Scenes/SampleScene.unity`（见 `ProjectSettings/EditorBuildSettings.asset:9`）

### Editor 运行（推荐）

- 用 Unity Hub / Unity Editor 打开仓库根目录（即包含 `Assets/`、`Packages/`、`ProjectSettings/` 的目录）
- 打开 `Assets/Scenes/SampleScene.unity` 并 Play

### 操作说明（当前实现）

- A/D 或 ←/→：移动（Input System + 键盘兜底）
- Space/W/↑：跳跃（带 Jump Buffer + Coyote Time）
- 鼠标左键 或 J：发射雪球（`Assets/Scripts/Player/PlayerShooter.cs:24`）

### 道具/强化（当前实现）

- 药水：红/蓝/黄/绿为“限时强化”，拾取后角色会叠加变色并在右上角显示倒计时（`Assets/Scripts/Player/PlayerController2D.cs:246`、`Assets/Scripts/Core/GameManager.cs:293`）。
- 点心奖励：蛋糕/点心为“加分拾取物”（`Assets/Scripts/Rewards/PickupItem.cs:6`）。
- 掉落来源：药水/蛋糕/点心只在敌人死亡瞬间随机生成并掉落；不会在敌人存活时预先显示（`Assets/Scripts/Enemies/EnemyController.cs:651`）。

## 3) 目录结构（当前仓库）

### 关键目录

- `Assets/Scenes/`：场景（当前仅 `SampleScene.unity`）
- `Assets/Scripts/`：运行时代码
- `Assets/Resources/`：运行时 `Resources.Load` 资源（当前 `Block16.png`）
- `Assets/Settings/`：URP/2D Renderer/模板场景等资产
- `Packages/`：Unity Package 依赖与锁定
- `ProjectSettings/`：项目设置（Unity 版本、Build Settings、输入系统开关等）

### 脚本模块（按目录）

- `Assets/Scripts/Core/`
  - `GameManager.cs`：分数/全局连击/敌人登记、物理/环境兜底修正、Perfect Clear 触发寿司雨、HUD（`OnGUI`，含右上角药水倒计时）；`Awake` 中确保 `RewardSystem` 存在（`Assets/Scripts/Core/GameManager.cs:51`）
  - `RuntimeSpriteLibrary.cs`：运行时通用 Sprite（优先 `Resources.Load("Block16")`，圆形/三角形 Sprite 运行时生成）
  - `Balance/`
    - `PotionBalance.cs`：药水时长/倍率/颜色与角色变色混合权重（`Assets/Scripts/Core/Balance/PotionBalance.cs:1`）
    - `EnemyDropBalance.cs`：敌人死亡掉落概率/权重/分值/显示层级/图层命名（`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:1`）
- `Assets/Scripts/Player/`
  - `PlayerController2D.cs`：移动/跳跃（InputAction + 键盘兜底）、自定义重力、与敌人“弹开”；药水系统（叠加时长、到期恢复、变色融合、绿药水巨大化）（`Assets/Scripts/Player/PlayerController2D.cs:183`）
  - `PlayerShooter.cs`：输入触发发射雪球投射物（运行时新建 `GameObject`）；投射物排序提高避免被遮挡（`Assets/Scripts/Player/PlayerShooter.cs:94`）
- `Assets/Scripts/Projectiles/`
  - `SnowballProjectile.cs`：投射物直线飞行，命中敌人叠加积雪，命中墙/地消失（Unity 6 用 `linearVelocity` 兼容旧版）
- `Assets/Scripts/Enemies/`
  - `EnemyController.cs`：敌人巡逻/避墙避悬崖、受击冻结过渡、冻结后挂载 `Snowball` 并切换碰撞体；敌人 Normal 状态使用“与玩家一致的自定义重力”下落；死亡瞬间掉落药水/蛋糕/点心（`Assets/Scripts/Enemies/EnemyController.cs:159`、`Assets/Scripts/Enemies/EnemyController.cs:651`）
  - `Snowball.cs`：核心雪球玩法（推动、墙反弹计数碎裂、速度阈值开启 Trigger 连击吸收/击杀、碎裂事件）
- `Assets/Scripts/Rewards/`
  - `RewardSystem.cs`：监听 `Snowball.Broken`，按击杀数生成寿司（药水不在这里生成）（`Assets/Scripts/Rewards/RewardSystem.cs:36`）
  - `PickupItem.cs`：拾取逻辑（寿司加分、蛋糕/点心加分；并在运行时把拾取物放到 `Pickup/PowerUp` 图层并忽略与敌人的实体碰撞）（`Assets/Scripts/Rewards/PickupItem.cs:33`）
- `Assets/Scripts/PowerUps/`
  - `PowerUpItem.cs`：药水拾取基类（Trigger 进玩家即应用效果并销毁）
  - `PowerUpDropBody.cs`：药水掉落物理（实体 collider 落地 + trigger 拾取），并忽略与玩家/敌人实体碰撞（`Assets/Scripts/PowerUps/PowerUpDropBody.cs:26`）
  - `RedPotion.cs`/`BluePotion.cs`/`YellowPotion.cs`/`GreenPotion.cs`：具体药水效果（参数来自 `PotionBalance`）
- `Assets/Scripts/Camera/`
  - `CameraFollow2D.cs`：相机平滑跟随（Target 为空时按 Tag `Player` 查找）

## 4) 玩法/数据流（关键事件链）

- 发射：`PlayerShooter` 生成 `SnowballProjectile`（`Assets/Scripts/Player/PlayerShooter.cs:93`）
- 冻结：投射物命中敌人后调用 `EnemyController.ApplySnowHit()`（`Assets/Scripts/Projectiles/SnowballProjectile.cs:55`）
- 变球：敌人累计命中达到阈值后 `FreezeFully()`，挂载 `Snowball` 并切换 CircleCollider2D（`Assets/Scripts/Enemies/EnemyController.cs:284`）
- 连击：`Snowball` 速度超过阈值开启 Trigger，命中敌人后 `AbsorbIntoSnowball` 并累计 combo（`Assets/Scripts/Enemies/Snowball.cs:405`）
- 掉落：
  - 敌人死亡瞬间触发 `TryDropDeathItem()`：随机生成药水/蛋糕/点心（`Assets/Scripts/Enemies/EnemyController.cs:651`）
  - 雪球碎裂/越界触发 `Snowball.Broken`：`RewardSystem` 生成寿司（`Assets/Scripts/Rewards/RewardSystem.cs:36`）
- HUD/清版：`GameManager` 统计敌人数、显示 HUD；Perfect Clear（同一雪球清掉所有初始敌人）触发寿司雨（`Assets/Scripts/Core/GameManager.cs:156`）

## 5) 构建与验证命令（CLI）

仓库内没有自定义 `-executeMethod` 构建脚本；命令行主要用于“导入/编译校验”和“跑测试”。

注意：同一个 Unity 工程同一时间只能被一个 Unity 实例打开；若 Editor 正在打开项目，batchmode 会报错并退出。

- 仅导入/编译校验：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`
- 跑测试（项目当前未提供自定义 Tests 目录，但框架依赖已在）：
  - `"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -runTests -testPlatform PlayMode -testResults "<repo>/TestResults.xml" -logFile -`

### 快速脚本编译（非 Unity，但适合本地快速校验）

- `dotnet build "<repo>/Assembly-CSharp.csproj"`
  - 说明：`Assembly-CSharp.csproj` 由 Unity 生成（文件头写明“Generated file”），可能会在重新生成工程文件时变化；仅建议作为本地/CI 的“快速 C# 编译烟测”，最终仍以 Unity 导入编译结果为准。

## 6) 代码规范与约定（以当前实现为准）

- 命名空间：统一 `Snow2.*`（例如 `Snow2.Player`、`Snow2.Enemies`）
- 运行时兜底优先：多个组件在 `Awake` 中强制设置 `Rigidbody2D` 为 `Dynamic`/`simulated` 并锁旋转，避免场景误配置导致“完全不动”（例如 `Assets/Scripts/Player/PlayerController2D.cs:106`、`Assets/Scripts/Enemies/EnemyController.cs:86`）
- Unity 版本兼容：涉及速度写入时用编译宏兼容 `Rigidbody2D.linearVelocity`（`Assets/Scripts/Projectiles/SnowballProjectile.cs:27`、`Assets/Scripts/Enemies/EnemyController.cs:485`）
- 输入系统：优先 Input System（`ENABLE_INPUT_SYSTEM`），并保留键盘/旧输入兜底以减少“Input 更新模式/焦点问题”带来的不可控（`Assets/Scripts/Player/PlayerController2D.cs:94`）
- 避免 Tag 依赖：核心玩法逻辑尽量不依赖 Tag（例如雪球墙反弹用碰撞法线判定，避免 Tag 未配置异常；见 `Assets/Scripts/Enemies/Snowball.cs:475`）；仅相机跟随使用 Tag `Player` 作为便捷兜底（`Assets/Scripts/Camera/CameraFollow2D.cs:18`）

### 数值/平衡参数（新增约定）

- 玩法数值集中管理：新增 `Snow2.Balance` 命名空间，优先把“可调数值”归档到 `Assets/Scripts/Core/Balance/` 下，而不是散落在各个 MonoBehaviour 的字段上。
  - 药水相关：`Assets/Scripts/Core/Balance/PotionBalance.cs:1`
  - 敌人掉落相关：`Assets/Scripts/Core/Balance/EnemyDropBalance.cs:1`

### 物理/图层（新增约定）

- 玩家与普通敌人（Normal）都使用“自定义重力 + MovePosition”以避免 Unity 重力积分被 MovePosition 干扰（`Assets/Scripts/Player/PlayerController2D.cs:524`、`Assets/Scripts/Enemies/EnemyController.cs:159`）。
- 药水/拾取物应与敌人不发生物理碰撞：
  - 运行时会尝试把拾取物放到 `Pickup`/`Pickups`/`PowerUp` 图层，找不到则退回 `Ignore Raycast`。
  - 并在 `Start` 时对敌人 collider 做 `Physics2D.IgnoreCollision` 兜底（`Assets/Scripts/Rewards/PickupItem.cs:45`、`Assets/Scripts/PowerUps/PowerUpDropBody.cs:38`）。

## 7) 仓库健康/注意事项

- 缓存目录：`.gitignore` 已忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等（见 `.gitignore:4`）；本地运行/导入会生成这些目录，排查问题时优先确认它们没有被误加入版本控制。
- `Resources` 下资源会进入打包体积；当前 `RuntimeSpriteLibrary` 依赖 `Resources.Load<Sprite>("Block16")`，如资源改名/移动需同步更新（`Assets/Scripts/Core/RuntimeSpriteLibrary.cs:20`）。
