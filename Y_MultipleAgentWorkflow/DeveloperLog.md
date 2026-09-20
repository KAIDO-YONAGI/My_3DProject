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
