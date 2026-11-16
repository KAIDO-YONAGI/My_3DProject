# Architecture 开发记录

## 维护规则

按项目约定记录带日期的任务事实、验证证据与维护计数，现行结论同步到对应 Guide。了解现状优先读取 Guide；需要任务历史时，只定位并读取最近一天或两天的相关条目，禁止整份日志载入上下文。只有任务需要追溯历史时才按日期、标题或关键词定向读取更早条目。读取范围不改变存储保留，删除具体历史条目须经用户明确确认。纯文档维护不增加业务计数。

跨子域的结构变更记录在此；业务证据记录到对应子域的 `DeveloperLog.md`。

## 2026-11-16：建立 Architecture 知识域

- 变动性质：新增顶层知识域与 `Composition` 子域，同步根 Router 的功能域路由与功能域导航、`Workflow/WorkflowInstance.json` 的分类，以及 `RULES.md` 的代码修改规则。
- 文件：`Architecture/Router.md`、`Architecture/DeveloperLog.md`、`Architecture/Composition/Composition_Guide.md`、`Architecture/Composition/Router.md`、`Architecture/Composition/DeveloperLog.md`。
- 依据：实现事实来自 `Assets/Core/Scripts/Common/YSingleton.cs` 与各组件校验点；规则来源为 `RULES.md`。
- 维护计数：`0/5 -> 1/5`。
