# Server 开发记录

## 2026-09-20：建立服务端权威指南

- 证据：`LocalServer/LocalServer.csproj` 和 `LocalServer/Scripts/`。
- 记录了服务端入口、客户端注册表、Enter/Move/Leave 处理、广播和既有玩家同步。
- 明确端点身份、移动转发、无心跳超时及 Attack 未接通等边界。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。
