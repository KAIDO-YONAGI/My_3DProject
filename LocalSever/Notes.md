# EchoServer 知识点总结

##  异步 vs 同步阻塞

### 同步阻塞写法

```csharp
Socket clientfd = listenfd.Accept();          // 阻塞，等到有客户端连上来
int bytesRead = clientfd.Receive(readBuffer);  // 阻塞，等到有数据
clientfd.Send(sendBytes);                      // 阻塞，等发完
```

### 异步写法（本项目）

```csharp
listenfd.BeginAccept(AcceptCallback, listenfd);      // 立即返回，连上后回调
clientfd.BeginReceive(..., ReceiveCallback, state);   // 立即返回，收到数据后回调
clientfd.BeginSend(..., SendCallback, clientfd);      // 立即返回，发完后回调
```

### 核心区别

| | 同步阻塞 | 异步（Begin/End） |
|---|---|---|
| **线程行为** | 每个操作卡住当前线程 | 调用后立即返回，线程继续干别的事 |
| **多客户端** | 需要每个客户端开一个线程 | 一个线程通过回调处理所有客户端 |
| **代码结构** | 线性、从上往下读 | 逻辑分散在各个回调函数里 |
| **状态传递** | 用局部变量就行 | 必须通过 `AsyncState` 手动传递状态对象 |

比喻：同步 = 一个服务员全程等一位客人；异步 = 服务员把菜单给客人后去看下一桌，客人准备好了再回来处理。

---

## Begin\* / End\* 函数族（APM 模式）

.NET 早期的异步编程模式，每个 I/O 操作都有一对：

| Begin\*（发起） | End\*（收割） | 作用 |
|---|---|---|
| `BeginAccept` | `EndAccept` | 接受连接 |
| `BeginConnect` | `EndConnect` | 建立连接 |
| `BeginReceive` | `EndReceive` | 接收数据 |
| `BeginSend` | `EndSend` | 发送数据 |

### Begin\* 的参数模式

```csharp
socket.BeginReceive(
    readBuffer, 0, readBuffer.Length,   // 和同步版 Receive 一样的参数
    SocketFlags.None,                    // 同上
    ReceiveCallback,                     // 回调函数（完成时调用）
    clientState                          // AsyncState（传给回调的状态对象）
);
```

最后两个参数是异步版多出来的：
- **Callback** — 完成通知
- **AsyncState** — 自定义的任意对象，会塞进 `IAsyncResult.AsyncState`

### End\* 的作用

**必须在回调里调用**，做三件事：
1. 拿到操作结果（`EndAccept` 返回新 socket，`EndReceive` 返回字节数）
2. 释放内核里的异步操作资源
3. 如果操作抛了异常，在这里重新抛出

### IAsyncResult

就是"这次异步操作的凭据"，你实际用的时候基本只碰 `AsyncState`。

---

## 回调

回调是**提前注册的一个函数**，告诉系统"这件事做完了，调用这个函数"。

```csharp
listenfd.BeginAccept(AcceptCallback, listenfd);
//                 ^^^^^^^^^^^^^^^^
//                 函数引用，不是调用（没有括号）
```

你从不自己调回调函数，是 .NET 运行时在操作完成后帮你调的。

### 回调不是调用

没有 `()`，没有参数，**不是在执行这个函数**。只是把函数的"地址"交给 `BeginReceive`。

### C# 中回调的实现 — 委托（delegate）

```csharp
// AsyncCallback 的定义：
public delegate void AsyncCallback(IAsyncResult ar);
```

`ReceiveCallback` 作为参数传进去时，编译器把它包装成一个委托对象。`BeginReceive` 内部把它存起来，内核完成 I/O 后，框架帮你调这个委托。

### 回调 vs 依赖注入

- **依赖注入**是设计模式，解决"谁创建依赖"的问题
- **回调**是底层机制，解决"一段代码在某个时机通知另一段代码"的问题

---

## IOCP

`BeginAccept` 等异步 API 的底层是 Windows 的 **I/O Completion Port (IOCP)**。

### 链路

```
你的代码调 BeginAccept
    ↓
.NET 封装后调 Win32 AcceptEx API
    ↓
向内核注册一个 I/O 请求，立即返回（不阻塞）
    ↓
内核在网卡驱动层面完成 TCP 握手
    ↓
内核把完成通知投递到 IOCP 队列
    ↓
.NET 线程池有一个线程在监听这个队列，取出完成结果
    ↓
在你的回调里调用 EndAccept 拿到结果
```

