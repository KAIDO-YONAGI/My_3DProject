# 角色系统

文档 ID：`CHARACTER-GUIDE`
状态：`Active`
最后核验：`2026-10-11`

## 角色渲染

两套娜娜莉模型的全部 23 个角色材质已从内置 `Standard` 着色器换成三渲二着色器 `My_3DProject/Toon/ToonLit (NiloCat URP)`，随工程整体迁到 URP 14.0.12 一起完成。

- **脸部/眼睛类材质的 `_IsFace` 已全部设为 `1`**，共 13 个材质（华丽飞踢 7 个、学园之星 6 个）。`_IsFace = 1` 时脸部跳过 NdotL 硬交界线，改用 `lerp(0.5, 1, litOrShadowArea)` 的专用明暗——直接用 NdotL 做脸部明暗在动漫脸上偏脏。
  对应材质（按目录各自存在，两侧不完全一致）：`eye_bantou`、`eye_touming`、`MI_common_face_mask`、`MI_player_010_eyelash`、`MI_player_010_nanally_eyes`、`MI_player_010_nanally_nighty_face`（仅学园之星）、`MI_player_010_nanally_face`、`MI_player_010_gaoguang`（仅华丽飞踢）。
  > `MI_player_010_gaoguang` 是脸部高光/光泽叠加层，透明度与脸部一致才对齐；`MI_player_010_eyelash` 属面部几何，同样按脸部处理。
  身体与头发材质保持 `_IsFace = 0`（`MI_player_010_nanally_fashion3_1/2`、`MI_player_010_nanally_fashion2_*`、各 `*_hair_*`），它们该走正常 NdotL 交界线。

