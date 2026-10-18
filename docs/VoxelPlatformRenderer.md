# 体素平台合批、碰撞与破碎接入

本文说明圆形石材平台的资源结构、两种平台的编辑与烘焙步骤、合批原理、运行流程和破碎脚本接法。项目结构与职责以 [Project 权威文档](../Y_MultipleAgentWorkflow/Project/Project_Guide.md) 和 [Scene 权威文档](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md) 为准，本文提供操作步骤与示例。

## 1. 优化解决什么问题

平台的开销分为持续运行和进入、退出 Play 两部分。

持续运行涉及渲染提交、顶点与像素处理、阴影、物理查询和脚本更新。把方块按材质合并可以减少 Renderer 和渲染提交，剔除完整方块间的接触面可以减少需要绘制的三角形。实际绘制次数还受到相机、材质 Pass、阴影和渲染管线影响，合并后的 Renderer 数量与最终 Draw Call 数量具有不同含义。

进入和退出 Play 涉及场景对象反序列化、组件注册、物理形状创建、脚本初始化以及对象销毁。停用的 `Fragments/Node_###` 仍属于场景对象，仍有 Transform、Renderer、Collider 等组件的数据与生命周期成本。只隐藏 Renderer 或执行静态合批，仍然保留这些对象。

平台采用三项互相配合的处理：

1. 在编辑器中生成分块表面和分块碰撞网格，把合并工作放到资源制作阶段。
2. 运行资源中的完整方块用数据表示，实际场景只保存合并后的视觉与碰撞对象。
3. 某个方块被破坏时，按该方块的数据实例化预切 Cube，再启用它的碎块。

启用运行平台时，组件读取方块数据并建立索引，复用已经保存的网格。Unity 仍需加载资源、启用 Renderer 并注册 MeshCollider，因此启动成本还与平台数量、资源规模和物理初始化有关。

## 2. 资源与层级

### 编辑用平台

资源路径：

`Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25_Authoring.prefab`

```text
Platform_Circle_R25_Authoring
  VoxelPlatformRenderer
  CenterDisk
    Cube_X_<x>_Z_<z>
      Intact
      Fragments
        Node_###
  OuterDisk
    Cube_X_<x>_Z_<z>
      Intact
      Fragments
        Node_###
```

`CenterDisk` 与 `OuterDisk` 用于编辑布局。每个 Cube 保留原始嵌套 Prefab，`Intact` 保存完整网格与 BoxCollider，默认停用的 `Fragments` 保存预切碎块与凸 MeshCollider。

四种 `Cube_` Prefab 共用预切模型，各自保存石材的外表面材质。碎块的切口使用石头材质。每个 `Node_###` 的原点位于自身的体积质心，局部位置保存装配关系。

### 运行用平台

资源路径：

`Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25.prefab`

```text
Platform_Circle_R25
  VoxelPlatformRenderer
  CombinedSurface
    PlatformSurface_<分块坐标>_<材质>
  CombinedCollision
    PlatformCollision_<分块坐标>
```

破坏方块后增加以下运行层级：

```text
Platform_Circle_R25
  FragmentsRuntime
    Fragments_<量化方块坐标>
      Intact
      Fragments
        Node_###
```

碎块实例中的 `Intact` 保持停用，`Fragments` 启用。每个实例保留原 Cube 的材质和碎块布局。平台恢复方块时销毁对应实例，平台停用时清理全部碎块实例。

场景 `Assets/Core/Scenes/MinecraftClassicPlatform.unity` 的六层平台作为独立根对象保存。每层平台具有独立的数据、表面、碰撞和碎块分组。对一层平台调用破坏接口，只更新这一层的运行状态。

第一层的中央平台保留独立方块，层级为：

```text
Platform_Circle_R25
  CenterDisk
    VoxelPlatformBakeExclusion
    Cube_X_<x>_Z_<z>
      Intact
      Fragments
        Node_###
  CombinedSurface
  CombinedCollision
```

`VoxelPlatformBakeExclusion` 是区域标记，挂在 `CenterDisk` 上。其子节点保持独立 Renderer、BoxCollider 和嵌套 Cube Prefab 引用；第一层的合并数据负责外围方块。其他层使用各自的完整烘焙布局。

## 3. 合批和邻面剔除原理

### 方块数据

