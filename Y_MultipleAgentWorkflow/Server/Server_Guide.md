# Mirror 专用服务器

文档 ID：`SERVER-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

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

`InitialScene` 加载 `PersistentScene`，`SceneChanger` 加载 `MultiplayerSampleScene`。Mirror 不执行在线场景切换，客户端和服务器使用相同的 Additive 场景结构。

## 玩家生命周期

连接认证完成后，客户端发送 Ready 和 AddPlayer。`NetworkManager.OnServerAddPlayer` 选择出生点并创建 `NetworkPlayer`。`NetworkServer.AddPlayerForConnection` 将对象归属给当前连接并向观察者发送 Spawn 数据。

`NetworkCharacterSync.OnStartServer` 设置默认角色编号。客户端提交角色切换时，服务器执行 `CmdSetCharacter` 校验并更新 `SyncVar`。位置与旋转由 `NetworkTransformReliable` 接收客户端拥有者数据，再广播到其他客户端。

## 构建与运行

- 构建目标为 Windows Server。
- 输出根目录为 `D:/Unity/Releases/3D_MultiplayerGame/Server/`。
- 可执行文件名为 `My_3DProject.exe`。
- 服务端启动日志显示监听 UDP `7777`。
- 构建包含 `InitialScene`、`PersistentScene` 和 `MultiplayerSampleScene`。

## 维护触发

修改 Headless 启动、服务器构建配置、NetworkManager、KCP 监听、玩家生成或服务端同步逻辑时更新本文档，并同步 Networking、Protocol 和 UnityRuntime 文档。
