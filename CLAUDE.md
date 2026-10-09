# CLAUDE.md

## 必须先读

- 先读取并遵守根目录 `AGENTS.md`。
- 再读取 `Y_MultipleAgentWorkflow/Router.md`，按任务进入对应文档。
- `docs/` 目录存放计划、草案和教学文档；作为权威事实时以 `Y_MultipleAgentWorkflow/` 为准。
- 详细分析、子代理、文件修改或状态变更操作前，按 `Y_MultipleAgentWorkflow/Workflow/Concurrency_Guide.md` 申请精确租约。

## 权威性

- 用户最新明确要求优先。
- 实际代码、资源、运行结果和可复现验证高于文档。
- `Y_MultipleAgentWorkflow/` 中被根路由索引的文档是项目知识的维护入口。

## 约定

- 始终使用中文交流和维护权威文档。
- 未经用户明确要求，不修改代码文件。
- 保留所有原有中文注释；发现乱码时立即停止并优先恢复。
- Mirror 配置与联机链路的事实来源是 `Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md`。
- `DeveloperLog.md` 一律**最新记录在前**：新增条目插入文件开头，不追加到文件末尾；同一天的条目按实际完成顺序倒序，最后完成的写在最前面。
