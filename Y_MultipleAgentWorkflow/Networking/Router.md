# Networking 知识域路由

文档 ID：`BUS-NETWORKING`
状态：`Active`
维护计数：`0/5`
最后更新：`2026-09-20`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| UDP 连接、断开、发送、接收 | `Networking_Guide.md` |
| 并发队列、异步任务、主线程派发 | `Networking_Guide.md` |
| 网络异常、重连、超时、背压 | `Networking_Guide.md` |
| 消息格式与字段兼容 | `../Protocol/Protocol_Guide.md` |

## 主要证据路径

- `Assets/Core/Scripts/Client/Net/NetManager.cs`
- `Assets/Core/Scripts/Client/Net/UdpConnection.cs`
- `LocalServer/Scripts/ServerCore.cs`
- `LocalServer/Scripts/ServerSocketSender.cs`

## 并发资源

- `workflow:Networking`
- `path:Assets/Core/Scripts/Client/Net`
- `path:LocalServer/Scripts/ServerCore.cs`
- `path:LocalServer/Scripts/ServerSocketSender.cs`

## 能力边界

本域描述传输和并发模型，不定义业务字段。改变消息文本布局时必须进入 `Protocol`。
