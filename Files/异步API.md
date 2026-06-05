# 异步 API 流程详解

本文档按完整链路讲解客户端和服务端的异步网络流程，覆盖每个方法的调用时机、线程归属、数据流向。

---

## 整体架构

```
┌─ 客户端 (Unity 主线程 + 线程池线程) ──────────────────────────────┐
│                                                                    │
│  SyncCharacter          NetManager            ClientProtocol      │
│  (业务入口)              (网络底层)             (打包/拆包)         │
│  ├─ Start()             ├─ Connect()          ├─ PackXxx()       │
│  ├─ ConnectWithRetry()  ├─ Disconnect()       └─ Unpack()        │
│  ├─ Update() 发送位置    ├─ Send()                               │
│  └─ OnDestroy()         ├─ ReceiveLoopAsync (线程池)              │
│                         ├─ SendAllAsync (线程池)                   │
│                         └─ Update() 消费队列 (主线程)              │
│                                                                    │
│  ClientMessageHandler   PlayerManager                             │
│  (消息→业务)             (远程玩家实体)                             │
│  ├─ OnEnter             ├─ InitPlayer                             │
│  ├─ OnMove              ├─ SetPosition                            │
│  └─ OnLeave             └─ RemovePosition                         │
└────────────────────────────────────────────────────────────────────┘
         │ TCP 127.0.0.1:8888
         ↓
┌─ 服务端 (.NET 线程池) ───────────────────────────────────────────┐
│                                                                    │
│  ServerCore              ServerNetHandler       ServerProtocol    │
│  (监听 + 收包)            (消息分发 + 广播)       (打包)           │
│  ├─ Main()               ├─ HandleMessage       ├─ PackEnter     │
│  ├─ AcceptLoopAsync      ├─ Broadcast           ├─ PackMove      │
│  ├─ ReceiveLoopAsync     ├─ BroadcastExcept     ├─ PackLeave     │
│  └─ TryReadMessage       ├─ SendTo              └─ PackAttacked  │
│                          ├─ SendAllAsync                          │
│                          ├─ SyncExistingClientsTo                 │
│                          └─ RemoveClient                           │
│                                                                    │
│  ClientState (每个客户端一个)                                       │
│  ├─ socket, readBuffer                                              │
│  ├─ modelID, health, damage, entered                               │
│  ├─ pendingMessages (StringBuilder 半包缓冲)                        │
│  └─ sendLock (SemaphoreSlim)                                       │
└────────────────────────────────────────────────────────────────────┘
```

---

## 线程模型

```
客户端                                    服务端
──────────                                ──────

Unity 主线程                              主线程 (AcceptLoopAsync)
│                                         │
├─ SyncCharacter.Update()                 ├─ AcceptAsync() 等待连接
│  └─ NetManager.Send()                   │
├─ NetManager.Update()                    └─ 为每个客户端启动 ReceiveLoopAsync
│  ├─ 消费连接结果队列                         (各自独立的线程池线程)
│  └─ 消费消息队列 → 分发 listener         线程池线程 A (客户端A的 ReceiveLoopAsync)
│                                         ├─ ReceiveAsync()
线程池线程                                 ├─ TryReadMessage()
│                                        ├─ HandleMessage()
├─ ReceiveLoopAsync (常驻循环)             │   ├─ Broadcast() → 并行发给所有人
│  ├─ ReceiveAsync()                      │   └─ BroadcastExcept() → 并行发给其他人
│  ├─ AppendMessages()                    └─ 循环
│  └─ Enqueue 到队列                      线程池线程 B (客户端B的 ReceiveLoopAsync)
│                                         ├─ ...同上
├─ SendAllAsync                           线程池线程 C (Broadcast 中的 SendAllAsync)
│  ├─ sendLock.WaitAsync()                ├─ sendLock.WaitAsync()
│  ├─ SendAsync() 循环                    ├─ SendAsync() 循环
│  └─ sendLock.Release()                  └─ sendLock.Release()
```

---

## 一、服务端启动

### ServerCore.Main()

```
Main()
  │
  ├─ new Socket(IPv4, Stream, TCP)     创建 TCP 监听 socket
  ├─ listenfd.Bind("127.0.0.1:8888")   绑定地址和端口
  ├─ listenfd.Listen(0)                开始监听
  │
  └─ await AcceptLoopAsync()           进入接受连接的无限循环
      │
      │  这是一个 async Task 方法，Main 本身会阻塞在这里
      │  直到 AcceptLoopAsync 结束（实际上永远不会结束）
      ↓
  AcceptLoopAsync()
```

