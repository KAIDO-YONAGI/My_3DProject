# 场景与流程

文档 ID：`SCENE-GUIDE`
状态：`Active`
最后核验：`2026-10-10`

## 场景列表

Build Settings 的启用场景顺序如下：

1. `Assets/Core/Scenes/InitialScene.unity`
2. `Assets/Core/Scenes/PersistentScene.unity`
3. `Assets/Core/Scenes/MultiplayerSampleScene.unity`
4. `Assets/Core/Scenes/MultiplayerSampleScene 1.unity`

后两个场景互为往返的两张玩法图。两者各 582 行，内容只差一个带 `isTrigger` 的 `BoxCollider` 与 `Teleport` 组件的 `Cube`：`MultiplayerSampleScene` 的触发器引用 `SO/MultiplayerSampleSceneSO 1.asset`，载入 `MultiplayerSampleScene 1`；`MultiplayerSampleScene 1` 的触发器引用 `SO/MultiplayerSampleSceneSO.asset`，载入 `MultiplayerSampleScene`。资产名中的 `" 1"` 来自 Unity 复制资产的自动命名，改名时同步 `GameSceneSO.sceneName`，编辑器下 `OnValidate` 按 `sceneAsset` 自动纠正。

## 加载链路

```text
InitialScene
  → InitialLoad 注册并加载 PersistentScene
  → SceneChanger 以 Additive 模式加载玩法场景
```

`InitialLoad` 读取 Persistent Scene 配置，向 `PersistentSceneRegistry` 注册常驻场景，再以 Additive 模式加载。`SceneChanger` 负责玩法场景的加载与卸载、过渡动画和玩家落点。

Mirror 的 `onlineScene` 为空，在线场景切换不经过 NetworkManager，客户端和服务器使用相同的 Additive 场景结构。场景切换由 `SceneChanger` 与 `Teleport` 组件驱动。

## 场景职责

### InitialScene

构建入口，根对象为 `Bootstrapper`。`InitialLoad` 从场景配置读取常驻场景，注册后以 Additive 模式加载 `PersistentScene`。

### PersistentScene

常驻运行层，包含：

- `Managers/NetworkManager`：Mirror、KCP、HUD 和自动连接。
- `Managers/NetworkCharacterManager`：本地与远程角色表现装配、相机归属和单机联机模式切换。
- `Managers/SceneChanger`：以 Additive 模式加载玩法场景。
- `Managers/TimeManager`：项目时间管理。
- `EventSystem`：UI 输入。
- `LocalPlayer`：单机开发角色与本地相机。
- `NetworkPlayerSpawn_A`：位置 `(-2.63, -9.3075, -31.27)`。
- `NetworkPlayerSpawn_B`：位置 `(-0.63, -9.05172, -31.27)`。

`SceneChanger.firstSceneToLoad` 引用 `Assets/Core/SO/MultiplayerSampleSceneSO.asset`，该资产配置场景名 `MultiplayerSampleScene` 和初始位置 `(-2.63, 3.38, -31.27)`。

`LocalPlayer` 是编辑器可见的单机角色实例，使用本地角色 Prefab，包含角色控制器、相机和音频监听器。`SceneChanger` 加载玩法场景后，单机角色留在常驻场景并由重力落到地形。`NetworkCharacterManager` 以 `NetworkClient.isConnected` 判断表现模式：连接尝试期间保留单机角色和相机，连接完成后收起单机角色，把表现交给 Mirror 创建的网络玩家。单机角色的启停通过 `NetworkCharacterManager.standalonePlayer` 场景序列化引用调用 `SetActive`，单机相机、控制器和监听器通过 `standaloneCameras`、`standaloneCameraControllers`、`standaloneListeners` 三组场景引用切换，当前均绑定该角色的 `Main Camera` 组件。

### MultiplayerSampleScene

玩法环境层，提供 Terrain、`TerrainCollider`、方向光、默认环境对象和 Terrain 分组。场景中的 `Camera` 对象保持停用，运行视角来自单机角色或本地网络角色。

## 场景配置资产

`Assets/Core/SO/` 保存 `GameSceneSO` 资产，字段为 `ID`、`sceneAsset`、`sceneName`、`sceneType` 和 `initialPosition`。

- `ID` 由 `OnValidate` 生成且不落盘，跨会话与打包会变，不作为存档键。
- 存档使用 `SaveKey`，编辑器下取场景文件 `.unity` 的 GUID，随资产落盘，跨会话与打包一致。`SceneDataForSave.gameScenes` 用它反查回资产。
- `sceneName` 是运行时实际加载的场景名，编辑器下由 `sceneAsset` 自动同步。

## 维护触发

修改场景列表、场景内容、加载与卸载链路、场景配置资产或单机联机表现切换规则时更新本文档。
