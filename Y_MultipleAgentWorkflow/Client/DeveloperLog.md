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
