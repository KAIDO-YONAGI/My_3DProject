# 体素平台：优化原理与使用

平台把完整方块转换为数据和合并网格，被破坏时才创建对应的碎块。编辑布局、烘焙成品、运行状态分别承担不同职责。

## 1. 平台由什么组成

**平台根**就是挂着 `VoxelPlatformRenderer` 的 GameObject。组件保存这一层平台的数据，提供破坏和恢复接口；下面的视觉、碰撞和碎块都相对它摆放。

```text
平台根（VoxelPlatformRenderer）
├─ CombinedSurface       合并后的 MeshRenderer，按分块、材质划分
├─ CombinedCollision     合并后的 MeshCollider，按分块划分
├─ CenterDisk            第一层保留的独立方块区，带烘焙排除标记
└─ FragmentsRuntime      第一次破坏时创建，收纳从纯碎块 Prefab 复用的碎块实例
```

平台有两种方块表示：

| 表示 | 保存什么 | 编辑方式 |
|---|---|---|
| 独立方块 | 实际的 Cube 对象，包含 `Intact` 和 `Fragments` | 在 Hierarchy 中直接修改 Cube |
| 烘焙方块 | 一条方块记录，以及合并网格中的部分几何 | 修改完整源布局，再烘焙 |

`_Authoring.prefab` 保存可逐块编辑的源布局；配对的运行 Prefab 保存烘焙结果。场景平台也可以拥有自己的烘焙结果。第一层中央的独立区通过祖先节点上的 `VoxelPlatformBakeExclusion` 保留，排除范围由层级关系决定。

组件里的 `bakedBlocks` 记录每块的位置、矩阵、资源与初始状态；`bakedChunks` 记录每个分块的表面、碰撞对象及网格引用。启用时把方块记录复制为运行状态，破坏修改副本；重新加载烘焙状态会复原布局。

