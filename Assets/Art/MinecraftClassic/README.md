# 经典石材与预切 Cube

## 纹理来源

`Textures/` 保存 Mojang 官方 Minecraft Java Edition 1.12.2 客户端中的原始 16 × 16 PNG。图像著作权归原权利人所有。

| 文件 | 方块 |
|---|---|
| `stone.png` | 石头 |
| `stonebrick.png` | 石砖 |
| `stonebrick_mossy.png` | 苔藓石砖 |
| `stonebrick_cracked.png` | 裂纹石砖 |

官方客户端地址：

`https://piston-data.mojang.com/v1/objects/0f275bc1547d01fa5f56ba34bdc87d981ee12daf/client.jar`

客户端 SHA-1：`0f275bc1547d01fa5f56ba34bdc87d981ee12daf`

压缩包内路径为 `assets/minecraft/textures/blocks/` 加上述文件名。`Materials/` 中的同名材质使用 URP Lit、Point 过滤和原始纹理，金属度与光滑度为零。

## Cube 资源

`Assets/Core/Prefabs/MinecraftClassic/` 保存四个 `Cube_` 开头的 Prefab，尺寸为一个 Unity 单位。每个 Prefab 的结构为：

```text
Cube_材质名
  Intact
  Fragments
    Node_000
    ...
    Node_015
```

`Intact` 保存完整 Cube 的网格、材质与 BoxCollider。`Fragments` 默认停用，保存十六个封闭的不规则碎块及各自的凸 MeshCollider。子块的局部位置表示装配位置，原点位于各自的体积质心。

`Models/Cube_FractureTemplate.fbx` 使用 Blender 官方 Cell Fracture 扩展中的 Voronoi 单元算法生成，随机种子为 `2607`。十六个碎块采用零间隙装配，总体积为一个立方单位。外表面保留完整 Cube 的局部 UV，切口使用石头材质。FBX 导入后的材质槽顺序随节点变化，外表面材质依据源材质名 `stonebrick` 对应，切口依据 `stone` 对应。

Blender 源文件位于本机：

`C:/Users/12248/.codex/tools/blender-platform-setup/MinecraftClassic.blend`

源文件包含四种材质样块、装配状态的切分模板和展开的碎块预览。Blender 启用 BlenderMCP 与 Cell Fracture，使用 `Industry_Compatible` 键位。

## 圆形平台

`Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25_Authoring.prefab` 是编辑用圆形平台，半径 25 格、厚一格，共有 1,976 个方块。四种经典石材随机混铺，方块中心按半格偏移的网格分布，位置、材质选择和绕竖轴的直角旋转保存在 Prefab 中。

半径五格内的 80 个方块位于 `CenterDisk` 下，外围 1,896 个方块位于 `OuterDisk` 下，平台根节点仅包含这两个分组。方块使用 `Cube_X_<x>_Z_<z>` 名称保存网格坐标，保留源 Cube 的嵌套 Prefab 引用，每块包含十六个不规则碎块。

`Platform_Circle_R25.prefab` 是运行用平台，根节点的 `VoxelPlatformRenderer` 保存轻量方块数据，`CombinedSurface` 保存按空间分块、按材质合并并剔除紧邻面的表面，`CombinedCollision` 保存分块 MeshCollider。网格在编辑器中生成并保存在 `Generated/`，运行时直接复用。

破坏时，平台根据方块记录从源 `Cube_` Prefab 创建实例，放置于 `FragmentsRuntime` 下，关闭 `Intact` 并启用 `Fragments`，同时更新受影响分块的视觉与碰撞。恢复时销毁该碎块实例并恢复完整方块。编辑步骤、代码职责与接入示例见 [平台合批、碰撞与破碎接入](../../../docs/VoxelPlatformRenderer.md)。

## 破坏控制器的接入约定

Cube 根对象表示方块容器，子块名表示容器内的稳定编号。算法侧通过显式映射关联方块编号、子块编号与 Transform，编号独立于层级遍历顺序。

攻击逻辑通过 `BreakBlockAtHit` 或局部方块中心调用 `BreakBlock`，由平台统一管理完整方块的表面、碰撞和碎块实例。破碎表现脚本挂在源 Cube 的 `Fragments` 节点，在 `OnEnable` 中接管已经就位的子块，配置刚体、受力与寿命。

需要整组断裂时，破坏算法维护跨 Cube 的连通关系，让失去支撑的连通分量由共同刚体驱动。仅作表现的小碎屑适合使用粒子系统。碎块实例留在平台生成的层级内时，平台恢复与停用会清理这些实例；移出该层级的碎块由破坏控制器管理。

## 粒子资源

`Particles/Prefabs/` 来自 `D:/Unity/Projects/GameJam2607/Assets/Prefabs/Particles/`，包含 `HurtParticle`、`IceParticle`、`LuoDi`、`RunParticle` 和 `ShootingParticle`。

`Particles/Materials/` 保存对应的三个材质，Shader 为 `Universal Render Pipeline/Particles/Unlit`。受击与拖尾材质使用 URP 默认粒子贴图，地面粒子材质使用白色贴图。发射、颜色、形状和运动参数保存在各自的 ParticleSystem 中。

## 预览入口

`Assets/Core/Scenes/MinecraftClassicPreview.unity` 展示完整 Cube、展开的不规则碎块样本和五种粒子，包含独立相机、方向光与预览地面。

`Assets/Core/Scenes/MinecraftClassicPlatform.unity` 展示六层圆形平台，包含独立相机与方向光。六层平台作为独立根对象，各自保存布局数据、合并表面与分块碰撞。
