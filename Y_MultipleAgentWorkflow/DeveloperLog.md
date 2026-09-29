# 根工作流开发记录

跨知识域结构变更和根索引维护记录在此；业务证据记录到对应知识域的 `DeveloperLog.md`。

## 2026-09-20：工作流初始化

- 创建根路由、工作流指南、WorkingAgent 注册表和六个顶层知识域。
- 初始化时没有修改模型入口文件，`EntryMode=None`。

## 2026-09-20：建立中文权威文档库

- 用户确认知识域为 `Workflow`、`Client`、`Networking`、`Server`、`Protocol`、`UnityRuntime`。
- 基于实际代码、Unity YAML 和项目配置建立项目总览及六个知识域 Guide。
- 修改 `AGENTS.md` 和 `CLAUDE.md`，保留原规则并引导到权威路由。
- 原有 `docs/` 和其他旧文档保持原样，未读取、未迁移、未索引为权威来源。
- `ProjectValidationMode=None`，未推断项目构建、启动或测试命令。
- 本次属于纯文档维护，所有知识域维护计数保持 `0/5`。

## 2026-09-29：自研网络栈整体退役，网络层切换至 Mirror

- 用户确认执行完整退役（删除迁移）：项目转型为物理派对游戏，按 `docs/plan/00-改造计划总览.md` 八阶段计划执行。
- 归档：tag `v0.2-selfbuilt-net` 指向退役提交 `5c2b019`（TCP 分帧版本在 `4305eeb` 之前历史可查）。
- 删除：客户端自研网络栈（`Assets/Core/Scripts/Client/` 全部）、独立服务端 `LocalServer/`、孤立损坏资产 `ConnectResultChannel.asset`。
- 清理：`MultiplayerSampleScene` 移除 `NetManager`/`PlayerPositionManager` 节点；两个角色 Prefab 剥离 `SyncCharacter` 引用；`BoolEventChannelSO` 保留备用。
- 文档：`Project_Overview.md` 重写为派对游戏目标架构与三代网络栈演进史；`Client`、`Networking`、`Server`、`Protocol` 四域 Guide 与 Router 状态改为 `Retired`（历史参考）；`UnityRuntime_Guide.md` 更新为退役后实际状态；根路由表与文档索引同步。
- 核验发现磁盘上 `Assets/Mirror` 不存在（gitignore 声明忽略该路径），阶段一接入时需重新导入 Mirror。
- 各域 DeveloperLog 已记录业务证据；UnityRuntime 域维护计数 `1/5`，其余退役域计数重置 `0/5`。