烘焙记录保存共享 Mesh、Material、完整方块变换、碎块容器变换、局部中心坐标、分块坐标、启用状态、碎块 Prefab 引用与渲染配置。

完整方块变换把 `Intact` 的网格局部坐标转换到平台局部空间。碎块容器变换把 Cube 根节点转换到平台局部空间。两者分别服务于合并完整网格和放置预切 Cube，`Intact` 自身的局部偏移只应用一次。

烘焙记录中的完整方块依赖共享资源和数值数据。源场景的 Transform、MeshRenderer、GameObject 引用只属于编辑收集期间的运行记录。

### 局部坐标、空间分块与占据索引

方块中心从实际网格包围盒中心计算，经过完整方块变换进入平台局部空间。中心的三个轴都参与索引：

```text
位置键 = RoundToInt(局部中心 × PositionPrecision)
分块键 = FloorToInt(局部中心 / ChunkSize)
```

`VoxelPlatform/Rendering/VoxelPlatformGeometry.cs` 定义位置精度和分块尺度，当前分别为 `10000` 和 `8`。负坐标使用向下取整，平台左右两侧的分块边界保持一致。

占据索引只收录启用且满足遮挡条件的单位方块。位置键包含高度，所以同一水平坐标上的不同高度属于独立位置。索引通过键赋值建立，布局仍应保证每个位置只放置一个方块；同位置重复源几何会形成重叠表面。

### 六方向可见面

满足下列条件的方块参与邻面遮挡：

- 源网格具有标准单位 Cube 的顶点、索引和包围盒结构。
- 变换后的三个轴保持单位长度，并对齐平台局部坐标轴。
- 变换保持正向朝向。
- 相邻方块的中心恰好相差一个单位。

算法检查六个面，把每个面映射到平台局部空间中的邻居位置。存在启用且满足遮挡条件的邻居时，该面从表面网格中剔除。直角旋转的 Cube 通过变换后的方向寻找邻居。

六个可见面组成六位掩码。完整可见的方块复用源 Mesh，部分可见的方块按掩码生成网格变体，变体按源 Mesh 与掩码缓存。网格变体保留源顶点属性，只筛选三角形索引。

方块之间有间隙、发生重叠、具有高度差或采用其他形状时，算法根据实际接触条件保留外露面。六面都被邻居遮挡的完整方块仍保留数据与碰撞，其视觉表面为空。

### 分块内按材质合并

每个分块把启用方块按 Material 分组，使用方块变换与可见面网格执行 `Mesh.CombineMeshes`。每个有效材质组创建一个 MeshFilter 和 MeshRenderer。

一个组的 Layer、阴影和探针配置取该组的代表方块。同一分块、同一材质的源方块应采用一致的渲染设置。透明材质和需要逐物体排序的表面适合使用独立的表现方案。

空间分块约束单次破坏时的重建范围。较小分块会增加 Renderer 和 Collider 数量；较大分块会增加局部重建与碰撞重新烹制的工作量。调整尺度后，通过编辑用平台重新烘焙。

## 4. 视觉与碰撞如何对应

`CombinedSurface` 保存剔除邻面的可见网格。`CombinedCollision` 按分块合并启用方块的完整网格，每个非空分块对应一个非凸 MeshCollider。

两者使用同一批方块记录和同一组完整方块变换，所以方块位置一致。视觉网格面向绘制，碰撞网格保留完整块的封闭几何。运行平台适合作为静态环境碰撞，运动碎块使用各自的凸 MeshCollider 与破碎脚本配置的 Rigidbody。

破坏或恢复方块时，组件同时更新：

1. 方块的启用状态与占据索引。
2. 该方块所在分块的表面与碰撞。
3. 六方向相邻占据方块涉及的分块。
4. 该方块对应的碎块实例。

边界方块移除后，邻块原本被遮挡的面重新生成。即使邻块处于另一分块，也进入本次更新范围。

## 5. 代码职责

组件入口保留在 `Assets/Core/Scripts/Environment/VoxelPlatformRenderer.cs`，协作类按功能放在同目录的 `VoxelPlatform/` 子文件夹中，采用普通类组合：

