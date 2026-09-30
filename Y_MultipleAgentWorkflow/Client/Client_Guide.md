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
- `LocalPlayerCamera` 不再承担模型实例化职责；联机归属规则详见 Networking。

## 输入归属

`PlayerCharacterController` 保持普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 判断联机本地所有权。网络客户端运行时，非本地玩家不采集输入；没有 `NetworkIdentity` 的单机角色保留原有输入行为。

不得仅为联机输入判断把它改成 `NetworkBehaviour`，否则会改变单机 Prefab 兼容性和 Mirror 组件序列化布局。

## 当前状态与边界

- 当前移动仍是 Unity 每帧驱动，不是固定 Tick 的 `InputFrame` 模拟。
- 联机根 Prefab 与本地视觉模型解耦：网络同步位置和 `modelId`，客户端按相同编号从本地模型目录加载表现资源。
- 固定 Tick、预测和校正属于 Proposal，计划来源为 `docs/plan/`，尚未写成当前实现。
- 最近运行验证中本地玩家前进约 `2.82m`，等待 2 秒后未被服务端位置拉回。

## 单机开发流程

1. 打开 `Assets/Core/Scenes/PersistentScene.unity`，确认 `LocalPlayer`、本地 Camera、`Managers/SceneChanger` 和 `Managers/TimeManager` 都在场景层级中。
2. 修改角色模型时，替换 `LocalPlayer` 使用的本地角色 Prefab 或调整其 Animator、Camera 和输入组件；不要把本地模型改成 `Player_Network`。
3. 点击 Play。`SceneChanger` 会根据 `MultiplayerSampleSceneSO` 加载玩法场景，`LocalPlayer` 不会因为 Mirror 场景切换而消失。
4. 联机测试时再启用 `AutoStartClient` 或 Host 流程；Mirror 的网络玩家使用 `Player_Network.prefab`，与单机 `LocalPlayer` 是两条入口。

## 维护触发

修改 Movement 目录、角色输入、CharacterController、动画数据流或本地相机时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。