### 关键点

**注册阶段没有线程在等你**。从 `BeginAccept` 到回调被触发之间，没有线程阻塞在 `Accept` 上。是内核自己完成工作，然后把结果"投递"出来。

- Worker 线程方案：1 个连接 = 1 个线程卡在 Accept 上，1000 个连接需要 1000 个线程
- IOCP 方案：完成通知都进同一个队列，少量线程就能处理大量 I/O

---

## ClientState 状态对象

### 为什么需要

```csharp
ClientState clientState = new ClientState();
clientState.socket = clientfd;
```

后续的 `BeginReceive` 需要知道两样东西：
- **`socket`** — 用来和客户端通信
- **`readBuffer`** — 存放收到的字节

必须绑在一起作为一个整体通过 `AsyncState` 传给回调。回调触发时：

```csharp
ClientState clientState = (ClientState)ar.AsyncState;  // 取回
Socket clientfd = clientState.socket;                    // 拿 socket
byte[] buffer = clientState.readBuffer;                  // 拿缓冲区
```

把一个客户端相关的数据打包在一起，方便在回调之间传递。

## BeginReceive 和回调的对应关系

`BeginReceive` 告诉系统"帮我盯着这个 socket，有数据来了叫我"：

```csharp
clientfd.BeginReceive(
    clientState.readBuffer,    // 数据来了往哪里写
    0,                         // 从缓冲区什么位置开始写
    clientState.readBuffer.Length, // 最多写多少字节
    SocketFlags.None,
    ReceiveCallback,           // 数据来了调谁（函数引用）
    clientState                // 给回调准备的状态对象
);
```

`readBuffer` 是提前给内核的，内核**直接往里面写数据**。缓冲区必须挂在 `clientState` 上通过 `AsyncState` 传递，确保不会被 GC 回收。

要持续接收，就在回调末尾再注册一次 `BeginReceive`，形成循环。

---

## TCP 断开检测（bytesRead == 0）

```csharp
if (bytesRead == 0)
{
    clients.Remove(clientfd);
    clientfd.Close();
    return;
}
```

这是 TCP 协议的内置行为，不是代码实现的。客户端调用 `Close()` 时，操作系统发送 **FIN 包**，服务端内核收到后 `EndReceive` 返回 0。

```
客户端 Close() → 发 FIN → 服务端内核收到 → EndReceive 返回 0
```

不做清理的后果：
- **不 Remove** — 字典留着已断开的客户端，内存泄漏
- **不 Close** — 文件描述符不释放，久了会耗尽
- **不 return** — 对已关闭的 socket 继续 `BeginReceive` 会抛异常

### 异常断开

客户端断网/崩溃（没发 FIN），`EndReceive` 会抛 `SocketException`，被 `try/catch` 捕获。

---

## BeginSend 为什么要注册

`Send` 也是 I/O 操作，把数据写到网卡缓冲区需要时间。用异步 `BeginSend` 避免阻塞线程。

在 Echo 服务端场景下数据量小，同步 `Send` 也几乎瞬间完成。用 `BeginSend` 主要是保持风格统一。如果发送量大（比如传文件），异步发送的意义就大了——不会因为一个客户端的发送阻塞住所有其他客户端。

---



---

## 两个回调的职责

### AcceptCallback — 接客

有新客户端连接时被调用（一次）。负责：接受连接 → 建档 → 注册 BeginReceive → 继续等下一个连接。

### ReceiveCallback — 收消息 + 回传

客户端发来数据时被调用（每条消息一次）。负责：读取数据 → 打印 → Echo 回传 → 继续等下一条消息。

关系：`AcceptCallback` 注册了 `ReceiveCallback`，`ReceiveCallback` 末尾又注册自己，形成持续监听。

```
AcceptCallback（一次）
  → 注册 BeginReceive
      → ReceiveCallback（每条消息一次）
          → 注册 BeginSend → SendCallback
          → 再注册 BeginReceive（循环）
```

---

## 完整调用链