| 文件 | 职责 |
|---|---|
| `VoxelPlatformRenderer.cs` | Unity 生命周期、公开接口、序列化数据入口和生成根节点 |
| `VoxelPlatform/Data/VoxelPlatformData.cs` | 运行方块、烘焙方块、表面、碰撞与分块记录 |
| `VoxelPlatform/Runtime/VoxelPlatformRuntime.cs` | 方块索引、启用状态、源对象变化检查与受影响分块调度 |
| `VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs` | 收集编辑用 Intact、计算变换和保存源渲染状态 |
| `VoxelPlatform/Baking/VoxelPlatformBakeExclusion.cs` | 标记保持独立编辑与交互的区域，几何收集和源对象清理共同跳过其子节点 |
| `VoxelPlatform/Rendering/VoxelPlatformGeometry.cs` | 单位 Cube 判定、六方向邻面判定和可见面网格缓存 |
| `VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs` | 分块视觉合并、表面烘焙对象复用及临时网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs` | 分块碰撞合并、碰撞烘焙对象复用及碰撞网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformHitResolver.cs` | 从合并碰撞的射线命中反查方块中心 |
| `VoxelPlatform/Fragments/VoxelPlatformFragments.cs` | 查找原 Cube Prefab、延迟创建碎块、应用容器变换与销毁实例 |
| `VoxelPlatform/Baking/VoxelPlatformBaking.cs` | 编辑器内生成烘焙记录与收集需要保存的网格 |

`Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs` 负责编辑器菜单、烘焙资产保存、编辑版与运行版转换和源层级移除。

运行模块围绕一个 `VoxelPlatformRenderer` 实例协作。平台入口组件承接渲染生命周期，各职责类由入口及其运行状态持有。

## 6. 编辑与烘焙步骤

### 先区分两种编辑方式

| 平台区域 | 层级中如何识别 | 逐块编辑入口 | 编辑后的保存方式 |
|---|---|---|---|
| 独立方块区域 | 分组挂有 `VoxelPlatformBakeExclusion`，下面保留 Cube、`Intact` 和 `Fragments` | 直接编辑分组中的 Cube 根节点 | 保存场景或所属编辑用 Prefab |
| 烘焙区域 | 方块由根组件的数据表示，显示和碰撞分别位于 `CombinedSurface`、`CombinedCollision` | 编辑与该区域布局对应的完整源 Cube | 保存源布局，再执行烘焙并保存运行结果 |

当前第一层采用两种方式的组合：`CenterDisk` 中央区域保留独立方块，外围区域使用烘焙数据。其他层采用各自的完整烘焙布局。`CenterDisk`、`OuterDisk` 是分组名称，实际排除范围由 `VoxelPlatformBakeExclusion` 所在的层级决定。

`CombinedSurface` 下的对象通常包含多个方块，`CombinedCollision` 下的对象表示整个分块的碰撞。逐块布局修改通过源 Cube 完成，合并对象交给烘焙器生成。

### 编辑第一层中央平台

1. 在编辑模式打开 `Assets/Core/Scenes/MinecraftClassicPlatform.unity`，展开第一层平台根节点下的 `CenterDisk`。
2. 选择某个 `Cube_X_<x>_Z_<z>` 根节点，移动、旋转、删除、复制或重新排列。整块编辑作用在 Cube 根节点上，使 `Intact` 和 `Fragments` 一起变换。
3. 增加方块时，复制已有 Cube，或把 `Assets/Core/Prefabs/MinecraftClassic/` 中的 `Cube_stone.prefab`、`Cube_stonebrick.prefab`、`Cube_stonebrick_mossy.prefab`、`Cube_stonebrick_cracked.prefab` 放到 `CenterDisk` 下。
4. 保持完整状态的 `Intact` 启用、BoxCollider 有效，`Fragments` 默认停用。更换石材时优先选用对应 Cube Prefab，使完整块与预切碎块的材质保持对应。
5. 保存场景。中央方块继续使用各自的网格与碰撞，外围沿用已经保存的烘焙结果。

单独移动 `Intact` 只改变完整网格与它的碰撞，碎块仍位于 Cube 容器原来的装配位置。只替换 `Intact` 的材质也只影响完整状态，碎块外表面材质由对应 Cube Prefab 的碎块 Renderer 保存。

`VoxelPlatformBakeExclusion` 覆盖标记节点自身及其全部子节点。方块移动到原来的圆盘范围之外，只要仍处于 `CenterDisk` 下，就继续保持独立；把它移到该层级之外时，按新的祖先标记关系判断是否参与烘焙。排除范围包含停用节点，标记组件的启停状态也保持排除效果。

