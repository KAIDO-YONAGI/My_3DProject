# UDP 与 KCP 传输链路

## 1. 数据链路

当前网络数据经过以下层次：

```text
Mirror 消息
  → KcpTransport
  → KcpClient 或 KcpServer
  → KcpPeer
  → Kcp
  → UDP socket
  → 网络
```

接收方向按相反顺序返回。UDP 负责传输数据报，KCP 负责可靠通道，kcp2k 负责连接状态、Cookie、可靠与非可靠通道封装，`KcpTransport` 把这些能力接入 Mirror。

## 2. UDP socket

UDP 是当前传输链路最底层的系统接口。它提供：

- 把一段字节发送到目标 IP 和端口。
- 从本地端口接收完整数据报。
- 保留数据报边界。
- 以非阻塞模式参与 Unity 每帧更新。

服务端在 UDP `7777` 绑定 socket。客户端解析 `127.0.0.1`，创建 UDP socket 并连接到服务器端点。

客户端使用 `Socket.Receive` 和 `Socket.Send`。服务端使用 `Socket.ReceiveFrom` 和 `Socket.SendTo`，因为同一个服务器 socket 同时服务多个远端端点。

## 3. KcpTransport.cs

路径：

`Assets/Mirror/Transports/KCP/KcpTransport.cs`

`KcpTransport` 继承 Mirror `Transport`，是 Mirror 与 kcp2k 的适配层。

### Awake

`Awake` 读取 Inspector 参数并创建 `KcpConfig`，随后创建 `KcpClient` 和 `KcpServer`。创建时传入回调，将 kcp2k 事件映射到 Mirror 事件：

```text
kcp2k 连接成功
  → OnClientConnected 或 OnServerConnected

kcp2k 收到数据
  → OnClientDataReceived 或 OnServerDataReceived

kcp2k 断开
  → OnClientDisconnected 或 OnServerDisconnected

kcp2k 错误
  → Mirror TransportError
```

### 客户端方法

- `ClientConnect` 调用 `KcpClient.Connect`。
- `ClientSend` 把 Mirror channelId 映射为 KCP 可靠或非可靠通道。
- `ClientEarlyUpdate` 调用 `KcpClient.TickIncoming`。
- `ClientLateUpdate` 调用 `KcpClient.TickOutgoing`。

### 服务端方法

- `ServerStart` 调用 `KcpServer.Start(Port)`。
- `ServerSend` 根据 connectionId 找到远端并发送。
- `ServerEarlyUpdate` 接收并处理 UDP 数据。
- `ServerLateUpdate` 刷新 KCP 发送队列。

Mirror 只依赖 Transport 接口，不直接操作 UDP socket。

## 4. KcpConfig.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/highlevel/KcpConfig.cs`

`KcpConfig` 保存 KCP 与 socket 参数：

- `DualMode`
- `RecvBufferSize`
- `SendBufferSize`
- `NoDelay`
- `Interval`
- `FastResend`
- `CongestionWindow`
- `SendWindowSize`
- `ReceiveWindowSize`
- `Timeout`
- `MaxRetransmits`

`KcpTransport.Awake` 从 Unity Inspector 创建这个配置对象，客户端和服务器再用同一组参数初始化各自的 `KcpPeer`。

## 5. KcpClient.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/highlevel/KcpClient.cs`

### Connect

`Connect` 完成以下工作：

1. 解析服务器主机名。
2. 选择地址族。
3. 创建 UDP Datagram socket。
4. 设置非阻塞模式和 socket 缓冲。
5. 连接服务器端点。
6. 重置 KCP 状态。
7. 发送 Hello 握手。

UDP 的 `Connect` 在这里用于固定远端端点，使客户端后续可以直接调用 `Send` 和 `Receive`。

### RawReceive 与 RawSend

`RawReceive` 从 UDP socket 读取一个数据报。`RawSend` 把 kcp2k 生成的数据报发给服务器。这两个方法只处理原始字节和 socket 异常。

### RawInput

`RawInput` 解析 kcp2k 数据报头：

1. 读取可靠或非可靠通道标记。
2. 读取连接 Cookie。
3. 校验数据长度。
4. 把可靠数据交给 `OnRawInputReliable`。
5. 把非可靠数据交给 `OnRawInputUnreliable`。

### TickIncoming 与 TickOutgoing

`TickIncoming` 循环读取当前帧可用的 UDP 数据报，再调用 `KcpPeer.TickIncoming` 处理 KCP 状态。`TickOutgoing` 刷新待发送数据、Ping 和 KCP 更新。

## 6. KcpServer.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/highlevel/KcpServer.cs`

### Start

`Start` 创建服务端 UDP socket：

1. 根据 `DualMode` 选择 IPv6 Dual Mode 或 IPv4。
2. 设置非阻塞模式。
3. 设置收发缓冲。
4. 绑定 UDP `7777`。

### RawReceiveFrom

