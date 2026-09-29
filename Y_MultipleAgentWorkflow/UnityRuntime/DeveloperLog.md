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
