# Protocol 知识域路由

文档 ID：`BUS-PROTOCOL`
状态：`Retired`
维护计数：`0/5`
最后更新：`2026-09-29`

> **本知识域描述的历史文本协议（`Enter/Move/Leave/Attack`）已随自研网络栈于 2026-09-29 退役删除**（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。新协议按 `docs/plan/` 计划在阶段七设计（`InputFrame` 输入上传 + 自定义快照 + 可靠事件），届时重开本域。

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 历史文本协议（`Enter`、`Move`、`Leave`、`Attack`、分隔符格式） | `Protocol_Guide.md`（Retired，仅历史参考） |
| 新协议（`InputFrame`、快照、序列化） | 尚无权威文档；阶段七落地时重建本域 |

## 历史证据路径（仅 git 历史）

- `git show v0.2-selfbuilt-net^:Assets/Core/Scripts/Client/Net/ClientProtocol.cs`
- `git show v0.2-selfbuilt-net^:LocalServer/Scripts/ServerProtocol.cs`

## 并发资源

- `workflow:Protocol`
- 新协议代码落地后，用 `UpdateScope` 扩展或修改本节路径

## 能力边界

新协议落地后仍沿用原规则：协议修改必须同步评估客户端、服务端和网络收发，不得只修改单端定义后宣称协议完成。
