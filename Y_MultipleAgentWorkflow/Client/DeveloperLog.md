# Client 开发记录

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

## 2026-09-29：建立客户端模块权威入口

- 依据 `Assets/Core/Scripts/Movement/` 实际代码建立客户端模块。
- 记录输入、运动、动画和相机职责；未把未来固定 Tick 设计写成当前能力。
- 记录联机时通过可选 `NetworkIdentity` 限制本地玩家输入，保持 `PlayerCharacterController` 为普通 `MonoBehaviour`。

## 2026-09-30：模型替换时重绑动画驱动

- `PlayerCharacterController` 增加模型替换后的 `Animator` 重绑定入口。
- `CharacterAnimator` 对已销毁的 Unity `Animator` 增加保护，避免切换模型后的下一帧继续调用失效对象。
- 保持摄像头和视觉模型在本地模型 Prefab 内，单机模型配置不会被网络根对象接管。

## 2026-09-30：修复联机角色落地与相机方向

- 修正 `PersistentScene/Managers/NetworkCharacterManager.localCharacterPrefabs[]`，改为引用本地角色 Prefab 根对象，不再引用 Prefab 内部骨骼节点。
- 将两个 `NetworkStartPosition` 放到 `TerrainCollider` 上方约 `0.2m`；角色高度由 `CharacterController`、重力和碰撞位移自然收敛，不增加运行时传送或位置钳制。
- 本地网络角色显式绑定自己的 `Main Camera` 为 `inputSpace`；实测相机偏航 `90°` 后前进方向同步旋转。
- 加法场景加载完成后重新禁用非网络相机，避免后加载玩法场景的相机抢占 `Camera.main`。

## 2026-09-30：修复单机无相机与联机第三角色

- 联机表现模式从 `NetworkClient.active` 改为 `NetworkClient.isConnected`，连接尝试和重试期间不再提前关闭单机相机。
- `PlayerCharacterController` 同步使用 `isConnected` 判断输入归属，未连接时单机角色继续输入、重力和碰撞运动。
- 已连接时禁用 `PersistentScene` 中没有 `NetworkIdentity` 的场景单机角色；双编辑器只显示两个网络角色。
- 单机角色保持场景初始高度，通过 `CharacterController` 重力自然落地，没有增加传送或位置钳制。

## 2026-10-08：输入统一到 Input System 并新增 Alt 释放光标

- 证据：`Assets/Core/Input/PlayerControls.inputactions` 与其导入器生成的 `PlayerControls.cs`、`Assets/Core/Scripts/Movement/Runtime/CharacterInputReader.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraCursorTests.cs`、Unity Console 0 error、PlayMode 6/6 通过、EditMode `Core.EditorTests` 5/5 通过、编辑器内长按 Alt 人工确认。
- 新增输入资产 `PlayerControls.inputactions`，动作表 `Player` 含 `Move`、`Look`、`Zoom`、`Sprint`、`Jump`、`ReleaseCursor`、`FreeCursor`、`LockCursor`；`Move` 用 WASD 与方向键两个 2DVector 复合加手柄左摇杆，包装类由导入器生成，不需要 Inspector 或 Prefab 引用。
- 新增 `CharacterInputReader` 作为唯一输入入口，集中处理旧轴量纲换算：`<Mouse>/delta * 0.1` 等价旧 `Mouse X/Y` 灵敏度 `0.1`，`<Mouse>/scroll/y * 0.1 / 120` 等价旧 `Mouse ScrollWheel` 每格 `0.1`（Windows 标定）。
- `ThirdPersonCamera` 与 `PlayerCharacterController` 移除 legacy `UnityEngine.Input`，输入实例按需创建，`OnDestroy` 中 `Disable` 后 `Dispose`，专用服务器与远程玩家不创建实例。
- 相机新增 Alt 自由光标：按住时强制释放并显示光标、忽略鼠标转动与滚轮缩放、左键不再重新锁定；松开时按进入前的意图恢复，窗口无焦点时不抢回鼠标，锁定状态跳变当帧跳过鼠标增量避免镜头跳变。
- 自动化用例覆盖初始锁定、Alt 释放、Alt 松开恢复、Alt 之前已释放时保持释放、锁定期间转动 20 度与滚轮 0.2 的量纲、Escape 释放、左键重新捕获、按住 Alt 时左键不抢回光标。
- 维护计数 `1/5 -> 2/5`。

## 2026-10-08：相机朝向纠正改为限速加死区，并为可配置字段补 Tooltip

