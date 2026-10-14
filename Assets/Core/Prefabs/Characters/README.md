# 角色 Prefab 维护

这里每个角色只保留一份完整 Prefab：

- `娜娜莉（华丽飞踢）.prefab`
- `娜娜莉（学园之星）.prefab`

每份 Prefab 同时包含模型、Animator、相机、AudioListener、ThirdPersonCamera、PlayerCharacterController、CharacterController、骨骼跟随和 DynamicBone 配置。

## 日常修改

修改模型外观、材质、骨骼物理、相机或角色组件时，直接打开对应角色 Prefab，在 Inspector 中修改并保存。

修改动画片段时，打开 `Assets/Core/Animations/Characters/` 下对应的 `AnimatorOverrideController`，替换 Inspector 中的动画槽位。

修改混合树、Animator 参数或跳跃转换时，打开 `Assets/Core/Models/Resources/CharacterLocomotion.controller`，修改共用 Animator Controller。

## 角色编号

`Assets/Core/Scenes/PersistentScene.unity` 中的 `NetworkCharacterManager.characters` 数组下标就是角色编号：

- `0` 对应 `娜娜莉（华丽飞踢）.prefab`
- `1` 对应 `娜娜莉（学园之星）.prefab`

新增角色时新增一份完整角色 Prefab，并把它放入数组对应下标。角色选择按钮的 `characterId` 使用同一个下标。

## 运行时职责

运行时从数组中读取同一份角色 Prefab：

- 单机保留控制器、碰撞体和相机。
- 本地网络角色启用相机和输入，停用视觉子对象的移动控制器与碰撞体。
- 远程网络角色停用相机、监听器、移动控制器与碰撞体，只保留视觉和动画。

不要为同一个角色再创建 Local、NetworkLocal、Sync 或 CharacterVisuals Prefab。身份差异由 `NetworkCharacterManager` 的组件启停处理。
