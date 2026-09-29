# Client 知识域路由

文档 ID：`BUS-CLIENT`
状态：`Retired`
维护计数：`0/5`
最后更新：`2026-09-29`

> **本知识域描述的客户端同步组件（`SyncCharacter`、`PlayerManager`、`ClientMessageHandler`、`NetManager`）已于 2026-09-29 退役删除**（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。新客户端链路按 `docs/plan/` 八阶段计划在阶段一至八重建，届时重开本域。

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 历史自研客户端（连接重试、远端玩家字典、消息监听） | `Client_Guide.md`（Retired，仅历史参考） |
| 场景和 Prefab 绑定（当前有效） | `../UnityRuntime/UnityRuntime_Guide.md` |
| Mirror 客户端、`InputFrame`、快照插值、预测校正 | 尚无权威文档；按 `docs/plan/` 阶段落地时重建本域 |

## 历史证据路径（仅 git 历史）

- `git show v0.2-selfbuilt-net^:Assets/Core/Scripts/Client/`

## 当前证据路径

- `Assets/Core/Scripts/Events/BoolEventChannelSO.cs`（仓库仅剩的 Core 脚本）
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`（原型基底场景）

## 并发资源

- `workflow:Client`
- `path:Assets/Core/Scripts`

## 能力边界

本域历史上描述自研客户端生命周期与玩家同步。重建后描述 Mirror 客户端与自研同步模型的客户端侧；网络传输细节归 `Networking`，线协议归 `Protocol`，Unity 序列化绑定归 `UnityRuntime`。