### ServerCore.AcceptLoopAsync()

```csharp
public static async Task AcceptLoopAsync()
{
    while (true)
    {
        Socket clientfd = await listenfd.AcceptAsync();
        // 阻塞等待新连接，操作系统完成三次握手后返回

        ClientState clientState = new ClientState();
        clientState.socket = clientfd;

        clients.TryAdd(clientfd, clientState);
        // 加入 ConcurrentDictionary，所有线程都能访问

        _ = ReceiveLoopAsync(clientState);
        // fire-and-forget：不等这个客户端的接收循环
        // 立即回到 while 开头，继续等待下一个连接
    }
}
```

流程：

```
while (true)
  │
  ├─ await AcceptAsync()           等待新连接
  │   │
  │   └─ 客户端A连接 → 返回 clientfd_A
  │
  ├─ new ClientState { socket = clientfd_A }
  ├─ clients.TryAdd(clientfd_A, state_A)    注册到全局字典
  │
  ├─ _ = ReceiveLoopAsync(state_A)          启动客户端A的接收循环
  │   └─ 线程池线程 A 开始跑
  │
  ├─ 回到 while 开头
  │
  ├─ await AcceptAsync()           继续等待
  │   │
  │   └─ 客户端B连接 → 返回 clientfd_B
  │
  ├─ new ClientState { socket = clientfd_B }
  ├─ clients.TryAdd(clientfd_B, state_B)
  │
  ├─ _ = ReceiveLoopAsync(state_B)          启动客户端B的接收循环
  │   └─ 线程池线程 B 开始跑
  │
  └─ ... 无限循环
```

每个客户端连接后，服务端为它创建独立的 `ClientState` 和独立的 `ReceiveLoopAsync`。这些循环在不同线程池线程上并行运行，互不阻塞。

---

## 二、客户端连接

### SyncCharacter.Start() → ConnectWithRetry() 协程

```csharp
void Start()
{
    // 注册三种消息的监听器
    NetManager.Instance.AddListenerIntoList(ClientMessageType.Enter,  OnEnter);
    NetManager.Instance.AddListenerIntoList(ClientMessageType.Move,   OnMove);
    NetManager.Instance.AddListenerIntoList(ClientMessageType.Leave,  OnLeave);

    // 订阅连接结果事件
    connectResultChannel.OnEventRaised += OnConnectResult;

    // 启动连接协程
    StartCoroutine(ConnectWithRetry());
}
```

### ConnectWithRetry() 协程流程

```
ConnectWithRetry()
  │
  ├─ connectResolved = false
  ├─ NetManager.Instance.Connect("127.0.0.1", 8888)
  │     │
  │     └─ _ = ConnectAsyncInternal(...)    fire-and-forget
  │
  ├─ yield return new WaitUntil(() => connectResolved)
  │     │
  │     └─ 协程挂起，等连接结果
  │        连接结果通过 BoolEventChannelSO → OnConnectResult() → connectResolved = true
  │
  ├─ 连接失败？
  │     └─ yield return new WaitForSeconds(3f)  等待后重试
  │        回到 while 开头
  │
  └─ 连接成功！
        ├─ myPlayerId = NetManager.Instance.GetDescribe()    保存自己的 "ip:port"
        ├─ NetManager.Instance.Send(Protocol.PackEnter(playerInitData))    发送 Enter 包
        └─ yield break    退出协程
```

### NetManager.ConnectAsyncInternal() 详细流程

```
ConnectAsyncInternal("127.0.0.1", 8888)       ← 在线程池线程上执行
  │
  ├─ CloseSocket()                              清理旧连接（如果有）
  ├─ pendingReceive = ""                        清空接收缓冲区
  ├─ new Socket(IPv4, Stream, TCP)              创建新 socket
  ├─ socket = currentSocket                     记住到静态字段
  │
  ├─ await currentSocket.ConnectAsync(ip, port).ConfigureAwait(false)
  │     │
  │     │  TCP 三次握手，操作系统异步完成
  │     │  ConfigureAwait(false)：完成后不回主线程，继续在线程池线程
  │     │
  │     └─ 连接成功，恢复执行
  │
  ├─ ReferenceEquals(socket, currentSocket)     安全检查
  │     └─ 确认 socket 没有在等待期间被其他调用替换
  │        如果被替换了，关掉旧的 socket，直接 return
  │
  ├─ Connected = true
  ├─ connectResultList.Enqueue(true)            通知主线程：连接成功
  ├─ receiveCancellationTokenSource = new()     创建取消令牌
  └─ _ = ReceiveLoopAsync(currentSocket, token)  启动接收循环
        │                                        fire-and-forget，不等
        └─ 另一个线程池线程开始跑接收循环

  连接失败时：
  ├─ catch: Connected = false
  ├─ connectResultList.Enqueue(false)           通知主线程：连接失败
  └─ CloseSocket()
```

