# Server 知识域路由

文档 ID：`BUS-SERVER`
状态：`Retired`
维护计数：`0/5`
最后更新：`2026-09-29`

> **本知识域描述的独立 `LocalServer` 已于 2026-09-29 退役删除**（提交 `5c2b019`，tag `v0.2-selfbuilt-net`）。服务端形态切换为 Unity Dedicated Server Build（`docs/plan/07` 落地），届时重建本域。

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 历史 LocalServer（接收循环、注册表、广播） | `Server_Guide.md`（Retired，仅历史参考） |
| Mirror NetworkManager 服务器生命周期 | 尚无权威文档；阶段七落地时重建本域 |
| 历史文本消息格式 | `../Protocol/Protocol_Guide.md`（Retired） |

## 历史证据路径（仅 git 历史）

- `git show v0.2-selfbuilt-net^:LocalServer/LocalServer.csproj`
- `git show v0.2-selfbuilt-net^:LocalServer/Scripts/`

## 并发资源

- `workflow:Server`
- 新服务端代码落地后，用 `UpdateScope` 扩展或修改本节路径

## 能力边界

本域历史上描述独立控制台服务端。重建后描述 Mirror 服务器权威模拟、Server Build 与玩家注册；线协议格式归 `Protocol`，传输策略归 `Networking`。
