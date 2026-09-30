# 客户端运行时与角色模块

文档 ID：`CLIENT-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

## 当前实现

客户端角色链路为：

```text
PlayerCharacterController
  → CharacterInput
  → CharacterMotor
  → CharacterMotion
  → CharacterAnimator / CharacterBoneFollower
```

- `PlayerCharacterController` 是 Unity 生命周期入口，组织输入、运动和表现顺序。
- `CharacterInput` 是单帧输入值对象，当前由旧版 Unity Input Manager 采集。
- `CharacterMotor` 负责方向转换、加减速、重力、跳跃和 CharacterController 碰撞位移。
- `CharacterMotion` 是传给表现层的单帧快照。
- `CharacterAnimator` 管理 AnimatorOverrideController 和移动动画。
- `CharacterBoneFollower` 在 Animator 更新后应用可选骨骼跟随。
- `ThirdPersonCamera` 保留在本地视觉模型 Prefab 内。单机开发时角色 Prefab 和相机直接放在 `PersistentScene` 的 `LocalPlayer` 对象上，由编辑器可见配置驱动，不在 Play 时生成。
- `PersistentScene` 是客户端常驻层；玩法场景由框架 `SceneChanger` 以 Additive 模式加载，不能改成 Mirror `onlineScene` 自动切场。
- `PlayerCharacterController` 挂在网络根对象上；模型切换前清空旧动画驱动，切换后重新绑定新模型的 `Animator`。
- `PersistentScene/Managers/NetworkCharacterManager` 负责联机角色表现和本地/远程相机归属；本地拥有者从 `CharactersForLocal` 加载带相机的完整 Prefab 根，远程拥有者从 `CharactersForSync` 加载不带相机的视觉 Prefab；`Player_Network` 上的 `NetworkCharacterSync` 只负责同步角色编号。
- 本地网络角色的 `PlayerCharacterController.inputSpace` 显式绑定到本地视觉 Prefab 的 Camera，移动前进方向使用该相机的水平朝向，不依赖可能被加法场景加载覆盖的 `Camera.main`。
- 出生点只负责把 `CharacterController` 放在地形上方约 `0.2m`；之后由 `CharacterMotor` 的重力和 `CharacterController.Move` 自然落地，不在运行时通过传送或位置钳制修正高度。
- `NetworkCharacterManager` 只在 `NetworkClient.isConnected=true` 后进入联机表现模式：禁用场景单机角色及其 Camera、`ThirdPersonCamera` 和 `AudioListener`；连接尝试和重试期间继续保留单机角色、相机、输入和重力。Additive 场景加载后会重新应用当前模式。

## 输入归属

`PlayerCharacterController` 保持普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 判断联机本地所有权。只有 `NetworkClient.isConnected=true` 时才限制输入归属；连接重试但尚未连上时，没有 `NetworkIdentity` 的单机角色继续采集输入并执行重力。

不得仅为联机输入判断把它改成 `NetworkBehaviour`，否则会改变单机 Prefab 兼容性和 Mirror 组件序列化布局。

## 当前状态与边界

- 当前移动仍是 Unity 每帧驱动，不是固定 Tick 的 `InputFrame` 模拟。
- 联机根 Prefab 与视觉模型解耦：网络同步位置和 `characterId`，客户端按相同编号分别从 `CharactersForLocal` 和 `CharactersForSync` 加载本地、远程表现资源；本地单机角色仍由 `PersistentScene/LocalPlayer` 配置。
- 固定 Tick、预测和校正属于 Proposal，计划来源为 `docs/plan/`，尚未写成当前实现。
- 2026-09-30 双 Unity 编辑器连接重建后的专用服务器成功；本地角色在 `Y=-9.167` 稳定落地并保持 `isGrounded=True`，模型最低点与根节点差约 `0.01m`。
- 2026-09-30 运行时验证本地 `inputSpace=Main Camera`；将该相机偏航增加 `90°` 后，前进方向从 `(0,0,1)` 变为 `(1,0,0)`，证明移动方向随相机朝向改变。
- 2026-09-30 无服务端单机验证：场景角色从 `Y=3.38` 依靠 `CharacterController` 重力自然落到 `Y=-9.491`，`isGrounded=True`，相机保持启用。
- 2026-09-30 双编辑器联机验证：每端 `networkPlayers=2`、`activeNetworkVisuals=2`、`standaloneActive=0`，不再显示第三个场景单机角色。

## 单机开发流程

1. 打开 `Assets/Core/Scenes/PersistentScene.unity`，确认 `LocalPlayer`、本地 Camera、`Managers/SceneChanger` 和 `Managers/TimeManager` 都在场景层级中。
2. 修改角色模型时，替换 `LocalPlayer` 使用的本地角色 Prefab 或调整其 Animator、Camera 和输入组件；不要把本地模型改成 `Player_Network`。
3. 点击 Play。`SceneChanger` 会根据 `MultiplayerSampleSceneSO` 加载玩法场景，`LocalPlayer` 不会因为 Mirror 场景切换而消失。
4. 联机测试时再启用 `AutoStartClient` 或 Host 流程；Mirror 的网络玩家使用 `Player_Network.prefab`，与单机 `LocalPlayer` 是两条入口。

## 维护触发

修改 Movement 目录、角色输入、CharacterController、动画数据流或本地相机时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。
