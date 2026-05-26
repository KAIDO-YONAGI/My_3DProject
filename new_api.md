# 从暴露回调的异步，到写起来像同步的异步 API

这份文档不再按“新语法名词表”来讲，而是专门解释一件事：

为什么旧代码里的异步，看起来必须把逻辑拆到一堆回调里；  
而改成 `async/await` 之后，代码虽然本质还是异步，但写起来像同步顺序代码。

协议格式本身没有变化，变化的是网络层 API 的组织方式。

---

## 1. 先说结论

旧写法的核心体验是：

- 你把“下一步要干嘛”注册成回调
- 系统稍后再回来调用你
- 所以你的业务逻辑会被拆散

新写法的核心体验是：

- 你直接把“下一步要干嘛”顺着写在后面
- 遇到 `await` 时先挂起
- 异步完成后再从这一行后面继续执行

所以：

- 旧写法是“异步暴露在 API 形态上”
- 新写法是“异步保留在运行时行为里，但顺序感保留在代码形态上”

---

## 2. 什么叫“暴露回调的异步”

旧 Socket 写法的典型样子是：

```csharp
socket.BeginConnect(ip, port, ConnectCallback, socket);
```

这里的意思不是“连接完再回来继续当前函数”，而是：

1. 现在先发起连接
2. 当前函数很快结束
3. 连接完成后，系统会调用 `ConnectCallback`

然后在 `ConnectCallback` 里，你还要继续写：

```csharp
void ConnectCallback(IAsyncResult ar)
{
    Socket socket = (Socket)ar.AsyncState;
    socket.EndConnect(ar);
    socket.BeginReceive(..., ReceiveCallback, socket);
}
```

这就导致一个很典型的问题：

- “连接成功后做什么”
- “收到数据后做什么”
- “发送完成后做什么”

这些逻辑都被拆到不同回调里了。

也就是说，异步不只是运行时异步，连代码结构也被迫异步化了。

---

## 3. 什么叫“写起来像同步的异步”

新写法的典型样子是：

```csharp
private async Task ConnectAsyncInternal(string ip, int port)
{
    await socket.ConnectAsync(ip, port);
    Connected = true;
    _ = ReceiveLoopAsync(socket, token);
}
```

它看起来像同步代码，是因为：

- 先连接
- 连接成功后改状态
- 再启动接收循环

这三个动作是按阅读顺序直接写下来的。

但它本质还是异步，因为：

- `await` 遇到未完成任务时，不会卡死线程
- 方法会先“挂起并返回控制权”
- 等异步完成后，再从 `await` 后面继续

所以你看到的“同步感”，只是代码组织方式像同步，不是执行机制变成同步。

---

## 4. 旧 API 和新 API 的核心差别

### 4.1 旧 API：把“下一步”交给回调

旧写法关注的是：

- 现在先发起操作
- 完成后让谁来接手

例如：

```csharp
socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, socket);
```

这里真正重要的信息是：

- 接收操作开始了
- 后续逻辑在 `ReceiveCallback`

所以旧 API 的思维方式更像：

- “注册后续动作”

### 4.2 新 API：把“下一步”写在后面

新写法关注的是：

- 现在等这个异步操作完成
- 完成后继续执行下面的代码

例如：

```csharp
int count = await currentSocket.ReceiveAsync(
    new ArraySegment<byte>(readBuffer),
    SocketFlags.None);

if (count <= 0) return;

string recvStr = Encoding.Default.GetString(readBuffer, 0, count);
AppendMessages(recvStr);
```

这里真正重要的信息是：

- 先收
- 再判断
- 再解码
- 再处理

所以新 API 的思维方式更像：

- “顺着写业务步骤”

---

## 5. 为什么 `await` 看起来像阻塞，但其实没有阻塞

这是最容易误解的一点。

看这段：

```csharp
await currentSocket.ConnectAsync(ip, port);
Debug.Log("Connected to server");
```

阅读体验像是：

- 连上之后再打印

这没错。

但执行机制不是：

- 傻等在这一行不动

而是：

1. 调用 `ConnectAsync`
2. 如果任务没完成，当前方法先挂起
3. 把线程还回去
4. 连接完成后，再恢复这个方法
5. 从 `await` 的下一行继续执行

所以：

- 代码顺序像同步
- 线程调度仍然是异步

这就是“写起来像同步的异步”。

---

## 6. 对照看连接流程

### 6.1 旧写法

```csharp
socket.BeginConnect(ip, port, ConnectCallback, socket);

void ConnectCallback(IAsyncResult ar)
{
    Socket socket = (Socket)ar.AsyncState;
    socket.EndConnect(ar);
    socket.BeginReceive(..., ReceiveCallback, socket);
}
```

特点：

- 连接逻辑和连接后的逻辑分开
- 状态要从 `ar.AsyncState` 里取
- 必须手动 `EndConnect`

### 6.2 新写法

```csharp
public void Connect(string ip, int port)
{
    _ = ConnectAsyncInternal(ip, port);
}

private async Task ConnectAsyncInternal(string ip, int port)
{
    await currentSocket.ConnectAsync(ip, port);
    Connected = true;
    _ = ReceiveLoopAsync(currentSocket, token);
}
```

特点：

- 外部接口还可以保持原样
- 真正的异步细节藏进内部 `Task` 方法
- “连接成功后做什么”直接写在下面

这就是从“回调式 API”到“顺序式异步 API”的典型转换。

---

## 7. 对照看接收流程

### 7.1 旧写法

```csharp
void ReceiveCallback(IAsyncResult ar)
{
    Socket socket = (Socket)ar.AsyncState;
    int count = socket.EndReceive(ar);
    ...
    socket.BeginReceive(..., ReceiveCallback, socket);
}
```

