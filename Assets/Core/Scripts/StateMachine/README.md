# 分层状态机框架

同步分层状态机。纯 C#、不依赖 `UnityEngine`，程序集 `Core.FrameWork.StateMachine`。

- `HierarchicalState<TContext>`：状态节点。子类重写 `InitialChild`、`ResolveTransition`、`OnEnter`、`OnExit`、`OnUpdate`。
- `HierarchicalStateMachine<TContext>`：驱动器。只需 `Start(context)` 一次，之后每帧 `Update(context)`。
- 转换按**最近公共祖先**执行：退出旧分支自下而上，进入新分支自上而下，**当帧完成**，不占额外帧。
- 刻意不做：异步过渡、反射装配、运行时改结构、正交（并行）状态。

本项目用它实现了角色运动状态机（`Assets/Core/Scripts/Character/Movement/States/`）。下面以假想的「滑铲」为例说明新增状态的写法。

## 一句话规矩

> **叶子状态只写「我是谁」；「什么时候换成谁」写在父节点上。**

## 一、写叶子状态

```csharp
/// <summary>按住滑铲键且贴地时的地面状态。</summary>
internal class SlideCharacterMovementState : CharacterMovementLeafState
{
    public SlideCharacterMovementState(GroundedStateNode parent) : base(parent)
    {
    }

    public override CharacterMovementStateId Id => CharacterMovementStateId.Slide;
}
```

只有 `Id`，不写任何判断。判断一旦写进叶子，就退回了「每个状态各写各的」，父节点管不住它。

## 二、在父节点登记（3 处）

改 `Assets/Core/Scripts/Character/Movement/States/CharacterMovementStates.cs` 的 `GroundedStateNode`：

```csharp
// 1. 构造函数里创建
Slide = new SlideCharacterMovementState(this);

// 2. 暴露属性
public SlideCharacterMovementState Slide { get; }

// 3. 在 ResolveGroundedTarget 里加判断，越靠上越优先
if (context.SlideHeld)
{
    return Slide;
}
```

父节点里现成的这段负责「目标即当前状态时不重复切换」，不要改它：

```csharp
CharacterMovementLeafState target = ResolveGroundedTarget(context);
return ReferenceEquals(ActiveChild, target) ? null : target;
```

挂在哪个父节点，决定了新状态自动继承哪些规则。**判断方法：这个状态只在地面出现，还是只在空中出现？**

## 三、新数据怎么进来

```text
输入设备 → CharacterInputReader → CharacterInput → CharacterMotor → CharacterMovementStateContext → 状态机
```

三处要同步改：

1. `Assets/Core/Scripts/Character/Input/CharacterInput.cs`：加字段
2. `.../Movement/States/CharacterMovementStateMachine.cs` 的 `CharacterMovementStateContext`：加字段。**它有两个构造函数**，6 参数的兼容版本要一并改并传默认值
3. `.../Movement/CharacterMotor.cs` 的 `Tick`：把 `input.XxxHeld` 填进上下文

## 四、要联网同步的状态，额外改 5 处

| # | 位置 | 改法 |
|---|---|---|
| 1 | `CharacterMovementStateId` 枚举 | **追加在末尾**（如 `Slide = 5`）。数值就是网络包内容，插在中间会让旧值整体错位 |
| 2 | `Assets/Core/Scripts/Networking/NetworkCharacterMovementSync.cs` 的上限校验 | `> (byte)...Falling` 改成新的最大值。漏改会让服务端把新状态当非法包丢弃，表现为「自己看得到、别人看不到」 |
| 3 | `Assets/Core/Scripts/Character/Animation/CharacterMotion.cs` 的 `IsGrounded` | 加 `\|\| State == 新状态`，否则动画把它误判成空中 |
| 4 | `Y_MultipleAgentWorkflow/Multiplayer/Sync_Protocol.md` 的状态表 | 加一行 |
| 5 | 专用服务器 | 重新构建，且与客户端同版本 |

## 五、三个常见错误

**1. 把判断写进叶子。** 规则一律写在父节点。

**2. 父节点里直接 `return 目标`。** 会导致每帧退出再进入同一状态，必须先比较：

```csharp
return ReferenceEquals(ActiveChild, Slide) ? null : Slide;
```

**3. 同一个条件在两处写。** 如果发现自己在第二个地方写 `if (context.JumpStarted)`，说明这条规则该上提到父节点，而不是再抄一遍。

## 六、写完自检

- 叶子类里只有 `Id`？
- 父节点的构造函数、属性、判断三处都改了？
- 判断有优先级顺序，默认分支在最后？
- 新数据在 `Input`、`Context`（含 6 参数兼容构造函数）、`Motor` 三处都通了？
- 要联网的话，第四节那 5 处都改了？
- `Window > General > Test Runner` → `EditMode` → `Run All` 通过？
