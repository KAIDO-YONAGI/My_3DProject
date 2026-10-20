# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-10-20`

## 项目定位

本项目是面向 2～5 人短回合乱斗的物理派对游戏，采用 Unity 客户端、Mirror 网络框架和 KCP 专用服务器。工程具备场景引导、自动连接、玩家生成、角色表现装配、客户端位置同步和专用服务器监听链路。

## 当前运行链路

```text
InitialScene
  → InitialLoad 注册并加载 PersistentScene
  → SceneChanger 以 Additive 模式加载 MultiplayerSampleScene

专用服务器
  → NetworkManager 自动启动 Server
  → KcpTransport 在 UDP 7777 监听
  → Mirror 接收连接、Ready 和 AddPlayer
  → 在 NetworkStartPosition 创建 NetworkPlayer

编辑器或普通客户端
  → AutoStartClient 连接 127.0.0.1
  → Mirror 创建本地和远程 NetworkPlayer
  → NetworkCharacterManager 为每个网络根装配角色表现
  → 本地拥有者与远程玩家使用同一角色 Prefab
  → NetworkCharacterManager 按身份启停相机、输入、控制器和碰撞体
  → NetworkTransformReliable 上传并广播位置与旋转
```

## 当前工程入口

- `Assets/Core/Scenes/InitialScene.unity`：构建入口，加载常驻场景。
- `Assets/Core/Scenes/PersistentScene.unity`：常驻管理层，包含场景切换、网络管理、角色表现管理、单机角色和两个网络出生点。
- `Assets/Core/Scenes/TestScenes/MultiplayerSampleScene.unity`：玩法环境，提供地形、碰撞和光照。
- `Assets/Core/Prefabs/NetworkManager.prefab`：Mirror、KCP、HUD 和自动连接配置。
- `Assets/Core/Prefabs/NetworkPlayer.prefab`：玩家网络根，承载网络身份、同步、物理控制器和角色编号同步。
- `Assets/Core/Prefabs/Characters/`：每个角色一份完整 Prefab，包含模型、动画、相机和角色组件。
- `Assets/Core/Animations/Characters/`：每个角色一份 `AnimatorOverrideController`，替换共用动画逻辑中的片段。
- `Assets/Core/Models/Resources/CharacterLocomotion.controller`：共用 Animator 混合树、参数和跳跃转换。
- `Assets/Core/Scripts/Networking/`：自动连接、角色编号同步和角色表现装配。
- `Assets/Core/Scripts/Character/`：输入、移动、重力与动画数据流。
- `Assets/Core/Scripts/Camera/`：第三人称相机轨道、光标、避让与转向数学。
- `Assets/Core/Scripts/Scene/`：场景加载链路、常驻场景注册、时间管理、传送与体素平台。
- `Assets/Core/Scripts/UI/`：画布管理器、焦点栈与按钮。
- `Assets/Core/Scripts/Audio/`：音频配置资产类型、播放器对象池与触发器。
- `Assets/Core/Scripts/SO/`：场景数据与事件通道的 ScriptableObject 类型。
- `Assets/Core/Scripts/Common/`：单例基类与通用枚举。
- `Assets/Plugins/Mirror/`：Mirror 96.11.2 与 KCP 源码。
- `Assets/Plugins/DynamicBone/`：骨骼跟随（发辫等）的第三方实现。
- `D:/Unity/Releases/3D_MultiplayerGame/`：客户端与专用服务器构建输出根目录。

## 代码模块地图

脚本按功能域划分：

```text
Assets/Core/Scripts/
├─ Audio/                  AudioPlayerManager、AudioPlayer、AudioConfigSO、AudioTriggerManager
├─ Camera/                 ThirdPersonCamera、CameraCursorController、CameraOrbitState、CameraRotationMath、CameraCollisionResolver
├─ Character/
│  ├─ Control/             PlayerCharacterController
│  ├─ Input/               CharacterInputReader、CharacterInput
│  ├─ Movement/            CharacterMotor、CharacterMovementSettings
│  │  └─ States/           CharacterMovementStateMachine、CharacterMovementStates
│  ├─ Animation/           CharacterAnimator、CharacterMotion、方向与跳跃动画配置
│  ├─ Interactions/        Attack
│  └─ Presentation/        CharacterBoneFollower、CharacterVisual
├─ Common/                 YSingleton、CommonEnums
├─ Networking/
│  ├─ Connection/            AutoStartClient
│  ├─ Sync/                  CharacterIdSync、CharacterMotionSync、NetworkAudioPlayer
│  └─ CharacterPresentation/ NetworkCharacterManager、CharacterPresentationService
├─ Scene/                  SceneChanger、InitialLoad、PersistentSceneRegistry、TimeManager、Teleport、FloatingSignboard
│  └─ VoxelPlatform/       平台数据、运行时状态、烘焙、碰撞、破坏、碎块与渲染
├─ SO/                     GameSceneSO、SceneLoadEventSO、SceneLoadedEventSO、ToggleCanvasEventSO、VoidEventSO、FeedbackEventSO
├─ StateMachine/           HierarchicalState、HierarchicalStateMachine
└─ UI/                     UIManager、CanvasFocusStack、ICanvasManager、CharacterSelectButton、ButtonSceneToggler、RetryButton、ESCButton
   └─ SystemCanvasManagers/ ESCMenuManager、GameOverCanvasManager、ExitManager
```

