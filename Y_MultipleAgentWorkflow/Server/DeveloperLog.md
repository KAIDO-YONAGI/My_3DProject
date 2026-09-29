# Server 开发记录

## 2026-09-20：建立服务端权威指南

- 证据：`LocalServer/LocalServer.csproj` 和 `LocalServer/Scripts/`。
- 记录了服务端入口、客户端注册表、Enter/Move/Leave 处理、广播和既有玩家同步。
- 明确端点身份、移动转发、无心跳超时及 Attack 未接通等边界。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。

## 2026-09-29：LocalServer 独立服务端退役

- 变更：删除 `LocalServer/` 全部工程文件（net10.0 UDP 控制台，提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。
- 服务端形态切换为 Unity Dedicated Server Build（`docs/plan/07`）；旧服务端为纯转发管道、客户端权威 Move，与服务器权威目标架构冲突。
- `Server_Guide.md` 与本域 Router 状态改为 `Retired`；阶段七落地时重建本域。
- 域退役，维护计数重置为 `0/5`。
