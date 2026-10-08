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
- `ThirdPersonCamera` 处理环绕、俯仰、目标跟随、碰撞避让、移动时的目标朝向和光标锁定规则。

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
- 输入实例按需创建：专用服务器分支和远程玩家分支在读取输入前就返回，因此不会创建 `PlayerControls`。实例在 `OnDestroy` 中 `Disable` 并 `Dispose`，避免生成类终结器断言动作表仍然启用。
- 键鼠默认映射：WASD 与方向键移动、左右 Shift 疾跑、空格跳跃、Escape 释放光标、鼠标左键重新捕获、Alt 自由光标。
- `ThirdPersonCamera` 的光标规则：`lockCursor` 为真时进入播放即锁定并隐藏光标；Escape 释放；需要锁定时左键点击重新捕获；按住 `Alt`（左右任一）期间强制释放并显示光标，同时忽略鼠标转动与滚轮缩放（此时左键用于操作 UI，不会重新锁定）。
- 松开 `Alt` 时按进入前的意图恢复：进入前已锁定且窗口有焦点才重新锁定，进入前已用 Escape 释放过则保持释放。窗口无焦点时不抢回鼠标。
- 锁定状态发生跳变的那一帧会跳过鼠标增量，避免光标回中造成的镜头跳变。
- `EventSystem` 使用 `InputSystemUIInputModule`，其 `CursorLockBehavior` 保持默认的 `OutsideScreen`；因此操作 UI 需要先按住 `Alt` 释放光标。
- `Assets/Mirror/`（本地不入库）中的示例与部分组件仍使用旧输入 API，均不在构建场景引用内；仅切换到 `Input System Package (New)` 后，运行这些示例场景会在运行时报错。

## 维护触发

修改 Movement 目录、输入资产、输入系统设置、角色输入、CharacterController、动画数据流、本地相机与光标规则、角色装配或单机与联机表现切换时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。
