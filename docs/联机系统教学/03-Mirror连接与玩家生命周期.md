# Mirror 连接与玩家生命周期

## 1. Mirror 在传输层之上做什么

Mirror 使用 `Transport` 接口收发字节，并在其上提供：

- 客户端与服务器连接状态。
- 消息注册和反序列化。
- Ready 状态。
- 玩家创建请求。
- 网络对象生成与销毁。
- 对象所有权。
- NetworkBehaviour 生命周期。
- SyncVar、Command 和状态同步。

当前项目的 `KcpTransport` 是 Mirror 选中的活动 Transport。

## 2. Transport.cs

路径：

`Assets/Mirror/Core/Transport.cs`

`Transport` 定义统一接口：

- `ClientConnect`
- `ClientSend`
- `ClientDisconnect`
- `ServerStart`
- `ServerSend`
- `ServerDisconnect`
- `ServerStop`
- `ClientEarlyUpdate`
- `ClientLateUpdate`
- `ServerEarlyUpdate`
- `ServerLateUpdate`

它还定义客户端和服务器的连接、数据、断开与错误事件。Mirror 核心只调用这些接口，因此上层生命周期不依赖具体的 KCP socket 实现。

## 3. AutoStartClient.cs

路径：

`Assets/Core/Scripts/Networking/AutoStartClient.cs`

`Update` 每帧检查：

1. 当前进程是否为批处理。
2. Server 或 Client 是否已经活动。
3. 当前运行环境是否允许自动连接。
4. 是否达到下一次连接时间。
5. `NetworkManager.singleton` 是否可用。

满足条件时，它把地址设置为 `127.0.0.1`，调用 `NetworkManager.StartClient`，并将下一次尝试时间设置为 3 秒后。

这个脚本只负责发起连接。连接状态、握手、Ready 和玩家生成由 Mirror 继续处理。

## 4. NetworkManager.cs

路径：

`Assets/Mirror/Core/NetworkManager.cs`

### StartServer

`StartServer` 调用 `SetupServer` 注册消息与回调，随后启动 `NetworkServer.Listen`。当前 `onlineScene` 为空，服务端直接生成场景网络对象并进入可接收连接状态。

### StartClient

`StartClient` 调用 `SetupClient` 注册客户端消息与回调，再调用 `NetworkClient.Connect(networkAddress)`。

### OnClientConnect

当前没有 Mirror 在线场景切换。连接认证完成后，默认 `OnClientConnect` 依次调用：

```text
NetworkClient.Ready
NetworkClient.AddPlayer
```

### OnServerAddPlayer

服务器收到 AddPlayer 请求后：

1. 调用 `GetStartPosition`。
2. 按 `Random` 模式从 NetworkStartPosition 列表选取 Transform。
3. 在该位置实例化 `playerPrefab`。
4. 调用 `NetworkServer.AddPlayerForConnection`。

当前 `playerPrefab` 是 `Assets/Core/Prefabs/Player_Network.prefab`。

## 5. NetworkClient.cs

路径：

`Assets/Mirror/Core/NetworkClient.cs`

### Connect

`Connect` 初始化客户端状态、挂接 Transport 事件，并调用：

```text
Transport.active.ClientConnect(address)
```

KCP 握手完成后，Transport 触发 `OnTransportConnected`，Mirror 更新客户端连接状态并进入认证流程。

### Ready

`Ready` 标记客户端已经准备接收场景中的网络对象，并向服务器发送 Ready 消息。

### AddPlayer

`AddPlayer` 向服务器发送 `AddPlayerMessage`。服务器根据这个消息创建当前连接的玩家对象。

### OnSpawn 与 ApplySpawnPayload

收到 Spawn 数据后，客户端找到或创建对应 Prefab，并通过 `ApplySpawnPayload` 应用：

- `netId`
- 初始位置和旋转
- 是否为本地玩家
- 是否拥有对象
- NetworkBehaviour 初始数据

完成后 Mirror 调用 `NetworkIdentity.OnStartClient`。当前连接拥有的玩家还会调用 `OnStartAuthority` 和 `OnStartLocalPlayer`。

### OnEntityStateMessage

运行阶段收到 Entity State 消息时，客户端根据 `netId` 找到 `NetworkIdentity`，再将数据交给该对象上的 NetworkBehaviour 反序列化。

## 6. NetworkServer.cs

路径：

`Assets/Mirror/Core/NetworkServer.cs`

