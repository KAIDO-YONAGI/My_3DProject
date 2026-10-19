# 角色系统

文档 ID：`CHARACTER-GUIDE`
状态：`Active`
最后核验：`2026-10-16`

## 角色渲染

两套娜娜莉模型使用三渲二着色器 `My_3DProject/Toon/ToonLit (NiloCat URP)`。

- 脸部/眼睛类材质设 `_IsFace = 1`，脸部跳过 NdotL 硬交界线，改用 `lerp(0.5, 1, litOrShadowArea)` 的专用明暗，动漫脸上使用 NdotL 做脸部明暗偏脏。脸部高光叠加层 `MI_player_010_gaoguang` 的透明度与脸部一致才对齐；`MI_player_010_eyelash` 属面部几何，同样按脸部处理。
- 身体与头发材质保持 `_IsFace = 0`，走正常 NdotL 交界线。
- 着色器对脸部有一条固化的深度偏移：`_OutlineZOffset * mask + 0.03 * _IsFace`，其中 `0.03` 是脸部多推一点的补偿，用来把脸部描边藏进脸内侧，避免在脸颊边缘露出一圈深色描边。脸部、下巴、额头边缘出现多余描边时，把脸部材质的 `_OutlineZOffset` 加大，配合 `_OutlineZOffsetMaskTex` 控制生效区域。
- `eye_touming.mat` 是半透明材质。眼睛高光不带描边，眼睛边缘出现多余描边时把该材质的 `_OutlineWidth` 调为 `0`。
- 双眼高光共用半张贴图，该缩放已写入 `_BaseMap_ST`，不要手工重置。
- 着色器默认 `Cull Off` 双面渲染，便于睫毛、发片这类单面模型。角色是封闭网格，双面渲染会多出一倍背面像素开销；压性能时把渲染 Pass 改为 `Cull Back`。
- 调参入口与参数含义见 `../Project/Project_Guide.md` 的渲染管线一节，以及 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`。

## 角色轮廓光

`Assets/Core/Prefabs/Lights/CharacterRimLight.prefab` 是角色专用的轮廓光，作为嵌套 Prefab 实例挂在每份完整角色 Prefab 的根节点下。

光源类型为 `Point`，不依赖朝向，角色转身时效果稳定。强度基线高于 1：ToonLit 的额外光通道在 `NiloToonLightingEquation.hlsl` 里按 `0.25` 缩放，与主方向光不是同一条通路。光位在角色上方偏背后，落在照射范围之内。轮廓光不投影，省一个阴影 pass；渲染模式固定 `ForcePixel`，避免被 Unity 降级成顶点光导致效果跳变。

**调整方式**：实例上的位置/旋转/缩放覆盖已清理，统一继承轮廓光 Prefab。改 `CharacterRimLight.prefab` 的属性对所有角色同时生效；单独调某个角色时在对应完整角色 Prefab 里覆盖那个实例的属性。颜色、强度、范围均为观感参数，按需微调。

**性能注意**：每个玩家角色带 1 个实时点光，走 ToonLit 的额外光通道。URP 前向渲染按每对象受光数量计算，多人同屏时该开销随人数线性增长，需要时按人数收敛范围或强度。

## 学园之星裙摆

学园之星的完整角色 Prefab 保存一套裙摆参数。裙骨链的 `DynamicBone.m_Root` 取下段骨骼，上段保持动画姿势，仅由下段驱动更下一级。后裙根挂着尾部骨骼，模拟起点下移，避免裙摆组件与尾部组件重复处理尾部，尾部的 DynamicBone 独立保留。

本插件的实际参数是主值与分布曲线的乘积，判断强度时以两者乘积为准。`Inert` 越大，粒子越直接跟随角色平移，急停转身时的拖曳越小。

裙摆只引用骨盆和左右大腿的碰撞器代理。两侧大腿的 Outside 胶囊方向为本地 `X`，尺寸按大腿网格中段的实测半径校准；本模型大腿到小腿沿本地负 X，不套用 Y 轴胶囊。

**技术边界**：DynamicBone 先碰撞再限制骨长，且只检测骨端粒子，不是裙面布料碰撞。大幅抬腿仍有局部穿模。要求所有姿势都避开腿部时，需要设计腿部驱动裙骨或布料约束。

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
- `AudioTriggerManager` 复用 `CharacterInputReader` 读取测试音效输入，在组件停用时释放输入资源。
- `Attack` 同样复用 `CharacterInputReader` 读取攻击输入，以相机为准心发射射线处理方块破坏，在组件停用时释放输入资源。
- `PlayerCharacterController` 读取输入并组织移动和表现更新。
- `CharacterInput` 保存当前帧的移动、疾跑和跳跃输入。
- `CharacterMotor` 根据相机水平朝向计算世界移动方向，处理加减速、重力、跳跃和 `CharacterController.Move`。
- `CharacterMovementStateMachine` 接收 `CharacterMotor` 的运动后反馈，维护五个离散状态；状态树按「根 → 地面/空中 → 叶子」三层组织，`CharacterMotion.State` 记录当前叶子状态。
- `CharacterMotion` 保存动画层使用的移动状态。
- `CharacterAnimator` 读取 `CharacterMotion`，驱动已配置 Animator 的参数并处理落地首帧。
- `CharacterBoneFollower` 使用视觉 Prefab 中保存的初始姿势偏移，在动画更新后执行骨骼跟随。
- `ThirdPersonCamera` 保留序列化配置、`SetTarget` 和生命周期编排；普通 C# 对象 `CameraOrbitState`、`CameraCursorController`、`CameraCollisionResolver` 与 `CameraRotationMath` 分别负责轨道状态、光标状态、最终候选避让和旋转数学。

## 运动状态与动画

- `Movement/States/CharacterMovementStateMachine.cs` 集中定义状态枚举、反馈上下文、层级节点基类和状态机；`CharacterMovementStates.cs` 集中定义根、地面、空中三个父状态与五个叶子状态。状态转换统一经过 `Exit`、按最近公共祖先切换和 `Enter`。
- 状态树为「根 → 地面/空中 → 叶子」三层：`Idle`、`Run`、`Sprint` 是地面子树的叶子，`Rising`、`Falling` 是空中子树的叶子。三个父状态只参与转换解析，不进入 `CharacterMovementStateId`，因此五个同步数值仍是 `Idle=0`、`Run=1`、`Sprint=2`、`Rising=3`、`Falling=4`，网络线格式与分层前一致。
- 转换规则按子树的共有部分上提。根状态承担起跳，起跳当帧清零离地计时，先于地面子树判定，向上速度因此不会在贴地判定中被当作 `Run` 或 `Idle`。地面父状态承担非跳跃离地与 `Idle`、`Run`、`Sprint` 三态解析。空中父状态承担接地与按竖直速度方向解析 `Rising`、`Falling`。非跳跃的短暂离地在 `CharacterMovementSettings.airborneTolerance` 窗口内仍按地面状态解析，超出窗口后进入空中子树，下坡与台阶造成的悬空帧因此不会误播跳跃动画。离地容忍窗口只在地面子树内生效，空中子树按移动后是否真实接地判定，主动起跳后窗口内保持空中状态。
- 转换以当前叶子为起点、按最近公共祖先执行：退出旧分支自下而上、进入新分支自上而下，全部在当帧同步完成，不占用额外帧；同一子树内的兄弟切换不会退出公共祖先。
- 分层状态机框架位于 `Assets/Core/Scripts/StateMachine/`（程序集 `Core.FrameWork.StateMachine`，纯 C#、不依赖 `UnityEngine`），提供同步的 `OnEnter`、`OnExit`、`OnUpdate` 钩子，不使用异步任务。角色状态当前未在钩子上挂业务副作用：运动仍由 `CharacterMotor` 承担，表现仍由 `CharacterAnimator` 承担。
- 状态机对外暴露 `CurrentStateId` 与 `TimeInState`（进入当前状态后累计的时长，不含本帧，由状态机维护、`CharacterMotor` 每帧回填进上下文），状态据此实现「持续 N 秒后转换」这类规则。
- 状态机**不对外暴露状态转换边沿**，边沿信息无法跨客户端复现；需要落地这类边沿的表现层自行比较相邻两帧快照，本地与远程才有一致语义。
- `CharacterMotor` 在移动前读取 `CharacterController.isGrounded` 决定跳跃许可，依次处理跳跃、目标水平速度、加减速与空中控制和重力，每帧只调用一次 `CharacterController.Move`，再清除顶碰上升速度、处理接地拉力、维护非跳跃离地计时并更新代码状态。
- 运动链路按帧长积分，起跳在按下当帧生效，动画权重直接取输入幅度。松开输入后物理速度按配置的减速度衰减。
- Animator 的 Grounded 状态通过 Direct 混合树组合 Idle、Run、Sprint，Run 与 Sprint 各使用一个方向混合树；`Rising` 与 `Falling` 使用 Jump 片段，落地首帧重置为待机权重，下一帧恢复运动权重。
- `CharacterAnimator.Initialize` 使用 Animator 已绑定的 Controller，关闭 Root Motion、执行 `Rebind` 并初始化参数。`Apply` 写入方向、接地和运动权重；`LocomotionWeight01` 夹取到 `0..1`。模型重绑读取视觉 Prefab 已配置的 Animator，沿用 Inspector 配置的 Controller 资产和根对象的运动驱动。

`CharacterMotion` 是 `CharacterMotor` 输出给 `CharacterAnimator` 的单帧表现快照，字段为离散状态 `State`、角色自身坐标系的移动方向 `LocalDirection` 和移动权重 `LocomotionWeight01`。`IsGrounded` 与 `WantsSprint` 由 `State` 派生。网络传输该快照的字段约定见 `../Multiplayer/Sync_Protocol.md`。

`CharacterMotionSnapshot`（`Animation/CharacterMotionSnapshot.cs`）是其中**能够跨客户端复现**的那部分，也是网络传输的唯一格式，只含上述三个字段。远程角色经 `PlayerCharacterController.ReceiveRemoteMotion(CharacterMotionSnapshot)` 接收后构造表现快照，表现层只依赖该结构体的字段。

## 编辑器动画配置

共用逻辑资产为 `Assets/Core/Models/Resources/CharacterLocomotion.controller`。在 Animator 窗口维护混合树、方向采样位置与跳跃转换。

- Grounded 的 Direct 混合树使用 `IdleWeight`、`RunWeight`、`SprintWeight`，运行时三者之和为 `1`。
- Run 与 Sprint 均为 `2D Freeform Directional`，使用 `MoveX`、`MoveY`，各有十个方向采样点。
- `Grounded` 布尔参数为 false 时进入 Jump，为 true 时返回 Grounded。两个转换均使用固定 `0.1` 秒过渡并关闭 Exit Time。

每个角色的动画片段在对应 `AnimatorOverrideController` 的 Inspector 中配置：

| 角色 | 覆盖资产 |
|---|---|
| 华丽飞踢 | `Assets/Core/Animations/Characters/娜娜莉（华丽飞踢）.overrideController` |
| 学园之星 | `Assets/Core/Animations/Characters/娜娜莉（学园之星）.overrideController` |

每份资产覆盖 22 个槽位：`__Idle`、`__Jump`、十个 `__Run_` 方向槽位和十个 `__Sprint_` 方向槽位，全部绑定有效片段。对应角色的完整角色 Prefab 的 Animator 引用该覆盖资产。替换角色动画时选择覆盖资产，在 Inspector 的对应槽位指定片段；调整混合或转换规则时编辑共用 Controller。

`CharacterVisual` 保存 Animator、相机、监听器、相机控制器和骨骼跟随配置。`PlayerCharacterController` 只保留运动依赖与动画驱动入口；模型重绑直接读取视觉 Prefab 已配置的 Animator 和相机参考。Controller 资产生命周期归资源系统管理，同一覆盖资产可供多个 Animator 使用，各实例独立维护动画参数。

## 人形映射与面部骨骼

华丽飞踢的模型导入配置位于 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`。该模型的 `mouth` 是脸部控制根，其下挂嘴唇、嘴角和牙齿控制骨骼，不映射为 Humanoid 的 `Jaw`。

