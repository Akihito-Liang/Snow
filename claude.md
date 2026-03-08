# Project: Snow Bros Clone (Unity 2D)

## 1. 项目概述 (Project Overview)
- **类型**: 2D 动作平台游戏 (Single-Screen Platformer)
- **引擎版本**: Unity 2022.3 LTS
- **渲染管线**: URP (Universal Render Pipeline) 2D
- **核心玩法**: 攻击敌人 -> 冻结成雪球 -> 推动雪球 -> 撞击其他敌人 -> 清版过关。
- **美术风格**: 像素风 (Pixel Art), 16 PPU (Pixels Per Unit), 调色板限制。

## 2. 核心架构 (Architecture)
- **设计模式**: 
  - **Managers**: 单例模式 (`GameManager`, `LevelManager`, `SoundManager`)。
  - **Data**: ScriptableObject 驱动数据 (`EnemyData`, `LevelConfig`)。
  - **Event**: 使用 C# Actions 或 UnityEvents 进行解耦 (Observer Pattern)。
  - **State Machine**: 角色和敌人使用 Animator State Machine 或代码状态机。
  
- **关键系统**:
  - **PlayerController**: 处理移动、跳跃 (Rigidbody2D)、攻击。
  - **SnowballSystem**: 处理雪球物理、推动逻辑、连击检测 (`ComboManager`)。
  - **EnemySystem**: 
    - `EnemyBase` (基类) -> `RedDemon`, `YellowFatty`, `BlueMonkey` (派生类)。
    - 统一使用对象池 (`ObjectPool`) 生成。
  - **LevelSystem**: 
    - 场景管理 (`SceneManager`)。
    - 胜利条件: `EnemyCount == 0`。

## 3. 代码规范 (Coding Standards)
- **命名**: 
  - `PascalCase` for classes/methods (`PlayerController`, `Move()`).
  - `camelCase` for private fields (`_moveSpeed`, `_isGrounded`).
  - `UPPER_CASE` for constants (`MAX_SPEED`).
- **注释**: 关键逻辑必须写 XML 文档注释 (`/// <summary>`).
- **序列化**: 
  - 使用 `[SerializeField] private` 而不是 `public` 变量。
  - 使用 `[Header]`, `[Tooltip]` 增强 Inspector 可读性。
- **性能**:
  - 避免在 `Update` 中使用 `GetComponent`, `Find`, `Instantiate`。
  - 使用 `StringBuilder` 处理 UI 文本拼接。
  - 物理检测使用 `Physics2D.OverlapCircleNonAlloc`。

## 4. 目录结构 (Folder Structure)
- `Assets/_Game/`:
  - `Scripts/`: 核心代码 (`Core`, `Player`, `Enemy`, `Systems`)
  - `Sprites/`: 纹理 (`Characters`, `Environment`, `UI`)
  - `Prefabs/`: 预制体
  - `ScriptableObjects/`: 数据配置文件
  - `Scenes/`: 关卡场景 (`Level_01`, `Level_02`)
  - `Audio/`: 音效

## 5. 当前进度 (Current Status)
- [x] 主角移动与跳跃 (Player Movement)
- [x] 基础攻击与雪球生成 (Shooting & Freezing)
- [ ] 敌人 AI (RedDemon, YellowFatty) - **正在开发中**
- [ ] 雪球推动与连击物理 (Pushing Physics)
- [ ] 关卡管理器与胜利判定 (Level Flow)
- [ ] UI 系统 (Score, HP)

## 6. 常用 Prompt 指令 (Custom Instructions)
- 当我要求实现新功能时，请优先考虑 **ScriptableObject** 配置方案。
- 当涉及物理逻辑时，请使用 `FixedUpdate` 和 `Rigidbody2D`。
- 如果代码超过 50 行，请拆分为多个方法或子类。
- **不要**使用 `PlayerPrefs` 存储游戏进度，请设计一个 `SaveSystem` (JSON)。

---