### 连接结果如何回到主线程

```
线程池线程                                Unity 主线程
──────────                               ──────────
ConnectAsyncInternal:
  connectResultList.Enqueue(true)    →   Update():
                                         while (connectResultList.TryDequeue(out result))
                                           connectResultChannel.Raise(result)
                                             │
                                             ↓
                                         SyncCharacter.OnConnectResult(true)
                                           connectResolved = true
                                           │
                                           ↓
                                         ConnectWithRetry 协程恢复执行
                                           myPlayerId = GetDescribe()
                                           Send(PackEnter(...))
```

---

## 三、服务端接收连接

### AcceptLoopAsync 接受连接

```
AcceptLoopAsync()
  ├─ await AcceptAsync()            客户端连接进来
  ├─ new ClientState                建档
  │    ├─ socket = clientfd
  │    ├─ readBuffer = new byte[1024]
  │    ├─ pendingMessages = new StringBuilder()
  │    └─ sendLock = new SemaphoreSlim(1,1)
  ├─ clients.TryAdd(clientfd, state)
  └─ _ = ReceiveLoopAsync(state)    启动该客户端的专属接收循环
```

### ServerCore.ReceiveLoopAsync() — 服务端收包

```csharp
static async Task ReceiveLoopAsync(ClientState clientState)
{
    Socket clientfd = clientState.socket;
    while (true)
    {
        int bytesRead = await clientfd.ReceiveAsync(...);
        // 等待该客户端发来数据

        if (bytesRead == 0)
        {
            // 客户端正常关闭连接，返回0字节
            await RemoveClient(clientfd);
            return;
        }

        string receiveStr = Encoding.Default.GetString(...);
        clientState.pendingMessages.Append(receiveStr);
        // 追加到 StringBuilder 缓冲区

        while (TryReadMessage(clientState.pendingMessages, out string msg))
        {
            if (!await ServerNetHandler.HandleMessage(msg, clientfd))
                return;
            // HandleMessage 返回 false 表示该客户端应该断开（Leave）
        }
    }
}
```

### TryReadMessage — 半包处理

```
StringBuilder 内容: "Move|1,2,3\nEnter|modelID,10,5\nMove|4,5"
                                         ↑ 没有结尾的 \n

TryReadMessage 扫描 StringBuilder：
  ├─ 找到第一个 \n → 提取 "Move|1,2,3"，返回 true
  ├─ 找到第二个 \n → 提取 "Enter|modelID,10,5"，返回 true
  └─ 没有更多 \n  → 剩余 "Move|4,5" 留在 StringBuilder，返回 false

每次只提取一条完整消息，HandleMessage 处理完后继续循环提取
```

与客户端的 `AppendMessages`（用 `Split` 一次性切开）不同，服务端用 `TryReadMessage`（逐条扫描提取）。两者效果相同：只处理以 `\n` 结尾的完整消息，不完整的留在缓冲区。

---

## 四、客户端 Enter 流程（端到端）

这是最完整的链路，展示一条消息从客户端发出到所有客户端生效的全过程。

### 步骤 1：客户端发送 Enter

```
Unity 主线程 — SyncCharacter.ConnectWithRetry 协程
  │
  ├─ myPlayerId = NetManager.Instance.GetDescribe()     → "127.0.0.1:12345"
  │
  └─ NetManager.Instance.Send(Protocol.PackEnter(playerInitData))
        │
        ├─ Protocol.PackEnter({modelID=0, health=100, damage=10})
        │     → "Enter|0,100,10\n"
        │
        └─ _ = SendAllAsync("Enter|0,100,10\n")
              │
              ├─ Encoding.Default.GetBytes(...)          → byte[]
              ├─ await sendLock.WaitAsync()              → 获取发送锁
              ├─ while (totalSent < length)
              │     └─ await socket.SendAsync(...)       → 循环发完
              └─ sendLock.Release()                      → 释放锁
```

### 步骤 2：服务端收到并处理 Enter

