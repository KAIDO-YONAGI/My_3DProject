# Character 开发记录

## 2026-10-11：角色运动状态机分层化并新增通用分层状态机框架

- 需求：用户要求按上一轮调研结论执行 A+B 两档改造（层级化重写 + 启用同步状态钩子），把相关文件放到正确目录，并清理拉取的教程代码。
- 调研背景：参考 `adammyhre/Unity-Hierarchical-StateMachine`（Unlicense，92 stars）。其 Core 库只依赖 `System` 与 `UnityEngine`，但示例驱动 `PlayerStateDriver` 使用 Unity 6 的 `Rigidbody.linearVelocity`；已用 Unity 活体反射确认本工程 `2022.3.62f3c1` 中该成员**不存在**（只有 `velocity`），原样拷入无法编译。其 `TransitionSequencer` 在过渡期不执行状态机更新，按源码逐行推导即使无活动也要吃掉 2 帧；且父状态发起的转换因 LCA 取到自身而**不会退出其活动子状态**。因此只借鉴层级划分思想，不引入异步活动、反射装配与过渡编排。
- 新框架：`Assets/Core/Scripts/StateMachine/`，程序集 `Core.FrameWork.StateMachine`（纯 C#、不依赖 `UnityEngine`、`autoReferenced`）。`HierarchicalState<TContext>` 提供 `InitialChild`、`ResolveTransition`、`OnEnter`、`OnExit`、`OnUpdate`；`HierarchicalStateMachine<TContext>` 每帧沿活动路径自上而下解析目标，按最近公共祖先切换：退出旧分支自下而上、进入新分支自上而下，全部在当帧完成。
- 相对教程实现修正两处：转换起点取**当前叶子**而非发起转换的状态，父状态发起的转换因此会正确退出其活动子状态；退出时先与父状态断开再回调，避免父状态沿活动路径重复退出同一子状态。
- 角色改造：状态树改为「根 → 地面/空中 → 叶子」。`Idle`、`Run`、`Sprint` 归入地面子树，`Rising`、`Falling` 归入空中子树；根承担起跳，地面父状态承担非跳跃离地与三态解析，空中父状态承担接地与速度换向。原先 `Idle`、`Run`、`Sprint` 三个状态类逐字重复的起跳与离地判定、`Rising` 与 `Falling` 镜像的速度换向判定，均上提到父状态。
- 协议零影响：三个父状态不进入 `CharacterMovementStateId`，五个同步值仍是 `Idle=0`、`Run=1`、`Sprint=2`、`Rising=3`、`Falling=4`；`CharacterMotor`、`CharacterMotion`、`NetworkCharacterMovementSync` 与 `Sync_Protocol.md` 均未改动。
- B 档钩子：状态机新增 `PreviousStateId` 与 `ChangedThisFrame`，把「本帧是否转换、从哪个状态转来」统一提供给表现层做边沿检测，替代各消费方自行维护上一帧状态。角色状态当前未在钩子上挂业务副作用：运动仍归 `CharacterMotor`、表现仍归 `CharacterAnimator`，落地首帧重置权重仍在 `CharacterAnimator.Apply` 内按 `wasGrounded` 边沿执行，未改动。
- 验证：Unity Console 0 错误 0 警告；`Core.EditorTests` 由 19 个测试增至 **27 个，全部通过**。`HierarchicalStateMachineTests` 新增 6 个框架测试，覆盖启动下钻顺序、子树内兄弟切换不退出公共祖先、父状态发起转换时退出活动子状态、同帧完成转换（日志末条为 `update:RightB`）、目标等于当前叶子时不重复触发钩子、回到祖先时重建默认分支；`CharacterMovementStateMachineTests` 新增状态边沿信息与同步编号冻结两个测试，原有 2 个状态转换测试未改动且继续通过。
- 验收边界：教程仓库未被引入工程，`.fetch-cache/Unity-HSM/` 拉取副本已按用户要求删除，上述关于其行为的两点结论来自源码逐行推导，未运行其 Demo 对照。本轮未进入用户场景 PlayMode，未执行双客户端联机或构建验收。
- 文档与计数：已更新本域 `Character_Guide.md`（角色链路、运动状态与动画、目录约定、验证覆盖、维护触发）、本域 `Router.md` 与 `../Project/Project_Guide.md`（新增程序集，以及 asmdef 不可引用预定义程序集 `Assembly-CSharp` 这一约束）。Character 维护计数 `4/5 → 5/5`，达到阈值后按规则复核本域权威文档并重置为 `0/5`。

## 2026-10-11：收紧学园之星裙摆并补齐大腿碰撞

