# 角色系统

文档 ID：`CHARACTER-GUIDE`
状态：`Active`
最后核验：`2026-10-10`

## 角色链路

```text
CharacterInputReader（PlayerControls.inputactions）
  → PlayerCharacterController
  → CharacterInput
  → CharacterMotor（内部维护 CharacterMovementStateMachine）
  → CharacterMotion
  → CharacterAnimator
  → CharacterBoneFollower
```

- `CharacterInputReader` 是唯一输入入口，按需创建并启用 `PlayerControls`。
- `PlayerCharacterController` 读取输入并组织移动和表现更新。
- `CharacterInput` 保存当前帧的移动、疾跑和跳跃输入。
- `CharacterMotor` 根据相机水平朝向计算世界移动方向，处理加减速、重力、跳跃和 `CharacterController.Move`。
- `CharacterMovementStateMachine` 接收 `CharacterMotor` 的运动后反馈，维护五个离散状态；`CharacterMotion.State` 记录当前状态。
- `CharacterMotion` 保存动画层使用的移动状态。
- `CharacterAnimator` 配置方向动画和 AnimatorOverrideController。
- `CharacterBoneFollower` 在动画更新后执行骨骼跟随。
- `ThirdPersonCamera` 保留序列化配置、`SetTarget` 和生命周期编排；普通 C# 对象 `CameraOrbitState`、`CameraCursorController`、`CameraCollisionResolver` 与 `CameraRotationMath` 分别负责轨道状态、光标状态、最终候选避让和旋转数学。

## 运动状态与动画

- `Movement/States/CharacterMovementStateMachine.cs` 集中定义状态枚举、反馈上下文、状态接口、共用基类和状态机；`CharacterMovementStates.cs` 集中定义五个独立状态类。状态转换统一经过 `Exit`、状态替换和 `Enter`。
- `Idle`、`Run`、`Sprint` 根据移动输入和冲刺意图选择地面状态；非跳跃的短暂离地（下坡、台阶造成的悬空帧）在 `CharacterMovementSettings.airborneTolerance`（默认 `0.3` 秒）窗口内仍按地面状态解析，超出窗口后才根据竖直速度进入 `Rising` 或 `Falling`，避免误播跳跃动画。主动跳跃当帧清零计时，立即进入 `Rising`。`Rising` 在竖直速度不再为正时进入 `Falling`，移动后接地时恢复对应地面状态。
- `CharacterMotor` 在移动前读取 `CharacterController.isGrounded` 决定跳跃许可，依次处理跳跃、目标水平速度、加减速与空中控制和重力，每帧只调用一次 `CharacterController.Move`，再清除顶碰上升速度、处理接地拉力、维护非跳跃离地计时并更新代码状态。
- 运动链路按帧长积分，起跳在按下当帧生效，动画权重直接取输入幅度。松开输入后物理速度按配置的减速度衰减。
- Animator 保留 Grounded 内 Idle/Run/Sprint 与方向混合；`Rising` 与 `Falling` 使用 Jump 素材，落地时重置权重，缺失素材回退到 Idle 或前进跑步片段。
- `CharacterAnimator.Initialize` 在绑定素材时缓存跑步组与冲刺组的可用性，`Apply` 读取缓存并写入动画参数。角色模型重绑时创建动画驱动并重新计算缓存。

`CharacterMotion` 是 `CharacterMotor` 输出给 `CharacterAnimator` 的单帧表现快照，字段为离散状态 `State`、角色自身坐标系的移动方向 `LocalDirection` 和移动权重 `LocomotionWeight01`。`IsGrounded` 与 `WantsSprint` 由 `State` 派生。网络传输该快照的字段约定见 `../Multiplayer/Sync_Protocol.md`。

## 相机与移动方向

本地网络角色的 `PlayerCharacterController.inputSpace` 指向本地角色 Prefab 中的 Camera。`CharacterMotor` 取该 Transform 的水平 forward 和 right，把输入转换成世界方向，相机偏航同步改变前进方向。

`ThirdPersonCamera` 只在本地拥有者一侧工作。玩法场景中的 `Camera` 对象保持停用，联机表现模式收拢全部非本地网络相机和多余的 `AudioListener`。

相机保留在角色 Prefab 内，不改变网络层级。`CameraOrbitState` 独立缓存镜头世界旋转，角色父节点转向后不读取被父级带动的 `transform.rotation` 作为插值起点。角色朝向跟随输入轨道的水平 yaw。镜头旋转使用 `1 - exp(-smoothSpeed * deltaTime)` 的指数插值系数，位置使用显式帧长的 `Vector3.SmoothDamp` 平滑焦点到镜头的相对偏移。

