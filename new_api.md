# 本次改动里旧代码没有的新东西

这份文档只讲这次把旧 Socket 回调 API 改成 `async/await` 之后，新出现的语法、类型和写法。

协议格式本身没有变，还是：

- `Enter|...`
- `Move|...`
- `Leave|...`
- 以 `\n` 作为一条消息的结束

变的是网络层的实现方式。

---

## 1. `async` / `await` / `Task`

这是新的异步写法。

以前是：

- `BeginConnect(...)`
- 等系统回调 `ConnectCallback`
- 回调里再 `EndConnect(...)`

现在是：

- 方法本身写成 `async Task`
- 里面直接 `await socket.ConnectAsync(...)`
- 连接完成后，从下一行继续往下执行

这让代码更像“顺着写”，不用把逻辑拆散在很多回调里。

出现位置：

- `Assets/Core/Scripts/Client/Net/NetManager.cs`
- `Assets/Core/Scripts/Client/ChatManager.cs`
- `LocalSever/Scripts/ServerCore.cs`
- `LocalSever/Scripts/ServerNetHandler.cs`

---

## 2. `_ = SomeAsyncMethod()`

这种写法表示：

- 我现在要启动一个异步任务
- 但当前这个调用点不等它执行完

例如：

- `Connect()` 还是 `void`
- 但内部真正做连接的是 `ConnectCallback(...)`
- 所以会写成 `_ = ConnectCallback(ip, port);`

这是典型的 fire-and-forget 写法。

注意：

- 这种写法方便保留旧接口
- 但它不表示“已经完成”
- 只表示“已经发起”

---

## 3. `ConnectAsync` / `AcceptAsync` / `ReceiveAsync` / `SendAsync`

这是新 API 的主体。

- `ConnectAsync`：异步连接
- `AcceptAsync`：异步接收新客户端连接
- `ReceiveAsync`：异步收包
- `SendAsync`：异步发包

它们对应以前的：

- `BeginConnect / EndConnect`
- `BeginAccept / EndAccept`
- `BeginReceive / EndReceive`
- `BeginSend / EndSend`

---

## 4. `ConfigureAwait(false)`

这是异步代码里的一个常见写法。

作用是：

- 告诉运行时，`await` 之后不必强制回到原上下文

在这种纯网络收发逻辑里，这么写通常更稳，也更轻。

注意：

- 它适合放在“纯计算 / 纯网络 / 纯 IO”代码里
- 不适合后面马上直接调用 Unity API 的地方

---

## 5. `ArraySegment<byte>`

这是“字节数组的一段视图”。

它不是重新拷贝一份新数组，而是告诉 `ReceiveAsync` 或 `SendAsync`：

- 用这个数组
- 从这个位置开始
- 长度是多少

例如发送时会写成：

```csharp
new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent)
```

意思是：

- 从 `sendBytes` 里
- 跳过已经发完的那部分
- 把剩下的继续发

---

## 6. `ConcurrentQueue<T>`

这是线程安全队列。

这次主要用来做“网络线程 -> 主线程”的消息传递。

客户端现在不是收到消息就直接处理，而是：

1. 网络线程把完整消息放进队列
2. `Update()` 里在主线程取出来
3. 再做协议解析和业务分发

这样可以避免直接在异步线程里碰 Unity API。

当前用途：

- `NetManager` 的消息队列
- `NetManager` 的连接结果队列
- `ChatManager` 的收到文本队列

---

## 7. `ConcurrentDictionary<TKey, TValue>`

这是线程安全字典。

服务端以前用的是普通 `Dictionary<Socket, ClientState>`。

现在改成 `ConcurrentDictionary<Socket, ClientState>`，原因是：

- 服务端收包是异步的
- 广播发送也是异步的
- 断线清理也可能同时发生

普通字典在这种情况下更容易出现并发访问问题。

---

## 8. `SemaphoreSlim`

这是轻量级异步锁。

这次它不是用来锁整个网络层，而是专门用来保证：

- 同一个 socket 的发送不要并发重叠

为什么要这样做：

- 如果两个异步发送同时写同一个连接
- 数据顺序和边界就可能乱掉

所以现在是：

- 客户端一个 `sendLock`
- 服务端每个 `ClientState` 一个 `sendLock`

注意：

- `lock` 不能直接配合 `await` 使用
- 异步发送串行化更适合用 `SemaphoreSlim`

---

## 9. `CancellationTokenSource` / `CancellationToken`

这是“取消异步任务”的配套对象。

这次它主要用来停止收包循环。

例如客户端断开连接时：

1. 调用 `receiveCancellationTokenSource.Cancel()`
2. 告诉收包循环应该停下来了
3. 再关闭 socket

这样清理会更干净。

不然常见情况是：

- 连接都关了
- 接收循环还在等
- 最后抛出各种收尾异常

---

## 10. `lock`

这个不是新语法，但在这次代码里承担了一个新职责：

- 保护“未完成消息缓存”

客户端现在有：

- `pendingReceive`

它的含义是：

- 当前这一波收到的字节还没组成完整消息
- 要先缓存起来

对这个缓存的拼接和裁剪要保持一致，所以要加 `lock`。

注意：

- 这里只用它保护短小的内存操作
- 不要在 `lock` 里面 `await`

---

## 11. `pendingReceive`

这是客户端新增的“半包缓存字符串”。

为什么要它：

- TCP 是字节流
- 一条消息不保证一次 `Receive` 就完整收到

以前那种写法有个隐患：

- 收一段
- 直接 `Split('\n')`
- 最后一段如果不完整，可能就丢了

现在做法是：