- **脸部描边深度偏移尚未调**。着色器里对脸部有一条固化的深度偏移：`_OutlineZOffset * mask + 0.03 * _IsFace`，其中 `0.03` 是硬编码的"脸部多推一点"补偿，用来把脸部描边藏进脸内侧、避免在脸颊边缘露出一圈深色描边。当前所有材质的 `_OutlineZOffset` 都还是默认值 `0.0001`，等于没调。若发现脸部/下巴/额头边缘有多余描边，把脸部材质的 `_OutlineZOffset` 加大（建议从 `0.01` 起试，配合 `_OutlineZOffsetMaskTex` 控制生效区域）。
- `eye_touming.mat` 是半透明材质（`_Color.a = 0.22`、渲染队列 3000、`ZWrite On`）。眼睛高光一般不希望带描边，若发现眼睛边缘有多余描边，把该材质的 `_OutlineWidth` 调为 `0`。
- 材质清单里 `eye_touming.mat` 的 `_MainTex` 缩放是 `x = 0.5`，因为双眼高光共用半张贴图。迁移时该 Scale 已写入 `_BaseMap_ST`，不要手工重置。
- 着色器默认 `Cull Off` 双面渲染，便于睫毛、发片这类单面模型。角色是封闭网格，会多出一倍背面像素开销；压性能时可把渲染 Pass 改为 `Cull Back`。
- 调参入口与参数含义见 `../Project/Project_Guide.md` 的渲染管线一节，以及 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`。

## 角色轮廓光

`Assets/Core/Prefabs/Lights/CharacterRimLight.prefab` 是角色专用的轮廓光，作为**嵌套 Prefab 实例**挂在 4 个角色 Prefab 的根节点下：

| 角色 Prefab | 光源子物体 |
|---|---|
| `CharactersForLocal/娜娜莉（学园之星）.prefab` | `CharacterRimLight` |
| `CharactersForLocal/娜娜莉（华丽飞踢）.prefab` | `CharacterRimLight` |
| `CharactersForSync/娜娜莉（学园之星）_Sync.prefab` | `CharacterRimLight` |
| `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` | `CharacterRimLight` |

光源配置：

| 参数 | 值 | 说明 |
|---|---|---|
| 类型 | `Point` | 不依赖朝向，角色转身时效果稳定 |
| 颜色 | `(1, 0.96, 0.90)` | 略暖白，避免死白把卡通色压掉 |
| 强度 | `2.2` | **基线高于 1 是刻意的**：ToonLit 的额外光通道在 `NiloToonLightingEquation.hlsl` 里按 `0.25` 缩放，与主方向光不是同一条通路 |
| 范围 | `4.5` | 角色高约 1.6 m，光到角色中心约 0.85 m，够覆盖且不溢出太远 |
| 阴影 | `None` | 轮廓光不投影，省一个阴影 pass |
| 渲染模式 | `ForcePixel` | 防止被 Unity 降级成顶点光导致效果跳变 |
| 烘焙类型 | `Realtime` | 场景本来就无烘焙光照数据 |
| 本地位置 | `(0, 1.35, -0.75)` | 见下方光位说明 |

**光位的定法与方向约定**（改位置时最容易搞反，务必按这条核对）：角色中心约 `(0, 0.8, 0)`，`Main Camera` 在角色本地 `(0, 2, -2.5)`，即**相机位于角色的 -Z 侧**，所以「远离相机的角色背后」是**更负的 Z**。实测光相对角色中心的偏移约 `(0, 0.57, -0.63)`，落在 4.5 范围之内。

光位是用相机离屏渲染逐像素对照选出来的（320×320 图，角色约占 88×122 像素）：

| 光位 | 受影响像素 | 最大 Δ | 平均 Δ |
|---|---|---|---|
| 相机侧 `z = +0.55` | 5019 | 0.344 | 0.00584 |
| 角色背后 `z = -0.75` | 5141 | 0.291 | **0.00752** |

背后光位平均变化强 29%、峰值更温和，是轮廓光该有的特征（照亮边缘而非正面大平光）。

**调整方式**：实例上的位置/旋转/缩放覆盖已清理，统一继承源 Prefab。改 `CharacterRimLight.prefab` 的属性即可对 4 个角色同时生效；要单独调某个角色，在对应 Prefab 里覆盖那个实例的属性。颜色、强度、范围均属观感参数，尚未做人工确认，按需微调。

**性能注意**：每个玩家角色带 1 个实时点光，走 ToonLit 的额外光通道。URP 前向渲染按每对象受光数量计算，多人同屏时该开销随人数线性增长，需要时按人数收敛范围或强度。

## 学园之星裙摆

学园之星的 Local、Sync 两套 Prefab 共用同一套裙摆参数。8 条裙骨链的 `DynamicBone.m_Root` 从 `Bn_*qun*_001` 下移到 `_002`，让 `_001` 上段保持动画姿势，仅由 `_002` 驱动 `_003` 下缘。后裙根 `Bn_m_qunB_001` 还挂着 `Bn_m_tail_001`，下移模拟起点同时避免裙摆组件与尾部组件重复处理尾部；尾部自己的 DynamicBone 保留。

参数为 `Damping = 0.75`、`Elasticity = 0.8`、`Stiffness = 0.94`、`Inert = 0.8`、`Radius = 0.006 m`，前四项的分布曲线统一为常数 `1`。本插件实际参数会乘以分布曲线；旧配置的 `0.45` 主值乘 `0.45` 曲线后只有 `0.2025`，不能仅看 Inspector 的主值判断强度。`Inert` 越大，粒子越直接跟随角色平移，急停转身时的拖曳越小。

裙摆只引用骨盆和左右大腿的 3 个碰撞器。两侧 `Bip001-*-Thigh` 新增 Outside 胶囊，方向为本地 `X`，中心 `(-0.18217, 0, 0)`，半径 `0.065 m`，高度 `0.36 m`；本模型大腿到小腿沿本地负 X，不能套用常见的 Y 轴胶囊。大腿网格中段半径实测中位数约 `0.0626 m`，不能为了降低数值穿透而把碰撞器缩到明显小于腿部。

2026-10-11 两套已保存 Prefab 各播放 22 个配置片段，每片段推进 180 帧，并叠加平移、急停及 90° 转向，共 7920 帧。上段相对动画姿势的物理偏转均为 `0°`；待机下缘最大偏移约 `0.00697 m`，全部片段最大偏移由旧配置的 `0.25978 m` 降为 `0.07330 m`，大偏移主要来自腿部碰撞。其余 20 个 DynamicBone 配置保持不变。

**验收边界**：DynamicBone 先碰撞再限制骨长，且只检测骨端粒子，不是裙面布料碰撞。胶囊代理的穿透采样数由每套 `6358` 降到 `1479`（阈值 `1 mm`），但最大残留仍为 `0.03757 m`；离屏跑步、跳跃画面确认大幅抬腿仍有局部穿模，不能宣称完全解决。若要求所有姿势都避开腿部，应另行设计腿部驱动裙骨或布料约束，而不是继续降低阻尼或随意缩小碰撞器。本轮未进行用户场景 PlayMode、双客户端联机或构建验收；可复现预览脚本及结果位于 `Logs/SkirtFix_20261011/`。

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
- `CharacterMovementStateMachine` 接收 `CharacterMotor` 的运动后反馈，维护五个离散状态；状态树按「根 → 地面/空中 → 叶子」三层组织，`CharacterMotion.State` 记录当前叶子状态。
- `CharacterMotion` 保存动画层使用的移动状态。
- `CharacterAnimator` 配置方向动画和 AnimatorOverrideController。
- `CharacterBoneFollower` 在动画更新后执行骨骼跟随。
- `ThirdPersonCamera` 保留序列化配置、`SetTarget` 和生命周期编排；普通 C# 对象 `CameraOrbitState`、`CameraCursorController`、`CameraCollisionResolver` 与 `CameraRotationMath` 分别负责轨道状态、光标状态、最终候选避让和旋转数学。

## 运动状态与动画

- `Movement/States/CharacterMovementStateMachine.cs` 集中定义状态枚举、反馈上下文、层级节点基类和状态机；`CharacterMovementStates.cs` 集中定义根、地面、空中三个父状态与五个叶子状态。状态转换统一经过 `Exit`、按最近公共祖先切换和 `Enter`。
- 状态树为「根 → 地面/空中 → 叶子」三层：`Idle`、`Run`、`Sprint` 是地面子树的叶子，`Rising`、`Falling` 是空中子树的叶子。三个父状态只参与转换解析，不进入 `CharacterMovementStateId`，因此五个同步数值仍是 `Idle=0`、`Run=1`、`Sprint=2`、`Rising=3`、`Falling=4`，网络线格式与分层前一致。
- 转换规则按子树的共有部分上提：根状态承担起跳（起跳当帧会清零离地计时，必须先于地面子树判定，否则向上速度会被当成贴地而误判为 `Run` 或 `Idle`）；地面父状态承担非跳跃离地与 `Idle`、`Run`、`Sprint` 三态解析；空中父状态承担接地与按竖直速度方向解析 `Rising`、`Falling`。非跳跃的短暂离地（下坡、台阶造成的悬空帧）在 `CharacterMovementSettings.airborneTolerance`（默认 `0.3` 秒）窗口内仍按地面状态解析，超出窗口后才进入空中子树，避免误播跳跃动画。
- 转换以当前叶子为起点、按最近公共祖先执行：退出旧分支自下而上、进入新分支自上而下，全部在当帧同步完成，不占用额外帧；同一子树内的兄弟切换不会退出公共祖先。
- 分层状态机框架位于 `Assets/Core/Scripts/GamePlayFrameWork/Scripts/StateMachine/`（程序集 `Core.FrameWork.StateMachine`，纯 C#、不依赖 `UnityEngine`），提供同步的 `OnEnter`、`OnExit`、`OnUpdate` 钩子，不使用异步任务。角色状态当前未在钩子上挂业务副作用：运动仍由 `CharacterMotor` 承担，表现仍由 `CharacterAnimator` 承担。
- 状态机对外暴露 `CurrentStateId`、`PreviousStateId` 与 `ChangedThisFrame`，把「本帧是否发生转换、从哪个状态转来」统一提供给表现层做边沿检测，替代各消费方自行维护上一帧状态。
- `CharacterMotor` 在移动前读取 `CharacterController.isGrounded` 决定跳跃许可，依次处理跳跃、目标水平速度、加减速与空中控制和重力，每帧只调用一次 `CharacterController.Move`，再清除顶碰上升速度、处理接地拉力、维护非跳跃离地计时并更新代码状态。
- 运动链路按帧长积分，起跳在按下当帧生效，动画权重直接取输入幅度。松开输入后物理速度按配置的减速度衰减。
- Animator 保留 Grounded 内 Idle/Run/Sprint 与方向混合；`Rising` 与 `Falling` 使用 Jump 素材，落地时重置权重，缺失素材回退到 Idle 或前进跑步片段。
- `CharacterAnimator.Initialize` 在绑定素材时缓存跑步组与冲刺组的可用性，`Apply` 读取缓存并写入动画参数。角色模型重绑时创建动画驱动并重新计算缓存。

`CharacterMotion` 是 `CharacterMotor` 输出给 `CharacterAnimator` 的单帧表现快照，字段为离散状态 `State`、角色自身坐标系的移动方向 `LocalDirection` 和移动权重 `LocomotionWeight01`。`IsGrounded` 与 `WantsSprint` 由 `State` 派生。网络传输该快照的字段约定见 `../Multiplayer/Sync_Protocol.md`。

## 人形映射与面部骨骼

华丽飞踢的模型导入配置位于 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`。该模型的 `mouth` 是脸部控制根，其下挂嘴唇、嘴角和牙齿控制骨骼，不应映射为 Humanoid 的 `Jaw`。

