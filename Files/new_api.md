# 网络层 API 文档

## 架构总览

```
客户端 (Unity)                              服务端 (.NET 控制台)
┌─────────────────────┐                    ┌──────────────────────┐
│ SyncCharacter       │                    │ ServerCore           │
│ ├─ 注册消息监听      │                    │ ├─ AcceptLoopAsync   │
│ ├─ 自动连接/重连     │  ──TCP 127.0.0.1── │ ├─ ReceiveLoopAsync  │
│ └─ 发送本地位置      │       :8888        │ └─ ClientState       │
├─────────────────────┤                    ├──────────────────────┤
│ NetManager          │                    │ ServerNetHandler     │
│ ├─ Socket 连接管理   │                    │ ├─ HandleMessage     │
│ ├─ 异步收发          │                    │ ├─ Broadcast         │
│ └─ 线程安全的消息队列 │                    │ └─ SyncExistingClientsTo│
├─────────────────────┤                    ├──────────────────────┤
│ ClientProtocol      │                    │ ServerProtocol       │
│ └─ 打包/解包         │                    │ └─ 打包              │
├─────────────────────┤                    └──────────────────────┘
│ ClientMessageHandler│
│ └─ Enter/Move/Leave │
├─────────────────────┤
│ PlayerManager       │
│ └─ 远程玩家实体管理  │
└─────────────────────┘
```

## 线程模型

```
线程池线程 (ConfigureAwait(false))          Unity 主线程
───────────────────────────                ───────────────
ReceiveLoopAsync                           Update()
  │                                          │
  ├─ socket.ReceiveAsync()                   ├─ connectResultList.TryDequeue()
  ├─ AppendMessages()                        │   → connectResultChannel.Raise()
  │    ├─ 拼接 pendingReceive                │
  │    ├─ Split('\n')                        ├─ messageList.TryDequeue()
  │    └─ messageList.Enqueue() ──────→      │   → Protocol.Unpack()
  │                                          │   → listenerList[type](msg)
  │                                          │
  SendAllAsync()                             PlayerManager.Update()
  ├─ sendLock.WaitAsync()                    └─ 应用远程玩家位置到 transform
  ├─ socket.SendAsync() (循环直到发完)
  └─ sendLock.Release()
```

网络收发在后台线程完成，通过 `ConcurrentQueue` 传递到主线程，主线程在 `Update()` 中消费队列并分发消息。

## 协议格式

所有消息为文本格式，以 `\n` 结尾，字段用 `|` 分隔，参数用 `,` 分隔。

### 客户端 → 服务端

| 类型 | 格式 | 说明 |
|------|------|------|
| Enter | `Enter\|modelID,health,damage\n` | 连接后发送玩家初始化数据 |
| Move | `Move\|x,y,z\n` | 位置同步，约 20Hz |
| Leave | `Leave\|\n` | 断开连接 |
| Attack | `Attack\|address\n` | 攻击指定玩家（未完成） |

### 服务端 → 客户端

| 类型 | 格式 | 说明 |
|------|------|------|
| Enter | `Enter\|ip:port,modelID,health,damage\n` | 广播新玩家加入 |
| Move | `Move\|ip:port,x,y,z\n` | 广播玩家位置 |
| Leave | `Leave\|ip:port\n` | 广播玩家离开 |
| Attack | `Attack\|ip:port,damage\n` | 攻击结果（未完成） |

## 连接流程

```
1. SyncCharacter.Start()
   注册 Enter/Move/Leave 监听器，启动连接协程

2. NetManager.Connect("127.0.0.1", 8888)
   TCP 三次握手

3. 服务端 AcceptLoopAsync 接受连接
   创建 ClientState，启动该客户端的 ReceiveLoopAsync

4. 客户端连接成功 → BoolEventChannelSO 触发
   SyncCharacter 收到事件 → 发送 PackEnter(playerInitData)

5. 服务端收到 Enter → 广播 PackEnter 给所有其他客户端
   → 调用 SyncExistingClientsTo 把现有玩家信息发给新客户端

6. 各客户端的 OnEnter 监听器 → PlayerManager.InitPlayer 生成远程玩家
```