这个写法本质上是在做一件事：

- 每次接收完成后，再手动注册下一次接收

所以它虽然也形成“循环”，但这个循环是分散在回调里的。

### 7.2 新写法

```csharp
private async Task ReceiveLoopAsync(Socket currentSocket, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        int count = await currentSocket.ReceiveAsync(...);
        if (count <= 0) return;
        ...
    }
}
```

这里的变化很关键：

- 旧的是“回调接力”
- 新的是“显式 while 循环”

所以现在你一眼就能看出来：

- 这是一个长期收包循环
- 每次循环先等收包
- 收到后再处理

这就是 `ReceiveLoopAsync` 比 `ReceiveCallback` 更贴切的原因。

---

## 8. 对照看发送流程

### 8.1 旧写法

```csharp
socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, socket);

void SendCallback(IAsyncResult ar)
{
    int count = socket.EndSend(ar);
}
```

特点：

- 发起发送和发送完成处理是分开的
- 默认阅读上会觉得“发一次就结束了”

### 8.2 新写法

```csharp
private async Task SendAllAsync(string sendStr)
{
    byte[] sendBytes = Encoding.Default.GetBytes(sendStr);

    int totalSent = 0;
    while (totalSent < sendBytes.Length)
    {
        int count = await socket.SendAsync(...);
        totalSent += count;
    }
}
```

这个新名字比 `SendCallback` 更准确，因为它真正表达的是：

- 不是“发送完成后被回调”
- 而是“把这一整包全部发完”

也就是说，这个方法现在是一个“完整发送动作”，不是一个“发送完成通知”。

---

## 9. 为什么我把名字改成这些

这次改名的目的，就是让方法名反映它现在的真实职责。

### 9.1 `ConnectCallback` -> `ConnectAsyncInternal`

旧名字容易让人误解成：

- 这是系统自动调用的回调

现在实际上是：

- 我们自己主动调用的内部异步连接步骤

所以改成：

- `ConnectAsyncInternal`

### 9.2 `ReceiveCallback` -> `ReceiveLoopAsync`

旧名字容易让人误解成：

- “收到一次数据后触发一下”

现在实际上是：

- 持续接收的异步循环

所以改成：

- `ReceiveLoopAsync`

### 9.3 `SendCallback` -> `SendAllAsync`

旧名字容易让人误解成：

- 这是发送结束通知

现在实际上是：

- 负责把整段数据完整发出去

所以改成：

- `SendAllAsync`

### 9.4 `AcceptCallback` -> `AcceptLoopAsync`

旧名字容易让人误解成：

- 接到一个客户端时被系统回调一次

现在实际上是：

- 服务器常驻的接入循环

所以改成：

- `AcceptLoopAsync`

---

## 10. 这次改造里，异步是怎么“藏进内部”的

这是理解 API 体验变化最关键的一层。

### 10.1 对外接口可以保持简单

例如：

```csharp
public void Connect(string ip, int port)
{
    _ = ConnectAsyncInternal(ip, port);
}
```

这表示：

- 外部调用方还是按以前方式调 `Connect(...)`
- 不需要一上来就把整个调用链全改成 `await`

### 10.2 对内实现改成顺序式异步

真正复杂的异步行为，放到内部：

```csharp
private async Task ConnectAsyncInternal(string ip, int port)
{
    await ...
    ...
}
```

这样做的效果是：

- API 使用体验尽量平滑
- 内部实现质量提升

也就是：

- 对外尽量少破坏
- 对内彻底摆脱旧回调风格

---

## 11. 这不是“同步 API”，只是“同步风格的异步 API”

必须把这句话记牢：

- `async/await` 不是把异步变同步
- 而是把异步写法变得更像同步

差别非常大。

同步 API 的特点是：

- 调用后线程卡住
- 直到结果出来才返回

现在这套 API 的特点是：

- 线程不会傻等
- 异步完成后再续上后面的逻辑

所以准确说法应该是：

- “顺序式异步”
- “写起来像同步的异步”

而不是：

- “同步 API”

---

## 12. 本次代码里最值得关注的三种新组织方式

### 12.1 用 `async Task` 包住一个完整步骤

例如：

- 连一次
- 发一整包
- 跑一个接收循环

这比“把步骤拆给回调”更容易维护。

### 12.2 用 `while + await` 表达长期循环

旧回调时代的“循环”常常藏在：

- 回调最后再次 `BeginReceive(...)`

现在直接把循环写出来，更清楚。

### 12.3 用队列把网络线程和 Unity 主线程隔开

这一步不是 `async/await` 独有的，但它和新写法配合得很好。

网络层负责：

- 收包
- 拼包
- 入队

主线程负责：

- 协议分发
- Unity 业务逻辑

---

## 13. 最后用一句话概括这次风格变化

旧风格是：

> 先发起异步操作，再把“后面怎么办”塞进回调里。

新风格是：

> 先把完整步骤顺着写出来，在需要等待异步结果的地方用 `await` 暂停。

所以你现在看到的最大变化，不是“异步消失了”，而是：

- 异步从代码结构表面退下去了
- 顺序逻辑重新回到了代码表面

---

## 14. 继续往下学时，最推荐盯住这三组对照

- `BeginConnect + ConnectCallback + EndConnect`  
  对照  
  `await ConnectAsync`

- `BeginReceive + ReceiveCallback + EndReceive + 再次 BeginReceive`  
  对照  
  `while (...) { await ReceiveAsync(...) }`

- `BeginSend + SendCallback + EndSend`  
  对照  
  `while (...) { await SendAsync(...) }`

如果这三组真正吃透了，你对“回调式异步”和“顺序式异步”的区别就已经抓住主干了。