避让顺序为「期望轨道偏移 → 最低高度约束 → 相对偏移平滑 → 最终候选方向 SphereCast → 写回裁剪偏移」。最低高度只在期望偏移上约束，平滑结果不做二次夹取，候选向量同时是 SphereCast 的检测路径。球形检测忽略目标及其所有子碰撞体和 Trigger；命中数组容量为 128，填满时重查，极端密集情况使用 `SphereCastAll` 兜底。`RaycastHit.distance` 已是球心行进距离，只保留 `0.01m` 接触余量。受阻时防穿墙优先于 `minDistance` 与 `minHeightY`；裁剪不改写用户期望 `distance`，同时清空受阻的平滑速度，障碍消失后平滑退回期望轨道。未按 Alt 时每帧夹取期望缩放距离，滚轮事件只改变期望值。

起始检测球与障碍重叠时，仅在焦点位于障碍包围盒外、沿分离平面外法线单调远离且最终球体完全退出时，忽略该零距离命中；其他障碍裁短路径后重新核验退出条件。焦点位于包围盒内部或无法证明安全退出时保守阻挡。检测只约束焦点至候选球心的路径，不覆盖相机整个近裁剪面。

## 高度与物理

两个 `NetworkStartPosition` 位于地形附近。Mirror 生成网络根后，`CharacterMotor` 持续计算重力并调用 `CharacterController.Move`。接地状态来自 `CharacterController.isGrounded` 和碰撞标志。

玩家移动链路不调用位置传送，也不做运行时高度钳制。出生位置、`CharacterController` 尺寸、地形高度和重力共同决定落地结果。

## 输入归属

`PlayerCharacterController` 是普通 `MonoBehaviour`。对象存在 `NetworkIdentity` 且客户端已经连接时，仅 `isLocalPlayer=true` 的实例读取输入。单机角色没有 `NetworkIdentity`，使用相同的移动代码。

`Awake` 调用 `EnsureRuntimeState`，补齐组件引用、配置和运动驱动。网络身份组件在首次初始化时查询一次，`networkIdentityInitialized` 同时缓存「有身份」和「无身份」两种结果。身份组件在初始化前配置，缓存的是组件引用，`isLocalPlayer` 等归属值实时读取。

视觉模型的延迟挂载和替换通过 `RebindAnimator` 绑定动画驱动，运动驱动沿用根对象中的实例。`Update` 检查已缓存的运动依赖后依次读取输入、执行移动、应用动画；远程动画更新读取已绑定的驱动。模型解绑时动画引用保持为空，下一次装配由 `RebindAnimator` 接入。

## 输入系统与光标规则

项目输入统一走 Input System 包，Player Settings 的 Active Input Handling 为 `Input System Package (New)`。

- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，包含单张动作表 `Player`：`Move`（WASD 与方向键两个 2DVector 复合，外加手柄左摇杆）、`Look`（`<Mouse>/delta`）、`Zoom`（`<Mouse>/scroll/y`）、`Sprint`、`Jump`、`ReleaseCursor`、`FreeCursor`、`LockCursor`。
- 包装类 `Assets/Core/Input/PlayerControls.cs` 由 `.inputactions` 的导入器按 `.meta` 的 `generateWrapperCode` 生成，JSON 内嵌在代码里，不需要在场景或 Prefab 里拖引用。
- `CharacterInputReader` 负责旧轴到 Input System 的量纲换算，使 `ThirdPersonCamera` 的 `sensitivity`、`scrollSpeed` 等既有数值保持原手感：`Mouse X/Y` 轴灵敏度 `0.1` 对应 `<Mouse>/delta * 0.1`，`Mouse ScrollWheel` 每格 `0.1` 对应 `<Mouse>/scroll/y * 0.1 / 120`，该常数按 Windows 每格 120 标定，跨平台需要重新标定。
- 输入实例按需创建：专用服务器分支和远程玩家分支在读取输入前返回，不创建 `PlayerControls`。角色控制器在 `OnDestroy` 释放实例；相机在停用或目标失效时 `Dispose`，恢复后按需重建。
- 键鼠默认映射：WASD 与方向键移动、左右 Shift 疾跑、空格跳跃、Escape 释放光标、鼠标左键重新捕获、Alt 自由光标。
- `ThirdPersonCamera` 的光标规则：`lockCursor` 为真时进入播放即锁定并隐藏光标；Escape 释放；需要锁定时左键点击重新捕获；按住 `Alt` 期间强制释放并显示光标，同时忽略鼠标转动与滚轮缩放，此时左键用于操作 UI。
- 松开 `Alt` 时按进入前的意图恢复：进入前已锁定且窗口有焦点才重新锁定，进入前已用 Escape 释放过则保持释放。窗口无焦点时不抢回鼠标。
- 锁定状态发生跳变的那一帧跳过鼠标增量，避免光标回中造成的镜头跳变。
- `CameraCursorController` 记录全局光标的当前相机持有者。未初始化或已经交出所有权的组件停用时不释放其他相机的光标；重新启用按 `lockCursor` 重新初始化，左键重新捕获丢弃当帧鼠标增量。外部解锁后，左键捕获核对实际锁定状态。目标失效时相机暂停并释放其输入、光标，可通过 `SetTarget` 恢复。
- `ThirdPersonCamera` 的角色朝向纠正仅在 `rotateTarget` 为真且存在移动输入时生效。`CameraRotationMath` 积分连续速度律 `dr/dt = -min(k*r, v)`，其中 `k=targetRotateSmooth` 默认 15/秒，`v=targetRotateSpeed` 默认 360 度/秒。剩余角差大于 `v/k` 时先匀速走到该阈值，余下帧长按指数收敛；一帧跨越阈值时分段积分，再用 `Quaternion.RotateTowards` 写入。零速率、零限速或零帧长不转向。
- `ThirdPersonCamera` 的可配置字段按运行期能否生效分组，结论写在 Header 上，例如「环绕（运行时可改）」，Tooltip 只描述字段作用；`maxDistance` 与 `lockCursor` 的生效时机见各自 Tooltip。新增字段沿用该约定。
- `EventSystem` 使用 `InputSystemUIInputModule`，其 `CursorLockBehavior` 为默认的 `OutsideScreen`，操作 UI 需要先按住 `Alt` 释放光标。
- `Assets/Plugins/Mirror/` 中的示例与部分组件使用旧输入 API，均不在构建场景引用内，运行这些示例场景会在运行时报错。

## 目录约定

- 角色脚本按功能归入 `Assets/Core/Scripts/Character/Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`。移动参数随运动代码，动画配置随动画代码。
- 相机位于 `Assets/Core/Scripts/Camera/Runtime/`。
- `AutoStartClient`、`NetworkCharacterManager`、`NetworkCharacterSync` 位于 `Assets/Core/Scripts/Networking/`。
- 第三方插件统一放 `Assets/Plugins/`：`Mirror/` 带 asmdef，`DynamicBone/` 无 asmdef，编译进 `Assembly-CSharp-firstpass`，被角色 Prefab 以 GUID 引用。
- 测试目录与运行时代码同构：`Assets/Core/Tests/PlayMode/Camera/` 对应程序集 `Core.Camera.PlayModeTests`，`Assets/Core/Tests/PlayMode/Networking/` 对应 `Core.Networking.PlayModeTests`，`Assets/Core/Tests/Editor/` 对应 `Core.EditorTests`。移动脚本必须连同 `.meta` 一起移动，保持 Prefab 与场景引用有效。

## 验证覆盖

`Core.EditorTests` 覆盖地面状态、起跳、最高点、下降、平台边缘掉落和落地。`CharacterUpdateLoopTests` 覆盖显式模型绑定、解绑后保留空引用、重绑时沿用运动驱动、动画素材可用性缓存、缺失素材回退与落地首帧权重重置。`Core.Camera.PlayModeTests` 与 `Core.Networking.PlayModeTests` 覆盖父子相机世界旋转、近墙裁剪、抬高后的路径、自身碰撞体下的避让、障碍移除恢复、起始重叠安全退出及双墙裁剪、光标交接、启停与外部解锁后捕获、目标销毁重绑，以及 30/60/120 FPS 的限速积分一致性和暂停后平滑恢复。两个本地角色 Prefab 保留 17 个序列化字段，`targetRotateSmooth=15` 与 `targetRotateSpeed=360`。

## 维护触发

修改 Character 目录、Camera 目录、输入资产、输入系统设置、角色输入、CharacterController、动画数据流、本地相机、朝向参数与 Tooltip 约定或光标规则时更新本文档。