- 用户确认修改后，调整 Local、Sync 两套学园之星 Prefab。8 条裙链的 DynamicBone 起点从 `_001` 下移到 `_002`，上段保持动画姿势，只模拟下缘；同时消除后裙组件对独立尾部链的重复处理，尾部组件保持原配置。
- 参数：阻尼 `0.75`、弹性 `0.8`、刚度 `0.94`、平移跟随 `0.8`、粒子半径 `0.006 m`，前四项分布曲线均为 `1`。旧阻尼、弹性、刚度实际为主值 `0.45 × 曲线 0.45 = 0.2025`，是不能只增主值的原因。
- 碰撞：每套增加两个大腿胶囊，Outside、X 轴、中心 `(-0.18217, 0, 0)`、半径 `0.065 m`、高度 `0.36 m`；裙链引用改为骨盆和两侧大腿。按腿部网格中段半径中位数 `0.0626 m` 校准，未采用试验中偏小的 `0.045 m`。组件总数保持 28 个 DynamicBone，Collider 从 6 增为 8，其余 20 个 DynamicBone 不变。
- 验证：两套 Prefab 各覆盖 22 个实际动画，每片段 180 帧，叠加平移、急停和 90° 转向。保存后共 7920 帧，实际播放片段不匹配为零，结果与候选配置一致。上段物理偏转 `146.92868° -> 0°`，下缘最大偏移 `0.2597848 -> 0.0733006 m`，待机最大约 `0.0069702 m`。每套胶囊代理穿透采样数 `6358 -> 1479`，阈值为 `1 mm`；最大代理穿透 `0.0707656 -> 0.0375702 m`。
- 范围核验：每套 Prefab 仅改变 8 个裙摆组件与 2 个大腿 GameObject 的组件列表，新增 2 个 Collider，无删除或其他序列化变化。常驻场景实例已继承新参数，场景未变脏；Unity Console 为 0 错误、0 警告。未修改运行时代码、FBX、材质或插件。
- 限制：离屏跑步、跳跃画面仍有大幅抬腿时的局部穿模。DynamicBone 的碰撞后骨长约束会重新引入穿透，且骨端检测不覆盖裙面；固定上段与所有姿势避让存在约束冲突，需要另行设计腿部驱动裙骨或布料方案。本次未执行用户场景 PlayMode、双客户端联机或构建验收，不把参数改善记录为穿模问题全面验收，维护计数保持 `4/5`。
- 证据：`Logs/SkirtFix_20261011/Preview.cs` 可重现已保存配置采样；`preview-results.json` 保存修改前及候选对照，`saved-results.json` 保存落盘后结果，`*-before.png`、`*-after.png` 与 `*-saved.png` 为离屏画面。关键参数和限制已同步 Character、Project 两域 Guide 与 Router。

## 2026-10-11：修复华丽飞踢运行时嘴部下移

