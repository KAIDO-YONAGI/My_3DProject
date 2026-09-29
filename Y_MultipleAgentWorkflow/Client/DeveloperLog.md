# Client 开发记录

## 2026-09-20：建立客户端权威指南

- 证据：`Assets/Core/Scripts/Client/` 及必要的直接引用配置。
- 记录了连接后的 Enter、约 20 次/秒的位置上报、主线程消息落地、远端玩家创建与移除。
- 记录了断线重连未完成、监听器覆盖、位置直接赋值和待刷新列表风险。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。

## 2026-09-29：自研客户端网络栈退役

- 变更：删除 `SyncCharacter`、`NetManager`、`PlayerManager`、`ClientMessageHandler`（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。
- `Client_Guide.md` 与本域 Router 状态改为 `Retired`，保留历史设计作为 git 历史解读参考。
- 当前 `Assets/Core/Scripts/` 仅剩 `Events/BoolEventChannelSO.cs`。
- 新客户端链路（Mirror + InputFrame + 快照插值/预测校正）按 `docs/plan/` 阶段一至八重建时重开本域。
- 本次为业务实现实质变更，Client 域维护计数不适用（域已退役），重置为 `0/5`。
