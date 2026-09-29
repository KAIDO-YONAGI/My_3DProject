# 客户端生命周期与玩家同步

文档 ID：`CLIENT-GUIDE`
状态：`Retired`
最后核验：`2026-09-29`

> **本文档描述的客户端同步组件（`SyncCharacter`、`NetManager`、`PlayerManager`、`ClientMessageHandler`）已于 2026-09-29 退役删除**（提交 `5c2b019`，归档 tag `v0.2-selfbuilt-net`）。这些脚本不再存在于工作区，本文件保留作为 git 历史的解读参考，不作为当前事实依据。

## 退役原因

- 自研 UDP 客户端栈整体让位于 Mirror KCP + 自研固定 Tick 同步模型（`docs/plan/` 八阶段计划）。
- 旧链路是"客户端直发位置 + 服务端转发"的客户端权威模型，与新架构"输入上传 + 服务器权威模拟 + 快照插值/预测校正"不兼容。

## 历史设计存档（仅参考 git 历史）

- `SyncCharacter`：连接重试协程（重连延迟 3 秒）、每 0.05 秒上报位置、监听 `Enter/Move/Leave`。
- `NetManager`：DontDestroyOnLoad 单例、连接编号防串扰、ConcurrentQueue 主线程消费。
- `PlayerManager`：远端玩家字典 + Prefab 实例 + 待刷新列表。
- 已知缺陷（原文档"已确认限制"）：无运行期断线重连、位置直接赋值无插值、监听器覆盖无移除接口、待刷新列表不去重等。

## 当前状态

`Assets/Core/Scripts/` 仅剩 `Events/BoolEventChannelSO.cs`（通用事件通道，保留备用）。两个角色 Prefab 上原 `SyncCharacter` 组件引用已从序列化数据中剥离，相机、CharacterController 等单机组件保留，作为派对游戏原型场景基底。

## 替代文档

阶段一至八推进时，本知识域将重写为 Mirror NetworkIdentity 客户端、`InputFrame` 采集、快照插值与预测校正的权威文档。
