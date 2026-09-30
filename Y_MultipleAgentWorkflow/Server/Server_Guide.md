# Mirror 专用服务器

文档 ID：`SERVER-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

## 当前实现

当前没有独立的 `Server/` C# 源码目录。服务端能力由 PersistentScene 中的 Mirror `NetworkManager`、`kcp2k.KcpTransport`、玩家 Prefab 和 Windows Server 构建配置共同承载。

- Headless 构建使用 `HeadlessStartMode=AutoStartServer`。
- KCP 使用 UDP `7777`。
- NetworkManager 的 `onlineScene` 为 `MultiplayerSampleScene`。
- `autoCreatePlayer=true`。
- 当前服务器构建：`D:/Unity/Releases/3D_MultiplayerGame/Server/Server_7_0/My_3DProject.exe`。

## 验证基线

`Server_7_0 + Unity Editor Play` 已验证连接、Ready、玩家生成和移动。验证结束后服务器进程停止，UDP 7777 无监听。

## 尚未实现

服务器权威固定 Tick、玩法状态结算、输入队列和自定义快照尚未落地；这些属于 Proposal，不能从计划文档推断为当前服务器能力。

## 维护触发

修改 Headless 启动、服务器构建配置、NetworkManager、KCP 监听或服务端权威逻辑时更新本文档，并同步 Networking、Protocol 文档。

