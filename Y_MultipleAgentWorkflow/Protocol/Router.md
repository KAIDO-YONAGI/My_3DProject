# Protocol 知识域路由

文档 ID：`BUS-PROTOCOL`
状态：`Active`
维护计数：`0/5`
最后更新：`2026-09-20`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| `Enter`、`Move`、`Leave`、`Attack` | `Protocol_Guide.md` |
| 字段分隔、编码、序列化、解析 | `Protocol_Guide.md` |
| 客户端与服务端不对称、兼容性 | `Protocol_Guide.md` |

## 主要证据路径

- `Assets/Core/Scripts/Client/Net/ClientProtocol.cs`
- `LocalServer/Scripts/ServerProtocol.cs`
- `LocalServer/Scripts/ServerNetHandler.cs`

## 并发资源

- `workflow:Protocol`
- `path:Assets/Core/Scripts/Client/Net/ClientProtocol.cs`
- `path:LocalServer/Scripts/ServerProtocol.cs`
- `path:LocalServer/Scripts/ServerNetHandler.cs`

## 能力边界

协议修改必须同步评估客户端、服务端和网络收发。不得只修改单端定义后宣称协议完成。