几何收集、邻面遮挡和源对象清理共同跳过排除区域。中央方块保留完整网格，外围保留朝向中央区域的外露面；两者相接处的面也保持各自的渲染规则。中央方块移动后，外围继续使用原有合并网格。

第一层只剩中央排除区域的源方块时，`HasBakeSources` 返回 `false`。场景批量烘焙跳过该平台，`Rebuild()` 与组件重新启用加载已保存的外围结果。中央方块的破碎脚本直接控制各 Cube 的 `Intact`、`Fragments` 和碰撞体；中央 BoxCollider 的命中交给该 Cube 的交互脚本，烘焙平台的命中接口处理 `CombinedCollision`。

### 修改烘焙区域的布局

整体移动或旋转一层平台时，选择该层的 `VoxelPlatformRenderer` 根对象，让合并视觉、碰撞和中央独立区域一起变换，然后保存场景。保留生成子节点的相对变换，方块数据与碰撞命中定位继续使用平台局部空间。

逐块移动、挖空、增删、改高度或替换材质时，在相应的编辑用布局中修改源 Cube，再生成运行结果。公共的 `Platform_Circle_R25_Authoring.prefab` 保存公共源布局；某层具有独立的高度或排列时，其编辑用资源也需要保存这套实际布局。

公共源布局与各层的场景烘焙数据分别保存。从公共源资源制作某层的编辑布局时，把该层实际使用的抬高、位移和挖空位置同步到源 Cube，保持重新生成前后的布局一致。

烘焙以当前参与收集的完整源布局为输入，清空旧方块记录与分块记录后生成整套结果。已有烘焙记录作为运行数据保存。**重新烘焙是整体替换：源布局要包含该平台所有需要合并的方块。** 在烘焙平台下只新增几个非排除 Cube 再执行烘焙，生成结果就只包含这几个源 Cube。

### 生成平台 Prefab

1. 打开 `Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25_Authoring.prefab`，编辑源 Cube 的布局、材质和高度。
2. 制作另一套布局时，先复制成独立的 `名称_Authoring.prefab`，在该资源上编辑。其输出是同目录下的 `名称.prefab`。
3. 需要保留独立区域时，在该布局对应的分组上添加 `VoxelPlatformBakeExclusion`。为第一层制作专用源布局时，把中央分组的排除标记保存在这份布局中。
4. 保存编辑用 Prefab，再打开根节点 `VoxelPlatformRenderer` 组件右上角菜单，执行 `Bake Platform Surface`。
5. 使用生成的运行用 Prefab。编辑用资源继续保留源 Cube，运行用资源保存方块数据、合并视觉、合并碰撞与排除区域的源对象。

Prefab 模式下的菜单读取当前 Prefab 资源路径，再从磁盘加载对应的 `_Authoring.prefab`。保存编辑用资源是烘焙输入生效的前提。从运行用 Prefab 执行同一菜单时，烘焙器也优先读取与其配对的编辑用资源；配对源资源首次创建时复制调用方资源，因此首次输入也需要带有完整源 Cube。

烘焙网格写入 `Assets/Art/MinecraftClassic/Generated/`，Prefab 输出文件名包含运行资源路径的散列。同名但不同目录的运行 Prefab 对应各自的网格资产，同一运行路径再次烘焙会替换其生成网格。

### 把场景自定义布局转换成运行平台

1. 在场景中准备带有 `VoxelPlatformRenderer` 的完整源平台，按层完成布局。每层作为独立平台处理；平台属于 Prefab 实例时，它的根节点应同时是最外层 Prefab 实例根节点。
2. 在第一层的中央分组上设置 `VoxelPlatformBakeExclusion`，保留中央 Cube。其余需要合并的方块位于排除区域之外。
3. 把每层的源布局保存为对应的编辑用 Prefab，包含该层实际使用的方块位置、材质、完整层级和排除标记。场景烘焙入口直接转换场景对象，逐块编辑所用的源副本由这些编辑用资源保存。
4. 转换一层时，在该层组件菜单执行 `Bake Platform Surface`，然后保存场景。
5. 转换多层时，执行 `My_3DProject/Platform/Bake Active Scene Surfaces`。它遍历当前活动场景中仍有非排除源方块的平台，完成转换并保存整个活动场景，也保存场景中的其他编辑内容。