```
线程池线程 — ServerCore.ReceiveLoopAsync
  │
  ├─ ReceiveAsync() 收到数据
  ├─ pendingMessages.Append(receiveStr)
  ├─ TryReadMessage() → 提取 "Enter|0,100,10"
  │
  └─ await HandleMessage("Enter|0,100,10", clientfd)
        │
        ├─ parts = msg.Split('|')  → ["Enter", "0,100,10"]
        ├─ parts[0] == "Enter"
        │
        ├─ args = "0,100,10".Split(',')  → ["0", "100", "10"]
        ├─ 解析到 ClientState：
        │    state.modelID = 0
        │    state.health = 100
        │    state.damage = 10
        │    state.entered = true
        │
        ├─ address = clientfd.RemoteEndPoint.ToString()  → "127.0.0.1:12345"
        │
        ├─ await BroadcastExcept(PackEnter(address, 0, 100, 10), clientfd)
        │     │
        │     │  打包: "Enter|127.0.0.1:12345,0,100,10\n"
        │     │  发给除 sender 外的所有客户端
        │     │
        │     └─ foreach (client in clients)
        │          if (client != sender)
        │            sendTasks.Add(SendTo(msg, client.socket))
        │          await Task.WhenAll(sendTasks)       ← 并行发送，等全部完成
        │
        └─ await SyncExistingClientsTo(clientfd)
              │
              │  把所有已在线玩家信息发给新客户端
              │
              └─ foreach (client in clients)
                   if (client != target && client.entered)
                     sendTasks.Add(SendTo(PackEnter(...), target))
                   await Task.WhenAll(sendTasks)
```

### 步骤 3：其他客户端收到 Enter

```
线程池线程 — NetManager.ReceiveLoopAsync（客户端B）
  │
  ├─ ReceiveAsync() 收到 "Enter|127.0.0.1:12345,0,100,10\n"
  ├─ AppendMessages(recvStr)
  │     ├─ pendingReceive += recvStr
  │     ├─ Split('\n') → ["Enter|127.0.0.1:12345,0,100,10", ""]
  │     ├─ messageList.Enqueue("Enter|127.0.0.1:12345,0,100,10")
  │     └─ pendingReceive = ""
  │
  └─ 继续循环，等待下一条数据

Unity 主线程 — NetManager.Update()
  │
  ├─ messageList.TryDequeue(out "Enter|127.0.0.1:12345,0,100,10")
  ├─ Protocol.Unpack(...)
  │     ├─ Split('|') → ["Enter", "127.0.0.1:12345,0,100,10"]
  │     ├─ ParseEnterArgs(...)
  │     │    ├─ Split(',') → ["127.0.0.1:12345", "0", "100", "10"]
  │     │    ├─ msg.playerId = "127.0.0.1:12345"
  │     │    ├─ msg.playerInfo.modelID = 0
  │     │    └─ msg.playerInfo.playerState = PlayerState(100, 10)
  │     └─ return true
  │
  └─ listenerList[Enter](msg)     → ClientMessageHandler.OnEnter(msg)
        │
        └─ PlayerManager.Instance.InitPlayer("127.0.0.1:12345", info)
              │
              ├─ players.ContainsKey("127.0.0.1:12345")? → 否，继续
              ├─ info.instance = Instantiate(models[0], Vector3.zero, Quaternion.identity)
              │     → 在场景中生成远程玩家模型
              ├─ players["127.0.0.1:12345"] = info
              └─ playerToRefreshList.Add("127.0.0.1:12345")
                    → 标记为脏，PlayerManager.Update() 会应用位置
```

---

## 五、Move 流程（高频）

### 客户端发送位置

```
Unity 主线程 — SyncCharacter.Update()     每 0.05 秒执行一次（20Hz）
  │
  ├─ Time.time - lastSendTime > 0.05?
  ├─ pos = localCharacter.transform.position
  └─ NetManager.Instance.Send(Protocol.PackMove(pos.x, pos.y, pos.z))
        │
        └─ PackMove(1.0, 2.0, 3.0) → "Move|1,2,3\n"
           SendAllAsync("Move|1,2,3\n")
             ├─ sendLock.WaitAsync()         排队等锁
             ├─ SendAsync 循环发完
             └─ sendLock.Release()
```

### 服务端收到 Move 并广播

