# Unity 工程与资产

文档 ID：`PROJECT-GUIDE`
状态：`Active`
最后核验：`2026-10-11`

## 渲染管线

工程已从内置渲染管线迁到 **URP 14.0.12**，并实装基于 `UnityURPToonLitShaderExample`（MIT）改造的三渲二着色器。

- 管线依赖：`Packages/manifest.json` 声明 `com.unity.render-pipelines.universal: 14.0.12`。同版本传递依赖为 `com.unity.render-pipelines.core` 与 `com.unity.shadergraph`（均 `14.0.12`），另有 `com.unity.render-pipelines.universal-config: 14.0.10`。
- 编译隔离：工程根目录的 `csc.rsp` 用 `DSH_URP_PRESENT` 宏把引用 `UnityEngine.Rendering.Universal` 的编辑器脚本隔离出去。URP 装好并编译通过后需手动启用该宏，否则「创建并绑定 URP Asset」菜单只会给出提示而不执行。
- 管线资产：`Assets/Core/Rendering/URP_Toon_PC.asset` 与 `URP_Toon_Renderer.asset`，绑定到 `GraphicsSettings` 与全部质量档位。
- 着色器：`Assets/Core/Shaders/Toon/ToonLit.shader`，着色器名 `My_3DProject/Toon/ToonLit (NiloCat URP)`，含 ForwardLit、Outline、ShadowCaster、DepthOnly、DepthNormalsOnly 五个 Pass。
- 编辑器工具菜单为 `My_3DProject/三渲二/`，含创建管线资产、批量材质转换、生成草地风动画材质三项。
- 迁移与调参说明见 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`；上游来源与 MIT 许可见同目录 `THIRD_PARTY_NOTICES.md`。

迁移依据的工程事实（改动前状态）：材质共 139 个，其中 133 个用内置 `Standard`、6 个属插件；排除 `Assets/Plugins/` 后需迁移的是 **38 个**（角色 23、草地 9、树与枝 6）。草地原本使用已被 Unity 2022 废弃的 `Nature/SpeedTree` 内置着色器。四个场景均无烘焙光照数据，`m_AmbientMode: 0`（Skybox 环境光）配实时阴影，因此迁移不需要重烘焙。`Assets/Core` 下 53 个 C# 文件全量扫描，0 处使用 `OnRenderImage`、`Graphics.Blit`、`CommandBuffer`、`RenderingPath` 等管线相关 API。

## 版本与依赖

- Unity 编辑器：`2022.3.62f3c1`。
- Mirror：`96.11.2`。
- **URP：`14.0.12`**。
- Addressables：`1.22.3`。
- Input System：`1.14.2`。
- Terrain Tools：`5.0.6`。
- TextMeshPro：`3.0.7`。
- Timeline：`1.7.7`。
- UGUI：`1.0.0`。
- Visual Scripting：`1.9.4`。
- Unity MCP：Git 依赖的 `main` 分支。

## 资产目录布局

顶层 `Assets/` 保留三个目录：

| 目录 | 内容 |
|---|---|
| `Core/` | 项目自有代码、场景、Prefab、模型与地形 |
| `Materials/` | 地形植被与地表贴图等素材 |
| `Plugins/` | 第三方插件 |

- `Assets/Plugins/` 收纳 `Mirror/`、`ParrelSync/`、`TextMesh Pro/`、`AddressableAssetsData/`。Mirror 的脚本模板位于 `Assets/Plugins/Mirror/MirrorScriptTemplates/`。
- 自写代码位于 `Assets/Core/Shaders/Toon/`（三渲二着色器与说明）与 `Assets/Core/Editor/Toon/`（管线与材质迁移工具）。
- 单机框架代码位于 `Assets/Core/My_FrameWork/`，包含 `Scripts/`、`Teleport/` 与 `Samples/`。
- 地形植被与地表贴图素材位于 `Assets/Materials/ForestMaterials/`。
- 角色脚本位于 `Assets/Core/Scripts/Character/` 下的 `Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`。`PlayerCharacterController` 位于 `Control/`，`CharacterMovementSettings` 位于 `Movement/`，动画配置位于 `Animation/`。
- 网络管理与同步脚本位于 `Assets/Core/Scripts/Networking/`。

## Addressables

- 配置目录为 `Assets/Plugins/AddressableAssetsData/`。
- 插件通过 GUID 链路解析配置。`ProjectSettings/EditorBuildSettings.asset` 的 `com.unity.addressableassets` 记录 `DefaultObject.asset` 的 GUID `00520c7ebfe0ced4ea09c7be32769208`，`DefaultObject.asset` 记录 `AddressableAssetSettings.asset` 的 GUID `3c0067e525af651409829bf27364034c`。
- 分组为内建的 `Built In Data` 和默认组 `Scenes`。
- 激活的构建脚本为 `BuildScriptPackedMode`。`Local.BuildPath` 与 `Local.LoadPath` 引用 `Addressables.BuildPath` 和 `Addressables.RuntimePath`，构建产物位于 `Library/com.unity.addressables/`。
- 内容状态文件位于 `Assets/Plugins/AddressableAssetsData/Windows/addressables_content_state.bin`，路径由 `GetContentStateBuildPath()` 按配置目录与平台子目录求值。
- 移动配置目录时，先让资源库完成该目录变更，再重新加载脚本域，然后核对 `ConfigFolder` 指向新目录。`AddressableAssetSettings` 的 `AssetPath` 与 `ConfigFolder` 在设置对象首次访问时求值，并在该脚本域内保持不变；在资源库完成目录变更前求值会得到变更前的位置，构建、Analyze 与内容更新随后按该位置创建目录。

## Player Settings 与输入

- `ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler` 为 `1`，只启用 `Input System Package (New)`。旧 `UnityEngine.Input` API 在运行时不可用。
- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，其导入器设置 `generateWrapperCode` 生成包装类 `Assets/Core/Input/PlayerControls.cs`，JSON 内嵌在代码里，不需要 Inspector 引用。
- `ProjectSettings/InputManager.asset` 保留旧轴定义，项目脚本不读取。
- `PersistentScene` 的 `EventSystem` 使用 `InputSystemUIInputModule`，动作资产为包的 `DefaultInputActions`，`CursorLockBehavior` 为默认的 `OutsideScreen`。
- `Assets/Plugins/Mirror/` 中有 27 个脚本使用旧 `Input` API，包括 `Components/GUIConsole.cs`、`Components/Profiling/ToggleHotkey.cs`、`Components/Profiling/RemoteStatistics.cs`。这些类型不在 `Assets/Core` 的场景、Prefab 或脚本引用内，也不在构建场景内；运行 Mirror 示例场景会在运行时报错。

## NetworkManager Prefab

路径：`Assets/Core/Prefabs/NetworkManager.prefab`

关键组件：

- `Mirror.NetworkManager`
- `Mirror.NetworkManagerHUD`
- `kcp2k.KcpTransport`
- `AutoStartClient`

关键配置：

- `networkAddress=127.0.0.1`
- `maxConnections=100`
- `sendRate=60`
- `runInBackground=true`
- `headlessStartMode=AutoStartServer`
- `onlineScene` 为空
- `offlineScene` 为空
- `playerPrefab=Assets/Core/Prefabs/NetworkPlayer.prefab`
- `autoCreatePlayer=true`
- `playerSpawnMethod=Random`

## NetworkPlayer Prefab

路径：`Assets/Core/Prefabs/NetworkPlayer.prefab`

根节点共 6 个组件，顺序如下：

1. `Transform`
2. `Mirror.NetworkIdentity`
3. `Mirror.NetworkTransformReliable`
4. `CharacterController`
5. `PlayerCharacterController`
6. `NetworkCharacterSync`

`NetworkTransformReliable` 使用 `ClientToServer`，同步位置和旋转，启用变化检测、旋转压缩和插值。`CharacterController` 的高度为 `2`，半径为 `0.5`，中心为 `(0, 1, 0)`。

`PlayerCharacterController` 的移动配置为运行速度 `6.8`、疾跑速度 `10.8`、加速度 `32`、减速度 `38`、空中控制 `0.45`、旋转速度 `360`、重力 `-28`、跳跃高度 `1.2`、接地吸附速度 `-2`。

## 角色表现资源

`NetworkCharacterManager` 的角色数组按下标配对：

| 角色编号 | 本地拥有者 Prefab | 远程同步 Prefab |
|---|---|---|
| `0` | `CharactersForLocal/娜娜莉（华丽飞踢）.prefab` | `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` |
| `1` | `CharactersForLocal/娜娜莉（学园之星）.prefab` | `CharactersForSync/娜娜莉（学园之星）_Sync.prefab` |

默认角色编号为 `1`。本地 Prefab 提供 Camera 和 `ThirdPersonCamera`。同步 Prefab 提供远程模型、Animator 和动画配置。

模型由 `NetworkCharacterManager` 挂在 `NetworkPlayer` 下，子模型的 `PlayerCharacterController` 与 `CharacterController` 停用，根节点承担网络 Transform、运动和碰撞。重绑只复制动画配置和本地相机参考，保留根 `CharacterMotor` 的速度与代码状态。

## Inspector 配置提示

`NetworkManager` Prefab 上的 `AutoStartClient` 字段显示中文 Tooltip，说明自动连接的运行环境、地址配置入口和重试间隔。连接地址统一填写在 Mirror `NetworkManager` 的 `Network Address`，使用 IP 或主机名。自动连接和 HUD 连接共用 `networkAddress`，端口由同一 Prefab 上的 `KcpTransport.Port` 提供。

`PersistentScene` 中 `NetworkCharacterManager` 的两个角色数组与默认编号显示中文 Tooltip。数组下标从 `0` 开始，两套数组的同一下标表示同一角色；默认编号对应的两个位置均配置有效 Prefab。

`standalonePlayer` 引用常驻场景内的单机角色实例，指向场景内“娜娜莉（学园之星）”根对象 fileID `2132654451`，通过该引用直接启停单机角色。`standaloneCameras`、`standaloneCameraControllers`、`standaloneListeners` 引用场景组件，当前均绑定该角色内的 `Main Camera`。新加入的单机角色与相机需要手动配置引用。

## 验证覆盖

`Core.EditorTests` 覆盖单机角色引用绑定、表现模式切换、本地角色 Prefab 完整性和出生点高度。场景与 Prefab 的序列化引用均可解析。构建输出根目录为 `D:/Unity/Releases/3D_MultiplayerGame/`，客户端与专用服务器分别输出到 `Client/` 与 `Server/`。

## 维护触发

修改 Unity 或包版本、渲染管线、着色器、资产目录布局、Addressables 配置目录与分组、Player Settings、Prefab 组件组成、角色数组或移动配置时更新本文档。场景内容归 `../Scene/Scene_Guide.md`，Mirror 与 KCP 参数归 `Mirror_KCP_Config.md`。