五个运动状态是独立类，集中在 `CharacterMovementStates.cs`；枚举、上下文和状态机基础结构集中在 `CharacterMovementStateMachine.cs`。

| 模块 | 实际代码或资源边界 | 当前职责 |
|---|---|---|
| `Character` | `Assets/Core/Scripts/Character/`、`Assets/Core/Scripts/Camera/`、`Assets/Core/Input/`、角色 Prefab | 输入、移动、重力、动画、本地视角和骨骼表现 |
| `Multiplayer` | `Assets/Core/Scripts/Networking/`、Mirror 组件、服务器构建 | 自动连接、玩家身份、角色编号、表现装配、同步协议和专用服务器 |
| `Scene` | `Assets/Core/Scenes/`、`Assets/Core/SO/`、`Assets/Core/Scripts/Scene/` | 场景引导、常驻层、玩法层、加载链路和表现模式切换 |
| `Project` | `ProjectSettings/`、`Packages/`、Prefab、Addressables 配置 | Unity 配置、包依赖、资产布局、组件序列化和构建产物 |
| `Workflow` | `Y_MultipleAgentWorkflow/` | 权威文档路由、功能域租约和维护规则 |

## 角色表现结构

`NetworkPlayer` 是稳定的网络根。服务器为每个连接生成一个网络根，客户端根据 `CharacterIdSync.characterId` 从 `NetworkCharacterManager.characters` 选择一份完整角色 Prefab。

- 单机角色、联机本地角色和远程角色共用同一角色 Prefab。
- `NetworkCharacterManager` 只在运行时实例化选中的完整 Prefab，并按身份启停相机、输入、视觉子对象上的移动控制器和 `CharacterController`。
- 网络根上的组件负责联机运动和碰撞，视觉子对象保留模型、Animator、相机和角色配置。
- 本地相机绑定到网络根的 `PlayerCharacterController.inputSpace`。
- 远程表现不启用相机、音频监听器和本地输入。
- 网络 Transform、角色控制器和同步职责位于 `NetworkPlayer` 根节点，不向视觉子预制件添加网络同步组件。
- 连接完成后，场景里的单机角色由 `NetworkCharacterManager` 收起，活动角色数量与 Mirror 玩家数量一致。

## 角色改造边界

本地采用“代码状态机 + 状态内动画混合”：代码维护 `Idle`、`Run`、`Sprint`、`Rising`、`Falling`，Animator 保留原 Grounded 内 Idle/Run/Sprint 与方向混合，上升和下降继续共用既有 Jump 素材。`CharacterMotor` 每帧只执行一次位移，保留既有加减速、空中控制、相机参考方向与重力积分顺序。

远程动画入口使用 Idle。运动状态、局部方向和动画权重的上行同步字段与约束见 `Multiplayer/Sync_Protocol.md` 的「运动表现」一节。

## 当前网络事实

- Transport 为 `kcp2k.KcpTransport`，使用 UDP `7777`。
- Mirror 的 `onlineScene` 为空，玩法场景由 `SceneChanger` 加载。
- `autoCreatePlayer=true`，出生点选择模式为 `Random`。
- `NetworkTransformReliable.SyncDirection=ClientToServer`。
- `CharacterIdSync` 使用 `SyncVar characterId` 和 `Command CmdSetCharacter`。
- `AutoStartClient` 连接 `127.0.0.1`，断开后每 3 秒重试。
- 客户端和服务器使用相同的网络组件布局与序列化字段。

详细配置见 `Project/Mirror_KCP_Config.md`。