原自动映射把 `mouth` 当成 `Jaw`，播放实际待机片段 `Idle_Wait_A` 时使其局部旋转偏离初始姿势 `10.28555°`，导致嘴部蒙皮向下变形。`mouth.localPosition` 不变、脸网格没有 BlendShape，都不能排除这种旋转驱动的蒙皮位移。诊断时应在同一动画姿势下用 `SkinnedMeshRenderer.BakeMesh` 烘焙网格，再仅恢复 `mouth.localRotation` 后重新烘焙，对比顶点以隔离该骨骼的影响。原配置影响 1122 个脸部顶点，最大位移 `0.01277462 m`，最大向下位移 `0.0118413 m`；只移除错误的 Jaw 映射后，上述旋转与顶点差均为零。

2026-10-11 已通过 `ModelImporter` 保存原 Avatar 的人形描述，仅移除 `humanName == "Jaw"` 的映射，并关闭 `autoGenerateAvatarMappingIfUnspecified`。其余 54 项人形映射保留，骨架描述显式保存 351 项；导入后的 `Avatar.isValid` 与 `Avatar.isHuman` 均为真。Local、Sync 两套华丽飞踢 Prefab 继续共用原 GUID 的 Avatar，引用与导入约定见 `../Project/Project_Guide.md` 的“角色表现资源”。

