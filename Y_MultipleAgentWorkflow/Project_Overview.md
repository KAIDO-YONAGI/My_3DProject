# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-09-29`

## 项目定位

本项目是面向 2～5 人短回合乱斗的物理派对游戏，采用 Unity 客户端与 Mirror 专用服务器。当前工程已经建立可运行的最小联机链路；`docs/plan/00-改造计划总览.md` 中的固定 Tick、`InputFrame`、快照插值和预测校正属于后续计划，不代表当前已实现。

## 当前运行链路

```text
专用服务器启动
  → PersistentScene 中的 NetworkManager + KcpTransport 监听 UDP 7777

编辑器或普通客户端启动
  → PersistentScene 中的 AutoStartClient 连接 127.0.0.1
  → Mirror 切换到 MultiplayerSampleScene
  → 自动生成当前玩家 Prefab
  → 本地玩家采集输入并以 ClientToServer 方向同步位置
  → 服务端向其他客户端广播状态
```

## 当前工程入口

- `Assets/Core/Scenes/PersistentScene.unity`：联机启动与常驻场景，包含 NetworkManager、KCP、HUD 和自动客户端连接。
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`：在线玩法场景，提供地形、光照和玩法环境。
- `Assets/Core/Prefabs/CharactersForSync/娜娜莉（华丽飞踢）.prefab`：NetworkManager 当前玩家 Prefab。
- `Assets/Core/Scripts/Networking/`：Mirror 集成脚本，包括 `AutoStartClient`、`NetworkCharacterManager`、`LocalPlayerCamera` 和 `NetworkPlayerController`。
- `Assets/Core/Scripts/Movement/Runtime/PlayerCharacterController.cs`：通用角色输入与移动入口；保持普通 `MonoBehaviour`，联机时仅本地玩家采集输入。
- `Assets/Core/FrameWork/`：单机框架、场景管理、UI 与 ScriptableObject 事件基础设施。
- `Assets/Mirror/`：Mirror 96.11.2 本地插件，不入库。
- `D:/Unity/Releases/3D_MultiplayerGame/`：客户端与专用服务器构建输出根目录。

## 代码模块地图

| 模块 | 实际代码或资源边界 | 当前职责 |
|---|---|---|
| `Client` | `Assets/Core/Scripts/Movement/`、角色 Prefab、客户端相机 | 输入采集、角色移动、动画表现、本地相机 |
| `Networking` | `Assets/Core/Scripts/Networking/`、Mirror 组件 | 客户端连接、玩家网络行为、本地玩家归属和相机隔离 |
| `Server` | `PersistentScene` 的 Mirror NetworkManager、Headless 构建 | 监听 KCP、管理连接和生成玩家；当前没有独立 Server 脚本目录 |
| `Protocol` | Mirror 内置消息、NetworkTransformReliable 序列化边界 | 当前没有自定义协议源码；未来自定义消息和状态快照归此模块 |
| `UnityRuntime` | `ProjectSettings/`、`Packages/`、`Assets/Core/Scenes/`、`Assets/Core/Prefabs/` | Unity 序列化配置、场景、Prefab、构建设置和运行时资产 |
| `Workflow` | `Y_MultipleAgentWorkflow/` | 权威文档路由、知识域租约和维护规则 |

各代码模块的详细入口见根路由对应的模块 Guide。模块文档不得代替实际源码；没有独立源码的模块必须明确记录其承载位置和当前空白。

## 当前网络事实

- Transport 为 `kcp2k.KcpTransport`，UDP 端口为 `7777`。
- NetworkManager 的 `onlineScene` 指向 `MultiplayerSampleScene`，`autoCreatePlayer=true`。
- 当前玩家的 `NetworkTransformReliable.SyncDirection=ClientToServer`。
- `PlayerCharacterController` 通过可选 `NetworkIdentity` 限制联机输入归属；没有 `NetworkIdentity` 的单机 Prefab 保持原行为。
- 编辑器和普通客户端由 `AutoStartClient` 自动连接，断线后每 3 秒重试；批处理或已经启动 Server/Client 时不重复连接。
- 详细配置与故障边界见 `UnityRuntime/Mirror_KCP_Config.md`。

## 最近验证基线

- 客户端：`Client/Client_5_0/My_3DProject.exe`，构建成功，0 error、1 warning。
- 服务器：`Server/Server_7_0/My_3DProject.exe`，构建成功，0 error、1 warning。
- `Server_7_0 + Unity Editor Play` 已验证连接、Ready、玩家生成与本地所有权。
- 本地玩家前进约 `2.82m`，等待 2 秒后未被服务端位置拉回；Unity Console 为 0 error，用户确认角色可以移动。
- 验证结束后服务器进程已停止，UDP 7777 无监听，编辑器已退出 Play。

## 计划与边界

- 固定 Tick、`InputFrame`、自定义状态快照、客户端预测与服务器权威玩法结算仍是 Proposal，以 `docs/plan/` 为计划来源。
- 当前实现以 Mirror 组件直接同步为主，不应把计划文档中的目标架构描述成已落地事实。
- 客户端和服务器必须使用相同组件布局的构建；混用新旧构建可能触发 Mirror 反序列化异常。