`RawReceiveFrom` 接收数据报和远端端点。kcp2k 根据远端端点计算 connectionId，使同一个 UDP socket 可以区分不同客户端。

### 连接创建

服务器收到尚未登记端点的数据时，先检查数据是否构成有效握手。握手通过后调用 `CreateConnection` 创建 `KcpServerConnection`。

### TickIncoming 与 TickOutgoing

`TickIncoming` 完成两层循环：

1. 从 UDP socket 读取所有当前可用数据报。
2. 让每个 `KcpServerConnection` 处理 KCP 收包、超时和连接状态。

`TickOutgoing` 遍历连接并刷新发送队列。

## 7. KcpServerConnection.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/highlevel/KcpServerConnection.cs`

每个远端客户端对应一个 `KcpServerConnection`。它继承 `KcpPeer`，保存：

- 当前 connectionId。
- 当前连接 Cookie。
- 将原始数据发送回指定端点的回调。
- 连接、数据、断开和错误回调。

服务端共享 UDP socket，每个连接拥有独立 KCP 状态、序号、确认、窗口和超时信息。

## 8. KcpPeer.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/highlevel/KcpPeer.cs`

`KcpPeer` 是客户端和服务器连接共用的协议层。

### Reset

`Reset` 创建低层 `Kcp` 对象，并应用：

- NoDelay
- Interval
- FastResend
- 发送窗口
- 接收窗口
- MTU
- 最大重传
- 超时

### 连接状态

连接状态由 `KcpState` 表达。握手从 Connected 进入 Authenticated。业务 Data 在认证完成后交给 Mirror。

### Cookie 与通道头

可靠数据报包含：

```text
1 字节通道标记
4 字节 Cookie
KCP 数据
```

非可靠数据报同样携带通道标记与 Cookie，数据内容直接交给上层。Cookie 用于确认数据属于当前握手产生的连接。

### 可靠发送

`SendReliable` 把上层数据包封装为 kcp2k Data，再交给 `Kcp.Send`。KCP 负责分片、序号、确认与重传。

### 非可靠发送

`SendUnreliable` 生成通道头和 Cookie 后直接调用原始发送回调。该路径保留 UDP 的低延迟数据报语义。

### Tick

`TickIncoming` 处理超时、KCP 收包和连接状态。`TickOutgoing` 发送 Ping 并调用 KCP Update。

## 9. Kcp.cs

路径：

`Assets/Mirror/Transports/KCP/kcp2k/kcp/Kcp.cs`

这是 KCP 算法实现。

### Send

`Send` 接收一条可靠消息，根据 MSS 将消息切成 Segment，写入发送队列。每个 Segment 保存分片编号和可靠传输元数据。

### Input

`Input` 解析收到的 KCP Segment：

- 处理 ACK。
- 更新远端窗口。
- 删除已经确认的发送 Segment。
- 记录待发送 ACK。
- 将收到的数据 Segment 放入接收缓冲。
- 按序移动到可读取队列。

### Update 与 Flush

`Update` 根据当前时间判断是否需要刷新。`Flush` 生成 ACK、窗口探测和数据 Segment，并根据超时或快速重传条件重新发送未确认数据。

## 10. 低层辅助文件

| 文件 | 作用 |
|---|---|
| `KcpHeader.cs` | 定义 Hello、Data、Ping、Disconnect 等高层头类型 |
| `KcpChannel.cs` | 定义可靠和非可靠通道 |
| `KcpState.cs` | 定义连接状态 |
| `Common.cs` | 提供 kcp2k 高层共用常量和消息尺寸计算 |
| `Segment.cs` | 保存一个 KCP 分片及其序号、窗口和重传状态 |
| `AckItem.cs` | 保存待发送确认的序号和时间戳 |
| `Pool.cs` | 复用 KCP 需要的缓冲对象 |
| `Utils.cs` | 提供低层时间和字节处理辅助方法 |

## 11. 一次可靠消息的发送

```text
Mirror NetworkConnection.Send
  → KcpTransport.ClientSend
  → KcpClient.Send
  → KcpPeer.SendReliable
  → Kcp.Send
  → Kcp.Update 与 Flush
  → KcpPeer.RawSendReliable
  → KcpClient.RawSend
  → UDP Socket.Send
```

服务端收到数据时：

```text
UDP Socket.ReceiveFrom
  → KcpServer.RawReceiveFrom
  → KcpServerConnection.RawInput
  → Kcp.Input
  → KcpPeer.TickIncoming
  → KcpTransport.OnServerDataReceived
  → Mirror NetworkServer
```

## 12. 当前参数

| 参数 | 值 |
|---|---:|
| UDP 端口 | `7777` |
| NoDelay | `true` |
| Interval | `10 ms` |
| Timeout | `10000 ms` |
| FastResend | `2` |
| SendWindowSize | `4096` |
| ReceiveWindowSize | `4096` |
| MaxRetransmit | `40` |
| ReliableMaxMessageSize | `297433` |
| UnreliableMaxMessageSize | `1194` |
