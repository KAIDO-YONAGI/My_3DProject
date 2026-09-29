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
