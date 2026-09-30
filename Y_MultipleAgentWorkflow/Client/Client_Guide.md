# 客户端运行时与角色模块

文档 ID：`CLIENT-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

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
- `ThirdPersonCamera` 和 `LocalPlayerCamera` 共同承担相机表现；联机归属规则详见 Networking。

## 输入归属

`PlayerCharacterController` 保持普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 判断联机本地所有权。网络客户端运行时，非本地玩家不采集输入；没有 `NetworkIdentity` 的单机角色保留原有输入行为。

不得仅为联机输入判断把它改成 `NetworkBehaviour`，否则会改变单机 Prefab 兼容性和 Mirror 组件序列化布局。

## 当前状态与边界

- 当前移动仍是 Unity 每帧驱动，不是固定 Tick 的 `InputFrame` 模拟。
- 固定 Tick、预测和校正属于 Proposal，计划来源为 `docs/plan/`，尚未写成当前实现。
- 最近运行验证中本地玩家前进约 `2.82m`，等待 2 秒后未被服务端位置拉回。

## 维护触发

修改 Movement 目录、角色输入、CharacterController、动画数据流或本地相机时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。

