# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-09-29`

## 项目定位

物理派对游戏，2～5 人短回合乱斗，Unity 客户端 + Mirror 专用服务器。玩法计划按 `docs/plan/00-改造计划总览.md` 的八个阶段执行，当前处于阶段一。

## 架构

```text
输入源（真人/机器人/回放/网络）→ InputFrame → 固定 Tick 玩法模拟 → GameState → Unity 表现
客户端输入 → Mirror Command/NetworkMessage → 服务器输入队列
→ 权威模拟 → StateSnapshot + 离散事件 → 客户端插值/预测校正
```

网络框架使用 Mirror（本地插件）与 KCP Transport。玩法模拟按阶段六从 Unity 对象中抽出，服务器权威结算集中在 `GameState`。远端对象用状态快照插值显示，本地角色预测移动、跳跃和冲刺。

## 代码结构

- `Assets/Core/Scripts/Networking/`：Mirror 集成脚本（`NetworkPlayerController`、`LocalPlayerCamera`、`AutoStartServerBuild`）。
- `Assets/Core/FrameWork/`：单机框架（UI 焦点栈、场景管理、SO 事件总线）。
- `Assets/Core/Scenes/LobbyScene.unity`：联机原型场景，NetworkManager 与 KCP 配置入口。
- `Assets/Core/Prefabs/Player_Network.prefab`：联网玩家 Prefab。
- `Assets/Mirror/`：Mirror 插件，本地不入库，重装方法见 `UnityRuntime/Mirror_KCP_Config.md`。
- 构建产物输出到 `D:/Unity/Releases/3D_MultiplayerGame/`（客户端 `Client_3_0`，服务器 `Server_3_0`）。

## 网络链路现状

服务器构建双击启动后自动监听 7777，客户端通过 HUD 或 networkAddress 连接 `127.0.0.1:7777`。双客户端连接服务器、玩家生成、移动同步已验证。配置细节与联调流程见 `UnityRuntime/Mirror_KCP_Config.md`。

## 权威边界

- Unity 场景、资产、构建：`UnityRuntime/UnityRuntime_Guide.md`
- Mirror 与 KCP 配置：`UnityRuntime/Mirror_KCP_Config.md`
- 路由与维护规则：`Workflow/Workflow_Guide.md`

## 风险与边界

- Build Settings 场景已配置，构建入口为 LobbyScene。
- 场景相机禁用，视角依赖玩家 Prefab 内的相机。
- NavMesh 未绑定烘焙数据。
- 玩法模拟尚未从 Unity 对象中抽出，固定 Tick 模型在阶段六建立。
