# 客户端运行时与角色模块

文档 ID：`CLIENT-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

## 客户端角色链路

```text
PlayerCharacterController
  → CharacterInput
  → CharacterMotor
  → CharacterMotion
  → CharacterAnimator
  → CharacterBoneFollower
```

- `PlayerCharacterController` 读取输入并组织移动和表现更新。
- `CharacterInput` 保存当前帧的移动、疾跑和跳跃输入。
- `CharacterMotor` 根据相机水平朝向计算世界移动方向，处理加减速、重力、跳跃和 `CharacterController.Move`。
- `CharacterMotion` 保存动画层使用的移动状态。
- `CharacterAnimator` 配置方向动画和 AnimatorOverrideController。
- `CharacterBoneFollower` 在动画更新后执行骨骼跟随。
- `ThirdPersonCamera` 处理环绕、俯仰、目标跟随、碰撞避让和移动时的目标朝向。

## 单机入口

`PersistentScene` 中的 `LocalPlayer` 是编辑器可见的单机角色实例。它使用本地角色 Prefab，包含角色控制器、相机和音频监听器。`SceneChanger` 加载玩法场景后，单机角色继续留在常驻场景并由重力自然落到地形。

`NetworkCharacterManager` 使用 `NetworkClient.isConnected` 判断表现模式。连接尝试期间保留单机角色和相机。连接完成后收起单机角色，把表现交给 Mirror 创建的网络玩家。

## 网络角色装配

服务器生成的 `Player_Network` 是网络根。该对象不直接包含具体角色模型和相机，客户端通过以下流程完成装配：

1. `NetworkCharacterSync.OnStartClient` 将当前 `characterId` 交给 `NetworkCharacterManager`。
2. 管理器检查 `NetworkIdentity.isLocalPlayer`。
3. 本地拥有者从 `localCharacterPrefabs[characterId]` 创建完整本地角色。
4. 远程玩家从 `characterPrefabs[characterId]` 创建同步角色。
5. 新角色成为 `Player_Network` 的子对象，本地位置和旋转归零。
6. 管理器停用视觉子对象上的 `PlayerCharacterController` 和 `CharacterController`，运动和碰撞统一由网络根负责。
7. 管理器把网络根上的 `PlayerCharacterController` 重新绑定到新模型的 `Animator` 和动画配置。
8. 本地拥有者启用角色相机、`ThirdPersonCamera` 和 `AudioListener`，并把相机 Transform 绑定到 `inputSpace`。
9. 远程玩家保持相机、音频监听器和本地输入关闭，只显示模型和动画。

角色切换时，管理器先清理旧表现和动画引用，再按相同流程装配新角色。

## 相机与移动方向

本地网络角色的 `PlayerCharacterController.inputSpace` 指向本地角色 Prefab 中的 Camera。`CharacterMotor` 取该 Transform 的水平 forward 和 right，把输入转换成世界方向。因此相机偏航会同步改变前进方向。

`ThirdPersonCamera` 只在本地拥有者一侧工作。远程角色不参与相机选择。玩法场景中的 `Camera` 对象保持停用，联机表现模式也会收拢所有非本地网络相机和多余的 `AudioListener`。

## 高度与物理

两个 `NetworkStartPosition` 位于地形附近。Mirror 生成网络根后，`CharacterMotor` 持续计算重力并调用 `CharacterController.Move`。接地状态来自 `CharacterController.isGrounded` 和碰撞标志。

玩家移动链路不调用位置传送或运行时高度钳制。出生位置、`CharacterController` 尺寸、地形高度和重力共同决定落地结果。

## 输入归属

`PlayerCharacterController` 是普通 `MonoBehaviour`。对象存在 `NetworkIdentity` 且客户端已经连接时，仅 `isLocalPlayer=true` 的实例读取输入。单机角色没有 `NetworkIdentity`，继续使用相同的移动代码。

## 维护触发

修改 Movement 目录、角色输入、CharacterController、动画数据流、本地相机、角色装配或单机与联机表现切换时更新本文档，并同步检查 Networking 与 UnityRuntime 文档。
