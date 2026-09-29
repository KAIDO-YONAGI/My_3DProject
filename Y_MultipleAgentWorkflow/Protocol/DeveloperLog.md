# Protocol 开发记录

## 2026-09-20：建立协议权威指南

- 证据：客户端与服务端协议定义、服务端消息处理器及直接调用方。
- 记录了 UTF-8 文本协议、分隔符和 Enter/Move/Leave/Attack 双向格式。
- 标记了 Attack 字段不对称、单数据报多消息处理差异和区域设置风险。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。

## 2026-09-29：文本协议退役

- 变更：随自研网络栈删除 `ClientProtocol.cs` 与 `ServerProtocol.cs`（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。
- `Protocol_Guide.md` 与本域 Router 状态改为 `Retired`；历史 `Enter/Move/Leave/Attack` 文本协议（含 Attack 不对称、无版本字段、区域设置风险）仅存于 git 历史。
- 新协议方向：`InputFrame` 输入上传 + 自定义快照 + 可靠事件，Mirror NetworkWriter/Reader 二进制序列化，阶段七设计并双端接通。
- 域退役，维护计数重置为 `0/5`。
