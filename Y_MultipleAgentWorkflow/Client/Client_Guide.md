# 客户端生命周期与玩家同步

文档 ID：`CLIENT-GUIDE`
状态：`Active`
最后核验：`2026-09-20`

## 主要组件

- `SyncCharacter`：客户端同步入口，注册 `Enter`、`Move`、`Leave` 监听，发起连接并定时上报本地位置。
- `NetManager`：跨场景单例，在 Unity 主线程消费连接结果、日志和入站消息。
- `ClientMessageHandler`：把协议消息转换为玩家管理操作。
- `PlayerManager`：场景级单例，保存远端玩家状态、Prefab 实例和待刷新位置。

## 生命周期

1. `SyncCharacter.Start` 注册消息监听和连接结果事件，然后启动 `ConnectWithRetry`。
2. 客户端固定连接 `127.0.0.1:8888`。连接成功后读取本地 UDP 端点作为 `myPlayerId`，发送 `Enter`。
3. `SyncCharacter.Update` 每隔超过 `0.05` 秒读取本地角色位置并发送 `Move`。
4. 对象销毁时取消连接结果订阅并请求 `NetManager.Disconnect`；主动断开会尝试先发送 `Leave`。

## 入站消息

- `Enter`：解析 `playerId`、`modelID`、`health`、`damage`，校验模型索引和 Prefab 后在原点实例化远端角色。
- `Move`：排除本地玩家 ID，更新已登记远端玩家的位置；尚未收到 `Enter` 的 ID 被忽略。
- `Leave`：销毁对应实例并从玩家字典移除。
- `Attack`：协议可解析，但当前同步入口没有注册业务监听。

## 线程边界

异步网络回调只写入并发队列。Unity 对象创建、销毁、Transform 更新和事件派发均在 `NetManager.Update` 或 `PlayerManager.Update` 的主线程阶段发生。

## 已确认限制

- UDP “连接成功”仅表示本地端点设置完成，不代表服务端可达。
- 初次连接成功后重试协程结束，当前没有运行期断线重连。
- 位置更新为直接赋值，没有插值、预测、旋转、速度、动画或时间戳同步。
- 每种消息类型只保存一个监听器，再次注册会覆盖旧监听器，且没有移除接口。
- 待刷新列表不去重；`RemovePosition` 也不清理该列表，存在移除后再次按键索引导致 `KeyNotFoundException` 的路径。
- `OnEnter` 当前不排除本地玩家，服务端若回发本地 `Enter`，客户端可能生成自身的远端副本。

## 维护触发

修改 `SyncCharacter.cs`、`PlayerManager.cs`、`ClientMessageHandler.cs` 或客户端消息分发行为时，更新本文档；涉及协议或传输时同步更新对应知识域。
