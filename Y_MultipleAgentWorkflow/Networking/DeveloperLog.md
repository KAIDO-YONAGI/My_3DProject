# Networking 开发记录

## 2026-09-20：建立网络权威指南

- 证据：客户端 `NetManager`、`UdpConnection`，服务端 `ServerCore`、`ServerSocketSender`。
- 记录了 UDP 连接语义、UTF-8 收发、异步任务、并发队列、发送锁和错误处理。
- 明确当前没有握手、可靠传输、背压、鉴权、心跳或超时清理。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。
