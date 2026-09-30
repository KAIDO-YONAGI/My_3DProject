# UnityRuntime 开发记录

## 2026-09-20：建立 Unity 运行时权威指南

- 证据：Unity 版本、Packages、Build Settings、场景、Prefab 和事件资产。
- 记录了场景层级、Prefab 分工、事件资产和构建场景状态。

## 2026-09-29：清理网络退役后的场景与资产

- 证据：`MultiplayerSampleScene.unity`、角色 Prefab、`Assets/Core/Scripts/` 目录状态。
- 场景删除 `NetManager`、`PlayerPositionManager` 节点；角色 Prefab 剥离 `SyncCharacter` 组件引用。
- 删除损坏资产 `ConnectResultChannel.asset`。

## 2026-09-29：整理脚本目录

- 证据：`Assets/Core/Scripts/` 目录现状、Unity Console 编译 0 error。
- `BoolEventChannelSO.cs` 移入 `Assets/Core/FrameWork/Scripts/SO/`，GUID 与资产绑定保持完好。
- `Networking/` 3 个 Mirror 脚本保持原位，被 `Player_Network.prefab` 和 `LobbyScene.unity` 按 GUID 引用。

## 2026-09-29：安装 Mirror 并建立联机链路

- 证据：`Assets/Mirror/`（96.11.2）、`LobbyScene.unity`、`Player_Network.prefab`、`Assets/Core/Scripts/Networking/` 三个脚本、构建产物、连接日志。
- Mirror 96.11.2 安装进工程；新建 `LobbyScene`（NetworkManager + KcpTransport 7777）；Build Settings 场景表配置为 LobbyScene 与 MultiplayerSampleScene。
- 新增脚本：`NetworkPlayerController`、`LocalPlayerCamera`、`AutoStartServerBuild`。
- 构建客户端 `Client_3_0` 与专用服务器 `Server_3_0`；双客户端连接服务器验证通过。
- 连接地址使用 `127.0.0.1`：`localhost` 解析为 `::1`，服务器 KCP socket 降级为 IPv4 绑定时握手包丢失。
- 新增 `UnityRuntime/Mirror_KCP_Config.md`。维护计数 `3/5`。

## 2026-09-29：更新为当前联机入口与玩家同步方案

- 本条取代上一条记录中的 `LobbyScene`、`Player_Network.prefab`、`Client_3_0` 和 `Server_3_0` 当前状态描述；旧条目仅保留为历史过程。
- 联机入口改为 `PersistentScene`，`onlineScene` 指向 `MultiplayerSampleScene`，Build Settings 同时启用两者。
- NetworkManager 当前玩家 Prefab 为 `Assets/Core/Prefabs/CharactersForSync/娜娜莉（华丽飞踢）.prefab`。
- 新增 `AutoStartClient`：编辑器和普通客户端连接 `127.0.0.1`，每 3 秒重试，批处理和已启动网络端不重复连接。
- `PlayerCharacterController` 保持普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 限制联机输入归属；`NetworkTransformReliable` 改为 `ClientToServer`。
- 客户端 `Client_5_0`、服务器 `Server_7_0` 构建成功；服务端配合编辑器验证角色移动约 `2.82m` 且 2 秒后未回弹，Console 0 error。
- 验证结束后已停止服务器、清理 UDP 7777 监听并退出 Editor Play。

## 2026-09-30：确认 Player_Network 与本地模型目录配置

- `NetworkManager.playerPrefab` 更新为 `Assets/Core/Prefabs/Player_Network.prefab`；`PersistentScene` 中的 NetworkManager 实例保持启用。
- `NetworkPlayerModel.models[]` 使用本地模型编号加载视觉模型，模型内的 `ThirdPersonCamera` 和 Animator 不迁移到网络根对象。
- `NetworkManager.prefab` 默认关闭 `autoConnectInEditor`，避免单机编辑器 Play 被自动连接流程干扰；需要联机时仍可通过 HUD 或显式启动 Host。
- 2026-09-30 在正确 Unity 实例 `My_3DProject@6d686e37950b774c` 中完成 Host、模型切换和截图验证。
