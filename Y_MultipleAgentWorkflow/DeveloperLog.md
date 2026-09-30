# 根工作流开发记录

跨知识域结构变更和根索引维护记录在此；业务证据记录到对应知识域的 `DeveloperLog.md`。

## 2026-09-20：建立中文权威文档库

- 创建根路由、工作流指南、WorkingAgent 注册表和知识域。
- `ProjectValidationMode=None`，未推断项目构建、启动或测试命令。

## 2026-09-29：知识域收敛

- 项目转型为物理派对游戏，`Client`、`Networking`、`Server`、`Protocol` 四个知识域从权威文档库移除，旧网络栈实现通过 git tag `v0.2-selfbuilt-net` 查阅。
- 权威文档收敛为 `Project_Overview.md`、`UnityRuntime/`（场景资产 + Mirror 配置）与 `Workflow/`。
- Mirror 96.11.2 安装进工程，联机链路验证通过，配置记录在 `UnityRuntime/Mirror_KCP_Config.md`。
- 教学与联调类文档位于 `docs/`。

## 2026-09-29：重建权威文档

- 根据当前代码、场景、Prefab、构建结果和运行验证重建活动文档，未读取、迁移或修改 `docs/`。
- 根路由、项目总览和 UnityRuntime 文档统一指向 `PersistentScene`、`MultiplayerSampleScene` 与当前角色 Prefab。
- 将固定 Tick、`InputFrame`、快照和预测明确标记为后续计划，避免把计划描述为已实现事实。
- 工作流实例知识域清单收敛为 `Workflow` 与 `UnityRuntime`，`ProjectValidationMode=None` 保持不变。

## 2026-09-29：按代码职责恢复模块化知识域

- 修正上一条“仅保留 `Workflow` 与 `UnityRuntime`”的过度收敛。
- 权威知识域恢复为 `Workflow`、`Client`、`Networking`、`Server`、`Protocol`、`UnityRuntime`。
- `Client` 对应 Movement、玩家输入、动画和相机；`Networking` 对应 Mirror 连接与玩家网络组件。
- `Server` 和 `Protocol` 当前没有独立源码目录，分别由 Mirror Headless/NetworkManager 与 Mirror 内置消息/序列化边界承载；文档明确记录这一事实，不虚构实现。
