# 炸药桶投掷物资源

## 来源与许可

模型为 Poly Haven 的 `Barrel_01`，作者为 Jorge Camacho。资源采用 CC0 1.0 公共领域贡献声明，允许商业使用、修改与再分发。

- 模型页面：https://polyhaven.com/a/Barrel_01
- 平台许可：https://polyhaven.com/license
- CC0 1.0：https://creativecommons.org/publicdomain/zero/1.0/
- 模型元数据：https://api.polyhaven.com/info/Barrel_01
- 下载清单与校验值：https://api.polyhaven.com/files/Barrel_01

源 FBX 与纹理为官方 1K 版本。源文件校验信息保存在 `SourceManifest.json`。

## 资源结构

`ExplosiveBarrel/Barrel_01_1k.fbx` 保存模型，邻接的 `textures/` 保存底色、法线、金属度与粗糙度纹理。`ExplosiveBarrel_MetallicSmoothness.png` 使用金属度纹理的红色通道作为金属度，使用一减粗糙度作为 Alpha 光滑度。金属度与光滑度贴图按线性颜色空间导入，法线贴图使用 Normal Map 导入类型。

`ExplosiveBarrel.mat` 使用 `Universal Render Pipeline/Lit`，承载底色、法线与金属度／光滑度贴图。

`Assets/Core/Prefabs/Props/ExplosiveBarrelProjectile.prefab` 的根节点承载 Rigidbody 与 CapsuleCollider，子节点 `ExplosiveBarrelVisual` 引用 FBX 和 URP 材质。根节点原点位于桶身中心，模型使用 Unity 的 Y 轴朝上。模型大小与碰撞参数以 Prefab 和 FBX 导入器中的取值为准。

Prefab 提供模型显示、重力和刚体碰撞。引信、伤害与爆炸表现由玩法组件承担。

`ExplosiveBarrel_Preview.png` 为 Unity 使用独立预览场景渲染的模型图。