### Listen

`Listen` 注册 Transport 服务端事件并调用：

```text
Transport.active.ServerStart()
```

KcpTransport 随后在 UDP `7777` 启动监听。

### AddPlayerForConnection

这个方法将玩家对象绑定到连接：

- 设置 `conn.identity`。
- 建立对象所有权。
- 将对象加入连接拥有对象集合。
- 调用 Spawn 流程。
- 向客户端发送玩家对象的生成数据。

### Spawn

`Spawn` 为 `NetworkIdentity` 分配 `netId`，调用服务器生命周期，并根据观察者列表发送 Spawn 消息。

## 7. NetworkConnection

相关文件：

- `Assets/Mirror/Core/NetworkConnection.cs`
- `Assets/Mirror/Core/NetworkConnectionToServer.cs`
- `Assets/Mirror/Core/NetworkConnectionToClient.cs`

`NetworkConnection` 提供消息发送、时间戳、批处理和连接状态基础。

`NetworkConnectionToServer` 表示客户端持有的服务器连接。

`NetworkConnectionToClient` 表示服务器持有的一个客户端连接，保存：

- connectionId
- 玩家 identity
- owned 对象
- Ready 状态
- 远端时间
- 快照缓冲限制

## 8. NetworkMessages.cs

路径：

`Assets/Mirror/Core/NetworkMessages.cs`

这个文件定义 Mirror 核心消息结构。当前链路重点包括：

- `ReadyMessage`
- `AddPlayerMessage`
- `SpawnMessage`
- `ObjectSpawnStartedMessage`
- `ObjectSpawnFinishedMessage`
- `EntityStateMessage`
- `CommandMessage`

消息结构通过 Mirror 生成的序列化代码写入 NetworkWriter，并在接收端从 NetworkReader 还原。

## 9. NetworkIdentity.cs

路径：

`Assets/Mirror/Core/NetworkIdentity.cs`

`NetworkIdentity` 是网络对象的身份中心，负责：

- 保存 `netId`。
- 保存服务器所有者连接。
- 缓存对象上的 NetworkBehaviour。
- 调用 `OnStartServer`。
- 调用 `OnStartClient`。
- 调用 `OnStartAuthority`。
- 调用 `OnStartLocalPlayer`。
- 按组件顺序序列化和反序列化 NetworkBehaviour。

`Player_Network` 的本地与远程身份都由这个组件表达。`isLocalPlayer` 决定当前客户端是否控制这个玩家。

## 10. NetworkBehaviour.cs

路径：

`Assets/Mirror/Core/NetworkBehaviour.cs`

`NetworkBehaviour` 为项目网络脚本提供：

- `isServer`
- `isClient`
- `isLocalPlayer`
- `isOwned`
- `connectionToClient`
- SyncVar 脏位
- Command 发送
- Rpc 发送
- `OnSerialize`
- `OnDeserialize`
- 网络生命周期回调

`NetworkCharacterSync` 继承它，因此可以声明 `SyncVar` 和 `[Command]`。

## 11. NetworkStartPosition.cs

路径：

`Assets/Mirror/Core/NetworkStartPosition.cs`

组件启用时向 `NetworkManager.startPositions` 注册 Transform，停用时移除。`NetworkManager.GetStartPosition` 根据 `playerSpawnMethod` 选择其中一个位置。

`PersistentScene` 中的 A、B 两个 Spawn 对象因此成为网络玩家出生候选点。

## 12. 玩家从连接到本地对象

```text
AutoStartClient.Update
  → NetworkManager.StartClient
  → NetworkClient.Connect
  → KcpTransport.ClientConnect
  → KCP 握手
  → NetworkClient.OnTransportConnected
  → NetworkManager.OnClientConnect
  → NetworkClient.Ready
  → NetworkClient.AddPlayer
  → 服务器收到 AddPlayerMessage
  → NetworkManager.OnServerAddPlayer
  → Instantiate Player_Network
  → NetworkServer.AddPlayerForConnection
  → NetworkServer.Spawn
  → 客户端收到 SpawnMessage
  → NetworkClient.ApplySpawnPayload
  → NetworkIdentity.OnStartClient
  → NetworkCharacterSync.OnStartClient
  → NetworkCharacterManager.ApplyCharacter
```

最后一步才把网络根装配成玩家实际看到的角色。角色装配详见 `04-玩家同步与角色装配.md`。
