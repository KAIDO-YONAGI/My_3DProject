# 网络协议边界

文档 ID：`PROTOCOL-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

## 当前协议形态

当前没有项目自定义的 `Protocol` 源码目录，也没有自定义 `NetworkMessage`、Command/Rpc 消息定义。网络边界由 Mirror 内置连接、对象生成和 `NetworkTransformReliable` 序列化承载。

- 连接传输：KCP，UDP `7777`。
- 玩家对象身份：`NetworkIdentity`。
- 玩家位置同步：`NetworkTransformReliable`，`SyncDirection=ClientToServer`。
- 玩家生成：NetworkManager `autoCreatePlayer=true`。

## 兼容性规则

客户端和服务端必须使用相同的玩家组件布局。只替换一端构建可能触发 `OnDeserialize` 或 `EndOfStreamException`。修改 NetworkBehaviour 列表、同步组件或序列化字段后，必须同时重建并验证两端。

## Proposal 边界

固定 Tick、`InputFrame`、服务器输入队列、自定义状态快照、插值和预测校正目前均未实现。它们可以在 `docs/plan/` 中作为计划阅读，但落地前不得写入“当前协议”。

## 维护触发

新增或修改消息、序列化字段、NetworkTransform、输入帧、快照或兼容版本规则时更新本文档，并同步 Networking、Server 文档。

