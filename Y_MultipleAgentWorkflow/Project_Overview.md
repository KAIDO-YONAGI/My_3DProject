# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-09-29`

## 项目边界

本仓库当前是 **Unity 物理派对游戏工程**，按 `docs/plan/00-改造计划总览.md` 的八阶段计划执行。网络与玩法目标架构：

- 网络框架：Mirror（本地插件，`Assets/Mirror/` 在 `.gitignore` 中，不入库）。
- 服务器形态：Unity Dedicated Server Build（计划阶段七落地）。
- 玩法模型：普通 C# 状态 + 30Hz 固定 Tick 自研同步（快照插值、预测校正）。
- 玩法目标：2～5 人短回合物理乱斗垂直切片。

**历史事实**：本项目前身为自研联机框架。第一代为自研 TCP（提交 `4305eeb` 之前，LineMessageBuffer 分帧），第二代为自研 UDP（`UdpConnection` + `LocalServer` 独立 net10.0 控制台服务端）。两代均已完成多客户端加入/离开/移动同步。自研栈已于提交 `5c2b019` 完整退役删除，归档 tag 为 `v0.2-selfbuilt-net`。旧实现细节只在 git 历史中可查，不再是当前事实来源。

## 当前代码结构

- `Assets/Core/Scripts/`：仅剩 `Events/BoolEventChannelSO.cs`（通用事件通道，保留备用）。
- `Assets/Core/Scenes/`：`Main.unity`（空场景）、`MultiplayerSampleScene.unity`（旧自研网络组件已剥离，保留地形、光照、EventSystem、Character 角色实例，作为派对游戏原型场景基底）。
- `Assets/Core/Prefabs/`：两个角色 Prefab（"学园之星""华丽飞踢"），已移除 `SyncCharacter` 组件引用。
- `Assets/FrameWork/`：单机框架（UI 焦点栈、场景管理、SO 事件总线），约 1859 行，服务后续对局与调试 UI。
- `LocalServer/`：已删除。
- Mirror 插件：`.gitignore` 声明忽略 `Assets/Mirror/` 与 `/Assets/Plugins/`；**本次核验时磁盘上 `Assets/Mirror` 目录不存在**（仅有历史 csproj 与 ScriptTemplates 模板残留），阶段一接入时需重新导入。

## 目标架构（八阶段计划）

```text
输入源（真人/机器人/回放/网络）→ InputFrame → 固定 Tick 玩法模拟 → GameState → Unity 表现
客户端输入 → Mirror Command/NetworkMessage → 服务器输入队列
→ 权威模拟 → StateSnapshot + 离散事件 → 客户端插值/预测校正
```

阶段索引、全局约束和闸门见 `docs/plan/00-改造计划总览.md`。当前处于阶段一（玩法规格与工程基线）开始之前。

## 权威边界

- 客户端行为：`Client/Client_Guide.md`
- 网络传输：`Networking/Networking_Guide.md`
- 服务端行为：`Server/Server_Guide.md`
- 线协议：`Protocol/Protocol_Guide.md`
- Unity 场景与资产：`UnityRuntime/UnityRuntime_Guide.md`

## 当前状态与风险（2026-09-29 退役后）

- 仓库当前没有任何可运行的网络链路；联机能力在阶段七重建。
- `MultiplayerSampleScene` 中角色 Prefab 的相机、CharacterController 等单机组件仍在，可作原型基底，但没有任何网络组件。
- Build Settings 场景列表仍为空。
- 自研 TCP/UDP 协议（`Enter/Move/Leave/Attack` 文本协议）已随代码退役；新协议将在阶段七按 `InputFrame`/快照重新设计，旧 `Protocol` 知识域文档已标记为历史参考。
