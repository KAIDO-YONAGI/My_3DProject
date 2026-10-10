# Protocol 知识域路由

文档 ID：`BUS-PROTOCOL`
状态：`Active`
维护计数：`1/5`
最后更新：`2026-10-10`

| 任务 | 必读文档 |
|---|---|
| Mirror 当前消息、序列化和同步边界 | `Protocol_Guide.md` |
| 玩家同步方向和 NetworkTransform | `../Networking/Networking_Guide.md` |
| 运动状态枚举、方向、权重、快照时间轴 | `Protocol_Guide.md` 的 Proposal；当前暂缓实现 |

## 主要证据路径

- `Assets/Core/Scripts/Networking/`
- `Assets/Core/Prefabs/NetworkPlayer.prefab`
- `Assets/Core/Scripts/Character/Animation/CharacterMotion.cs`
- `Assets/Core/Scripts/Character/Movement/States/CharacterMovementStateMachine.cs`
- `Assets/Plugins/Mirror/`

## 并发资源

- `workflow:Protocol`
- `path:Assets/Core/Scripts/Networking`
- `path:Assets/Core/Prefabs/CharactersForSync`
