# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-09-30`

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
  → 在 NetworkStartPosition 创建 Player_Network

编辑器或普通客户端
  → AutoStartClient 连接 127.0.0.1
  → Mirror 创建本地和远程 Player_Network
  → NetworkCharacterManager 为每个网络根装配角色表现
  → 本地拥有者使用带相机的本地角色 Prefab
  → 远程玩家使用同步角色 Prefab
  → NetworkTransformReliable 上传并广播位置与旋转
```

## 当前工程入口

- `Assets/Core/Scenes/InitialScene.unity`：构建入口，加载常驻场景。
- `Assets/Core/Scenes/PersistentScene.unity`：常驻管理层，包含场景切换、网络管理、角色表现管理、单机角色和两个网络出生点。
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`：玩法环境，提供地形、碰撞和光照。
- `Assets/Core/Prefabs/NetworkManager.prefab`：Mirror、KCP、HUD 和自动连接配置。
- `Assets/Core/Prefabs/Player_Network.prefab`：玩家网络根，承载网络身份、同步、物理控制器和角色编号同步。
- `Assets/Core/Prefabs/CharactersForLocal/`：本地拥有者使用的完整角色表现，包含本地相机。
- `Assets/Core/Prefabs/CharactersForSync/`：远程玩家使用的同步角色表现。
- `Assets/Core/Scripts/Networking/`：自动连接、角色编号同步和角色表现装配。
- `Assets/Core/Scripts/Movement/`：输入、移动、重力、动画和第三人称相机。
- `Assets/FrameWork/`：场景管理、时间管理、UI 与 ScriptableObject 基础设施。
- `Assets/Mirror/`：Mirror 96.11.2 与 KCP 源码，本地插件目录。
- `D:/Unity/Releases/3D_MultiplayerGame/`：客户端与专用服务器构建输出根目录。

## 代码模块地图

| 模块 | 实际代码或资源边界 | 当前职责 |
|---|---|---|
| `Client` | `Assets/Core/Scripts/Movement/`、本地角色 Prefab、第三人称相机 | 输入、移动、重力、动画和本地视角 |
| `Networking` | `Assets/Core/Scripts/Networking/`、Mirror 组件 | 自动连接、玩家身份、角色编号、表现装配和同步 |
| `Server` | NetworkManager、KcpTransport、服务器构建 | UDP 监听、连接管理、玩家生成和状态转发 |
| `Protocol` | Mirror 消息、SyncVar、Command、NetworkTransformReliable | 连接消息、角色编号和变换数据的序列化 |
| `UnityRuntime` | `ProjectSettings/`、`Packages/`、场景和 Prefab | Unity 配置、构建列表、组件序列化和资源引用 |
| `Workflow` | `Y_MultipleAgentWorkflow/` | 权威文档路由、知识域租约和维护规则 |

## 角色表现结构

`Player_Network` 是稳定的网络根。服务器为每个连接生成一个网络根，客户端根据网络身份在该根下面装配视觉角色。

- 本地拥有者装配 `CharactersForLocal` 中的完整角色 Prefab。
- 远程玩家装配 `CharactersForSync` 中的同步角色 Prefab。
- 角色编号由 `NetworkCharacterSync.characterId` 统一选择两套数组中的同一角色。
- 视觉子对象自带的移动控制器和 `CharacterController` 在装配后停用，网络根上的组件负责运动和碰撞。
- 本地相机绑定到网络根的 `PlayerCharacterController.inputSpace`。
- 远程表现不启用相机、音频监听器和本地输入。
- 连接完成后，场景里的单机角色由 `NetworkCharacterManager` 收起，活动角色数量与 Mirror 玩家数量一致。

## 当前网络事实

- Transport 为 `kcp2k.KcpTransport`，使用 UDP `7777`。
- Mirror 的 `onlineScene` 为空，玩法场景由 `SceneChanger` 加载。
- `autoCreatePlayer=true`，出生点选择模式为 `Random`。
- `NetworkTransformReliable.SyncDirection=ClientToServer`。
- `NetworkCharacterSync` 使用 `SyncVar characterId` 和 `Command CmdSetCharacter`。
- `AutoStartClient` 连接 `127.0.0.1`，断开后每 3 秒重试。
- 客户端和服务器使用相同的网络组件布局与序列化字段。

详细配置见 `UnityRuntime/Mirror_KCP_Config.md`，逐文件教学见 `docs/联机系统教学/README.md`。
