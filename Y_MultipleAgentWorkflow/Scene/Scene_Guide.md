# 场景与流程

文档 ID：`SCENE-GUIDE`
状态：`Active`
最后核验：`2026-10-18`

## 场景列表

Build Settings 的启用场景顺序如下：

1. `Assets/Core/Scenes/InitialScene.unity`
2. `Assets/Core/Scenes/PersistentScene.unity`
3. `Assets/Core/Scenes/MultiplayerSampleScene.unity`
4. `Assets/Core/Scenes/MultiplayerSampleScene 1.unity`

后两个场景互为往返的两张玩法图，由各自的 `SceneToggler` 触发器载入对方。改名时同步 `GameSceneSO.sceneName`，编辑器下 `OnValidate` 按 `sceneAsset` 自动纠正。

## 加载链路

```text
InitialScene
  → InitialLoad 注册并加载 PersistentScene
  → SceneChanger 以 Additive 模式加载玩法场景
```

`InitialLoad` 读取 Persistent Scene 配置，向 `PersistentSceneRegistry` 注册常驻场景，再以 Additive 模式加载。`SceneChanger` 负责玩法场景的加载与卸载、过渡动画和玩家落点。

`SceneChanger` 从 `SceneManager.sceneLoaded` 通知取得本次加载的实际 `Scene` 实例，按加载顺序保存 SO 与实例的对应关系。卸载使用记录的实例句柄，确认实例已卸载后移除对应记录。旧实例仍处于加载状态时，切换流程保留记录并终止本次切换。`GetCurrentScene` 返回实际加载的首个内容场景，`GetCurrentScenes` 返回对应的 SO 列表副本。

Mirror 的 `onlineScene` 为空，在线场景切换不经过 NetworkManager，客户端和服务器使用相同的 Additive 场景结构。场景切换由 `SceneChanger` 与 `SceneToggler` 驱动。

## 场景职责

### InitialScene

构建入口，根对象为 `Bootstrapper`。`InitialLoad` 从场景配置读取常驻场景，注册后以 Additive 模式加载 `PersistentScene`。

### PersistentScene

常驻运行层，持有网络管理、角色表现装配、场景切换、时间管理、UI 事件系统，以及单机开发角色与网络出生点。`SceneChanger` 以 Additive 模式加载玩法场景。

`NetworkCharacterManager` 以 `NetworkClient.isConnected` 判断表现模式：连接尝试期间保留场景中的单机角色和相机，连接完成后收起单机角色，把表现交给 Mirror 创建的网络玩家。单机、联机本地和远程角色使用 `characters` 数组中同一编号的完整角色 Prefab，身份差异由运行时组件启停处理。

`AudioPlayerManager` 与 `AudioTriggerManager` 位于常驻层。单机触发器从 `NetworkCharacterManager.StandalonePlayer` 取得当前单机角色，角色替换后继续使用新实例；联机时从 `NetworkClient.localPlayer` 取得本地网络玩家。

### MultiplayerSampleScene

玩法环境层，提供地形、碰撞和光照。场景中的 `Camera` 对象保持停用，运行视角来自单机角色或本地网络角色。

### MinecraftClassicPreview

`Assets/Core/Scenes/MinecraftClassicPreview.unity` 是经典石材、不规则预切 Cube 与粒子资源的预览入口，使用独立相机、方向光和预览地面。

| 分组 | 职责 |
|---|---|
| `MaterialSamples` | 通过完整 Cube 展示石头、石砖、苔藓石砖与裂纹石砖 |
| `FractureSamples` | 展开预切碎块，展示不规则形状、外表面与切口材质 |
| `ParticleSamples` | 承载迁入的粒子 Prefab 样本 |

完整方块与碎块样本均引用 `Assets/Core/Prefabs/Environment/MinecraftClassic/` 中的 Prefab。碎块展开效果通过场景实例覆盖切换 `Intact`、`Fragments` 的显示状态，并调整碎块局部位置；Prefab 资产保存装配状态。

纹理、材质、预切模型与粒子资源位于 `Assets/Art/MinecraftClassic/`。资源来源、Cube 层级、材质映射和 Blender 制作入口见 `../Project/Project_Guide.md` 的“经典石材与不规则预切资源”。

### MinecraftClassicPlatform

`Assets/Core/Scenes/MinecraftClassicPlatform.unity` 展示随机混铺经典石材的六层圆形平台，包含独立相机、方向光和统一的 `GO` 父对象。六层平台隶属 `GO`，各层保存自身的方块布局、烘焙外表面和分块碰撞，外沿呈现方块轮廓。

平台根节点的 `VoxelPlatformRenderer` 保存轻量方块数据，`CombinedSurface` 显示编辑器预先合并并剔除紧邻面的网格，`CombinedCollision` 使用分块 MeshCollider。破坏时由 `BreakBlockAtHit(RaycastHit)` 根据 `CombinedCollision` 的命中点定位方块，在当前平台的 `FragmentsRuntime` 下创建对应方块的碎块，并局部更新外表面和碰撞。恢复时销毁对应碎块实例并恢复完整方块数据。各层独立管理运行状态，停用再启用时加载各自的烘焙布局。

编辑用平台中的 `CenterDisk`、`OuterDisk` 保留源 Cube 层级。场景烘焙转换把独立布局保存为方块数据、合并表面与合并碰撞，清理源方块与空分组。编辑用与运行用平台资产、代码职责和破坏接口见 `../Project/Project_Guide.md` 的“圆形平台资源”。操作与破碎表现接入示例见 [平台合批、碰撞与破碎接入](../../docs/VoxelPlatformRenderer.md)。

## 场景配置资产

`Assets/Core/SO/` 保存 `GameSceneSO` 资产，字段为 `ID`、`sceneAsset`、`sceneName`、`sceneType` 和 `initialPosition`。

- `ID` 在字段为空时由 `OnValidate` 生成，作为序列化字段保存在 SO 资产中。切换器使用 SO 引用关联实际加载的场景实例。
- 存档使用 `SaveKey`，编辑器下取场景文件 `.unity` 的 GUID，随资产落盘，跨会话与打包一致。`SceneDataForSave.gameScenes` 用它反查回资产。
- `sceneName` 是运行时实际加载的场景名，编辑器下由 `sceneAsset` 自动同步。

## 光照与渲染

工程不依赖烘焙光照，明暗由实时方向光与实时阴影决定。着色器的间接光取 `SampleSH(0)` 平均环境色，并由 `_IndirectLightMinColor` 兜底，避免没有烘焙探针时角色全黑。

地形出画需要把 TerrainLayer 画到地形上，入口为 Terrain → Paint Texture / Paint Details。草地材质的风动画模板位于 `Assets/Materials/ForestMaterials/grass/Materials/Toon/`，TerrainLayer 指向该目录即可启用风动画。

## 维护触发

修改场景列表、场景内容、加载与卸载链路、场景配置资产、单机联机表现切换规则或常驻层音频触发器时更新本文档。经典石材平台与预览场景的分组职责、Prefab 引用、合批设置或显示方式变化时，同步更新本文档与 `../Project/Project_Guide.md`。
