# My_3DProject 权威文档路由

文档 ID：`ROOT-ROUTER`
状态：`Active`
最后更新：`2026-09-20`

本目录是项目权威文档的唯一入口。原有 `docs/` 和其他未在本文档索引中的旧文档继续保留，但默认不读取、不迁移，也不作为当前事实依据。

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
| 项目整体结构、跨域调用链、启动关系 | `Project_Overview.md` | `Project_Overview.md` |
| 玩家生命周期、角色创建/移除、位置同步 | `Client\Router.md` | `Client\Client_Guide.md` |
| UDP 连接、异步收发、队列、网络错误 | `Networking\Router.md` | `Networking\Networking_Guide.md` |
| 服务端启动、客户端注册、广播和状态 | `Server\Router.md` | `Server\Server_Guide.md` |
| `Enter`、`Move`、`Leave`、`Attack`、字段格式 | `Protocol\Router.md` | `Protocol\Protocol_Guide.md` |
| Unity 版本、场景、Prefab、事件资产、构建配置 | `UnityRuntime\Router.md` | `UnityRuntime\UnityRuntime_Guide.md` |
| 路由、租约、维护周期、文档分类 | `Workflow\Router.md` | `Workflow\Workflow_Guide.md` |

跨知识域任务必须读取所有受影响入口。例如协议修改至少同时进入 `Protocol`、`Client`、`Server` 和 `Networking`。

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
| `PROJ-OVERVIEW` | `Project_Overview.md` | 项目边界、总体架构和跨域数据流 | Active | 2026-09-20 |
| `CLIENT-GUIDE` | `Client/Client_Guide.md` | 客户端生命周期与玩家同步 | Active | 2026-09-20 |
| `NETWORK-GUIDE` | `Networking/Networking_Guide.md` | UDP 连接、收发、队列与错误处理 | Active | 2026-09-20 |
| `SERVER-GUIDE` | `Server/Server_Guide.md` | 服务端核心、注册表与广播 | Active | 2026-09-20 |
| `PROTOCOL-GUIDE` | `Protocol/Protocol_Guide.md` | 双端共享协议及不对称点 | Active | 2026-09-20 |
| `UNITY-GUIDE` | `UnityRuntime/UnityRuntime_Guide.md` | Unity 场景、Prefab、资产和配置 | Active | 2026-09-20 |
| `WF-GUIDE` | `Workflow/Workflow_Guide.md` | 路由与维护规则 | Active | 2026-09-20 |
| `WF-CONCURRENCY` | `Workflow/Concurrency_Guide.md` | WorkingAgent 租约规则 | Active | 2026-09-20 |
| `WF-CONFIG-METHOD` | `Workflow_Configuration_Guide.md` | 工作流配置通用方法 | Active | 2026-09-20 |

## 维护

只有成功且实质影响业务实现的任务才增加对应知识域的维护计数。只读分析、租约操作和纯文档维护不计数。计数达到 `5/5` 时，必须根据实际项目状态复核权威文档并重置计数。
