# 客户端运行时与角色模块

文档 ID：`CLIENT-GUIDE`
状态：`Active`
最后核验：`2026-10-08`

## 客户端角色链路

```text
CharacterInputReader（PlayerControls.inputactions）
  → PlayerCharacterController
  → CharacterInput
  → CharacterMotor
  → CharacterMotion
  → CharacterAnimator
  → CharacterBoneFollower
```

- `CharacterInputReader` 是唯一输入入口，按需创建并启用 `PlayerControls`。
- `PlayerCharacterController` 读取输入并组织移动和表现更新。
- `CharacterInput` 保存当前帧的移动、疾跑和跳跃输入。
- `CharacterMotor` 根据相机水平朝向计算世界移动方向，处理加减速、重力、跳跃和 `CharacterController.Move`。
- `CharacterMotion` 保存动画层使用的移动状态。
- `CharacterAnimator` 配置方向动画和 AnimatorOverrideController。
- `CharacterBoneFollower` 在动画更新后执行骨骼跟随。
- `ThirdPersonCamera` 保留序列化配置、`SetTarget` 和生命周期编排；普通 C# 对象 `CameraOrbitState`、`CameraCursorController`、`CameraCollisionResolver` 与 `CameraRotationMath` 分别负责轨道状态、光标状态、最终候选避让和旋转数学。

## 单机入口

`PersistentScene` 中的 `LocalPlayer` 是编辑器可见的单机角色实例。它使用本地角色 Prefab，包含角色控制器、相机和音频监听器。`SceneChanger` 加载玩法场景后，单机角色继续留在常驻场景并由重力自然落到地形。

`NetworkCharacterManager` 使用 `NetworkClient.isConnected` 判断表现模式。连接尝试期间保留单机角色和相机。连接完成后收起单机角色，把表现交给 Mirror 创建的网络玩家。

## 网络角色装配

服务器生成的 `NetworkPlayer` 是网络根。该对象不直接包含具体角色模型和相机，客户端通过以下流程完成装配：

1. `NetworkCharacterSync.OnStartClient` 将当前 `characterId` 交给 `NetworkCharacterManager`。
2. 管理器检查 `NetworkIdentity.isLocalPlayer`。
3. 本地拥有者从 `localCharacterPrefabs[characterId]` 创建完整本地角色。
4. 远程玩家从 `characterPrefabs[characterId]` 创建同步角色。
5. 新角色成为 `NetworkPlayer` 的子对象，本地位置和旋转归零。
6. 管理器停用视觉子对象上的 `PlayerCharacterController` 和 `CharacterController`，运动和碰撞统一由网络根负责。
7. 管理器把网络根上的 `PlayerCharacterController` 重新绑定到新模型的 `Animator` 和动画配置。
8. 本地拥有者启用角色相机、`ThirdPersonCamera` 和 `AudioListener`，并把相机 Transform 绑定到 `inputSpace`。
9. 远程玩家保持相机、音频监听器和本地输入关闭，只显示模型和动画。

角色切换时，管理器先清理旧表现和动画引用，再按相同流程装配新角色。

`OnStartClient`、`OnStartLocalPlayer` 和 SyncVar hook 共用装配入口。管理器为每个玩家维护一条表现状态，缓存实例、编号、来源 Prefab、本地身份和相机组件。相同实例、编号、来源 Prefab 和身份的重复调用直接复用表现；身份变化且来源 Prefab 相同时重新配置相机，来源 Prefab 变化时重新装配。停用视觉控制器与读取动画配置共用一次组件查找，相机配置使用当前实例的缓存数组。

## 相机与移动方向

本地网络角色的 `PlayerCharacterController.inputSpace` 指向本地角色 Prefab 中的 Camera。`CharacterMotor` 取该 Transform 的水平 forward 和 right，把输入转换成世界方向。因此相机偏航会同步改变前进方向。

`ThirdPersonCamera` 只在本地拥有者一侧工作。远程角色不参与相机选择。玩法场景中的 `Camera` 对象保持停用，联机表现模式也会收拢所有非本地网络相机和多余的 `AudioListener`。

相机继续保留在角色 Prefab 内，不改变网络层级。`CameraOrbitState` 独立缓存镜头世界旋转，角色父节点转向后不再读取被父级带动的 `transform.rotation` 作为插值起点。角色朝向仍跟随输入轨道的水平 yaw，而非滞后的显示旋转。镜头旋转使用 `1 - exp(-smoothSpeed * deltaTime)` 的指数插值系数，位置使用显式帧长的 `Vector3.SmoothDamp` 平滑焦点到镜头的相对偏移。

