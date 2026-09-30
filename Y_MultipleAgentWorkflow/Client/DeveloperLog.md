# Client 开发记录

## 2026-09-29：建立客户端模块权威入口

- 依据 `Assets/Core/Scripts/Movement/` 实际代码建立客户端模块。
- 记录输入、运动、动画和相机职责；未把未来固定 Tick 设计写成当前能力。
- 记录联机时通过可选 `NetworkIdentity` 限制本地玩家输入，保持 `PlayerCharacterController` 为普通 `MonoBehaviour`。

## 2026-09-30：模型替换时重绑动画驱动

- `PlayerCharacterController` 增加模型替换后的 `Animator` 重绑定入口。
- `CharacterAnimator` 对已销毁的 Unity `Animator` 增加保护，避免切换模型后的下一帧继续调用失效对象。
- 保持摄像头和视觉模型在本地模型 Prefab 内，单机模型配置不会被网络根对象接管。