- 证据：`Assets/Core/Scripts/Movement/Runtime/ThirdPersonCamera.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraRotationTests.cs`、PlayMode `Core.Movement.PlayModeTests` 8/8 通过、EditMode `Core.EditorTests` 5/5 通过、Unity Console 0 error、Prefab 反序列化核对 `targetRotateDeadZone=1` 与 `targetRotateSpeed=360`、反射核对 17 个可配置字段全部带标记。
- 现象：镜头朝向与角色朝向差异很大时纠正“鬼畜”。根因是 `target.rotation = Quaternion.Slerp(target.rotation, Euler(0, yaw, 0), Time.deltaTime * targetRotateSmooth)`：角速度正比于剩余角差（180 度时约 2700 度/秒，是 `CharacterMovementSettings.rotationSpeed` 的 7.5 倍且无上限）；系数随帧长变化，掉帧到 66.7 毫秒以上时被 Slerp 夹断成一帧贴到目标朝向；接近 180 度时最短路径数值不稳。定位时先排除其它写入者：`CharacterMotor.RotateTowards` 因所有 Prefab 的 `orientToMovement: 0` 未生效，`NetworkTransformReliable` 在 owner 与 host 上通过 `!isOwned` 与 `!IsClientWithAuthority` 明确跳过纠偏。
- 修复：改为 `Quaternion.RotateTowards(target.rotation, Euler(0, yaw, 0), targetRotateSpeed * deltaTime)`，角差进入 `targetRotateDeadZone`（默认 1 度）后不再写入；`targetRotateSpeed` 默认 360 度/秒，与 `CharacterMovementSettings.rotationSpeed` 对齐。删除 `targetRotateSmooth`，Prefab 中残留的序列化行被 Unity 忽略，运行时取代码默认值。
- 用例：新增 `ThirdPersonCameraRotationTests`（限速上限与死区两侧行为）。先跑出红色：180 度角差单帧纠正 26.12 度、超过限速允许的 10 度，且死区字段不存在；实现后 2/2 通过，与既有光标用例合计 8/8。
- Tooltip：`ThirdPersonCamera` 的 17 个可配置字段全部补 Tooltip，并以【运行时可改】/【运行时不可改】标注播放模式中修改是否生效，约定写进类注释；其中 `maxDistance`、`lockCursor` 为条件生效，已在 Tooltip 内写明触发条件。
- 维护计数 `2/5 -> 3/5`。

## 2026-10-08：去掉朝向纠正死区、修掉收尾瞬移，运行期标记移到 Header

- 证据：`Assets/Core/Scripts/Movement/Runtime/ThirdPersonCamera.cs`、`Assets/Core/Tests/PlayMode/Movement/ThirdPersonCameraRotationTests.cs`、PlayMode `Core.Movement.PlayModeTests` 9/9 通过、EditMode `Core.EditorTests` 5/5 通过、Unity Console 0 error、Prefab 反序列化核对 `targetRotateSmooth=15` 与 `targetRotateSpeed=360`、反射核对 17 个可配置字段全部带 Tooltip 且不再含运行期标记。
- 用户连续反馈两个问题：加死区后镜头移动卡顿；改用纯限速后大角度回正的最后一段仍然瞬移。两者都先用失败用例复现再改：死区实现下 0.5 度角差永远停在 0.5 度（停走交替就是卡顿来源），纯限速实现的收尾由 `Quaternion.RotateTowards` 在剩余角差小于单帧行程时一次转完。
- 最终规则：单帧步长 = `min(remaining * (1 - exp(-targetRotateSmooth * deltaTime)), targetRotateSpeed * deltaTime)`，再交给 `Quaternion.RotateTowards` 写入。指数项按帧长计算、收尾多帧递减且与帧率无关；限速项只限制大角差峰值。恢复 `targetRotateSmooth`（默认 15/秒，与改造前的比例系数同值，Prefab 上原有序列化值直接复用），删除 `targetRotateDeadZone`。
- 用例：`ThirdPersonCameraRotationTests` 三条——大角差单帧行程受限速约束、小角差持续收敛（用时间预算判定，跨帧率稳定）、收尾存在多帧递减行程。与既有光标用例合计 PlayMode 9/9。
- Tooltip 约定调整：运行期能否生效不再写进 Tooltip，改为写在各分组 Header 上（如「环绕（运行时可改）」），Tooltip 只描述字段作用。
- 维护计数 `3/5 -> 4/5`。

## 2026-10-08：审查并拆分第三人称相机

