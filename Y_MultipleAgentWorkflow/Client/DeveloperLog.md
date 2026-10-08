# Client 开发记录

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