场景转换解包平台的最外层 Prefab 实例，为该平台保存独立的方块记录和网格资产，然后移除参与烘焙的 Cube 容器，连同其中的 `Intact`、`Fragments` 和 `Node_###` 一起清理。已经为空的 `CenterDisk`、`OuterDisk` 也被清理；排除区域保留原有源层级。

场景网格资产名由场景名和平台对象的 `GlobalObjectId` 组成。各层的烘焙数据独立保存，更新公共运行用 Prefab 与重新生成这些场景平台分别作用于各自的资源。

### 重新编辑已经烘焙的场景平台

保留现有平台根节点，可以继续沿用该层对象及其场景引用。操作顺序如下：

1. 打开与目标层实际布局对应的编辑用资源，完成逐块修改并保存。
2. 将需要合并的完整源 Cube 布局复制回现有平台根节点下。源分组和 Cube 相对平台根节点的变换要与编辑布局一致，可以先放置同变换的编辑用平台实例，再转移对应源分组。
3. 第一层保留场景现有的 `CenterDisk` 及其手工调整，只补回完整外围源布局。其他层补回全部需要合并的中央与外围源方块。参与收集的每个位置保留一个源方块。
4. 在该平台组件菜单执行 `Bake Platform Surface`，或在完整源布局全部就绪后使用场景批量菜单。
5. 保存场景，检查修改位置的表面、碰撞和破坏后的碎块装配关系。

单个平台菜单要求存在参与收集的源方块。仅有中央排除区域的第一层继续使用外围烘焙结果，场景批量菜单自动跳过它。需要更改外围时，先按上述步骤补齐外围源布局。

需要把某片已经烘焙的区域改为独立编辑时，在源布局中恢复这片区域的 Cube，为其分组设置排除标记，并用包含全部其余合并方块的布局重新烘焙。标记影响源对象收集，现有烘焙记录中的区域划分通过这次重新生成更新。

### 烘焙前的几何与显示条件

| 检查项 | 现有收集规则与编辑方法 |
|---|---|
| 源节点 | 完整网格所在 MeshFilter 的对象名为 `Intact`，Cube 容器保留 `Fragments` 与原 Prefab 引用 |
| 网格与材质 | 源 Mesh 可读，具有一个子网格；MeshRenderer 具有一个有效材质，`lightmapIndex` 为负值 |
| 完整块状态 | 启用 `Intact` 与其 Renderer，保持完整状态；收集器保存实际启用状态 |
| 方块位置 | 每个局部中心只放一个源方块，按实际高度编辑，名称中的坐标只服务于识别 |
| 邻面剔除 | 标准单位 Cube 按平台局部轴对齐并紧邻，接触面参与剔除；间隙、非单位缩放与其他朝向按实际几何条件处理 |
| 破碎装配 | 布局变换作用在 Cube 容器上，使完整块与碎块一起移动，使用对应 Cube Prefab 保存碎块材质和脚本 |
| 排除范围 | 独立区放在标记节点下，完整合并源布局位于标记范围之外 |

源几何满足这些条件时进入烘焙记录。源层级清理按 `Intact` 命名及排除范围识别容器，因此自定义方块先满足收集条件，再进行转换。项目四种源 Cube Prefab 提供与当前流程对应的结构。

烘焙区域修改完整块布局、网格、材质引用或渲染配置后，通过源布局重新烘焙。碎块行为脚本配置在源 Cube Prefab 的 `Fragments` 上，运行时创建的碎块实例从保存的 Prefab 引用取得表现逻辑，接法见第 8 节。

### 预览、保存与运行时破坏的区别

| 操作 | 作用 |
|---|---|
| `Rebuild Platform Surface` / `Rebuild()`，存在非排除源方块 | 从当前源对象生成表面和碰撞预览，保留源 Cube 供编辑 |
| `Rebuild()`，只有烘焙数据和排除区域 | 加载已有烘焙结果，中央独立方块继续使用自己的对象 |
| `Bake Platform Surface` | 把完整源布局生成并保存为运行资源，清理运行结果中参与烘焙的源层级 |
| `Bake Active Scene Surfaces` | 转换当前活动场景中具有非排除源方块的平台，并保存场景 |
| `BreakBlock` / `RestoreBlock` | 在运行状态中破坏与恢复方块，同时维护表面、碰撞和碎块 |

