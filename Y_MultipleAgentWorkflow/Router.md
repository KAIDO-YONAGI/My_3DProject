# My_3DProject 权威文档路由

文档 ID：`ROOT-ROUTER`
状态：`Active`
最后核验：`2026-09-29`

本目录是项目权威文档的唯一入口。`docs/` 保留计划、草案和教学材料，不作为当前实现事实；不因本次重建迁移、删除或改写其中内容。

## 权威顺序

1. 用户最新明确要求。
2. 实际代码、资源、运行结果和可复现验证。
3. 对应知识域的 Guide。
4. 对应知识域的 Router。
5. DeveloperLog。
6. `AGENTS.md`、`CLAUDE.md`、Skills 等入口与通用规则。

文档与实现不一致时，以实现和验证结果为准，并在任务结束前更新对应 Guide 与 DeveloperLog。

## 当前实现入口

- 联机启动场景：`Assets/Core/Scenes/PersistentScene.unity`。
- 在线玩法场景：`Assets/Core/Scenes/MultiplayerSampleScene.unity`。
- 当前玩家 Prefab：`Assets/Core/Prefabs/CharactersForSync/娜娜莉（华丽飞踢）.prefab`。
- Mirror/KCP、自动连接、同步方向和构建事实：`UnityRuntime/Mirror_KCP_Config.md`。

## 任务路由

| 触发词或目标 | 必读文档 |
|---|---|
| 项目定位、当前架构、联机链路、实施边界 | `Project_Overview.md` |
| Mirror、KCP、NetworkManager、玩家 Prefab、自动连接、构建和联调 | `UnityRuntime/Mirror_KCP_Config.md` |
| Unity 版本、Packages、Build Settings、场景、Prefab、事件资产 | `UnityRuntime/UnityRuntime_Guide.md` |
| 路由、租约、维护周期、文档分类 | `Workflow/Workflow_Guide.md`、`Workflow/Concurrency_Guide.md` |
| 八阶段计划、InputFrame、固定 Tick、快照、预测 | `docs/plan/00-改造计划总览.md`，仅作为计划读取 |
| Mirror/KCP 教学与从零配置 | `docs/Mirror+KCP配置学习指南.md`，当前工程事实仍以 `UnityRuntime/Mirror_KCP_Config.md` 为准 |

跨知识域任务必须同时读取所有受影响入口。

## 开始步骤

1. 通过本文档确定知识域。
2. 阅读 `Workflow/Concurrency_Guide.md`。
3. 轻量只读定位完成后，在详细分析、子代理、修改或状态变更前，使用 `Workflow/Scripts/WorkingAgent.ps1` 申请精确租约。
4. 仅读取被路由的权威文档和完成任务所需的实际代码或资源。
5. 成功、失败或取消时都释放租约。

## 项目验证

`ProjectValidationMode=None`。该设置表示工作流不自动推断或强制执行项目级构建、启动、测试和部署命令；已经实际完成的人工或任务内验证仍可作为证据记录。

## 文档索引

| 文档 ID | 路径 | 职责 | 状态 | 最近核验 |
|---|---|---|---|---|
| `PROJ-OVERVIEW` | `Project_Overview.md` | 项目边界、当前架构和联机链路 | Active | 2026-09-29 |
| `UNITY-GUIDE` | `UnityRuntime/UnityRuntime_Guide.md` | Unity 场景、Prefab、资产和配置 | Active | 2026-09-29 |
| `UNITY-NETCFG` | `UnityRuntime/Mirror_KCP_Config.md` | Mirror 与 KCP 工程配置及验证证据 | Active | 2026-09-29 |
| `WF-GUIDE` | `Workflow/Workflow_Guide.md` | 路由与维护规则 | Active | 2026-09-20 |
| `WF-CONCURRENCY` | `Workflow/Concurrency_Guide.md` | WorkingAgent 租约规则 | Active | 2026-09-20 |
| `WF-CONFIG-METHOD` | `Workflow_Configuration_Guide.md` | 工作流配置通用方法 | Active | 2026-09-20 |

## 维护

只有成功且实质影响业务实现的任务才增加对应知识域的维护计数。只读分析、租约操作和纯文档维护不计数。计数达到 `5/5` 时，必须根据实际项目状态复核权威文档并重置计数。