代码：[平台入口与生命周期](../Assets/Core/Scripts/VoxelPlatform/VoxelPlatformRenderer.cs#L39)、[数据结构](../Assets/Core/Scripts/VoxelPlatform/Data/VoxelPlatformData.cs#L39)、[排除区域识别](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L20)。

## 2. 整套优化如何配合

```text
完整源布局
  → 采集每块的局部变换和资源
  → 建立占据索引，剔除紧邻面
  → 按空间分块、按材质合并视觉，按分块合并碰撞
  → 保存方块记录、生成网格，移除参与烘焙的源对象
  → Play 时复用成品
  → 破坏时创建碎块，重建受影响分块
```

| 优化点     | 实现与收益                                                     |
| ------- | --------------------------------------------------------- |
| 手动合批    | 同一空间分块内，按材质调用 `Mesh.CombineMeshes`，减少完整方块的 Renderer 和绘制提交 |
| 空间分块    | 将合批范围限制在邻近方块，保留较小的剔除范围，也限制破坏后的网格重建范围                      |
| 紧邻面剔除   | 用占据索引查询六个方向，隐藏被邻块遮住的面，减少提交的三角形                            |
| 网格变体缓存  | 同一源 Mesh、同一六面可见掩码复用网格，避免重复生成相同变体                          |
| 分块碰撞    | 每个分块合并活动方块的完整网格，减少 Collider 对象数量                          |
| 编辑器烘焙   | 提前完成几何采集、剔面和合并，运行时加载已保存结果                                 |
| 移除源层级   | 烘焙成品以方块记录代替逐块 GameObject，也移除其预切碎块层级，减少加载与组件初始化负担          |
| 延迟生成碎块  | 只为实际破坏的方块提供碎块实例；完整平台不预建碎块实例                               |
| 碎块实例复用  | 碎块按来源纯碎块 Prefab 分池，寿命结束、恢复方块与平台清理时归还；池空才实例化扩容             |
| 批量归并    | 一次调用统一更新占据索引，用 `HashSet` 去重受影响分块，每个分块重建一次                 |
| 重建复用    | 分块重建复用分组字典、实例数组与合并网格；运行时表面与碰撞对象回收进池，下次重建直接取用，避免重复创建对象与组件  |
| 跳过源对象轮询 | 无源对象的烘焙平台直接跳过逐块 Transform、启停状态检查                          |

空间分块服务于**剔除与更新的局部性**：附近几何放在一起，摄像机只看到局部区域时，可以按较小的 Renderer 范围剔除；打掉一块时，也只重建附近分块。分块越大，合并越充分，单次更新范围也越大。实际绘制次数还受材质 Pass、阴影等渲染流程影响。

这里的数据表是 `List` 和 `Dictionary`，方块记录是类对象。空间分组本身没有把记录改成连续的内存布局；当前明确实现的缓存是“源网格 + 可见面掩码”的网格变体缓存。

**烘焙解决的是提前计算和减少对象加载两个问题。** 只合并网格、仍保留全部源 Cube 与碎块层级，进入 Play 时仍需加载这些对象；运行时现算合并，又会把几何处理放进启动过程。烘焙成品直接加载网格和数据，仍有资源加载、索引构建和物理初始化成本。

实现边界：剔面通过替换三角形索引完成，保留原顶点属性；视觉没有做贪心面合并。碰撞使用完整方块网格，与剔面后的视觉网格分别生成，Collider 数量减少也不等于碰撞三角形同步减少。

代码：[视觉合批](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L90)、[剔面与变体缓存](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L85)、[碰撞合并](../Assets/Core/Scripts/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs#L107)、[烘焙生成](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformBaking.cs#L14)、[源层级清理](../Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs#L149)。

## 3. 性能参数：来源、计算与优化归属

性能数字有三种来源：Unity 渲染计数、Profiler 标记耗时、平台自建计数器。三者的统计对象不同，数值不能互相换算，也不能直接相减。下面逐项给出定义与一般计算式、平台侧的构成，以及采集这些数字的方式。实测数据见 [烘焙前后性能对比](performance/VoxelPlatformBakingBenchmark.md)。

### 3.1 渲染计数

来源是 Game View 的 Statistics 面板与 Profiler 的 Rendering 模块。两个界面读同一组引擎计数器，计数对象是该帧实际提交的绘制，已经过视锥与遮挡剔除，并包含场景里的其他对象。同视角实测一列取自 Game View 的 Statistics 截图。

| 参数 | 定义与一般计算式 | 平台侧的构成 | 同视角实测 |
|---|---|---|---|
| Batches | 一帧内处理的绘制批次总数，含静态、动态与实例批次。一般计算式：剔除后每个可见 Renderer 的每个子网格各产生一次绘制，静态、动态与实例合批把多次绘制并成一次 | 各非空分块的材质种类数之和，当前烘焙数据为 984 | 5,511 → 989 |
| Draw Calls Count | 一帧内发出的绘制调用总数，含未合批、动态合批与静态合批的调用。一般计算式：未合批的调用数 + 动态合批的调用数 + 静态合批的调用数 | 与 Batches 相同，平台不使用运行时合批 | 未记录 |
| SetPass calls | 一帧内切换着色器 Pass 的次数。一般计算式：按绘制顺序比较相邻绘制的 Pass，相同不计数，变化计一次 | 由材质种类与 Pass 数量决定，与批次数不是同一个量 | 65 → 39 |
| Tris | 一帧内处理的三角形数。一般计算式：Σ 每个绘制批次提交的索引数 ÷ 3 | 每块 2 × 可见面数，可见面数是掩码的置位数，掩码 63 为 12 | 296.9k → 271.8k |
| Verts | 一帧内处理的顶点数。一般计算式：Σ 每个绘制批次提交的顶点数 | 每块 24 个，变体只替换索引、不删顶点 | 293.8k → 279.7k |
| Shadow casters | 一帧内投射阴影的 GameObject 数。一般计算式：按投射阴影的对象计数，Profiler 的 `Shadow Casters Count` 对同一个对象的每个光源各计一次 | 表面对象数，投影开关继承源方块 | 2,886 → 547 |
| Saved by batching | 被合批合并掉的绘制调用数。一般计算式：未合批时的绘制调用数 − 实际提交的批次数 | 恒为 0，合并由 `Mesh.CombineMeshes` 在生成阶段完成 | 0 → 0 |

这些数字由引擎在提交绘制时累加，属于 `ProfilerCategory.Render` 的 Profiler 计数器。渲染线程每提交一次绘制累加批次数与绘制调用数，每次切换着色器 Pass 累加 SetPass，按提交的索引数与顶点数累加 Tris 与 Verts，按投射阴影的对象累加 Shadow casters，Game View 的 Statistics 面板与 Rendering 模块读的是同一组值。计数器按帧归零，读数是该帧的提交总量，不是资产总量。同一组计数可以用 `ProfilerRecorder` 按名称读取，Rendering 模块的图表用 `Batches Count`、`SetPass Calls Count`、`Triangles Count`、`Vertices Count`，详情面板另有 `Draw Calls Count`、`Total Batches Count`、`Shadow Casters Count`。口径依据 Unity 2022.3 Manual 的 [The Rendering Statistics window](https://docs.unity3d.com/2022.3/Documentation/Manual/RenderingStatistics.html) 与 [Rendering Profiler module](https://docs.unity3d.com/2022.3/Documentation/Manual/ProfilerRendering.html)。

按当前场景的烘焙数据逐块计算，六个平台合计 11,776 个方块、264 个非空分块、984 个表面对象、49,584 个三角形、282,624 个顶点、264 个分块碰撞体。同样这些方块若各绘制完整立方体，三角形为 141,312 个，剔面后减少 64.9%。这组数是数据总量，与 Statistics 的绘制计数口径不同；表面对象数与同视角截图的 Batches 相差 5 个，差值来自场景其他渲染对象与视锥裁剪。

代码：[材质分组与合并](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L100)、[三角形统计](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L165)、[分块碰撞](../Assets/Core/Scripts/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs#L95)。

### 3.2 标记耗时

来源是 Profiler 的标记汇总。录制时每个标记由代码区间两端的打点记录，一帧内同一标记可以被调用多次，帧内累计耗时是这些调用之和；Profiler 按帧保存样本，Profile Analyzer 把窗口内的帧样本汇总成一张表。

| 参数 | 定义与一般计算式 |
|---|---|
| 平均标记耗时 `Mean Time` | 窗口内该标记的耗时总和 `Total Time` ÷ 出现该标记的帧数 `Number of frames containing Marker` |
| 中位数 `Median Time` | 逐帧样本的中位数，即包含该标记的帧中帧内累计耗时的中位数 |
| 单次平均耗时 | 窗口内该标记的耗时总和 ÷ 调用总次数 `Count Total` |
| 样本最大值 `Max Time` | 逐帧样本中的最大值；单次调用的最大值另记在 `Time Max Individual` |
| 四分位距 IQR | `Time Upper Quartile` − `Time Lower Quartile`，中间 50% 帧的分布范围 |
| 调用次数 `Count Total` | 窗口内该标记被调用的累计次数；`Count Median`、`Count Min`、`Count Max` 是逐帧调用次数 |
| 托管分配 | `GC Alloc Total (bytes)` 与 `GC Alloc Count`，归在该标记下的分配总量与次数 |

同一帧内标记被调用多次时先求和再入库，所以逐帧累计耗时不等于逐次调用耗时，`Mean Time` 与单次平均耗时的分母也不同。上述统计量对应 Profile Analyzer 导出表的列名，单元格是窗口内逐帧样本的汇总结果；导出字段没有保存线程筛选与 Self Time 设置，同一份原始录制在不同设置下会得到不同读数。

本次采集涉及以下标记，第三列是平台侧会影响它的来源。

| 标记 | 覆盖的工作 | 平台侧的影响来源 |
|---|---|---|
| `PlayerLoop` | 一帧主线程 PlayerLoop 累计 | 全部系统的合计观测值 |
| `Inl_UniversalRenderTotal` | URP 渲染总标记 | 批次数、状态切换、剔除 |
| `Inl_ScriptableRenderContext.Submit` | 渲染提交 | 批次数 |
| `RenderLoop.ScheduleDraw` | 绘制调度 | 批次数 |
| `RenderLoop.DrawSRPBatcher` | SRP Batcher 普通绘制 | 批次调用次数 |
| `CullScriptable`、`SceneCulling` | 剔除 | Renderer 数量与包围盒范围 |
| `MainLightShadow` | 主光阴影 | Shadow casters 数量 |
| `SRPBatcher.Flush` | 常量缓冲刷新 | 批次调用次数 |
| `Physics.Simulate` | 一次物理模拟 | Collider 数量与碰撞网格 |
| `PhysX.ScScene.broadPhase` | 物理粗筛 | Collider 数量 |
| `PhysX.ScScene.rigidBodyNarrowPhase` | 物理窄相 | 碰撞三角形数量 |

以导出表为例：`PlayerLoop` 在烘焙后的窗口里 `Total Time` 为 895.2414 ms、出现于 300 帧，两者相除得 `Mean Time` 2.9841 ms；`Physics.Simulate` 在未烘焙窗口里 `Total Time` 为 36.5129 ms、出现于 268 帧、调用 283 次，`Mean Time` 为 0.1362 ms，单次平均为 0.1290 ms。同一份耗时按不同分母得到不同结论，引用时写明用的是哪一个。

标记带层级深度，导出表另有 `Min Depth` 与 `Max Depth`。`PlayerLoop` 位于深度 1，`Inl_UniversalRenderTotal` 与 `Physics.Simulate` 位于深度 3，父子标记的耗时存在包含关系，这些标记的耗时不能相加。平均标记耗时只覆盖对应标记，不是帧时间，不能换算成 FPS。

原始数据见 [markerTable_un.csv](performance/markerTable_un.csv)、[markerTable_do.csv](performance/markerTable_do.csv)。

### 3.3 平台自建计数器

平台组件在运行时直接暴露三个计数，用于核对合批与剔面的结果。

| 属性 | 计算式 | 代码 |
|---|---|---|
| `RenderedTriangleCount` | 各表面索引数除以 3 后求和；加载烘焙数据时累加序列化的 `Triangles`，清理分块时回减 | [统计与回减](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L165) |
| `SurfaceRendererCount` | 表面对象数，生成时加一，清理时减一 | [递增](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L173) |
| `CollisionColliderCount` | 非空分块的 `MeshCollider` 数 | [碰撞计数](../Assets/Core/Scripts/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs#L127) |

另有三个规模量决定内存与单次更新的上限：

- 变体网格数等于每个源网格出现过的不同掩码数，上界 62。掩码 0 在调用处跳过，掩码 63 直接返回源网格。
- 脏分块数等于自身分块与已占据邻居所在分块的并集，上限 7。
- 单次重建的变体查询次数等于分块内活动且掩码非零的方块数，上限 512，单层布局为 64。

代码：[变体缓存](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L103)、[脏分块归并](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformChunkUpdates.cs#L29)。

### 3.4 优化归属

| 参数 | 降低它的实现 | 依据 |
|---|---|---|
| Batches、SetPass calls、Shadow casters | 分块内按材质合并成单个表面对象，并剥离源方块层级 | [材质分组](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L100)、[合并](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L137)、[源层级剥离](../Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs#L152) |
| Tris | 六邻域剔面，掩码为 0 的方块不生成表面，其余按掩码过滤索引 | [可见掩码](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L85)、[变体生成](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L103) |
| 渲染提交、绘制调度、SRP Batcher 绘制与刷新 | 上述渲染参数下降的结果 | 性能文档第 3 节 |
| 剔除标记耗时 | Renderer 数量与包围盒范围随合批下降 | 性能文档第 3 节 |
| `Physics.Simulate`、粗筛 | 每个非空分块合并成一个非凸 `MeshCollider` | [分块碰撞](../Assets/Core/Scripts/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs#L76) |
| 破坏帧的重建成本 | 脏分块去重、网格变体缓存、表面与网格对象池 | [脏分块](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformChunkUpdates.cs#L29)、[对象池](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L218) |
| 冷启动的几何计算 | 编辑器烘焙提前完成采集、剔面与合并 | [烘焙入口](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformBaking.cs#L24) |
| 托管分配 | 分组字典、实例数组与合并网格按轮复用 | [复用容器](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L24) |

### 3.5 不受影响与容易混淆的部分

| 对应关系 | 实际情况 |
|---|---|
| 合批降低 Batches，同时等比例降低三角形 | 合批只减少批次。同视角 Batches 减少 82.1%，Tris 只减少约 8.5% |
| Collider 数量减少，碰撞三角形同步减少 | 碰撞使用完整方块网格，三角形总量不变。Collider 数量降到分块数量级，粗筛单次耗时下降 49.1%，窄相单次耗时上升 4.3% |
| 视觉剔面也作用于碰撞 | 视觉与碰撞分别生成，剔面只作用于视觉网格 |
| 剔面或合批降低顶点数 | 变体保留全部 24 个顶点，只替换索引；两种表示下平台的顶点总量相同。同视角 Verts 减少约 4.8%，成因在汇总数据中没有分离 |
| Saved by batching 为 0，说明合批没有生效 | 合并由 `Mesh.CombineMeshes` 在生成阶段完成，不进入运行时合批统计 |
| 收益来自静态合批或动态合批 | 两者都不参与。动态合批在 [URP 资产](../Assets/Rendering/URP_Toon_PC.asset#L64) 中关闭，平台对象也没有标记为静态，收益来自预合并网格 |
| SRP Batcher 减少批次数 | SRP Batcher 优化每个批次的常量缓冲设置，批次数不变 |
| 平均标记耗时可换算成帧时间或 FPS | 标记耗时只覆盖对应标记，渲染标记之间存在父子包含关系，不能相加，不能换算帧率 |
| 均值下降说明每一帧都更快 | 中位数可以反向。主光阴影平均耗时下降 51.7%，中位数上升 25.8% |
| 烘焙后加载与物理初始化没有成本 | 资源加载、索引构建和物理初始化仍然存在 |
| 稳态帧率代表破坏帧耗时 | 破坏帧同时实例化碎块、生成网格并重建 `MeshCollider` |
| Statistics 的计数就是平台资产量 | 计数包含场景其他对象，并经过视锥裁剪 |

## 4. 怎样定位方块和重建

### 坐标与索引

方块矩阵将“方块网格局部坐标”转换为“平台局部坐标”。位置键取变换后网格包围盒的中心，包含高度；游戏接口接收这个**平台局部中心**。

| 数据 | 内容与用途 |
|---|---|
| `VoxelPlatformBlockTable` 的分块表 | 分块键 → 该分块的方块列表，全部运行方块只经此表访问 |
| `VoxelPlatformBlockTable.Occupied` | 位置键 → 活动且可遮挡邻面的方块，供邻居查询与接触面剔除 |
| `VoxelPlatformBlockTable.MaxBlockHalfSize` | 已索引方块的最大包围盒半尺寸，破坏体积映射按它扩张 |

位置键是 `RoundToInt(局部中心 × PositionPrecision)`；分块键是各轴分别计算 `FloorToInt(局部中心 / ChunkSize)`。前者把小数中心量化为整数键，后者确定重建范围，尺寸单位属于平台局部空间。常量取值见 [VoxelPlatformGeometry.cs](../Assets/Core/Scripts/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L11)。

这些键都是 `Vector3Int`，与 GameObject 名称无关。烘焙方块没有逐块对象引用，通过位置键找到的是方块记录；分块级的视觉和碰撞对象由烘焙分块记录引用。按中心查询先查 `Occupied`，未命中再检查所属局部 chunk 的方块记录，以覆盖已破坏或不参与遮挡的方块。攻击另有世界 XZ chunk 列索引，按破坏中心邻接扩展候选。运行状态由 `VoxelPlatformRuntime` 编排，方块表、分块重建、破坏候选筛选、破坏结算和源对象同步分属 `Runtime/` 下的协作类。

代码：[采集矩阵和键](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L42)、[运行表与查找](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformBlockTable.cs#L10)。

### 命中解析与局部重建

```text
射线命中 CombinedCollision 下的 MeshCollider
  → VoxelPlatformDestruction 内部定位平台与碰撞分块
  → HitResolver：命中点转平台局部，再转每块的网格局部，比较包围盒距离
  → 返回命中方块的局部中心
  → 区域触发器的内部批量接口：方块 Active=false，从碎块池取用实例
  → 更新 Occupied
  → 收集自身分块和六向邻块所在分块，用 HashSet 去重
  → MeshBuilder.RebuildChunk：重新剔面、按材质合并视觉、重新合并碰撞
```

**重建**指替换整个受影响分块的几何：重新遍历该分块的活动方块，生成当前应有的视觉和碰撞网格。它没有直接从旧大网格里删除某一块的三角形。移除方块会露出邻块原本隐藏的面，所以跨分块的邻居也要纳入更新。

批量破坏在**同一调用栈内同步完成**，没有“本帧提交、下一帧处理”的队列。一次批量调用内，重叠目标只破坏一次、每个受影响分块只重建一次；连续调用多个单块接口，仍会连续重建。

攻击查询只检查水平圆形范围相交的世界 chunk 列及其中方块中心；破坏后的占据索引只更新状态变化方块，受影响分块的重建仍需要遍历该分块内活动方块。区域触发器按世界体积映射到局部 chunk 范围。大量破坏还会同时实例化碎块、生成网格并更新 MeshCollider，合批后的静态帧率不能代表破坏帧耗时。

代码：[命中反查](../Assets/Core/Scripts/VoxelPlatform/Collision/VoxelPlatformHitResolver.cs#L11)、[批量破坏](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs#L123)、[破坏结算](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformBlockDestroyer.cs#L25)、[受影响分块与占据索引](../Assets/Core/Scripts/VoxelPlatform/Runtime/VoxelPlatformChunkUpdates.cs#L29)。

## 5. 怎么编辑平台和烘焙

### 独立平台区域：直接编辑

第一层中央平台的 `CenterDisk` 带有 `VoxelPlatformBakeExclusion`，其中的方块保留独立对象。

1. 在 Hierarchy 中选择 `Cube_...` 容器，移动、旋转、复制或删除整块。
2. 修改材质、完整块或碎块表现时，编辑该 Cube 或其源 Prefab。
3. 保存场景或所属编辑资源。区域继续由独立方块的 Renderer、Collider 和交互脚本负责。

整块变换作用在 Cube 容器上，使 `Intact` 和 `Fragments` 一起移动。方块只要仍处于排除标记的子层级，就继续保持独立；平台合并破坏接口只处理参与合并的方块。

### 烘焙平台区域：改源布局后生成

**Prefab 平台：**

1. 打开配对的 `_Authoring.prefab`，修改源 Cube，并保存。
2. 在平台组件菜单执行 `Bake Platform Surface`。
3. 烘焙器读取已保存的 `_Authoring`，覆盖配对运行 Prefab，保留编辑源。场景中的独立烘焙平台使用自己的场景烘焙入口更新。

**已经独立烘焙到场景的某层平台：**

1. 找到该层平台根，在其下面补回该层**完整的合并源布局**，再逐块编辑；完整源布局应另存为编辑资源。
2. 第一层重做外围时，保留现有带排除标记的中央区域。
3. 执行组件的 `Bake Platform Surface`，保存场景；多层一起处理可用 `My_3DProject/Platform/Bake Active Scene Surfaces`。

每次烘焙会整体替换该平台的方块和分块记录，因此源布局必须包含全部需要合并的方块。只补回少数修改块，其他旧烘焙块会从结果中消失。源对象已被剥离时，现有工具需要编辑源才能恢复逐块编辑，未提供从合并网格还原源 Cube 的入口。

### 烘焙规则

| 项目 | 行为 |
|---|---|
| 输入 | 名为 `Intact` 的 MeshFilter；网格可读、单子网格、单有效材质、未使用烘焙光照，且不在排除标记下 |
| 保存 | 方块与分块记录写入组件，生成网格作为子资产写入 `Assets/Materials/MinecraftClassic/Generated/` |
| 清理 | 删除参与转换的源方块及其碎块层级，清理空的圆盘分组；排除区保留 |
| 场景入口 | 直接转换场景平台，外层平台 Prefab 实例会解包；场景批量入口跳过无源平台 |
| `Rebuild Platform Surface` | 有源布局时重建当前表面供预览；无源的烘焙平台重新加载现有烘焙状态 |
| `Bake Platform Surface` | 在编辑模式生成并保存烘焙成品，Prefab 分支读取已保存的编辑资源 |

移动或旋转**平台根**，所有局部数据和子对象一起跟随，无须重烘。改变参与烘焙的单块布局，需要重新烘焙。`CombinedSurface`、`CombinedCollision` 是生成结果，直接移动它们会破坏网格与方块记录的对应关系。

代码：[Prefab 与场景烘焙入口](../Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs#L14)、[输入筛选](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L27)、[整体替换记录](../Assets/Core/Scripts/VoxelPlatform/Baking/VoxelPlatformBaking.cs#L24)。

## 6. 接入破坏与碎块表现

### 攻击入口

攻击服务由常驻场景中的 `VoxelPlatformDestruction` 单例提供，`DestroyAt` 与 `DestroyFromRay` 两个入口均返回 `AttackResult`。`DestroyAt` 接收平台局部位置、世界水平半径与平台 ID，内部转换为世界中心后结算该平台；`DestroyFromRay` 在服务内部完成射线过滤、最近有效平台选择和命中方块解析，返回世界中心、平台局部位置与平台 ID。半径是世界空间 XZ 水平半径，不设高度上限；候选从中心所在的世界 chunk 列向相邻列扩展，再按方块中心的水平距离精确筛选。

```csharp
using Core.Scripts.VoxelPlatform.Destruction;
using UnityEngine;

AttackResult atPosition = VoxelPlatformDestruction.Instance.DestroyAt(
    platformLocalPoint, destructionRadius, platformId);
AttackResult fromRay = VoxelPlatformDestruction.Instance.DestroyFromRay(
    ray, maxDistance, hitLayers, playerRoot, destructionRadius);
if (fromRay.HitPoint is Vector3 hitPoint)
{
    // hitPoint 可用于本地特效锚定。
}
```

`DestroyAt` 返回输入局部位置对应的世界中心；`DestroyFromRay` 未命中有效平台时命中点为空、平台 ID 为零、破坏数量为零。`AttackResult.Radius` 返回本次实际使用的水平半径，`PlatformLocalPosition` 供网络请求复用。

### 区域触发器调用

独立区域触发器将所有标记碰撞体一次传入 `DestroyInRegions`：

```csharp
using Core.Scripts.VoxelPlatform.Destruction;

// regions 为 Collider 数组；collisionDuration 为本次碎块保持碰撞的时长。
int destroyed = VoxelPlatformDestruction.Instance.DestroyInRegions(regions, collisionDuration);
```

区域入口按标记 Collider 的世界包围盒或球形范围只结算与体积重叠的注册平台，返回实际破坏数；它与两个攻击入口分开。

`VoxelPlatformDestructionTrigger` 将触发事件接到同一入口；区域字段留空时收集子碰撞体，再退到自身碰撞体。它还承接开局事件、平台刚体与围栏行为，使用时应按当前组件职责配置引用。

代码：[平台局部方块接口](../Assets/Core/Scripts/VoxelPlatform/VoxelPlatformRenderer.cs#L118)、[破坏与区域结算](../Assets/Core/Scripts/VoxelPlatform/Destruction/VoxelPlatformDestruction.cs#L21)、[触发器](../Assets/Core/Scripts/VoxelPlatform/Destruction/VoxelPlatformDestructionTrigger.cs#L49)。

### 碎块 Prefab 与脚本

原始 Cube Prefab 保留完整方块与碎块两级：

```text
Cube 根
├─ Intact                 完整方块
└─ Fragments（初始停用）   挂 FragmentBurst
   └─ 预切碎块             网格、Renderer、凸 MeshCollider
```

`Assets/Core/Prefabs/MinecraftClassic/` 中与每个 `Cube_<材质名>` 配对的 `Fragments_<材质名>.prefab` 是纯碎块 Prefab：沿用 Cube 容器根节点、嵌套 `Fragments` 节点与各材质覆盖，移除 `Intact` 及其渲染与碰撞组件。烘焙记录里的 `FragmentPrefab` 指向纯碎块 Prefab；`VoxelPlatformFragments.FindPrefab` 按 `Cube_` 前缀换成 `Fragments_` 资源，资源缺失时退回原 Cube。

破坏时，`VoxelPlatformFragments.Spawn` 按来源 Prefab 从闲置池取实例，池空时实例化新对象完成扩容；实例挂到 `FragmentsRuntime`，按保存的 **Cube 容器矩阵**摆位，最后启用 `Fragments`。因此表现脚本在 `Fragments.OnEnable` 中可以读取已就位的世界坐标，创建或接管刚体并施力。

平台负责移除完整块、更新合并视觉与碰撞，并维护碎块实例池：寿命结束、恢复方块和平台清理时把实例停用并归还对应 Prefab 的池，池按来源 Prefab 各留 16 个闲置实例，超出后销毁；平台清理会销毁整池，场景里不残留运行时碎块对象。表现脚本负责碎块的运动、碰撞时序和寿命。`FragmentBurst` 在寿命结束时通过 `Bind` 绑定的回收入口交还实例，不再销毁自身；再次启用时按首次记录的局部姿态复位预切片，并清掉上一轮刚体速度与延迟计时，平台在启用前写入本次的单次碰撞时长。自定义表现替换 `FragmentBurst` 时，需要接入同一回收约定，否则实例不回池。修改同一源 Prefab 的表现可供后续实例使用；改变方块引用的碎块 Prefab 或容器变换，则通过源布局重新烘焙更新记录。

现有 `FragmentBurst` 的碰撞语义：

| 调用方式 | 碎块碰撞 |
|---|---|
| 省略 `collisionDuration` | 按 Prefab 的 `escapeTime` 先关闭碰撞，时间结束后恢复 |
| 显式传入 `collisionDuration` | 生成时有碰撞，经过指定时长后关闭 |

时长覆盖只对现有 `FragmentBurst` 生效，在启用 `Fragments` 前写入；自定义脚本需要自行接收同等参数。碎块实例递归设置为 `Fragment` 层，攻击射线可用 `VoxelPlatformRenderer.FragmentMask` 排除。

代码：[实例装配、池与纯碎块 Prefab 解析](../Assets/Core/Scripts/VoxelPlatform/Fragments/VoxelPlatformFragments.cs#L43)、[刚体、寿命、回收与复用复位](../Assets/Core/Scripts/VoxelPlatform/Fragments/FragmentBurst.cs#L38)。

项目级职责见 [Project Guide](../Y_MultipleAgentWorkflow/Project/Project_Guide.md#圆形平台资源)，场景中的区域划分见 [Scene Guide](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md#minecraftclassicplatform)。
