# Character 知识域路由

文档 ID：`BUS-CHARACTER`
状态：`Active`
维护计数：`4/5`
最后更新：`2026-10-11`

## 任务路由

| 任务 | 权威文档 |
|---|---|
| 客户端启动、输入、移动、动画、本地相机 | `Character_Guide.md` |
| 角色运动算法和输入数据流 | `Character_Guide.md`，并读取 `Assets/Core/Scripts/Character/` |
| Idle、Run、Sprint、Rising、Falling、状态机与状态内混合 | `Character_Guide.md`，并读取 `Assets/Core/Scripts/Character/Movement/States/` |
| 输入资产、动作表、相机朝向参数与光标规则 | `Character_Guide.md`，并读取 `Assets/Core/Input/` |
| 相机轨道状态、世界姿态、最终候选避让与转向积分 | `Character_Guide.md`，并读取 `Assets/Core/Scripts/Camera/Runtime/` 与相机回归测试 |
| 角色模型、动画素材与本地相机 Prefab | `Character_Guide.md`，并读取 `Assets/Core/Prefabs/CharactersForLocal/` |
| 嘴部下移、面部蒙皮、Humanoid Jaw 与 Avatar 映射 | `Character_Guide.md`、`../Project/Project_Guide.md`，并核对模型导入配置与 Local、Sync 两套 Prefab |
| Mirror 本地玩家归属与玩家网络行为 | `../Multiplayer/Multiplayer_Guide.md` |
| 场景加载与单机联机表现切换 | `../Scene/Scene_Guide.md` |
| 工程版本、包与 Build Settings | `../Project/Project_Guide.md` |

## 下级导航

| 子类 | Router |
|---|---|
| 无 | 无 |

## 主要证据路径

- `Assets/Core/Scripts/Character/`
- `Assets/Core/Scripts/Character/Control/`
- `Assets/Core/Scripts/Character/Movement/States/`
- `Assets/Core/Tests/Editor/CharacterMovementStateMachineTests.cs`
- `Assets/Core/Scripts/Camera/`
- `Assets/Core/Input/`
- `Assets/Core/Tests/PlayMode/Camera/`
- `Assets/Core/Prefabs/CharactersForLocal/`
- `Assets/Core/Prefabs/CharactersForSync/`
- `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`

## 并发资源

- `workflow:Character`
- `path:Assets/Core/Scripts/Character`
- `path:Assets/Core/Scripts/Camera`
- `path:Assets/Core/Input`
- `path:Assets/Plugins/DynamicBone`
- `path:Assets/Core/Prefabs/CharactersForLocal`
- `path:Assets/Core/Prefabs/CharactersForSync`
- `path:Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`

## 能力边界

维护角色输入、运动状态机、动画混合、本地相机与骨骼表现。角色运动的网络同步设计见 `../Multiplayer/Sync_Protocol.md`。