该配置通过 `ModelImporter` 保存原 Avatar 的人形描述，`humanName == "Jaw"` 的映射为空，`autoGenerateAvatarMappingIfUnspecified` 关闭，其余人形映射保留。完整角色 Prefab 共用该 Avatar，引用与导入约定见 `../Project/Project_Guide.md` 的“角色表现资源”。

映射为 `Jaw` 时，`mouth` 在通用人形动画求值中偏离初始姿势，引起嘴部蒙皮向下变形。该骨骼的局部旋转变化驱动蒙皮位移，`mouth.localPosition` 不变与脸网格无 BlendShape 均不排除这种位移。诊断时在同一动画姿势下用 `SkinnedMeshRenderer.BakeMesh` 烘焙网格，再仅恢复 `mouth.localRotation` 后重新烘焙，对比顶点即可隔离该骨骼的影响。

重新导入或替换该模型时核对 `mouth` 未重新进入 Jaw 映射，并保持显式映射及关闭自动补映射的配置。

## 相机与移动方向

本地网络角色的 `PlayerCharacterController.inputSpace` 指向本地角色 Prefab 中的 Camera。`CharacterMotor` 取该 Transform 的水平 forward 和 right，把输入转换成世界方向，相机偏航同步改变前进方向。

`ThirdPersonCamera` 只在本地拥有者一侧工作。玩法场景中的 `Camera` 对象保持停用，联机表现模式收拢全部非本地网络相机和多余的 `AudioListener`。

