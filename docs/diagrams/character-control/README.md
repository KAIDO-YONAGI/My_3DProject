# 角色控制器图表

本目录的图讲语义，代码标识符集中在本文件。图由 PlantUML 源码渲染，源码与图片一一对应。

## 图清单

| 图 | PlantUML 源码 | 图片 | 内容 |
|---|---|---|---|
| 角色运动状态图 | `01-state-machine.puml` | `png/CharacterMovementStates.png` | 五个状态、转换条件、状态编号 |
| 角色一帧运动的流程图 | `02-frame-flow.puml` | `png/CharacterMotionFrame.png` | 一帧内从输入到动画的完整处理顺序 |
| 状态判定顺序图 | `03-state-decision.puml` | `png/CharacterStateDecision.png` | 每帧判定的分支顺序 |
| 角色状态与网络边界图 | `04-sync-boundary.puml` | `png/CharacterStateSync.png` | 状态在哪个节点产生、哪个节点消费 |

## 状态集合

`CharacterMovementStateId` 是 byte 枚举，位于
`Assets/Core/Scripts/Character/Movement/States/CharacterMovementStateMachine.cs`。

| 状态 | 枚举名 | 编号 |
|---|---|---|
| 待机 | `Idle` | 0 |
| 跑动 | `Run` | 1 |
| 冲刺 | `Sprint` | 2 |
| 上升 | `Rising` | 3 |
| 下落 | `Falling` | 4 |

## 判定输入

状态机每帧接收一个 `CharacterMovementStateContext`，六个只读字段：

| 字段 | 含义 |
|---|---|
| `GroundedBeforeMove` | 移动前的贴地结果，取自 `CharacterController.isGrounded` |
| `GroundedAfterMove` | 移动后的贴地结果，取自碰撞标志的 `Below` 位 |
| `HasMoveInput` | 移动输入长度平方大于 `0.0001` |
| `SprintHeld` | 冲刺键按住 |
| `JumpStarted` | 移动前贴地且本帧按下跳跃 |
| `VerticalVelocity` | 施加重力后、进入状态机前的竖直速度 |

`GroundedBeforeMove` 服务于起跳许可与贴地拉力，不参与状态转换判定。

## 转换条件

`CharacterMovementStateMachine.Update` 先调用当前状态的 `Update` 取得下一个状态，
再比较编号：编号相同则直接返回，编号变化则依次执行 `Exit`、替换状态对象、`Enter`。
五个状态的 `Exit` 与 `Enter` 都是空实现，位于
`Assets/Core/Scripts/Character/Movement/States/CharacterMovementStates.cs`。

三个地面状态 `Idle`、`Run`、`Sprint` 的判定逻辑相同：

| 条件 | 下一个状态 |
|---|---|
| `JumpStarted` | `Rising` |
| 非 `GroundedAfterMove` 且 `VerticalVelocity > 0` | `Rising` |
| 非 `GroundedAfterMove` 且 `VerticalVelocity <= 0` | `Falling` |
| `GroundedAfterMove` 且 非 `HasMoveInput` | `Idle` |
| `GroundedAfterMove` 且 `HasMoveInput` 且 非 `SprintHeld` | `Run` |
| `GroundedAfterMove` 且 `HasMoveInput` 且 `SprintHeld` | `Sprint` |

`Rising`：

| 条件 | 下一个状态 |
|---|---|
| `GroundedAfterMove` | 进入落地判定 |
| 非 `GroundedAfterMove` 且 `VerticalVelocity > 0` | `Rising` |
| 非 `GroundedAfterMove` 且 `VerticalVelocity <= 0` | `Falling` |

`Falling`：

| 条件 | 下一个状态 |
|---|---|
| `GroundedAfterMove` | 进入落地判定 |
| 非 `GroundedAfterMove` 且 `VerticalVelocity > 0` | `Rising` |
| 非 `GroundedAfterMove` 且 `VerticalVelocity <= 0` | `Falling` |

落地判定由 `CharacterMovementStateBase.ResolveGroundedState` 实现，被上述三处调用，
按 `GroundedAfterMove`、`HasMoveInput`、`SprintHeld` 的顺序依次判断。

空中的两个状态不读取 `JumpStarted`，空中按跳跃保持在原状态。

## 运动快照

`CharacterMotion` 位于 `Assets/Core/Scripts/Character/Animation/CharacterMotion.cs`，
是 `CharacterMotor` 每帧交给动画系统的只读结构。

| 字段 | 类型 | 含义 |
|---|---|---|
| `State` | `CharacterMovementStateId` | 本帧离散状态 |
| `LocalDirection` | `Vector2` | 角色自身坐标系中的移动方向，归一化 |
| `LocomotionWeight01` | `float` | 移动权重，取自输入幅度并截断到 `[0,1]` |

`IsGrounded` 与 `WantsSprint` 由 `State` 派生。
运动快照由
`Assets/Core/Scripts/Character/Movement/CharacterMotor.cs` 的 `Tick` 产出，
调用点位于 `CharacterController.Move` 之后。

线格式与传输约定记录在 `Y_MultipleAgentWorkflow/Multiplayer/Sync_Protocol.md`。

## 渲染

```powershell
python "C:\Users\12248\.agents\skills\plantuml-class-diagram\scripts\render_puml.py" `
  "docs/diagrams/character-control" -o "docs/diagrams/character-control/png"
```

渲染脚本读取同技能目录的 `config.json`，其中已配置 plantuml.jar 路径、
中文字体目录与图片尺寸上限。