预览用于检查源布局，烘焙用于保存运行资源，逐块编辑的数据由编辑用布局保留。运行中的破坏与恢复修改当前平台状态，平台重新启用时加载保存的烘焙布局。

## 7. 破坏与恢复接口

命名空间为 `Core.Scripts.Environment`。

| 接口 | 使用方式 |
|---|---|
| `TryGetBlockPosition(RaycastHit, out Vector3)` | 从本平台 CombinedCollision 的命中取得平台局部方块中心 |
| `BreakBlockAtHit(RaycastHit)` | 定位命中方块并执行破坏 |
| `BreakBlock(Vector3)` | 按平台局部方块中心破坏 |
| `RestoreBlock(Vector3)` | 按同一局部中心恢复 |
| `SetBlockActive(Vector3, bool)` | 统一设置完整方块状态 |
| `RefreshChangedBlocks()` | 在源对象流程中同步 Intact 启停与变换变化 |
| `UseBakedSurface()` | 清理当前运行变化并加载烘焙状态 |
| `Rebuild()` | 根据源对象或已有烘焙数据重建当前平台状态 |

`BreakBlock`、`RestoreBlock` 和 `SetBlockActive` 的参数来自方块中心，处于平台局部空间。射线的世界空间命中点位于表面，先通过 `TryGetBlockPosition` 转换成方块中心。

`SetBlockActive` 找到方块时返回 `true`，方块已经处于目标状态也返回 `true`。游戏侧需要自行判断一次伤害是否产生新的破坏，再决定是否计分、播放一次性声音或扣除攻击次数。

### 从攻击射线接入

下面的脚本作为攻击逻辑的调用示例：

```csharp
using Core.Scripts.Environment;
using UnityEngine;

public sealed class PlatformDamageExample : MonoBehaviour
{
    [SerializeField] private LayerMask platformMask = ~0;

    public bool TryBreak(Ray ray, float maxDistance)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, platformMask,
                QueryTriggerInteraction.Ignore))
            return false;

        VoxelPlatformRenderer platform =
            hit.collider.GetComponentInParent<VoxelPlatformRenderer>();
        return platform && platform.BreakBlockAtHit(hit);
    }
}
```

LayerMask 应包含合并碰撞所在 Layer。命中 `FragmentsRuntime` 中的碎块时，伤害逻辑按碎块处理；`TryGetBlockPosition` 的定位范围是该平台的 `CombinedCollision`。

需要记录位置以便恢复时，先定位，再破坏：

```csharp
if (platform.TryGetBlockPosition(hit, out Vector3 localCenter))
{
    if (platform.BreakBlock(localCenter))
    {
        // 保存 platform 与 localCenter，在恢复逻辑中调用 RestoreBlock。
    }
}
```

恢复调用：

```csharp
platform.RestoreBlock(localCenter);
```

平台发生平移或旋转后，同一个 `localCenter` 仍标识该平台内的同一方块。外部破坏脚本通过上述接口驱动完整块状态，组件负责维护合并视觉、碰撞与碎块实例之间的一致性。

## 8. 破碎脚本如何接管碎块

破坏时的调用链为：

```text
攻击逻辑
  -> BreakBlockAtHit / BreakBlock
  -> 完整方块状态关闭
  -> 从该方块的原 Cube Prefab 创建实例
  -> 应用保存的 Cube 容器变换
  -> 关闭 Intact，启用 Fragments
  -> 重建受影响分块的表面与碰撞
```

平台模块负责预切几何的创建、装配位置和实例生命周期。刚体分组、爆炸力、断裂判定、失去支撑的连通分量、粒子与碎片寿命由破碎脚本负责。

### 在源 Cube 上配置表现

把破碎表现脚本挂在四种源 `Cube_` Prefab 的 `Fragments` 节点上，并保持该节点默认停用。平台创建实例后，先设置父节点与容器变换，再启用 `Fragments`。破碎脚本在该节点的 `OnEnable` 中取得已经就位的 `Node_###`。

Cube 根节点的 `Awake` 在实例创建时执行，适合缓存引用。依赖实际装配位置的初始化放在 `Fragments.OnEnable` 中。脚本位于源 Cube Prefab 时，延迟创建的实例自动携带该脚本。