```
程序启动
│
├─ Main()
│   ├─ 创建 listenfd（TCP socket）
│   ├─ Bind 绑定 127.0.0.1:8888
│   ├─ Listen 开始监听
│   ├─ BeginAccept(AcceptCallback)  ──注册──→ 内核开始等连接
│   └─ Console.ReadLine() 阻塞主线程，程序不退出
│
│  ═══════════════ 阶段一：客户端连接 ═══════════════
│
├─ AcceptCallback(ar)  ← 内核完成 TCP 握手后触发
│   ├─ EndAccept(ar) → 拿到 clientfd（新 socket）
│   ├─ new ClientState() → 建档，socket = clientfd
│   ├─ clients.Add(clientfd, clientState) → 存入字典
│   ├─ clientfd.BeginReceive(ReceiveCallback) ──注册──→ 内核开始等数据
│   └─ listenfd.BeginAccept(AcceptCallback) ──再注册──→ 继续等下一个连接
│
│  ═══════════════ 阶段二：收到消息 ═══════════════
│
├─ ReceiveCallback(ar)  ← 内核收到客户端数据后触发
│   ├─ EndReceive(ar) → bytesRead（读了多少字节）
│   │
│   ├─ [bytesRead == 0]  ← 客户端断开（收到 FIN）
│   │   ├─ clients.Remove(clientfd)
│   │   ├─ clientfd.Close()
│   │   └─ return（不再注册，链路终止）
│   │
│   └─ [bytesRead > 0]  ← 正常收到数据
│       ├─ 解码 readBuffer → receiveStr
│       ├─ BeginSend(SendCallback) ──注册──→ 内核开始发送回传
│       └─ BeginReceive(ReceiveCallback) ──再注册──→ 继续等下一条消息
│
│  ═══════════════ 阶段三：回传完成 ═══════════════
│
├─ SendCallback(ar)  ← 内核发送完毕后触发
│   ├─ EndSend(ar) → bytesSent
│   └─ 打印日志（到这就结束了，不再注册）
│
│  ═══════════════ 循环状态 ═══════════════
│
│  AcceptCallback 会不断自我注册 → 持续接受新连接
│  ReceiveCallback 会不断自我注册 → 持续接收消息
│  SendCallback 不注册 → 发完即止
```

### 具体场景：客户端 A 连接发 "Hello"，客户端 B 连接发 "World"

```
1.  BeginAccept 注册
2.  客户端 A 连接进来
3.    → AcceptCallback 触发
4.        → EndAccept 拿到 clientfd_A，建档
5.        → BeginReceive(ReceiveCallback, state_A) 注册
6.        → BeginAccept 注册（继续等）
7.  客户端 B 连接进来
8.    → AcceptCallback 触发
9.        → EndAccept 拿到 clientfd_B，建档
10.       → BeginReceive(ReceiveCallback, state_B) 注册
11.       → BeginAccept 注册（继续等）
12. 客户端 A 发 "Hello"
13.   → ReceiveCallback 触发（state_A）
14.      → 读出 "Hello"
15.      → BeginSend → SendCallback 触发 → 回传 "Hello" 给 A
16.      → BeginReceive 注册（继续等 A 的消息）
17. 客户端 B 发 "World"
18.   → ReceiveCallback 触发（state_B）
19.      → 读出 "World"
20.      → BeginSend → SendCallback 触发 → 回传 "World" 给 B
21.      → BeginReceive 注册（继续等 B 的消息）
```

第 12 和 17 是**并发的**——两个客户端的 ReceiveCallback 可能由线程池的不同线程同时执行，互不阻塞。

## 回调被调用的具体时机

| 回调 | 触发时机 | 确定性 |
|---|---|---|
| **ConnectCallback** | TCP 三次握手完成（通常几十毫秒） | 确定性最强 |
| **SendCallback** | 数据从用户态拷贝到内核发送缓冲区（微秒级） | 也很快，不保证对方已收到 |
| **ReceiveCallback** | 对方发来数据，网卡收到后 | 最不确定，取决于对方 |

三个回调都在 **.NET 线程池的线程**上执行，不是主线程。Unity 客户端中 `receiveStr` 在子线程写入、主线程 `Update` 读取，属于跨线程访问——严格来说应加锁，但 string 赋值在简单场景下基本不会出问题。



# 空值

Socket 可能在解析时为空，不过异步方法会确保非空，可以用 variable!.fun 即空值容忍!来消除警告（仅消除警告，异常正常抛出）