```
线程池线程 — ServerCore.ReceiveLoopAsync
  │
  ├─ TryReadMessage() → "Move|1,2,3"
  └─ HandleMessage("Move|1,2,3", clientfd_A)
        │
        ├─ parts[0] == "Move"
        ├─ address = clientfd_A.RemoteEndPoint.ToString()   → "127.0.0.1:12345"
        │
        └─ await Broadcast(PackMove("127.0.0.1:12345", "1,2,3"))
              │
              │  打包: "Move|127.0.0.1:12345,1,2,3\n"
              │  发给所有客户端（包括发送者）
              │
              └─ Task.WhenAll(sendTasks)    并行发给每个人
```

### 客户端收到 Move

```
线程池线程 — ReceiveLoopAsync
  └─ AppendMessages → messageList.Enqueue("Move|127.0.0.1:12345,1,2,3")

Unity 主线程 — Update()
  ├─ Unpack → ParseMoveArgs
  │    ├─ playerId = "127.0.0.1:12345"
  │    └─ position = (1, 2, 3)
  │
  └─ listenerList[Move](msg) → OnMove(msg, myPlayerId)
        │
        ├─ msg.playerId == myPlayerId?
        │     └─ 是 → return（过滤自己的消息）
        │     └─ 否 → PlayerManager.Instance.SetPosition(playerId, position)
        │              └─ playerToRefreshList.Add(playerId)
        │
        └─ PlayerManager.Update() 在帧末应用位置到 transform
```

---

## 六、Leave 流程

### 客户端主动断开

```
Unity 主线程
  │
  └─ SyncCharacter.OnDestroy()
        └─ NetManager.Instance.Disconnect()
              │
              └─ _ = DisconnectAsync()          fire-and-forget
                    │
                    ├─ socket.Connected?
                    │    └─ await SendAllAsync(PackLeave())
                    │         发送 "Leave|\n"
                    │
                    ├─ Connected = false
                    └─ CloseSocket()
                          ├─ Cancel()          → ReceiveLoopAsync 退出
                          ├─ Shutdown(Both)    → 发 FIN 包
                          ├─ Close()           → 释放句柄
                          └─ socket = null
```

### 服务端处理 Leave

```
ReceiveLoopAsync 收到 "Leave|"
  │
  └─ HandleMessage("Leave|", clientfd)
        │
        ├─ parts[0] == "Leave"
        ├─ await RemoveClient(clientfd)
        │     │
        │     ├─ clients.TryRemove(clientfd, out _)     从字典移除
        │     ├─ clientfd.Shutdown(Both)                优雅关闭
        │     ├─ clientfd.Close()                       释放
        │     └─ await Broadcast(PackLeave(address))    广播 "Leave|127.0.0.1:12345\n"
        │
        └─ return false    → ReceiveLoopAsync 退出循环

  如果客户端异常断开（没有发 Leave）：
  ├─ ReceiveAsync 抛 SocketException
  └─ catch → await RemoveClient(clientfd)    同样的清理流程
```

### 其他客户端收到 Leave

```
Update() → Unpack → listenerList[Leave] → OnLeave(msg)
  │
  └─ PlayerManager.Instance.RemovePosition("127.0.0.1:12345")
        ├─ Destroy(info.instance)       销毁远程玩家 GameObject
        └─ players.Remove(playerId)     从字典移除
```

---

## 七、服务端广播机制

### Broadcast — 发给所有人

```csharp
public static async Task Broadcast(string sendStr)
{
    List<Task> sendTasks = new();
    foreach (var pair in clients)
    {
        sendTasks.Add(SendTo(sendStr, pair.Value.socket));
    }
    await Task.WhenAll(sendTasks);
}
```

```
Broadcast("Move|127.0.0.1:12345,1,2,3\n")
  │
  ├─ sendTasks.Add(SendTo(msg, clientA.socket))    Task A
  ├─ sendTasks.Add(SendTo(msg, clientB.socket))    Task B
  ├─ sendTasks.Add(SendTo(msg, clientC.socket))    Task C
  │
  └─ await Task.WhenAll([A, B, C])
        │
        │  三个发送任务并行执行，各自有自己的 sendLock
        │  等全部完成后才继续
        │
        └─ 返回
```

### SendAllAsync — 带锁的单连接发送

```csharp
public static async Task SendAllAsync(byte[] sendBytes, Socket target)
{
    await state.sendLock.WaitAsync();           // 每个 ClientState 有自己的锁
    try
    {
        while (totalSent < sendBytes.Length)    // TCP 可能分段，循环发完
        {
            int bytesSent = await target.SendAsync(...);
            totalSent += bytesSent;
        }
    }
    catch (SocketException)
    {
        await RemoveClient(target);             // 发送失败 → 清理该客户端
    }
    finally
    {
        state.sendLock.Release();               // 一定释放锁
    }
}
```