## 关键类

### NetManager — 网络底层

| 方法 | 作用 |
|------|------|
| `Connect(ip, port)` | 发起异步连接 |
| `Disconnect()` | 发送 Leave 包并关闭 Socket |
| `Send(string)` | 异步发送（带发送锁，保证完整性） |
| `AddListenerIntoList(type, listener)` | 注册消息监听器 |

### SyncCharacter — 网络入口

场景中挂载的 MonoBehaviour，负责：
- 注册消息监听到 NetManager
- 连接/重连逻辑
- 每帧发送本地角色位置（0.05s 间隔）

### ClientMessageHandler — 消息处理

| 方法 | 触发时机 | 作用 |
|------|----------|------|
| `OnEnter` | 收到 Enter | 调用 PlayerManager 生成远程玩家 |
| `OnMove` | 收到 Move | 过滤掉自己的消息，更新远程玩家位置 |
| `OnLeave` | 收到 Leave | 销毁远程玩家 GameObject |

### PlayerManager — 玩家实体管理

| 方法 | 作用 |
|------|------|
| `InitPlayer(id, info)` | 实例化远程玩家模型 |
| `SetPosition(id, pos)` | 标记位置脏数据 |
| `RemovePosition(id)` | 销毁远程玩家 |
| `Update()` | 批量应用脏位置到 transform |

### 服务端 ServerNetHandler

| 方法 | 作用 |
|------|------|
| `HandleMessage(msg, socket)` | 解析消息类型并路由 |
| `Broadcast(msg)` | 发送给所有客户端 |
| `BroadcastExcept(msg, socket)` | 发送给除指定外的所有客户端 |
| `SyncExistingClientsTo(socket)` | 把所有已连接玩家同步给新客户端 |

## 异步模式

### async/await vs 回调式

```
旧：BeginConnect + ConnectCallback + EndConnect（回调分散，状态靠传参）
新：await ConnectAsync()（顺序书写，状态在局部变量）

旧：BeginReceive + ReceiveCallback + 再次 BeginReceive（回调接力形成循环）
新：while + await ReceiveAsync（显式循环，一眼看出是长期收包）

旧：BeginSend + SendCallback + EndSend（发完回调通知）
新：while + await SendAsync（完整发送一个包的全部字节）
```

### ConfigureAwait(false)

网络层的所有 `await` 都使用 `ConfigureAwait(false)`，让后续代码直接在线程池线程继续执行，而不是回到 Unity 主线程。因为收发操作只涉及 `lock` 和 `ConcurrentQueue`，不需要 Unity API。真正的 Unity 操作（生成/移动/销毁 GameObject）在主线程的 `Update()` 中通过队列触发。

### CancellationToken

`ReceiveLoopAsync` 通过 `CancellationToken` 接收退出信号。`CloseSocket()` 调用 `Cancel()` 将标志位置为 true，while 循环在下次检查时退出。这是协作式取消——Source 只管置标志，循环自己决定何时退出。

### SemaphoreSlim(1, 1)

异步信号量充当发送锁。计数器为 1 时获取成功并减到 0，其他发送请求阻塞等待。Release 时唤醒等待者。因为用计数器而非线程身份，可以跨 await 持有，解决了 `lock` 不能配合 `await` 使用的问题。

## 未完成功能

- **Attack（攻击）**：协议已定义，但 `ServerNetHandler.HandleMessage` 没有 Attack 分支，未接入
- **断线重连**：`SyncCharacter.ConnectWithRetry` 只运行一次，没有真正的重连逻辑
- **ServerProtocol.PackAttacked**：`address` 和 `damage` 之间缺少分隔符，疑似 bug
- **服务端 PlayerInfoManager**：已定义但未使用，玩家数据存在 ClientState 中
