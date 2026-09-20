# Client 知识域路由

文档 ID：`BUS-CLIENT`
状态：`Active`
维护计数：`0/5`
最后更新：`2026-09-20`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| 玩家进入、离开、远端角色生成、位置更新 | `Client_Guide.md` |
| `SyncCharacter`、`PlayerManager`、`ClientMessageHandler` | `Client_Guide.md` |
| UDP 生命周期、异步队列、错误处理 | `../Networking/Networking_Guide.md` |
| 消息字段、序列化、双端兼容性 | `../Protocol/Protocol_Guide.md` |
| 场景和 Prefab 绑定 | `../UnityRuntime/UnityRuntime_Guide.md` |

## 主要证据路径

- `Assets/Core/Scripts/Client/`
- `Assets/Core/Scripts/Client/Net/`

## 并发资源

- `workflow:Client`
- `path:Assets/Core/Scripts/Client`
- 与协议相关时同时申请 `workflow:Protocol`、`path:LocalServer/Scripts/ServerProtocol.cs`
- 与场景或 Prefab 绑定相关时同时申请 `workflow:UnityRuntime`

## 能力边界

当前权威范围是客户端生命周期、玩家管理和消息落地。网络传输细节归 `Networking`，线协议归 `Protocol`，Unity 序列化绑定归 `UnityRuntime`。