相机保留在角色 Prefab 内，不改变网络层级。`CameraOrbitState` 独立缓存镜头世界旋转，角色父节点转向后不读取被父级带动的 `transform.rotation` 作为插值起点。角色朝向跟随输入轨道的水平 yaw。镜头旋转使用 `1 - exp(-smoothSpeed * deltaTime)` 的指数插值系数，位置使用显式帧长的 `Vector3.SmoothDamp` 平滑焦点到镜头的相对偏移。

观察焦点由 `targetOffset` 抬到胸口高度。最低高度按 `minHeightY` 减去 `targetOffset.y` 施加在相对偏移上，焦点抬高后该下限降到与焦点同高，俯视时镜头整体抬高，准心落点由角色脚边移到前方数米。

避让顺序为「期望轨道偏移 → 最低高度约束 → 相对偏移平滑 → 最终候选方向 SphereCast → 写回裁剪偏移」。最低高度只在期望偏移上约束，平滑结果不做二次夹取，候选向量同时是 SphereCast 的检测路径。球形检测忽略目标及其所有子碰撞体和 Trigger，命中数组填满时重查，极端密集情况使用 `SphereCastAll` 兜底。`RaycastHit.distance` 已是球心行进距离，只保留接触余量。受阻时防穿墙优先于 `minDistance` 与 `minHeightY`；裁剪不改写用户期望 `distance`，同时清空受阻的平滑速度，障碍消失后平滑退回期望轨道。未按 Alt 时每帧夹取期望缩放距离，滚轮事件只改变期望值。

