# WorkingAgent 租约注册表

本目录保存名为 `<AgentName>_<Hash>.txt` 的临时 JSON 租约标记。只有本 README 和 `.gitignore` 进入版本控制。所有注册表操作必须通过 `..\Workflow\Scripts\WorkingAgent.ps1` 执行。

超过 15 分钟没有心跳的标记只属于疑似过期，不得自动删除或接管。

Codex 原生任务消息可以用于提示租约重叠或范围已经释放，但不能替代本注册表。编辑前必须核对租约与实际工作区；宣布完成前必须先释放租约。
