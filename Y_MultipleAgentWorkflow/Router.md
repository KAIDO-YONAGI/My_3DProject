# My_3DProject 权威文档路由

文档 ID：`ROOT-ROUTER`
状态：`Active`
最后更新：`2026-09-29`

本目录是项目权威文档的唯一入口。原有 `docs/` 和其他未在本文档索引中的旧文档继续保留，但默认不读取、不迁移，也不作为当前事实依据。

> **2026-09-29 重大变更**：自研 TCP/UDP 网络栈与 `LocalServer` 已退役删除（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。`Client`、`Networking`、`Server`、`Protocol` 四个知识域当前为 **Retired** 状态，仅作 git 历史解读参考；项目当前目标架构与活跃文档见 `Project_Overview.md` 与 `docs/plan/`。新网络栈（Mirror + 自研固定 Tick 同步）落地时逐域重建。

## 权威顺序

1. 用户最新明确要求。
2. 实际代码、资源、运行结果和可复现验证。
3. 对应知识域的 Guide。
4. 对应知识域的 Router。
5. DeveloperLog 和 Proposal。
6. `AGENTS.md`、`CLAUDE.md`、Skills 等入口与通用规则。

文档与实现不一致时，以实现和验证结果为准，并在任务结束前更新对应 Guide 与 DeveloperLog。

## 任务路由

| 触发词或目标 | 知识域入口 | 首要权威文档 |
|---|---|---|
| 项目整体结构、目标架构、网络栈演进史 | `Project_Overview.md` | `Project_Overview.md` |
| 玩法计划、八阶段执行、InputFrame/快照/预测 | `docs/plan/00-改造计划总览.md` | `docs/plan/`（计划文档，非权威库域） |
| 历史玩家同步、自研客户端组件 | `Client\Router.md`（Retired） | `Client\Client_Guide.md`（Retired） |
| 历史自研 UDP 传输 | `Networking\Router.md`（Retired） | `Networking\Networking_Guide.md`（Retired） |
| 历史 LocalServer | `Server\Router.md`（Retired） | `Server\Server_Guide.md`（Retired） |
| 历史文本协议 `Enter`/`Move`/`Leave`/`Attack` | `Protocol\Router.md`（Retired） | `Protocol\Protocol_Guide.md`（Retired） |
| Unity 版本、场景、Prefab、事件资产、构建配置 | `UnityRuntime\Router.md` | `UnityRuntime\UnityRuntime_Guide.md` |
| 路由、租约、维护周期、文档分类 | `Workflow\Router.md` | `Workflow\Workflow_Guide.md` |

跨知识域任务必须读取所有受影响入口。修改当前 Unity 资产时进入 `UnityRuntime`；重建网络链路时同时重建受影响的 Retired 域。

## 必须遵循的开始步骤

1. 通过本文档确定知识域。
2. 阅读 `Workflow/Concurrency_Guide.md`。
3. 轻量只读定位完成后，在详细分析、子代理、修改或状态变更前，使用 `Workflow/Scripts/WorkingAgent.ps1` 申请精确租约。
4. 仅读取被路由的 Guide、Router 和完成任务所需的实际代码或资源。
5. 成功、失败或取消时都释放租约。

## 项目验证

`ProjectValidationMode=None`。当前没有经过用户确认的构建、启动、测试或部署命令，工作流不得自行推断或强制执行项目级命令。

## 文档索引

| 文档 ID | 路径 | 职责 | 状态 | 最近核验 |
|---|---|---|---|---|
| `PROJ-OVERVIEW` | `Project_Overview.md` | 项目边界、目标架构和网络栈演进史 | Active | 2026-09-29 |
| `CLIENT-GUIDE` | `Client/Client_Guide.md` | 自研客户端同步（已退役，历史参考） | Retired | 2026-09-29 |
| `NETWORK-GUIDE` | `Networking/Networking_Guide.md` | 自研 UDP 传输（已退役，历史参考） | Retired | 2026-09-29 |
| `SERVER-GUIDE` | `Server/Server_Guide.md` | 历史 LocalServer（已退役，历史参考） | Retired | 2026-09-29 |
| `PROTOCOL-GUIDE` | `Protocol/Protocol_Guide.md` | 历史文本协议（已退役，历史参考） | Retired | 2026-09-29 |
| `UNITY-GUIDE` | `UnityRuntime/UnityRuntime_Guide.md` | Unity 场景、Prefab、资产和配置 | Active | 2026-09-29 |
| `WF-GUIDE` | `Workflow/Workflow_Guide.md` | 路由与维护规则 | Active | 2026-09-20 |
| `WF-CONCURRENCY` | `Workflow/Concurrency_Guide.md` | WorkingAgent 租约规则 | Active | 2026-09-20 |
| `WF-CONFIG-METHOD` | `Workflow_Configuration_Guide.md` | 工作流配置通用方法 | Active | 2026-09-20 |

## 维护

只有成功且实质影响业务实现的任务才增加对应知识域的维护计数。只读分析、租约操作和纯文档维护不计数。计数达到 `5/5` 时，必须根据实际项目状态复核权威文档并重置计数。