起始检测球与障碍重叠时，仅在焦点位于障碍包围盒外、沿分离平面外法线单调远离且最终球体完全退出时，忽略该零距离命中；其他障碍裁短路径后重新核验退出条件。焦点位于包围盒内部或无法证明安全退出时保守阻挡。检测只约束焦点至候选球心的路径，不覆盖相机整个近裁剪面。

## 高度与物理

网络出生点位于地形附近。Mirror 生成网络根后，`CharacterMotor` 持续计算重力并调用 `CharacterController.Move`。接地状态来自 `CharacterController.isGrounded` 和碰撞标志。

玩家移动链路不调用位置传送，也不做运行时高度钳制。出生位置、`CharacterController` 尺寸、地形高度和重力共同决定落地结果。

## 输入归属

`PlayerCharacterController` 是普通 `MonoBehaviour`。对象存在 `NetworkIdentity` 且客户端已经连接时，仅 `isLocalPlayer=true` 的实例读取输入。单机角色没有 `NetworkIdentity`，使用相同的移动代码。

`Awake` 调用 `EnsureRuntimeState`，补齐组件引用、配置和运动驱动。网络身份组件在首次初始化时查询一次，`networkIdentityInitialized` 同时缓存「有身份」和「无身份」两种结果。身份组件在初始化前配置，缓存的是组件引用，`isLocalPlayer` 等归属值实时读取。

视觉模型的延迟挂载和替换通过 `RebindAnimator` 绑定动画驱动，运动驱动沿用根对象中的实例。`Update` 检查已缓存的运动依赖后依次读取输入、执行移动、应用动画；远程动画更新读取已绑定的驱动。模型解绑时动画引用保持为空，下一次装配由 `RebindAnimator` 接入。