后续重新导入或替换该模型时，必须核对 `mouth` 没有重新进入 Jaw 映射，并保持显式映射及关闭自动补映射的配置。此问题修复在模型导入层，不需要在动画更新后强制覆盖嘴部骨骼旋转。

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
- 通用分层状态机框架归入 `Assets/Core/Scripts/GamePlayFrameWork/Scripts/StateMachine/`，带独立程序集 `Core.FrameWork.StateMachine`（`autoReferenced`，运行时与测试均可引用）；角色专用的状态节点仍留在 `Movement/States/`，不放进框架目录。
- 相机位于 `Assets/Core/Scripts/Camera/Runtime/`。
- `AutoStartClient`、`NetworkCharacterManager`、`NetworkCharacterSync` 位于 `Assets/Core/Scripts/Networking/`。
- 第三方插件统一放 `Assets/Plugins/`：`Mirror/` 带 asmdef，`DynamicBone/` 无 asmdef，编译进 `Assembly-CSharp-firstpass`，被角色 Prefab 以 GUID 引用。
- 测试目录与运行时代码同构：`Assets/Tests/PlayMode/Camera/` 对应程序集 `Core.Camera.PlayModeTests`，`Assets/Tests/PlayMode/Networking/` 对应 `Core.Networking.PlayModeTests`，`Assets/Tests/Editor/` 对应 `Core.EditorTests`。移动脚本必须连同 `.meta` 一起移动，保持 Prefab 与场景引用有效。

## 验证覆盖

`Core.EditorTests` 覆盖地面状态、起跳、最高点、下降、平台边缘掉落、落地，以及状态边沿信息与同步状态编号冻结；其中 `HierarchicalStateMachineTests` 覆盖分层框架的启动下钻、子树内兄弟切换、父状态发起转换时退出活动子状态、同帧完成转换、目标等于当前叶子时不重复触发钩子，以及回到祖先时重建默认分支。`CharacterUpdateLoopTests` 覆盖显式模型绑定、解绑后保留空引用、重绑时沿用运动驱动、动画素材可用性缓存、缺失素材回退与落地首帧权重重置。`Core.Camera.PlayModeTests` 与 `Core.Networking.PlayModeTests` 覆盖父子相机世界旋转、近墙裁剪、抬高后的路径、自身碰撞体下的避让、障碍移除恢复、起始重叠安全退出及双墙裁剪、光标交接、启停与外部解锁后捕获、目标销毁重绑，以及 30/60/120 FPS 的限速积分一致性和暂停后平滑恢复。两个本地角色 Prefab 保留 17 个序列化字段，`targetRotateSmooth=15` 与 `targetRotateSpeed=360`。

2026-10-11 的嘴部修复使用隔离预览场景中的实际 Animator 求值验证：Local、Sync 两套华丽飞踢 Prefab 各播放全部 22 个配置片段（待机 1、跑步 10、冲刺 10、跳跃 1），每片段连续推进 `0.05 / 0.17 / 0.31 / 0.53` 秒，共 176 个采样点。最大嘴部旋转与嘴部引起的蒙皮顶点差均为零，播放片段不匹配为零；左大腿动画旋转达到 `105.3603°`，确认身体动画仍正常求值。另按两套 Prefab 实际配置调用项目 `CharacterAnimator.Initialize`，各以 `1/60` 秒推进 120 帧，实际播放 `Idle_Wait_A`，嘴部旋转仍为零。核验结束时 Unity Console 为 0 错误、0 警告，临时对象与预览场景已清理。本次未进入用户场景 PlayMode，未执行双客户端联机或构建验收；过程证据保存在 `Logs/MouthAnimationFix_20261011.md`，关键结论同时记录于本域 `DeveloperLog.md`。

## 维护触发

修改 Character 目录、Camera 目录、输入资产、输入系统设置、角色输入、CharacterController、动画数据流、本地相机、朝向参数与 Tooltip 约定、光标规则、分层状态机框架或角色状态树时更新本文档。角色 FBX 导入、人形 Avatar 映射或面部骨骼配置变化时，同时维护本文档与 `../Project/Project_Guide.md`；分层状态机框架的程序集边界变化时，同时维护 `../Project/Project_Guide.md` 的工程配置一节。
