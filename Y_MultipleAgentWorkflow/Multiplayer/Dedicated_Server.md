# 专用服务器

文档 ID：`DEDICATED-SERVER`
状态：`Active`
最后核验：`2026-10-11`

## 当前实现

服务端能力由 `PersistentScene` 中的 NetworkManager 实例、`Assets/Core/Prefabs/NetworkManager.prefab`、`KcpTransport`、玩家 Prefab 和 Windows Server 构建共同承载。

- `HeadlessStartMode=AutoStartServer`。
- KCP 使用 UDP `7777`。
- `runInBackground=true`。
- `maxConnections=100`。
- `sendRate=60`。
- `onlineScene` 为空。
- `autoCreatePlayer=true`。
- 出生点选择模式为 `Random`。
- 玩家 Prefab 为 `Assets/Core/Prefabs/NetworkPlayer.prefab`。

## 启动流程

```text
Windows Server 构建启动
  → NetworkManager 检测 Headless 模式
  → StartServer
  → NetworkServer.Listen
  → KcpTransport.ServerStart
  → KcpServer 在 UDP 7777 绑定 socket
  → 接收客户端握手和 Mirror 消息
```

`InitialScene` 加载 `PersistentScene`，`SceneChanger` 加载玩法场景。Mirror 不执行在线场景切换，客户端和服务器使用相同的 Additive 场景结构。场景职责见 `../Scene/Scene_Guide.md`。

## 玩家生命周期

连接认证完成后，客户端发送 Ready 和 AddPlayer。`NetworkManager.OnServerAddPlayer` 选择出生点并创建 `NetworkPlayer`。`NetworkServer.AddPlayerForConnection` 将对象归属给当前连接并向观察者发送 Spawn 数据。

`NetworkCharacterSync.OnStartServer` 写入服务器配置的默认角色编号，作为客户端未上报时的兜底值。本地玩家启动时上报本机默认编号，运行中提交角色切换，两者共用 `CommandSetCharacter` 入口，服务器校验编号后更新 `SyncVar`。Host 的本地玩家同样走该入口。位置与旋转由 `NetworkTransformReliable` 接收客户端拥有者数据，再广播到其他客户端。

纯专用服务器上的 `PlayerCharacterController.Update` 不读取输入，不积分重力，不运行本地位移。客户端的五态代码状态机不参与服务器模拟。服务端对运动表现状态的校验与转发见 `Sync_Protocol.md` 的「运动表现」一节。

## 构建与运行

- 构建目标为 Windows Server。
- 输出根目录为 `D:/Unity/Releases/3D_MultiplayerGame/Server/`。
- 可执行文件名为 `My_3DProject.exe`。
- 服务端启动日志显示监听 UDP `7777`。
- 构建包含 `InitialScene`、`PersistentScene`、`MultiplayerSampleScene` 和 `MultiplayerSampleScene 1`。

## 维护触发

修改 Headless 启动、服务器构建配置、NetworkManager、KCP 监听、玩家生成或服务端同步逻辑时更新本文档。