## 输入系统与光标规则

项目输入统一走 Input System 包，Player Settings 的 Active Input Handling 为 `Input System Package (New)`。

- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，包含单张动作表 `Player`，动作覆盖移动、视角、缩放、疾跑、跳跃与光标释放/锁定，移动为键鼠复合与手柄左摇杆。
- 包装类 `Assets/Core/Input/PlayerControls.cs` 由 `.inputactions` 的导入器按 `.meta` 的 `generateWrapperCode` 生成，JSON 内嵌在代码里，不需要在场景或 Prefab 里拖引用。
- `CharacterInputReader` 负责输入量纲换算，把鼠标位移与滚轮读数换算成 `ThirdPersonCamera` 的 `sensitivity`、`scrollSpeed` 量纲，使既有手感数值保持有效。滚轮换算常数按 Windows 每格标定，跨平台需要重新标定。
- 输入实例按需创建：专用服务器分支和远程玩家分支在读取输入前返回，不创建 `PlayerControls`。角色控制器在 `OnDestroy` 释放实例；相机在停用或目标失效时 `Dispose`，恢复后按需重建。
- 键鼠默认映射：WASD 与方向键移动、左右 Shift 疾跑、空格跳跃、Escape 释放光标、鼠标左键重新捕获、Alt 自由光标。
- `ThirdPersonCamera` 的光标规则：`lockCursor` 为真时进入播放即锁定并隐藏光标；Escape 释放；需要锁定时左键点击重新捕获；按住 `Alt` 期间强制释放并显示光标，同时忽略鼠标转动与滚轮缩放，此时左键用于操作 UI。
- 松开 `Alt` 时按进入前的意图恢复：进入前已锁定且窗口有焦点才重新锁定，进入前已用 Escape 释放过则保持释放。窗口无焦点时不抢回鼠标。
- 锁定状态发生跳变的那一帧跳过鼠标增量，避免光标回中造成的镜头跳变。
- `CameraCursorController` 记录全局光标的当前相机持有者。未初始化或已经交出所有权的组件停用时不释放其他相机的光标；重新启用按 `lockCursor` 重新初始化，左键重新捕获丢弃当帧鼠标增量。外部解锁后，左键捕获核对实际锁定状态。目标失效时相机暂停并释放其输入、光标，可通过 `SetTarget` 恢复。
- `ThirdPersonCamera` 的角色朝向纠正仅在 `rotateTarget` 为真且存在移动输入时生效。`CameraRotationMath` 积分连续速度律 `dr/dt = -min(k*r, v)`，其中 `k` 取 `targetRotateSmooth`、`v` 取 `targetRotateSpeed`。剩余角差大于 `v/k` 时先匀速走到该阈值，余下帧长按指数收敛；一帧跨越阈值时分段积分，再用 `Quaternion.RotateTowards` 写入。零速率、零限速或零帧长不转向。
- `ThirdPersonCamera` 的可配置字段按运行期能否生效分组，结论写在 Header 上，例如「环绕（运行时可改）」，Tooltip 只描述字段作用。新增字段沿用该约定。
- `EventSystem` 使用 `InputSystemUIInputModule`，其 `CursorLockBehavior` 为默认的 `OutsideScreen`，操作 UI 需要先按住 `Alt` 释放光标。
- `Assets/Plugins/Mirror/` 中的示例与部分组件使用旧输入 API，均不在构建场景引用内。

## 准心交互

`Attack` 挂在相机 Prefab 上，随嵌套实例进入两份完整角色 Prefab。它从 `CharacterInputReader` 读取 `AttackPressed`，只在光标锁定期间生效；按住 `Alt` 或按 `Escape` 释放光标后，左键留给 UI。

射线以相机位置为起点、相机朝向为方向。检测距离取角色根节点到相机的距离再加上 `reachDistance`，相机在角色身后的那段不占用攻击范围。射线从相机出发必定先穿过角色自身，`Transform.root` 之下的碰撞体全部排除。碎块层的层名由 `VoxelPlatformRenderer` 提供，破坏检测在查询掩码里去掉这一层，废墟中的碎块既不占用命中缓冲，也不挡住它们后方的方块。`Physics.RaycastNonAlloc` 的结果未排序，在其中取最近的非自身命中；缓冲填满时扩容后重查，命中查询不产生托管分配。