1. 把新收到的内容追加到 `pendingReceive`
2. 按 `\n` 切分
3. 前面完整的消息拿出去处理
4. 最后那个可能不完整的尾巴继续留在 `pendingReceive`

等下一次数据到了，再继续拼。

---

## 12. `StringBuilder pendingMessages`

服务端的半包缓存不是字符串，而是 `StringBuilder`。

原因很简单：

- 服务端会频繁追加收到的内容
- 还会不断把已经消费掉的前缀删掉

这种“反复追加 + 删除头部”的操作，用 `StringBuilder` 比直接拼字符串更合适。

---

## 13. `TryReadMessage(...)`

这是这次新增的小工具函数。

它的职责是：

- 从缓存里尝试读取一条完整消息

读取规则是：

- 找到第一个 `\n`
- `\n` 前面的内容就是一条完整消息
- 读出来以后，把那一段从缓存里移除

如果还没找到 `\n`：

- 就说明当前还是半包
- 不能继续解析

---

## 14. `Task.WhenAll(...)`

这是“等待一组异步任务全部完成”。

服务端广播现在不是：

- 一个发完
- 再发下一个

而是：

1. 给每个客户端都创建一个发送任务
2. 放到 `List<Task>` 里
3. `await Task.WhenAll(sendTasks)`

这样广播更自然。

注意：

- 这是“不同客户端之间并发发送”
- 不是“同一个客户端上并发乱发”
- 同一个客户端仍然由 `sendLock` 串行保护

---

## 15. `TryAdd` / `TryRemove` / `TryGetValue` / `TryDequeue`

这类 `TryXxx` 系列方法都是一个思路：

- 尝试做某事
- 成功返回 `true`
- 失败返回 `false`
- 不把“失败”当成异常流程

在异步网络代码里很常见，因为：

- 客户端可能刚好断开
- 某个对象可能已经被清理
- 某条队列可能就是空的

这时候用 `TryXxx` 会比“直接取，失败再异常”更稳。

---

## 16. `ReferenceEquals(socket, currentSocket)`

这个判断是为了防止“过期异步结果回写当前状态”。

场景举例：

1. 发起第一次连接
2. 还没连上，又断开并重连
3. 第一条连接的异步结果晚一点才回来

如果不做判断，就可能发生：

- 老连接把新连接的状态覆盖掉

所以现在会检查：

- 当前恢复回来的这个 `currentSocket`
- 还是不是现在真正使用的那个 `socket`

如果不是，就丢掉。

---

## 17. `ObjectDisposedException`

这是资源已经被释放后，异步流程又回来继续访问它时常见的异常。

网络层里特别常见的情况是：

- 你主动 `CloseSocket()`
- 但另一个异步接收 / 发送任务还没彻底结束
- 它恢复执行时就会碰到“对象已释放”

所以现在很多地方会单独 `catch (ObjectDisposedException)`。

这不是为了掩盖 bug，而是把“正常收尾过程中的异常”单独吃掉。

---

## 18. `CloseSocket()`

这次把清理流程收口成了统一入口。

以前很多地方是：

- 直接 `socket.Close()`

现在会统一做这些事：

1. 取消接收循环
2. 尝试 `Shutdown`
3. 再 `Close`
4. 最后置空引用

这样做的好处是：

- 不容易漏清理
- 不同退出路径都走同一套收尾逻辑

---

## 19. `Shutdown(SocketShutdown.Both)`

这个动作比直接 `Close()` 多一层语义：

- 明确告诉 socket：发送和接收都要停止

一般来说：

- `Shutdown` 是协议层面的“我要关了”
- `Close` 是资源层面的“把它释放掉”

不是每次都必须成功，所以代码里会包 `try/catch`。

---

## 20. `totalSent` 发送循环

这次很重要的一个改动是：

- 不再假设一次 `SendAsync` 就能把整个包发完

所以现在会写成：

```csharp
int totalSent = 0;
while (totalSent < sendBytes.Length)
{
    int count = await socket.SendAsync(...);
    totalSent += count;
}
```

含义是：

- 一次发出去多少算多少
- 没发完就继续发

这是更稳的网络层写法。

---

## 21. “网络线程入队，主线程消费”

这不是某一个 API，而是这次改造里最重要的新模式。

现在客户端整体流程是：

1. 网络线程收包
2. 拼完整消息
3. 放进线程安全队列
4. `Update()` 主线程取出
5. 再做事件分发和业务处理

这样做的核心目的，是避免：

- 在异步 socket 线程里直接调用 Unity API

这一点以后继续扩功能时要一直保持。

---

## 22. 这次最值得记住的三条原则

- 协议层面仍然要自己处理半包，`ReceiveAsync` 不会自动帮你按消息分包。
- 同一个 socket 的发送必须串行，不要让多个异步发送直接并发写同一连接。
- Unity 相关逻辑尽量回到主线程做，网络线程只负责收发和缓存。

---

## 23. 后面继续学时可以重点对照的“旧 -> 新”

- `BeginConnect / EndConnect` -> `await ConnectAsync(...)`
- `BeginAccept / EndAccept` -> `await AcceptAsync()`
- `BeginReceive / EndReceive` -> `await ReceiveAsync(...)`
- `BeginSend / EndSend` -> `await SendAsync(...) + totalSent 循环`
- 回调里直接处理业务 -> 先进队列，再在 `Update()` 主线程消费
- 普通 `Dictionary` / `List` 跨线程混用 -> `ConcurrentDictionary` / `ConcurrentQueue`

---

如果后面你要，我可以继续在这份文档下面补第二部分：

- “这些新东西分别解决了旧代码里的什么坑”
- 或者“每一个新东西对应旧教程里的哪一段旧写法”
