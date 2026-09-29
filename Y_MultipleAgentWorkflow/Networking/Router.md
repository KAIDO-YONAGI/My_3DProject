# Networking 知识域路由

文档 ID：`BUS-NETWORKING`
状态：`Retired`
维护计数：`0/5`
最后更新：`2026-09-29`

> **本知识域描述的自研 UDP 传输栈已于 2026-09-29 退役**（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。路由保留用于检索 git 历史；当前项目没有可运行的网络链路，新网络层按 `docs/plan/` 八阶段计划在阶段七重建。

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 自研 UDP 历史（UdpConnection、NetManager、主线程队列） | `Networking_Guide.md`（Retired，仅历史参考） |
| Mirror 传输、KCP 配置 | 尚无权威文档；阶段七落地时重建本域 |
| 历史文本消息格式 | `../Protocol/Protocol_Guide.md`（Retired） |

## 历史证据路径（仅 git 历史）

- `git show v0.2-selfbuilt-net^:Assets/Core/Scripts/Client/Net/NetManager.cs`
- `git show v0.2-selfbuilt-net^:Assets/Core/Scripts/Client/Net/UdpConnection.cs`
- `git show v0.2-selfbuilt-net^:LocalServer/Scripts/ServerCore.cs`

## 并发资源

- `workflow:Networking`
- 新网络代码落地后，用 `UpdateScope` 扩展或修改本节路径

## 能力边界

本域历史上描述自研传输和并发模型。重建后描述 Mirror 传输层接入与自研快照/插值/预测的传输边界；消息字段定义仍归 `Protocol`。
