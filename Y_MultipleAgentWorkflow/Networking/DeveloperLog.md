# Networking 开发记录

## 2026-09-20：建立网络权威指南

- 证据：客户端 `NetManager`、`UdpConnection`，服务端 `ServerCore`、`ServerSocketSender`。
- 记录了 UDP 连接语义、UTF-8 收发、异步任务、并发队列、发送锁和错误处理。
- 明确当前没有握手、可靠传输、背压、鉴权、心跳或超时清理。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。

## 2026-09-29：自研 UDP 传输栈退役

- 变更：删除 `UdpConnection`、`NetManager` 及 `LocalServer` 服务端收发组件（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。
- `Networking_Guide.md` 与本域 Router 状态改为 `Retired`；退役原因：缺失的握手/可靠性/心跳能力属于已解决问题，由 Mirror KCP 承担传输，同步模型按 `docs/plan/` 自研。
- 新传输层文档在阶段七落地时重建。
- 域退役，维护计数重置为 `0/5`。