- 根因：FBX 自动 Humanoid 映射将脸部控制根 `mouth` 错映射为 `Jaw`。该骨骼下挂嘴唇、嘴角和牙齿控制骨骼，实际待机片段 `Idle_Wait_A` 使其局部旋转偏离初始姿势 `10.28555°`；局部位置不变也会通过蒙皮造成嘴部下移。
- 因果对照：同一动画姿势下先 `BakeMesh`，再仅恢复 `mouth.localRotation` 后重新烘焙。原 Avatar 的 1122 个脸部顶点受影响，最大位移 `0.01277462 m`，最大向下位移 `0.0118413 m`。临时 Avatar 仅移除 Jaw 映射后，嘴部旋转、受影响顶点数与顶点差均为零，确认导入映射是原因。
- 正式修复：通过 `ModelImporter` 更新 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`，仅移除 Jaw 映射，将人形映射从 55 项改为 54 项，显式保存 351 项骨架描述，并关闭 `autoGenerateAvatarMappingIfUnspecified`。导入后的 `Avatar.isValid`、`Avatar.isHuman` 均为真，Jaw 映射数为零。模型 GUID 保持 `f75e3b29cf8cbb4458c41c2184aeb2da`，Local、Sync Prefab 的 Avatar 引用不变。
- 动画验证：隔离预览场景中两套 Prefab 各核验全部 22 个配置片段、88 个采样点，合计 176 个。最大嘴部旋转与嘴部引起的蒙皮差均为零，播放片段不匹配为零；左大腿动画旋转达到 `105.3603°`，身体动画仍正常求值。
- 实际初始化链路：按两套 Prefab 的配置分别调用项目 `CharacterAnimator.Initialize`，各以 `1/60` 秒推进 Animator 120 帧，实际播放片段均为 `Idle_Wait_A`，最大嘴部旋转均为零。Unity Console 为 0 错误、0 警告，临时实例、覆盖器和网格已销毁，临时预览场景已关闭。
- 验收边界：未修改运行时代码、材质、着色器或用户场景；本次验证为编辑器内 Animator 求值与蒙皮顶点对照，未进入用户场景 PlayMode，未执行双客户端联机或构建验收。过程证据见 `Logs/MouthAnimationFix_20261011.md`。
- 文档与计数：本次补齐 Character、Project 两域 Guide、Router 与置顶开发记录。补记上轮已落地且经上述验证的实质修复，Character 维护计数 `3/5 -> 4/5`；此次纯文档同步不另计数。

## 2026-10-11：脸部与眼睛类材质开启 _IsFace

- 需求：用户确认把脸部/眼睛类材质的 `_IsFace` 由 `0` 改为 `1`。
- 更正前一条记录的数量：`新增角色轮廓光并挂到 4 个角色 Prefab` 条写的「8 个」是手工清点错误。按「材质名含 face/eye/eyelash/bantou/touming/gaoguang/nighty 关键词」且「位于角色自身 `Materials/` 目录（排除 `Dependencies/`）」两条同时命中，实际为 **13 个**：华丽飞踢 7 个（`eye_bantou`、`eye_touming`、`MI_common_face_mask`、`MI_player_010_eyelash`、`MI_player_010_gaoguang`、`MI_player_010_nanally_eyes`、`MI_player_010_nanally_face`）、学园之星 6 个（`eye_bantou`、`eye_touming`、`MI_common_face_mask`、`MI_player_010_eyelash`、`MI_player_010_nanally_eyes`、`MI_player_010_nanally_nighty_face`）。
- 改动：13 个材质的 `_IsFace` 全部由 `0` 置为 `1`。已核对磁盘与 AssetDatabase 两侧均为 `1`，无遗漏。
- 判定依据（`NiloToonLightingEquation.hlsl` 的 `ShadeSingleLight`）：`_IsFace = 0` 时 `litOrShadowArea` 取 `smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, NoL)`，即标准 NdotL 硬交界线；`_IsFace = 1` 时改为 `lerp(0.5, 1, litOrShadowArea)`，把明暗压缩到 0.5~1 区间，脸部不再出现贴脸的明暗交界。直接用 NdotL 做脸部明暗在动漫脸上偏脏，这是上游作者写明的脸部专用处理。
- 归属判定：`MI_player_010_gaoguang` 是脸部高光/光泽叠加层（`_Mode: 2`、`_SrcBlend: 5`、`_DstBlend: 10`、队列 3000），透明度与脸部一致才对齐，因此一并纳入；`MI_player_010_eyelash` 属面部几何，同样按脸部处理。身体与头发材质（`MI_player_010_nanally_fashion3_1/2`、`MI_player_010_nanally_fashion2_*`、各 `*_hair_*`）保持 `0`，它们该走正常 NdotL 交界线。
- 效果实测：在同一场景、同一相机位、同一次执行内先测 `_IsFace = 0` 再测 `_IsFace = 1`（320×320 脸部特写，排除跨调用干扰）。受影响 3558 像素，其中 **3546 变亮、仅 12 变暗，最大提亮 0.357**，呈单向提亮。全图平均亮度 0.3540 → 0.3553（脸部只占画面约 3.5%，故全图均值变化小）。对照图存在 `Logs/ToonMigration/face_isfaces0.png` 与 `face_isfaces1.png`。
- 过程曲折，记录以免误判：首次测量得到「63% 像素变暗、均值降 0.14」的反常结果。排查后确认是编辑器状态陈旧所致——`editor_state` 报 `is_stale: true`（69 秒无更新）且 `external_changes_dirty: true`，AssetDatabase 尚未重读材质。刷新资产库并改为单次执行内先测 0 再测 1 后，结果转为干净的单向提亮。**结论：材质改动后用跨调用的独立测量对比不可靠，必须在同一次执行内完成对照，或先确认 `is_stale` 为 false。**
- 未完成：未做人工观感确认；脸部描边深度偏移 `_OutlineZOffset` 仍是默认 `0.0001`，着色器里 `0.03 * _IsFace` 那条脸部固化补偿尚未配合调整，若脸部边缘出现多余描边需再调；未执行 PlayMode 与双客户端联机验收。维护计数保持 `3/5`。

## 2026-10-11：新增角色轮廓光并挂到 4 个角色 Prefab

- 需求：用户要求为角色加光源，先做成 Prefab，再加进其余角色 Prefab。经询问确认：4 个角色 Prefab 都加、用 Point Light、颜色强度先取默认值由用户后续微调。
- 新资产：`Assets/Core/Prefabs/Lights/CharacterRimLight.prefab`。Point Light，颜色 `(1, 0.96, 0.90)`，强度 `2.2`，范围 `4.5`，`shadows = None`，`renderMode = ForcePixel`，`lightmapBakeType = Realtime`，本地位置 `(0, 1.35, -0.75)`。
- 强度取 2.2 而非 1 的理由：ToonLit 的额外光通道在 `NiloToonLightingEquation.hlsl` 里按 `0.25` 缩放（`isAdditionalLight ? 0.25 : _DirectLightMultiplier`），与主方向光不是同一条通路，基线需要抬高才能看出效果。
- 应用方式：作为**嵌套 Prefab 实例**加到 4 个角色 Prefab 根节点下（`CharactersForLocal` 两个、`CharactersForSync` 两个）。实例上的位置/旋转/缩放覆盖已用 `PrefabUtility.RevertPropertyOverride` 清理（各 10 项），统一继承源 Prefab，改光源 Prefab 即可对 4 个角色同时生效。
- 光位的定法：角色中心约 `(0, 0.8, 0)`，`Main Camera` 在角色本地 `(0, 2, -2.5)`，即相机位于角色的 **-Z 侧**，因此「远离相机的角色背后」是更负的 Z。第一次误把光源放在 `+Z`（相机侧）并写下了相反的方向结论，随后用实测坐标纠正。
- 光位证据：用相机离屏渲染逐像素对照两种光位（320×320，角色约占 88×122 像素）。相机侧 `z=+0.55`：受影响 5019 像素、最大 Δ 0.344、平均 Δ 0.00584；角色背后 `z=-0.75`：受影响 5141 像素、最大 Δ 0.291、平均 Δ **0.00752**。背后光位平均变化强 29%、峰值更温和，符合轮廓光「照亮边缘而非正面大平光」的特征，故取 `z=-0.75`。实测光相对角色中心偏移约 `(0, 0.57, -0.63)`，落在 4.5 范围内。
- 顺带核实并纠正前一条记录的状态：`角色材质切换到三渲二着色器` 条写的「转换由编辑器菜单执行，当前尚未跑过」已经过时。本轮已在编辑器内实际执行，38 个项目自有材质全部转换成功（角色 23、草地 9、树与枝 6），插件 116 个未动；`LocalCharacter` 的 12 个 `SkinnedMeshRenderer` 全部使用 ToonLit，0 个残留。属性核对通过，含两处易错项：`eye_touming` 的 `_BaseMap_ST` 保持 `scale(0.5, 1)`、`_Src/_DstBlend` 为 `5/10`、队列 3000；`pine` 与 `bark` 的 `_BaseMap` 偏移保持 `(0, -1)`。
- 新发现的待办：8 个脸部/眼睛类材质的 `_IsFace` 当前全部为 `0`，按设计应为 `1`（`MI_player_010_nanally_face`、`MI_player_010_nanally_eyes`、`MI_common_face_mask`、`MI_player_010_nanally_nighty_face`、`eye_bantou`、`eye_touming`，逐个模型实例计为 8 个）。`_IsFace=0` 时脸部会走 NdotL 硬交界线，动漫脸上通常偏脏。已向用户提出，待确认后处理。
- 未完成：轮廓光的颜色、强度、范围均未做人工观感确认；未执行 PlayMode、双客户端联机或构建验收。维护计数保持 `3/5`。

## 2026-10-11：角色材质切换到三渲二着色器

- 背景：工程整体从内置渲染管线迁到 URP 14.0.12（管线与依赖变化记在 `../Project/DeveloperLog.md` 的 `接入 URP 14.0.12 并实装三渲二着色器` 条）。
- 范围：两套娜娜莉模型共 23 个角色材质，原用内置 `Standard` 着色器，需换成 `My_3DProject/Toon/ToonLit (NiloCat URP)`。转换由编辑器菜单执行，当前尚未跑过。
- 材质事实：23 个角色材质中 21 个为不透明，`eye_bantou` 与 `eye_touming` 为半透明（`_Mode: 2`、`_SrcBlend: 5`、`_DstBlend: 10`、`_ZWrite: 0`、渲染队列 3000）。全部 23 个都声明了 `_Cutoff = 0.5`，转换时按 alpha 裁切处理。
- `eye_touming.mat` 的 `_MainTex` 缩放为 `x = 0.5`，双眼高光共用半张贴图。该 Scale 已写入新材质的 `_BaseMap_ST`，手工调整材质时不要重置。
- 脸部与眼睛类材质（`MI_player_010_nanally_face`、`MI_player_010_nanally_eyes`、`MI_common_face_mask`、`MI_player_010_nanally_nighty_face`、`eye_bantou`、`eye_touming`）需要把 `_IsFace` 设为 `1`。新材质的 `_IsFace` 默认为 `0`，需在转换后按材质逐个确认。
- 未完成：未执行 Unity 编译、PlayMode 回归、双客户端联机或人工观感验收。角色材质转换后的实际渲染效果尚未验证，维护计数保持 `3/5`。

## 2026-10-10：修正远程动画注释中的失效组件引用

- `PlayerCharacterController` 的 `LateUpdate` 注释原称动画“只消费 `NetworkCharacterMovementSync` 已按网络时间采样的状态”，但该组件在磁盘上不存在；改为如实描述当前直接使用 Idle 回退。`ApplyRemoteAnimation` 内的“首个快照尚未到达”同步改为“运动状态尚未接入”。
- 只改注释：`IsRemoteNetworkPlayer` 判定、`ApplyRemoteAnimation` 的 Idle 回退行为、`CharacterMotor` 与状态机代码均未动。
- 验证：未执行编译与 PlayMode 回归（Unity 编辑器未运行）。本次不改变业务实现，维护计数保持 `3/5`。

## 2026-10-10：修复第三人称相机避让的 y 轴高频抖动

- 现象：镜头处理障碍避让时 y 轴逐帧抖动。根因在 `CameraOrbitState.Advance`：它在 `Vector3.SmoothDamp` 之后又对候选向量夹取最低高度，而该候选向量同时是避让 `SphereCast` 的检测路径。夹取 y 使路径变陡、越过矮障碍后不裁剪，镜头升到最低高度；下一帧从高位出发、路径恢复水平又撞上障碍，镜头掉回裁剪位置并清空平滑速度，形成逐帧极限环。
- 修改：删除平滑结果的二次高度夹取，最低高度只由期望偏移保证，平滑只负责收敛并允许过渡期短暂低于它。`distance`、`minDistance`、`minHeightY` 语义、`0.01m` 接触余量、命中数组扩容与受阻清速逻辑均未改动，原有中文注释保留。
- 证据：Unity 编辑器当时未运行，改用离线数值复刻 `Vector3.SmoothDamp` 公式与膨胀包围盒 slab 相交检测。静态矮墙（高 `0.75~1.25`、`z=-1.5`、`smoothSpeed=10`、低俯角）下，改前相邻帧 y 差稳定为 `±0.34m` 且每帧交替，改后恒为 `0`；无限高墙下改前 `f0→f1` 单帧跳 `0.92m`，改后恒定不变。
- 未完成：未执行 Unity 编译检查、`Core.Camera.PlayModeTests` 回归、双客户端联机与人工手感验收。按项目惯例不计为已验收的成功业务改造，维护计数保持 `3/5`。

## 2026-10-10：角色功能目录与五态代码边界文档同步

- 当前角色目录改为 `Control/`、`Input/`、`Movement/States/`、`Animation/`、`Presentation/`；运动参数归 `Movement/`，动画配置归 `Animation/`。状态基础结构和五个独立状态类集中为两个文件，原 Networking 脚本保持原目录。
- 依据实际 `CharacterMotor`、状态机、`CharacterMotion` 与根控制器更新 Guide；运动状态网络同步按用户最新要求暂缓，远程动画当前固定回退 Idle。只读当前快照/远程接收接口、顶碰当帧与相同输入帧长位移对比仍未完成。
- 主编辑器 EditMode `Core.EditorTests` 10/10 通过，无失败或跳过；11 个迁移脚本 GUID 与 `HEAD` 对照一致。未重新执行 PlayMode、双客户端、Host 或构建。历史日志路径不改写。
- 本次收尾只维护文档，不再修改代码或资产；维护计数保持 `3/5`，不将未完成整体验收计为成功业务改造。

## 2026-10-08：缓存网络身份缺失结果

- `PlayerCharacterController` 新增 `networkIdentityInitialized`，首次身份查询即标记完成，单机角色不再每帧查询不存在的 `NetworkIdentity`；保留 Animator 延迟初始化、重绑和原有中文注释。
- 回归测试先确认旧逻辑会重复发现后添加的身份，修改后验证缺失结果缓存、已有身份及归属变化、延迟挂载 Animator。PlayMode 网络任务成功，完成 12 项、未报告失败；EditMode 8/8；控制台无错误或警告。
- 未修改场景、Prefab、相机兜底或碰撞逻辑，未构建或执行双客户端联机验证。维护计数 `2/5 -> 3/5`。

## 2026-10-08：单机相机和音频启停改用显式引用

- 新增 `standaloneCameras`、`standaloneCameraControllers`、`standaloneListeners` 场景引用数组，绑定单机角色的 `Main Camera` 组件；移除单机相机路径的三次全局扫描和父级身份查询，原有中文注释保留。
- 新增引用有效性、启停恢复和未配置对象隔离测试。EditMode `Core.EditorTests` 8/8；PlayMode 网络与相机测试任务成功，完成 49 项、未报告失败；未重新构建或进行双客户端联机验证。
- 其他性能风险仅只读排查，未进行 Profiler 耗时测量或扩展代码修改。维护计数 `1/5 -> 2/5`。

## 2026-10-08：单机角色启停改用场景引用

- `NetworkCharacterManager.standalonePlayer` 直接引用常驻场景单机角色，启停不再扫描角色控制器；单机相机逻辑及原有中文注释保留。
- 新增场景引用和仅切换指定对象的测试；停用后恢复及既有角色、相机联合切换均通过。主编辑器 EditMode `Core.EditorTests` 7/7、PlayMode `Core.Networking.PlayModeTests` 9/9，无失败或跳过。
- 未重新构建或进行双客户端联机验证。维护计数 `0/5 -> 1/5`。

## 2026-10-08：脚本目录按知识域重组，相机脚本独立成域

- 证据：`git status --short` 只出现重命名与新增、没有脚本丢失；Unity Console 0 error；PlayMode `Core.Camera.PlayModeTests` 40/40 与 `Core.Networking.PlayModeTests` 9/9（合计 49/49）、EditMode `Core.EditorTests` 5/5 通过；编辑器内读取三个含相机的 Prefab 均解析出 `ThirdPersonCamera` ×1 且脚本路径为 `Assets/Core/Scripts/Camera/Runtime/ThirdPersonCamera.cs`，角色 Prefab 上 `DynamicBone` 仍为 ×8。
- 结构：`Assets/Core/Scripts/Movement/Runtime/` 的 5 个相机脚本移入新域 `Assets/Core/Scripts/Camera/Runtime/`；`Movement/` 更名 `Character/`（保留 `Config/`、`Runtime/` 分层，只放输入、运动、动画与外观装配）；第三方 `DynamicBone` 的 4 个脚本移入 `Assets/Plugins/DynamicBone/`，与 `Mirror/` 同级，因无 asmdef 编译进 `Assembly-CSharp-firstpass`；测试目录与运行时代码同构，`Tests/PlayMode/Movement/` 更名 `Tests/PlayMode/Camera/` 且程序集更名 `Core.Camera.PlayModeTests`，联机用例归入 `Tests/PlayMode/Networking/`（程序集名不变）。
- 所有移动都连同 `.meta` 一起执行，脚本与目录 GUID 不变，因此三个含相机的 Prefab 与场景引用无需改动；新目录的 `.meta` 由 Unity 刷新时生成。
- 本次为纯目录整理与文档同步，不改变业务实现，维护计数保持 `0/5`；相机职责拆分本身见 `审查并拆分第三人称相机` 条。
- 按根 `DeveloperLog.md` 的约定，既有条目里的 `Movement/` 路径保留原文作为当时证据、不做改写；新旧映射以本条为准。被取代的 `ThirdPersonCameraRotationTests.cs` 现由 `CameraRotationMathTests.cs` 与 `ThirdPersonCameraBehaviourTests.cs` 承担。

## 2026-10-08：审查并拆分第三人称相机

- 证据：相机入口与四个 `Camera*.cs` 辅助类、`ThirdPersonCameraBehaviourTests.cs`、`CameraRotationMathTests.cs`、原光标测试；主编辑器 PlayMode 相机与网络装配 49/49、EditMode 5/5 通过，无失败或跳过；Console 无 error。读取两个本地角色 Prefab 的 `SerializedObject`，17 个字段、脚本 GUID 与朝向参数保持不变。本次未改场景、Prefab 或网络代码。
- 旧代码先跑出失败：角色父节点转向额外带动镜头约 1.74 度，近墙仍留在墙后，抬高后未查真实路径，目标销毁访问失效 Transform，启停后光标未恢复，左键捕获增量进入镜头，未初始化的远程组件停用释放本地光标。父子用例先校正启用时机；抬高用例检查第一次候选裁剪，避免把后续合法绕过墙顶误判成漏检。
- 拆分：`ThirdPersonCamera` 只保留配置、绑定和生命周期编排；`CameraOrbitState` 缓存独立世界姿态与相对偏移；`CameraCursorController` 管理持有者及切换帧；`CameraCollisionResolver` 检测最终候选路径并处理满缓冲区；`CameraRotationMath` 提供指数系数和连续限速积分。
- 镜头旋转改为指数系数，角色转向精确积分 `min(k*r,v)`，30/60/120 FPS 的固定时长结果一致，不引入人工死区。更新的记录取代更早条目中的旧公式；不能把纯限速最终一步到位直接认定为视觉瞬移，也不能保证指数旋转在浮点运算中永远不到位。
- 避让在高度约束与平滑之后执行，不再重复扣球半径或用缩放下限推回墙后；命中裁剪写回偏移并清空速度。覆盖 20/140 个自身碰撞体、半径与层配置、障碍移除恢复。焦点已在环境内部及完整近裁剪面覆盖仍不在本次能力范围内。
- 保留原中文注释并迁移到对应职责；修正 `minDistance`、`maxDistance`、`minHeightY` 与启停光标 Tooltip。输入量纲、网络归属和 Prefab 层级不变。
- 独立只读复审发现外部释放光标后不能重新捕获、检测球起始重叠会阻止向外退出两个边界；先用失败测试复现再修复。退出重叠仅在包围盒分离平面能证明安全时允许，并对其他墙裁短后的最终偏移再次检查，避免退回未完全脱离的重叠位置。暂停后平滑恢复测试通过，无需额外修改算法。
- 复核 `Client_Guide.md` 的角色链路、输入与装配边界，更新相机事实及验证边界；维护计数 `4/5 -> 5/5 -> 0/5`。差异检查和 UTF-8 核验未发现空白错误或替换字符；工作区其他会话变更保留。

## 2026-10-08：去掉朝向纠正死区、修掉收尾瞬移，运行期标记移到 Header

- 证据：`Assets/Core/Scripts/Movement/Runtime/ThirdPersonCamera.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraRotationTests.cs`、PlayMode `Core.Movement.PlayModeTests` 9/9 通过、EditMode `Core.EditorTests` 5/5 通过、Unity Console 0 error、Prefab 反序列化核对 `targetRotateSmooth=15` 与 `targetRotateSpeed=360`、反射核对 17 个可配置字段全部带 Tooltip 且不再含运行期标记。
- 用户连续反馈两个问题：加死区后镜头移动卡顿；改用纯限速后大角度回正的最后一段仍然瞬移。两者都先用失败用例复现再改：死区实现下 0.5 度角差永远停在 0.5 度（停走交替就是卡顿来源），纯限速实现的收尾由 `Quaternion.RotateTowards` 在剩余角差小于单帧行程时一次转完。
- 最终规则：单帧步长 = `min(remaining * (1 - exp(-targetRotateSmooth * deltaTime)), targetRotateSpeed * deltaTime)`，再交给 `Quaternion.RotateTowards` 写入。指数项按帧长计算、收尾多帧递减且与帧率无关；限速项只限制大角差峰值。恢复 `targetRotateSmooth`（默认 15/秒，与改造前的比例系数同值，Prefab 上原有序列化值直接复用），删除 `targetRotateDeadZone`。
- 用例：`ThirdPersonCameraRotationTests` 三条——大角差单帧行程受限速约束、小角差持续收敛（用时间预算判定，跨帧率稳定）、收尾存在多帧递减行程。与既有光标用例合计 PlayMode 9/9。
- Tooltip 约定调整：运行期能否生效不再写进 Tooltip，改为写在各分组 Header 上（如「环绕（运行时可改）」），Tooltip 只描述字段作用。
- 维护计数 `3/5 -> 4/5`。

## 2026-10-08：相机朝向纠正改为限速加死区，并为可配置字段补 Tooltip

- 证据：`Assets/Core/Scripts/Movement/Runtime/ThirdPersonCamera.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraRotationTests.cs`、PlayMode `Core.Movement.PlayModeTests` 8/8 通过、EditMode `Core.EditorTests` 5/5 通过、Unity Console 0 error、Prefab 反序列化核对 `targetRotateDeadZone=1` 与 `targetRotateSpeed=360`、反射核对 17 个可配置字段全部带标记。
- 现象：镜头朝向与角色朝向差异很大时纠正“鬼畜”。根因是 `target.rotation = Quaternion.Slerp(target.rotation, Euler(0, yaw, 0), Time.deltaTime * targetRotateSmooth)`：角速度正比于剩余角差（180 度时约 2700 度/秒，是 `CharacterMovementSettings.rotationSpeed` 的 7.5 倍且无上限）；系数随帧长变化，掉帧到 66.7 毫秒以上时被 Slerp 夹断成一帧贴到目标朝向；接近 180 度时最短路径数值不稳。定位时先排除其它写入者：`CharacterMotor.RotateTowards` 因所有 Prefab 的 `orientToMovement: 0` 未生效，`NetworkTransformReliable` 在 owner 与 host 上通过 `!isOwned` 与 `!IsClientWithAuthority` 明确跳过纠偏。
- 修复：改为 `Quaternion.RotateTowards(target.rotation, Euler(0, yaw, 0), targetRotateSpeed * deltaTime)`，角差进入 `targetRotateDeadZone`（默认 1 度）后不再写入；`targetRotateSpeed` 默认 360 度/秒，与 `CharacterMovementSettings.rotationSpeed` 对齐。删除 `targetRotateSmooth`，Prefab 中残留的序列化行被 Unity 忽略，运行时取代码默认值。
- 用例：新增 `ThirdPersonCameraRotationTests`（限速上限与死区两侧行为）。先跑出红色：180 度角差单帧纠正 26.12 度、超过限速允许的 10 度，且死区字段不存在；实现后 2/2 通过，与既有光标用例合计 8/8。
- Tooltip：`ThirdPersonCamera` 的 17 个可配置字段全部补 Tooltip，并以【运行时可改】/【运行时不可改】标注播放模式中修改是否生效，约定写进类注释；其中 `maxDistance`、`lockCursor` 为条件生效，已在 Tooltip 内写明触发条件。
- 维护计数 `2/5 -> 3/5`。

## 2026-10-08：输入统一到 Input System 并新增 Alt 释放光标

- 证据：`Assets/Core/Input/PlayerControls.inputactions` 与其导入器生成的 `PlayerControls.cs`、`Assets/Core/Scripts/Movement/Runtime/CharacterInputReader.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraCursorTests.cs`、Unity Console 0 error、PlayMode 6/6 通过、EditMode `Core.EditorTests` 5/5 通过、编辑器内长按 Alt 人工确认。
- 新增输入资产 `PlayerControls.inputactions`，动作表 `Player` 含 `Move`、`Look`、`Zoom`、`Sprint`、`Jump`、`ReleaseCursor`、`FreeCursor`、`LockCursor`；`Move` 用 WASD 与方向键两个 2DVector 复合加手柄左摇杆，包装类由导入器生成，不需要 Inspector 或 Prefab 引用。
- 新增 `CharacterInputReader` 作为唯一输入入口，集中处理旧轴量纲换算：`<Mouse>/delta * 0.1` 等价旧 `Mouse X/Y` 灵敏度 `0.1`，`<Mouse>/scroll/y * 0.1 / 120` 等价旧 `Mouse ScrollWheel` 每格 `0.1`（Windows 标定）。
- `ThirdPersonCamera` 与 `PlayerCharacterController` 移除 legacy `UnityEngine.Input`，输入实例按需创建，`OnDestroy` 中 `Disable` 后 `Dispose`，专用服务器与远程玩家不创建实例。
- 相机新增 Alt 自由光标：按住时强制释放并显示光标、忽略鼠标转动与滚轮缩放、左键不再重新锁定；松开时按进入前的意图恢复，窗口无焦点时不抢回鼠标，锁定状态跳变当帧跳过鼠标增量避免镜头跳变。
- 自动化用例覆盖初始锁定、Alt 释放、Alt 松开恢复、Alt 之前已释放时保持释放、锁定期间转动 20 度与滚轮 0.2 的量纲、Escape 释放、左键重新捕获、按住 Alt 时左键不抢回光标。
- 维护计数 `1/5 -> 2/5`。

## 2026-09-30：修复单机无相机与联机第三角色

- 联机表现模式从 `NetworkClient.active` 改为 `NetworkClient.isConnected`，连接尝试和重试期间不再提前关闭单机相机。
- `PlayerCharacterController` 同步使用 `isConnected` 判断输入归属，未连接时单机角色继续输入、重力和碰撞运动。
- 已连接时禁用 `PersistentScene` 中没有 `NetworkIdentity` 的场景单机角色；双编辑器只显示两个网络角色。
- 单机角色保持场景初始高度，通过 `CharacterController` 重力自然落地，没有增加传送或位置钳制。

## 2026-09-30：修复联机角色落地与相机方向

- 修正 `PersistentScene/Managers/NetworkCharacterManager.localCharacterPrefabs[]`，改为引用本地角色 Prefab 根对象，不再引用 Prefab 内部骨骼节点。
- 将两个 `NetworkStartPosition` 放到 `TerrainCollider` 上方约 `0.2m`；角色高度由 `CharacterController`、重力和碰撞位移自然收敛，不增加运行时传送或位置钳制。
- 本地网络角色显式绑定自己的 `Main Camera` 为 `inputSpace`；实测相机偏航 `90°` 后前进方向同步旋转。
- 加法场景加载完成后重新禁用非网络相机，避免后加载玩法场景的相机抢占 `Camera.main`。

## 2026-09-30：模型替换时重绑动画驱动

- `PlayerCharacterController` 增加模型替换后的 `Animator` 重绑定入口。
- `CharacterAnimator` 对已销毁的 Unity `Animator` 增加保护，避免切换模型后的下一帧继续调用失效对象。
- 保持摄像头和视觉模型在本地模型 Prefab 内，单机模型配置不会被网络根对象接管。

## 2026-09-29：建立客户端模块权威入口

- 依据 `Assets/Core/Scripts/Movement/` 实际代码建立客户端模块。
- 记录输入、运动、动画和相机职责；未把未来固定 Tick 设计写成当前能力。
- 记录联机时通过可选 `NetworkIdentity` 限制本地玩家输入，保持 `PlayerCharacterController` 为普通 `MonoBehaviour`。