命中后通过 `GetComponentInParent<VoxelPlatformRenderer>` 取得平台，先用 `TryGetBlockPosition` 按命中点反查方块局部中心，再调用 `BreakBlock`。破坏改动调用侧的平台实例，平台组件是普通 `MonoBehaviour`，结果不跨客户端同步。

## 目录约定

- 角色脚本按功能归入 `Assets/Core/Scripts/Character/Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`、`Interactions/`。移动参数随运动代码，动画驱动位于 `Animation/`，角色覆盖资产位于 `Assets/Core/Animations/Characters/`。
- 通用分层状态机框架归入 `Assets/Core/Scripts/StateMachine/`，带独立程序集 `Core.FrameWork.StateMachine`，`autoReferenced` 使其可被运行时与测试引用；角色专用的状态节点留在 `Movement/States/`，不放进框架目录。新增状态的写法和联网需同步改动的清单见该目录 `README.md`。
- 相机位于 `Assets/Core/Scripts/Camera/Runtime/`。
- 网络脚本位于 `Assets/Core/Scripts/Networking/`：`Connection/` 放自动连接，`Sync/` 放角色编号与运动同步，`CharacterPresentation/` 放角色表现装配。
- 第三方插件统一放 `Assets/Plugins/`：`Mirror/` 带 asmdef，`DynamicBone/` 无 asmdef，编译进 `Assembly-CSharp-firstpass`，被角色 Prefab 以 GUID 引用。
- 测试目录与运行时代码同构：`Assets/Tests/PlayMode/Camera/` 对应程序集 `Core.Camera.PlayModeTests`，`Assets/Tests/PlayMode/Networking/` 对应 `Core.Networking.PlayModeTests`，`Assets/Tests/Editor/` 对应 `Core.EditorTests`。移动脚本必须连同 `.meta` 一起移动，保持 Prefab 与场景引用有效。

## 验证覆盖

`Core.EditorTests` 覆盖地面状态、起跳、最高点、下降、平台边缘掉落、落地，以及同步状态编号冻结；其中 `HierarchicalStateMachineTests` 覆盖分层框架的启动下钻、子树内兄弟切换、父状态发起转换时退出活动子状态、同帧完成转换、目标等于当前叶子时不重复触发钩子，以及回到祖先时重建默认分支。`CharacterUpdateLoopTests` 覆盖显式模型绑定、解绑后保留空引用、重绑时沿用运动驱动、Inspector Controller 引用保持、共享覆盖资产的参数独立性、运动权重夹取、落地首帧重置，以及两份覆盖资产的 22 个槽位和两份完整角色 Prefab 引用完整性。`Core.Camera.PlayModeTests` 与 `Core.Networking.PlayModeTests` 覆盖父子相机世界旋转、近墙裁剪、抬高后的路径、自身碰撞体下的避让、障碍移除恢复、起始重叠安全退出及双墙裁剪、光标交接、启停与外部解锁后捕获、目标销毁重绑，以及限速积分一致性和暂停后平滑恢复。网络表现测试同时覆盖模型延迟挂载时已配置 Controller 的绑定与初始化。完整角色 Prefab 保留相机旋转参数的序列化字段。

两套角色视觉源各播放全部配置片段，最大嘴部旋转与嘴部引起的蒙皮顶点差均为零；按两套视觉源实际配置调用 `CharacterAnimator.Initialize` 推进后，实际播放 `Idle_Wait_A`，嘴部旋转仍为零。

## 维护触发

修改 Character 目录、Camera 目录、输入资产、输入系统设置、角色输入、CharacterController、动画数据流、本地相机、朝向参数与 Tooltip 约定、光标规则、分层状态机框架或角色状态树时更新本文档。共用动画 Controller、角色覆盖资产、Prefab 动画绑定、角色 FBX 导入、人形 Avatar 映射或面部骨骼配置变化时，同时维护本文档与 `../Project/Project_Guide.md`；分层状态机框架的程序集边界变化时，同时维护 `../Project/Project_Guide.md` 的工程配置一节。
