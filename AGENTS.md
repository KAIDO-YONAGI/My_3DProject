# 项目指令

每次读取到了就输出已读取AGENTS.md

## 语言

- 始终使用**中文**回答用户的问题和进行所有交流。
- 代码中的中文注释是有意为之的，**不得将其视为乱码而删除、修改或替换**。
- 修改后遇到注释等乱码则暂停工作，优先进行恢复
- 生成或修改代码时，保留原有的中文注释不变。

## 权威文档入口

- 开始项目任务时，先读取 `Y_MultipleAgentWorkflow/Router.md`，再按任务触发词进入对应文档。
- `Y_MultipleAgentWorkflow/` 是当前项目的权威文档库；实际代码、资源、运行结果和可复现验证高于文档。
- `docs/` 目录存放计划、草案和教学文档；作为权威事实时以 `Y_MultipleAgentWorkflow/` 为准。
- 仅进行轻量只读定位时可以先不申请租约；详细分析、调用子代理、修改文件或执行会改变状态的工具前，必须遵循 `Y_MultipleAgentWorkflow/Workflow/Concurrency_Guide.md` 申请精确租约。
- 权威文档统一使用中文；源码标识符、协议字面量、命令和路径保持原样。

## 代码修改限制

- **不要主动修改代码仓库中的任何代码文件**，除非用户明确要求你这样做。
- 当用户提出问题或请求分析时，只进行分析和回答，不要触碰代码。
- 如果用户明确要求修改代码，确认后再进行操作。

## 项目结构

本项目是物理派对游戏：Unity 客户端 + Mirror 专用服务器，按 `docs/plan/00-改造计划总览.md` 的八个阶段执行。

- 联机脚本: `Assets/Core/Scripts/Networking/`
- 单机框架: `Assets/Core/My_FrameWork/`
- 网络玩家 Prefab: `Assets/Core/Prefabs/NetworkPlayer.prefab`
- 角色 Prefab: `Assets/Core/Prefabs/CharactersForLocal/`、`Assets/Core/Prefabs/CharactersForSync/`
- 场景: `Assets/Core/Scenes/InitialScene.unity`（初始）、`PersistentScene.unity`（常驻）、`MultiplayerSampleScene.unity`（玩法）
- 第三方插件: `Assets/Plugins/`（含 `Mirror/`）
- 构建产物: `D:/Unity/Releases/3D_MultiplayerGame/`

## 约定

- Mirror 配置与构建的事实来源是 `Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md`。
- 涉及多个知识域的任务，必须同时读取并维护所有受影响知识域的权威文档。
