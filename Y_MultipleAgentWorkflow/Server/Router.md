# Server 知识域路由

文档 ID：`BUS-SERVER`
状态：`Active`
维护计数：`0/5`
最后更新：`2026-09-20`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 服务端入口、接收循环、客户端注册 | `Server_Guide.md` |
| 广播、现有玩家同步、离开清理 | `Server_Guide.md` |
| 服务端状态所有权、并发集合 | `Server_Guide.md` |
| 消息字段和双端兼容性 | `../Protocol/Protocol_Guide.md` |

## 主要证据路径

- `LocalServer/LocalServer.csproj`
- `LocalServer/Scripts/`

## 并发资源

- `workflow:Server`
- `path:LocalServer`
- 协议变更同时申请 `workflow:Protocol` 和客户端协议文件路径

## 能力边界

本域描述服务端运行行为和状态。线协议格式归 `Protocol`，Socket 传输策略归 `Networking`。
