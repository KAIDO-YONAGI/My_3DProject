# My_3DProject 权威文档路由

文档 ID：`ROOT-ROUTER`
状态：`Active`
最后核验：`2026-09-30`

本目录是项目权威文档入口。项目知识按代码职责拆分为 `Client`、`Networking`、`Server`、`Protocol`、`UnityRuntime`，`Workflow` 管理文档路由与并发租约。工程当前事实以实际代码、资源、运行结果和本目录 Guide 为准。

## 权威顺序

1. 用户最新明确要求。
2. 实际代码、资源、运行结果和可复现验证。
3. 对应知识域的 Guide。
4. 对应知识域的 Router。
5. DeveloperLog。
6. `AGENTS.md`、`CLAUDE.md`、Skills 等入口与通用规则。

文档与实现不一致时，以实现和验证结果为准，并在任务结束前更新受影响的 Guide。

## 当前实现入口

- 初始场景：`Assets/Core/Scenes/InitialScene.unity`。
- 常驻场景：`Assets/Core/Scenes/PersistentScene.unity`。
- 玩法场景：`Assets/Core/Scenes/MultiplayerSampleScene.unity`。
- 场景框架：`Assets/FrameWork/Scripts/Scene/`。
- 网络玩家 Prefab：`Assets/Core/Prefabs/Player_Network.prefab`。
- Mirror/KCP、自动连接、玩家生成、同步方向和构建事实：`UnityRuntime/Mirror_KCP_Config.md`。

## 任务路由

| 触发词或目标 | 必读文档 |
|---|---|
| 项目定位、当前架构、联机链路、实施边界 | `Project_Overview.md` |
| 客户端生命周期、玩家输入、移动、相机和客户端表现 | `Client/Client_Guide.md` |
| Mirror、KCP、NetworkManager、自动连接、玩家网络组件 | `Networking/Networking_Guide.md` |
| 专用服务器启动、Headless 构建、监听和部署事实 | `Server/Server_Guide.md` |
| 客户端与服务器消息边界、同步字段和兼容规则 | `Protocol/Protocol_Guide.md` |
| Unity 版本、Packages、Build Settings、场景和 Prefab | `UnityRuntime/UnityRuntime_Guide.md` |
| Mirror 参数、玩家 Prefab、联机验证和构建产物 | `UnityRuntime/Mirror_KCP_Config.md` |
| 路由、租约、维护周期、文档分类 | `Workflow/Workflow_Guide.md`、`Workflow/Concurrency_Guide.md` |
| 场景配置、UDP/KCP、Mirror 生命周期、角色装配和联机验收 | `UnityRuntime/Mirror_KCP_Config.md`、`UnityRuntime/UnityRuntime_Guide.md` |

跨知识域任务同时读取所有受影响入口。

## 开始步骤

1. 通过本文档确定知识域。
2. 阅读 `Workflow/Concurrency_Guide.md`。
3. 轻量只读定位完成后，在详细分析、子代理、修改或状态变更前，使用 `Workflow/Scripts/WorkingAgent.ps1` 申请精确租约。
4. 读取被路由的权威文档和完成任务所需的实际代码或资源。
5. 成功、失败或取消时释放租约。

## 项目验证

`ProjectValidationMode=None`。工作流不自动推断项目级构建、启动、测试和部署命令。任务中实际完成的验证作为工程证据。

## 文档索引

| 文档 ID | 路径 | 职责 | 状态 | 最近核验 |
|---|---|---|---|---|
| `PROJ-OVERVIEW` | `Project_Overview.md` | 项目边界、当前架构和联机链路 | Active | 2026-09-30 |
| `CLIENT-GUIDE` | `Client/Client_Guide.md` | 客户端生命周期、输入、移动和相机 | Active | 2026-09-30 |
| `NETWORKING-GUIDE` | `Networking/Networking_Guide.md` | Mirror 网络运行时组件与连接管理 | Active | 2026-09-30 |
| `SERVER-GUIDE` | `Server/Server_Guide.md` | 专用服务器运行与构建事实 | Active | 2026-09-30 |
| `PROTOCOL-GUIDE` | `Protocol/Protocol_Guide.md` | 协议边界、同步字段和兼容规则 | Active | 2026-09-30 |
| `UNITY-GUIDE` | `UnityRuntime/UnityRuntime_Guide.md` | Unity 场景、Prefab、资产和配置 | Active | 2026-09-30 |
| `UNITY-NETCFG` | `UnityRuntime/Mirror_KCP_Config.md` | Mirror 与 KCP 工程配置及验证方式 | Active | 2026-09-30 |
| `WF-GUIDE` | `Workflow/Workflow_Guide.md` | 路由与维护规则 | Active | 2026-09-20 |
| `WF-CONCURRENCY` | `Workflow/Concurrency_Guide.md` | WorkingAgent 租约规则 | Active | 2026-09-20 |
| `WF-CONFIG-METHOD` | `Workflow_Configuration_Guide.md` | 工作流配置通用方法 | Active | 2026-09-20 |

## 维护

只有成功且实质影响业务实现的任务才增加对应知识域的维护计数。只读分析、租约操作和纯文档维护不计数。计数达到 `5/5` 时，根据实际项目状态复核权威文档并重置计数。
