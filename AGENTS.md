# Snow2（Unity）代码库架构概览

> 目标：让后续接手的开发/重构/测试/排障人员在 5 分钟内了解“项目是什么、入口在哪、怎么跑、怎么改”。

本仓库是一个 Unity 2D 原型项目（URP 2D），当前实现了一个最小可玩的“雪球→冻结→推动→撞击→清版/奖励”循环。

## 1) Project Overview

### 引擎与渲染
- Unity 版本：`6000.3.10f1`（见 `ProjectSettings/ProjectVersion.txt:1`）
- 渲染管线：URP（见 `Packages/manifest.json:16` 依赖 `com.unity.render-pipelines.universal`）
- 2D 方向：依赖 2D 包（动画、Tilemap、SpriteShape 等，见 `Packages/manifest.json:3`）

### 运行时结构（高层）
- **Scene 作为关卡/对象真源**：场景里放置 Player、Enemies、Environment、Background、GameRoot 等（见 `Assets/Scenes/SampleScene.unity`）
- **脚本入口**：主要通过场景挂载的 `MonoBehaviour` 驱动（例如 `GameManager`、`PlayerController2D`）
- **运行时生成对象**：投射物、掉落物、寿司雨为运行时 `new GameObject(...)` 生成（见 `Assets/Scripts/Core/GameManager.cs:126`、`Assets/Scripts/Player/PlayerShooter.cs:65`）

### 核心模块与职责（按命名空间/目录）
- `Snow2`（全局）
  - `Assets/Scripts/Core/GameManager.cs`：分数/连击/清版寿司雨、敌人存活登记、HUD（`OnGUI`）
  - `Assets/Scripts/Core/RuntimeSpriteLibrary.cs`：运行时加载通用 Sprite（当前从 `Resources` 加载）
- `Snow2.Player`
  - `Assets/Scripts/Player/PlayerController2D.cs`：移动/跳跃（基于 Input System `InputAction`）、设置刚体约束
  - `Assets/Scripts/Player/PlayerShooter.cs`：鼠标/键盘触发射击并生成雪球投射物
- `Snow2.Projectiles`
  - `Assets/Scripts/Projectiles/SnowballProjectile.cs`：投射物飞行、命中敌人触发“积雪/冻结”
- `Snow2.Enemies`
  - `Assets/Scripts/Enemies/EnemyController.cs`：敌人状态（Normal/Frozen/Dead）、受击变白、冻结后变“雪球”并可撞死其他敌人
- `Snow2.Rewards`
  - `Assets/Scripts/Rewards/PickupItem.cs`：寿司/药水拾取、越界销毁
- `Snow2.Camera`
  - `Assets/Scripts/Camera/CameraFollow2D.cs`：相机跟随 Player（LateUpdate 平滑跟随）

### 资源与场景入口
- 主场景：`Assets/Scenes/SampleScene.unity`
- 输入资源：`Assets/InputSystem_Actions.inputactions`（项目也启用了 Input System，见 `ProjectSettings/ProjectSettings.asset:683`）
- 通用方块 Sprite：`Assets/Resources/Block16.png`（`RuntimeSpriteLibrary` 使用 `Resources.Load`，见 `Assets/Scripts/Core/RuntimeSpriteLibrary.cs:19`）

## 2) Build & Commands

### 开发运行（推荐）
- 用 Unity Hub / Unity Editor 打开项目目录：`/Users/bytedance/project/unity_project/Snow2`
- 打开并运行主场景：`Assets/Scenes/SampleScene.unity`

### 命令行（Unity batchmode）
本项目没有自定义构建脚本；如需 CI/批处理，可使用 Unity Editor 的 batchmode。
（下列是 Unity Editor 常用参数形式，项目路径与 Editor 路径按你的机器调整）

- 编译/导入验证：
  - `"<Unity.app>/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath "<repo>" -logFile -`

> 说明：仓库内当前未提供任何 CI 配置文件或构建流水线脚本。

## 3) Code Style

### 目录与命名
- 代码集中在 `Assets/Scripts/**`，按功能分目录：`Core/Player/Enemies/Projectiles/Rewards/Camera`
- 命名空间统一以 `Snow2.*` 开头（例如 `Snow2.Player`、`Snow2.Enemies`）
- 类型命名使用 PascalCase；场景对象名使用可读的英语标识（例如 `Player`、`Enemies`）

### Unity 6 / 2D 物理注意点
- 本项目使用 `Rigidbody2D.linearVelocity`（Unity 6 新 API）而非旧的 `velocity`（见 `PlayerController2D`、`EnemyController`）
- 对“约束/冻结”的配置尽量在运行时显式设置（例如 `freezeRotation=true`），避免不同版本序列化位差异造成行为变化（见 `Assets/Scripts/Player/PlayerController2D.cs:28`）

### 输入系统约定
- 项目启用了 Input System（见 `ProjectSettings/ProjectSettings.asset:683`），避免使用旧输入 API（`UnityEngine.Input.*`）
- `PlayerController2D` 通过自建 `InputAction` 读取移动/跳跃；`PlayerShooter` 通过 `Mouse.current`/`Keyboard.current` 读取射击（Input System）

## 4) Testing

### 框架现状
- 项目依赖 Unity Test Framework：`com.unity.test-framework`（见 `Packages/manifest.json:17`）
- 当前 `Assets` 下未发现自定义测试用例目录（例如 `Assets/**/Tests/**/*.cs` 不存在）

### 建议的测试落点（基于当前结构）
- 纯逻辑可优先抽到非 `MonoBehaviour` 类以便 EditMode 测试；现阶段大部分逻辑在 `MonoBehaviour` 内，测试更偏 PlayMode。

## 5) Security

### 代码扫描结果（基于当前 `Assets/Scripts`）
- 未发现网络访问：`UnityWebRequest`/`HttpClient`/`System.Net`（脚本搜索无匹配）
- 未发现本地持久化：`PlayerPrefs`/`System.IO` 文件读写（脚本搜索无匹配）
- 未发现明显硬编码敏感信息关键词：`SECRET`/`API_KEY`/`TOKEN`/`PASSWORD`（搜索无匹配）

### 需要注意的点（与当前实现相关）
- `Resources` 资源会被打包进 Player 构建；当前 `RuntimeSpriteLibrary` 依赖 `Resources.Load<Sprite>("Block16")`，确保该资源路径稳定。

## 6) Configuration

### 关键配置文件
- Unity 版本锁定：`ProjectSettings/ProjectVersion.txt`
- Package 依赖：`Packages/manifest.json` 与锁文件 `Packages/packages-lock.json`
- 输入系统开关：`ProjectSettings/ProjectSettings.asset:683`（`activeInputHandler: 1`）

### 场景与资源
- 主场景：`Assets/Scenes/SampleScene.unity`
- 运行时 Sprite 资源：`Assets/Resources/Block16.png`

### 贡献/协作提示
- 本项目的可维护关卡内容应以 Scene/Prefab 为主；当前投射物/掉落物仍是运行时生成，若后续需要可视化配置，建议迁移为 Prefab 引用再 Instantiate。