下面的示例让每个预切碎块成为独立刚体，适合小规模散落表现：

```csharp
using UnityEngine;

public sealed class FragmentBurstExample : MonoBehaviour
{
    [SerializeField] private float outwardSpeed = 2f;
    [SerializeField] private float upwardSpeed = 1f;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        foreach (Transform node in transform)
        {
            MeshCollider collider = node.GetComponent<MeshCollider>();
            if (!collider) continue;

            Rigidbody body = node.GetComponent<Rigidbody>();
            if (!body) body = node.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;

            Vector3 outward = node.position - transform.position;
            if (outward.sqrMagnitude > 0f) outward.Normalize();
            body.AddForce(outward * outwardSpeed + Vector3.up * upwardSpeed,
                ForceMode.VelocityChange);
        }
    }
}
```

源碎块的 MeshCollider 使用凸形状。平台与碎块采用常规正缩放，物理表现配置以实际 Rigidbody 和 Collider 组合为准。

示例中的速度方向来自碎块相对容器的位置。需要定向冲击时，破碎控制器根据攻击信息计算世界空间受力。需要整组断裂时，由控制器把连通碎块交给共同的 Rigidbody，并用方块标识和 `Node_###` 标识维护映射。

碎块留在该方块的运行实例下时，`RestoreBlock` 和平台停用会连同刚体一起清理。控制器把碎块移出该实例后，需要接管这些碎块的销毁与恢复。长期驻留的残骸和对象池同样由控制器管理。

### 源对象调试流程

保留 `Intact` 的编辑布局可以通过切换完整方块状态调试邻面变化。直接切换源对象后，调用 `RefreshChangedBlocks()` 可以立即刷新，Play 中也有定时变化检查。

源对象的显示状态检查服务于表面同步。需要完整的延迟碎块创建流程时，调用 `BreakBlock` 或 `BreakBlockAtHit`。

## 9. 生命周期与性能边界

组件停用会恢复源 Renderer 的原始渲染状态，清理临时表面、临时碰撞、面变体缓存与碎块实例，并收起烘焙对象。烘焙网格作为资产保留。再次启用运行平台时加载烘焙状态。跨停用、跨场景的破坏状态由游戏状态层保存，并在平台启用后按局部方块中心重新应用。

完整运行平台具有数据记录，源对象变化轮询只扫描保留源 Cube 的流程。单次破坏仍需要更新占据索引、合并受影响分块，并给 MeshCollider 提交新网格。命中反查遍历当前平台中的启用方块。

优化大范围爆炸时，需要关注同一帧的破坏次数与重复分块重建。大量独立刚体会把开销转移到物理与碎块 Renderer，控制同时活动的碎块数量、持续时间与阴影范围可以约束这部分成本。

观察到约 60 FPS 时，先在 Profiler 中检查帧耗时来源，并同时核对垂直同步、目标帧率和显示刷新率。稳定帧率上限与 CPU、GPU 超时需要分别判断。进入 Play、退出 Play 和破坏瞬间分别采样，才能定位资源加载、脚本、网格重建与物理烹制的成本。

## 10. 验证入口

`Assets/Tests/Editor/PlatformOptimization/VoxelPlatformRendererTests.cs` 覆盖邻面剔除、旋转与高度、间隙与重叠、跨分块移动、破坏恢复、源网格保留、编辑版与运行版层级、烘焙网格复用、合并碰撞、原 Cube 引用、延迟碎块、碎块位置、碰撞命中定位、Prefab Stage 烘焙、同名资产隔离，以及独立编辑区的收集排除、清理保留和烘焙结果重新加载。

在 Unity Test Runner 的 EditMode 中选择 `VoxelPlatformRendererTests` 执行。

场景验证关注以下行为：

1. 六层平台保持独立根对象，正常启用时表面和碰撞同时有效。
2. 破坏前由合并对象显示完整平台，破坏目标块后洞口与碰撞同步变化。
3. 新建碎块保持原 Cube 的装配位置与材质，`Fragments` 上的表现脚本收到启用回调。
4. 恢复同一局部中心后，完整面和碰撞恢复，对应碎块实例被清理。
5. 停用再启用平台后恢复烘焙布局，各平台的状态独立。
6. 第一层 `CenterDisk` 的方块保持可单独编辑，外围合并网格与碰撞覆盖外围区域。