- 证据：相机入口与四个 `Camera*.cs` 辅助类、`ThirdPersonCameraBehaviourTests.cs`、`CameraRotationMathTests.cs`、原光标测试；主编辑器 PlayMode 相机与网络装配 49/49、EditMode 5/5 通过，无失败或跳过；Console 无 error。读取两个本地角色 Prefab 的 `SerializedObject`，17 个字段、脚本 GUID 与朝向参数保持不变。本次未改场景、Prefab 或网络代码。
- 旧代码先跑出失败：角色父节点转向额外带动镜头约 1.74 度，近墙仍留在墙后，抬高后未查真实路径，目标销毁访问失效 Transform，启停后光标未恢复，左键捕获增量进入镜头，未初始化的远程组件停用释放本地光标。父子用例先校正启用时机；抬高用例检查第一次候选裁剪，避免把后续合法绕过墙顶误判成漏检。
- 拆分：`ThirdPersonCamera` 只保留配置、绑定和生命周期编排；`CameraOrbitState` 缓存独立世界姿态与相对偏移；`CameraCursorController` 管理持有者及切换帧；`CameraCollisionResolver` 检测最终候选路径并处理满缓冲区；`CameraRotationMath` 提供指数系数和连续限速积分。
- 镜头旋转改为指数系数，角色转向精确积分 `min(k*r,v)`，30/60/120 FPS 的固定时长结果一致，不引入人工死区。本文后续状态取代历史条目中的旧公式；不能把纯限速最终一步到位直接认定为视觉瞬移，也不能保证指数旋转在浮点运算中永远不到位。
- 避让在高度约束与平滑之后执行，不再重复扣球半径或用缩放下限推回墙后；命中裁剪写回偏移并清空速度。覆盖 20/140 个自身碰撞体、半径与层配置、障碍移除恢复。焦点已在环境内部及完整近裁剪面覆盖仍不在本次能力范围内。
- 保留原中文注释并迁移到对应职责；修正 `minDistance`、`maxDistance`、`minHeightY` 与启停光标 Tooltip。输入量纲、网络归属和 Prefab 层级不变。
- 独立只读复审发现外部释放光标后不能重新捕获、检测球起始重叠会阻止向外退出两个边界；先用失败测试复现再修复。退出重叠仅在包围盒分离平面能证明安全时允许，并对其他墙裁短后的最终偏移再次检查，避免退回未完全脱离的重叠位置。暂停后平滑恢复测试通过，无需额外修改算法。
- 复核 `Client_Guide.md` 的角色链路、输入与装配边界，更新相机事实及验证边界；维护计数 `4/5 -> 5/5 -> 0/5`。差异检查和 UTF-8 核验未发现空白错误或替换字符；工作区其他会话变更保留。

## 2026-10-08：脚本目录按知识域重组，相机脚本独立成域

- 证据：`git status --short` 只出现重命名与新增、没有脚本丢失；Unity Console 0 error；PlayMode `Core.Camera.PlayModeTests` 40/40 与 `Core.Networking.PlayModeTests` 9/9（合计 49/49）、EditMode `Core.EditorTests` 5/5 通过；编辑器内读取三个含相机的 Prefab 均解析出 `ThirdPersonCamera` ×1 且脚本路径为 `Assets/Core/Scripts/Camera/Runtime/ThirdPersonCamera.cs`，角色 Prefab 上 `DynamicBone` 仍为 ×8。
- 结构：`Assets/Core/Scripts/Movement/Runtime/` 的 5 个相机脚本移入新域 `Assets/Core/Scripts/Camera/Runtime/`；`Movement/` 更名 `Character/`（保留 `Config/`、`Runtime/` 分层，只放输入、运动、动画与外观装配）；第三方 `DynamicBone` 的 4 个脚本移入 `Assets/Plugins/DynamicBone/`，与 `Mirror/` 同级，因无 asmdef 编译进 `Assembly-CSharp-firstpass`；测试目录与运行时代码同构，`Tests/PlayMode/Movement/` 更名 `Tests/PlayMode/Camera/` 且程序集更名 `Core.Camera.PlayModeTests`，联机用例归入 `Tests/PlayMode/Networking/`（程序集名不变）。
- 所有移动都连同 `.meta` 一起执行，脚本与目录 GUID 不变，因此三个含相机的 Prefab 与场景引用无需改动；新目录的 `.meta` 由 Unity 刷新时生成。
- 本次为纯目录整理与文档同步，不改变业务实现，维护计数保持 `0/5`；相机职责拆分本身见上一条 `审查并拆分第三人称相机`。
- 按根 `DeveloperLog.md` 的约定，既有条目里的 `Movement/` 路径保留原文作为当时证据、不做改写；新旧映射以本条为准。被取代的 `ThirdPersonCameraRotationTests.cs` 现由 `CameraRotationMathTests.cs` 与 `ThirdPersonCameraBehaviourTests.cs` 承担。
