# My_3DProject 权威文档路由

## 编写准则

编写文档时注意

- 除非特别说明，所有文字保持完全的实时性陈述。
- 没有的东西、过时的东西不要特地说明。
- 严禁莫名其妙的版本变更记录。
- 严禁加上修改记录和各种莫名其妙的版本补丁。
- 除非有文档结构要求，否则未完成、未测试等涉及过去、将来性质的过程性描述都严禁出现。
- 避免用括号括出莫名其妙的解释，直接用正文格式解释明白。
- 避免否定和转折句式的滥用。

文档 ID：`ROOT-ROUTER`
状态：`Active`
最后核验：`2026-10-11`

本目录是项目权威文档入口。项目知识按功能域拆分为 `Character`、`Multiplayer`、`Scene`、`Project`，`Workflow` 管理文档路由与并发租约。工程当前事实以实际代码、资源、运行结果和本目录 Guide 为准。

权威文档不是垃圾场，新增内容一律服从本准则。

## 权威顺序

1. 用户最新明确要求。
2. 实际代码、资源、运行结果和可复现验证。
3. 对应功能域的 Guide。
4. 对应功能域的 Router。
5. DeveloperLog。
6. `AGENTS.md`、`CLAUDE.md`、Skills 等入口与通用规则。

文档与实现不一致时，以实现和验证结果为准，并在任务结束前更新受影响的 Guide。

## 当前实现入口

- 初始场景：`Assets/Core/Scenes/InitialScene.unity`。
- 常驻场景：`Assets/Core/Scenes/PersistentScene.unity`。
- 玩法场景：`Assets/Core/Scenes/MultiplayerSampleScene.unity`、`Assets/Core/Scenes/MultiplayerSampleScene 1.unity`。
- 场景框架：`Assets/Core/Scripts/GamePlayFrameWork/Scripts/Scene/`。
- 网络玩家 Prefab：`Assets/Core/Prefabs/NetworkPlayer.prefab`。
- Mirror/KCP、自动连接、玩家生成、同步方向和构建事实：`Project/Mirror_KCP_Config.md`。

## 功能域路由

| 触发词或目标 | 必读文档 |
|---|---|
| 项目定位、当前架构、联机链路、实施边界 | `Project_Overview.md` |
| 玩家输入、角色移动、状态机、动画和本地相机 | `Character/Character_Guide.md` |
| 角色模型、动画素材与相机 Prefab | `Character/Character_Guide.md`、`Project/Project_Guide.md` |
| Mirror、KCP、NetworkManager、自动连接、玩家网络组件 | `Multiplayer/Multiplayer_Guide.md` |
| 客户端与服务器消息边界、同步字段和兼容规则 | `Multiplayer/Sync_Protocol.md` |
| 运动表现快照设计 | `Multiplayer/Sync_Protocol.md` |
| 专用服务器启动、Headless 构建、监听和部署 | `Multiplayer/Dedicated_Server.md` |
| 场景列表、场景职责、加载链路和表现模式切换 | `Scene/Scene_Guide.md` |
| Unity 版本、Packages、Addressables、Player Settings 和 Prefab | `Project/Project_Guide.md` |
| Mirror 参数、玩家 Prefab、联机验证和构建产物 | `Project/Mirror_KCP_Config.md` |
| 路由、租约、维护周期、文档分类 | `Workflow/Workflow_Guide.md`、`Workflow/Concurrency_Guide.md` |

跨功能域任务同时读取所有受影响入口。

## 功能域导航

| 功能域 | Router |
|---|---|
| `Character` | `Character\Router.md` |
| `Multiplayer` | `Multiplayer\Router.md` |
| `Scene` | `Scene\Router.md` |
| `Project` | `Project\Router.md` |
| `Workflow` | `Workflow\Router.md` |

## 开始步骤

1. 通过本文档确定功能域。
2. 阅读 `Workflow/Concurrency_Guide.md`。
3. 轻量只读定位完成后，在详细分析、子代理、修改或状态变更前，使用 `Workflow/Scripts/WorkingAgent.ps1` 申请精确租约。
4. 读取被路由的权威文档和完成任务所需的实际代码或资源。
5. 成功、失败或取消时释放租约。

## 项目验证

`ProjectValidationMode=None`。工作流不自动推断项目级构建、启动、测试和部署命令。任务中实际完成的验证作为工程证据。

## 文档索引

| 文档 ID | 路径 | 职责 | 状态 |
|---|---|---|---|
| `PROJ-OVERVIEW` | `Project_Overview.md` | 项目边界、当前架构和联机链路 | Active |
| `BUS-CHARACTER` | `Character/Router.md` | Character 域路由 | Active |
| `CHARACTER-GUIDE` | `Character/Character_Guide.md` | 输入、移动、状态机、动画和本地相机 | Active |
| `BUS-MULTIPLAYER` | `Multiplayer/Router.md` | Multiplayer 域路由 | Active |
| `MULTIPLAYER-GUIDE` | `Multiplayer/Multiplayer_Guide.md` | Mirror 网络运行时组件与连接管理 | Active |
| `SYNC-PROTOCOL` | `Multiplayer/Sync_Protocol.md` | 协议边界、同步字段和兼容规则 | Active |
| `DEDICATED-SERVER` | `Multiplayer/Dedicated_Server.md` | 专用服务器运行与构建事实 | Active |
| `BUS-SCENE` | `Scene/Router.md` | Scene 域路由 | Active |
| `SCENE-GUIDE` | `Scene/Scene_Guide.md` | 场景列表、职责、加载链路和表现模式 | Active |
| `BUS-PROJECT` | `Project/Router.md` | Project 域路由 | Active |
| `PROJECT-GUIDE` | `Project/Project_Guide.md` | Unity 工程配置、依赖和资产布局 | Active |
| `UNITY-NETCFG` | `Project/Mirror_KCP_Config.md` | Mirror 与 KCP 工程配置及验证方式 | Active |
| `WF-GUIDE` | `Workflow/Workflow_Guide.md` | 路由与维护规则 | Active |
| `WF-CONCURRENCY` | `Workflow/Concurrency_Guide.md` | WorkingAgent 租约规则 | Active |
| `WF-CONFIG-METHOD` | `Workflow_Configuration_Guide.md` | 工作流配置通用方法 | Active |

## 维护

只有成功且实质影响业务实现的任务才增加对应功能域的维护计数。只读分析、租约操作和纯文档维护不计数。计数达到 `5/5` 时，根据实际项目状态复核权威文档并重置计数。