避让顺序为“期望轨道偏移 → 最低高度约束 → 相对偏移平滑 → 候选高度约束 → 最终候选方向 SphereCast → 写回裁剪偏移”。球形检测忽略目标及其所有子碰撞体和 Trigger；命中数组从 16 扩容至 128，填满时重查，极端密集情况使用 `SphereCastAll` 兜底。`RaycastHit.distance` 已是球心行进距离，仅留 `0.01m` 接触余量，不重复扣半径。受阻时防穿墙优先于 `minDistance` 与 `minHeightY`；裁剪不改写用户期望 `distance`，同时清空受阻的平滑速度，障碍消失后平滑退回期望轨道。未按 Alt 时每帧夹取期望缩放距离，不要求滚轮事件。

起始检测球与障碍重叠时，仅在焦点位于障碍包围盒外、沿分离平面外法线单调远离且最终球体完全退出时，允许忽略该零距离命中；其他障碍裁短路径后重新核验退出条件。焦点位于包围盒内部或无法证明安全退出时保守阻挡，不执行通用穿透恢复。检测只约束焦点至候选球心的路径，也不保证球半径覆盖相机整个近裁剪面；此类场景仍需另行设计。

## 高度与物理

两个 `NetworkStartPosition` 位于地形附近。Mirror 生成网络根后，`CharacterMotor` 持续计算重力并调用 `CharacterController.Move`。接地状态来自 `CharacterController.isGrounded` 和碰撞标志。

玩家移动链路不调用位置传送或运行时高度钳制。出生位置、`CharacterController` 尺寸、地形高度和重力共同决定落地结果。

## 输入归属

`PlayerCharacterController` 是普通 `MonoBehaviour`。对象存在 `NetworkIdentity` 且客户端已经连接时，仅 `isLocalPlayer=true` 的实例读取输入。单机角色没有 `NetworkIdentity`，继续使用相同的移动代码。

## 输入系统与光标规则

项目输入统一走 Input System 包，Player Settings 的 Active Input Handling 为 `Input System Package (New)`。

- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，包含单张动作表 `Player`：`Move`（WASD 与方向键两个 2DVector 复合，外加手柄左摇杆）、`Look`（`<Mouse>/delta`）、`Zoom`（`<Mouse>/scroll/y`）、`Sprint`、`Jump`、`ReleaseCursor`、`FreeCursor`、`LockCursor`。
- 包装类 `Assets/Core/Input/PlayerControls.cs` 由 `.inputactions` 的导入器生成（`.meta` 的 `generateWrapperCode`），JSON 内嵌在代码里，因此不需要在场景或 Prefab 里拖引用。
- `CharacterInputReader` 是运行时唯一输入入口，负责旧轴到 Input System 的量纲换算，使 `ThirdPersonCamera` 的 `sensitivity`、`scrollSpeed` 等既有数值保持改造前手感：`Mouse X/Y` 轴灵敏度 `0.1` 对应 `<Mouse>/delta * 0.1`，`Mouse ScrollWheel` 每格 `0.1` 对应 `<Mouse>/scroll/y * 0.1 / 120`（该常数按 Windows 每格 120 标定，跨平台需重新标定）。
- 输入实例按需创建：专用服务器分支和远程玩家分支在读取输入前就返回，因此不会创建 `PlayerControls`。角色控制器在 `OnDestroy` 释放实例；相机还在停用或目标失效时 `Dispose`，恢复后按需重建，避免停用相机留下启用的动作表。
- 键鼠默认映射：WASD 与方向键移动、左右 Shift 疾跑、空格跳跃、Escape 释放光标、鼠标左键重新捕获、Alt 自由光标。
- `ThirdPersonCamera` 的光标规则：`lockCursor` 为真时进入播放即锁定并隐藏光标；Escape 释放；需要锁定时左键点击重新捕获；按住 `Alt`（左右任一）期间强制释放并显示光标，同时忽略鼠标转动与滚轮缩放（此时左键用于操作 UI，不会重新锁定）。
- 松开 `Alt` 时按进入前的意图恢复：进入前已锁定且窗口有焦点才重新锁定，进入前已用 Escape 释放过则保持释放。窗口无焦点时不抢回鼠标。
- 锁定状态发生跳变的那一帧会跳过鼠标增量，避免光标回中造成的镜头跳变。
- `CameraCursorController` 记录全局光标的当前相机持有者。未初始化或已经交出所有权的组件停用时不释放其他相机的光标；重新启用按 `lockCursor` 重新初始化，左键重新捕获也丢弃当帧鼠标增量。编辑器或其他代码在外部解锁后，左键捕获会核对实际锁定状态，不因内部标志仍为锁定而跳过。目标失效时相机暂停并释放其输入、光标，可通过 `SetTarget` 恢复，不再永久关闭脚本。
- `ThirdPersonCamera` 的角色朝向纠正仅在 `rotateTarget` 为真且存在移动输入时生效。`CameraRotationMath` 精确积分连续速度律 `dr/dt = -min(k*r, v)`，其中 `k=targetRotateSmooth`（默认 15/秒），`v=targetRotateSpeed`（默认 360 度/秒）。剩余角差大于 `v/k` 时先匀速走到该阈值，余下帧长按指数收敛；一帧跨越阈值时也分段积分，再用 `Quaternion.RotateTowards` 写入。不设人工死区；零速率、零限速或零帧长不转向。该模型避免旧版“每帧指数步长事后取最小值”在跨限速阈值时的帧划分偏差。浮点精度或很大帧长仍可能到位，不承诺永远无法达到目标；纯限速的最终一步到位也不等同于一定出现视觉瞬移。网络归属与同步逻辑未改动。
- `ThirdPersonCamera` 的可配置字段按运行期能否生效分组，结论写在 Header 上（如「环绕（运行时可改）」），Tooltip 只描述字段作用；`maxDistance` 与 `lockCursor` 的生效时机需要看各自 Tooltip。新增字段沿用该约定。
- `EventSystem` 使用 `InputSystemUIInputModule`，其 `CursorLockBehavior` 保持默认的 `OutsideScreen`；因此操作 UI 需要先按住 `Alt` 释放光标。
- `Assets/Plugins/Mirror/` 中的示例与部分组件仍使用旧输入 API，均不在构建场景引用内；仅切换到 `Input System Package (New)` 后，运行这些示例场景会在运行时报错。