为什么每个 `ClientState` 有独立的 `sendLock`：Broadcast 可能同时向多个客户端发送，它们之间互不影响。但同一个客户端的多次发送必须串行（比如 Broadcast 的消息和 SyncExistingClientsTo 的消息不能交错）。

---

## 八、异常处理与资源清理

### 客户端异常路径

```
ReceiveLoopAsync 异常（服务端断开、网络中断）
  │
  ├─ catch (ObjectDisposedException) { }         socket 已被关，忽略
  ├─ catch (SocketException) { Log }             网络错误
  └─ catch (Exception) { Log }                   其他错误
  │
  └─ finally
       ├─ ReferenceEquals(socket, currentSocket)?   确认是当前 socket
       │    └─ Connected = false
       │       CloseSocket()                        Cancel → Shutdown → Close → null
       └─ 如果不是当前 socket（已被替换），不做处理

SendAllAsync 异常
  │
  ├─ catch (ObjectDisposedException) { }         socket 已关
  ├─ catch (SocketException)                     发送失败
  │    └─ Connected = false; CloseSocket()
  └─ catch (Exception)                           其他
       └─ Connected = false; CloseSocket()
  │
  └─ finally: sendLock.Release()                 无论成功失败都释放锁
```

### 服务端异常路径

```
ReceiveLoopAsync 异常
  │
  ├─ bytesRead == 0                              客户端正常关闭
  ├─ SocketException                             网络错误
  ├─ ObjectDisposedException                     socket 已关
  └─ 所有异常 → await RemoveClient(clientfd)

SendAllAsync 异常
  │
  ├─ SocketException → await RemoveClient(target)
  ├─ ObjectDisposedException → await RemoveClient(target)
  └─ Exception → await RemoveClient(target)
  │
  └─ finally: sendLock.Release()

RemoveClient 是幂等的：
  ├─ clients.TryRemove() → 已经移除了就直接 return
  ├─ Shutdown(Both)
  ├─ Close()
  └─ Broadcast(Leave)     只在第一次移除成功时广播
```

---

## 九、fire-and-forget vs await 使用总结

| 调用 | 方式 | 原因 |
|------|------|------|
| `ConnectAsyncInternal` | `_ =` fire-and-forget | 连接是异步的，调用方不需要等 |
| `ReceiveLoopAsync`（客户端） | `_ =` fire-and-forget | 长期循环不会主动结束，await 会卡住后续代码 |
| `ReceiveLoopAsync`（服务端） | `_ =` fire-and-forget | 同上，每个客户端的循环独立运行 |
| `DisconnectAsync` | `_ =` fire-and-forget | 断开不需要调用方等 |
| `SendAllAsync` | `_ =` fire-and-forget | 发送由 sendLock 保护，调用方不需要等 |
| `Broadcast` | `await Task.WhenAll` | 需要确保所有客户端都收到后才继续 |
| `BroadcastExcept` | `await Task.WhenAll` | 同上 |
| `SyncExistingClientsTo` | `await Task.WhenAll` | 同上 |
| `RemoveClient` | `await` | 需要确认清理完成后再广播 Leave |
| `HandleMessage` | `await` | 需要等消息处理完再继续读取下一条 |

---

## 十、ReferenceEquals 安全检查

客户端多处使用 `ReferenceEquals(socket, currentSocket)` 检查：

```
场景：用户快速点击两次 Connect

时间线：
  t1: ConnectAsyncInternal() 第一次调用
      socket = currentSocket_A
      await ConnectAsync(...)          等待中...

  t2: ConnectAsyncInternal() 第二次调用（用户又点了一次）
      CloseSocket()                    Cancel → 关掉 socket_A
      socket = currentSocket_B          替换为新的 socket
      await ConnectAsync(...)          等待中...

  t3: 第一次连接完成
      ReferenceEquals(socket, currentSocket_A)?
      socket 现在指向 B，不是 A → 不相等
      → currentSocket_A.Close()         关掉旧的
      → return                           不继续执行后续代码

  t4: 第二次连接完成
      ReferenceEquals(socket, currentSocket_B)?
      相等 → 正常继续
```

如果不用这个检查，第一次连接成功后会把 `socket`（已经是 B）的状态设为 connected，覆盖第二次连接的状态，导致混乱。