## 目录约定

- 客户端脚本按知识域分目录：`Assets/Core/Scripts/Camera/Runtime/`（`ThirdPersonCamera` 与轨道状态、光标、避让、转向数学）、`Assets/Core/Scripts/Character/`（输入、运动、动画与外观装配，内含 `Config/` 与 `Runtime/`）、`Assets/Core/Scripts/Networking/`、`Assets/Core/Scripts/UI/`。新增脚本按域归位，域内再分 `Config/` 与 `Runtime/`。
- 第三方插件统一放 `Assets/Plugins/`：`Mirror/`（带 asmdef）与 `DynamicBone/`（无 asmdef，编译进 `Assembly-CSharp-firstpass`，被角色 Prefab 以 GUID 引用）。
- 测试目录与运行时代码同构：`Assets/Core/Tests/PlayMode/Camera/`（`Core.Camera.PlayModeTests`）、`Assets/Core/Tests/PlayMode/Networking/`（`Core.Networking.PlayModeTests`）、`Assets/Core/Tests/Editor/`（`Core.EditorTests`）。移动脚本必须连同 `.meta` 一起移动，否则 Prefab 与场景引用会断开。

## 回归证据

`2026-10-08` 主编辑器验证：`Core.Camera.PlayModeTests`（原 `Core.Movement.PlayModeTests`）40/40 与 `Core.Networking.PlayModeTests` 9/9（合计 49/49），`Core.EditorTests` 5/5，无失败或跳过。覆盖父子相机世界旋转、近墙裁剪、抬高后的路径、20/140 个自身碰撞体下的避让、障碍移除恢复、起始重叠安全退出及双墙裁剪、光标交接、启停/外部解锁后捕获、目标销毁重绑，以及 30/60/120 FPS 的限速积分一致性和暂停后平滑恢复。两个本地角色 Prefab 均保留 17 个序列化字段，原相机脚本 GUID、`targetRotateSmooth=15` 与 `targetRotateSpeed=360` 不变；本次未修改场景或 Prefab。

## 维护触发

修改 Character 目录、Camera 目录、输入资产、输入系统设置、角色输入、CharacterController、动画数据流、本地相机、朝向参数与 Tooltip 约定、光标规则、角色装配或单机与联机表现切换时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。
