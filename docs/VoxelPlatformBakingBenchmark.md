# 体素平台烘焙前后性能对比

本次六平台测试中，烘焙后 `PlayerLoop` 平均标记耗时由 **5.076592 ms 降至 2.984138 ms，下降 41.2%**；URP 渲染总标记平均耗时由 **3.449742 ms 降至 1.629718 ms，下降 52.8%**。主要可观测收益集中在渲染调度、提交与剔除，同时高耗时样本明显收敛。依据：U:3、5 及 D:3、5，第 3 节。

同视角 Game View 截图中，Batches 从 **5,511 降至 989，减少 82.1%**，SetPass calls 从 **65 降至 39，减少 40.0%**，Tris 从 **296.9k 降至 271.8k，减少约 8.5%**。这组直接计数进一步支持“主要减少绘制批次和提交工作，同时降低部分几何量”的结论。依据：第 3.4 节的优化前、后截图。

按调用次数归一化后，单次物理模拟平均耗时下降 **11.8%**，粗筛下降 **49.1%**。阴影的均值下降而中位数上升，物理窄相单次耗时也有小幅上升，因此本次结果不能概括为所有环节均加速。依据：第 3.2 节、第 4 节。

实现说明见 [VoxelPlatformRenderer.md](D:/Unity/Projects/My_3DProject/docs/VoxelPlatformRenderer.md)；下文分别说明实测变化、代码机制与尚未测得的效果。

## 1. 数据与测试条件

分析日期：2026-10-22。

| 代号 | 实际读取的数据文件 | 含义 |
|---|---|---|
| U | [markerTable_un.csv](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/markerTable_un.csv) | 未烘焙 |
| D | [markerTable_do.csv](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/markerTable_do.csv) | 烘焙后 |
| S-U | [优化前 Stats 截图](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/VoxelPlatformStats_unbaked.png) | 用户第二张截图 |
| S-D | [优化后 Stats 截图](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/VoxelPlatformStats_baked.png) | 用户第一张截图 |

用户提供的两个桌面路径在本次读取时没查到，本文使用项目内上述同名 CSV。两份表均包含 `PlayerLoop` 的 300 帧样本；证据列中的 `U:行号 / D:行号` 指这两个文件的物理行号。依据：路径读取结果、U:3 / D:3。

测试条件由测试者提供：

- 场景包含六个平台。
- 两侧行为顺序一致：载入后保持稳定，在首层跑跳，再从边缘下落到底下的平台。
- 测试不涉及破坏。
- Collider 与 Rigidbody 调试绘制均关闭。

本次比较覆盖这两个采样窗口。CSV 仅有标记汇总，无法单独提取载入、稳定、跑跳、下落各阶段的耗时；相同行为顺序也不代表两个窗口的每一帧已按阶段对齐。依据：测试者说明、U:1 / D:1 的导出字段。

两张截图由测试者确认采用相同视角，Game View 均为 1920×1080，显示缩放均为 0.42x。截图用于补充该视角下的渲染计数和 Stats 当时显示的性能读数，与 CSV 的 300 帧统计分别记录。依据：S-U / S-D 的 Game View 顶栏及 Statistics 面板。

## 2. 统计口径

耗时单位统一为 **ms**。下降比例按 `(U - D) / U × 100%` 计算，上升比例按 `(D - U) / U × 100%` 计算；均使用原始数值计算后四舍五入。

| 口径 | 定义 |
|---|---|
| 平均标记耗时 | `Mean Time`，等于 `Total Time / Number of frames containing Marker` |
| 中位数 | `Median Time`，包含该标记的帧中，标记累计耗时的中位数 |
| 样本最大值 | `Max Time`，采样窗口中最大的帧内标记累计耗时 |
| 单次平均耗时 | `Total Time / Count Total`，用于按调用次数归一化比较物理工作 |
| 标记调用次数 | `Count Total`，采样窗口内的累计标记调用数 |
| 四分位距 IQR | `Time Upper Quartile - Time Lower Quartile`，衡量中间 50% 样本的离散程度 |

例如 U 中 `Physics.Simulate` 累计 36.512900 ms，出现于 268 帧，执行 283 次：`Mean Time` 为 **0.136242 ms**，单次平均为 **0.129021 ms**。两者分母不同，不能混用。依据：U:37；均值实现见 [ProfileAnalysis.cs:205](D:/Unity/Projects/My_3DProject/Library/PackageCache/com.unity.performance.profile-analyzer@1.4.2/Editor/ProfileAnalysis.cs:205)。

导出字段没有保存线程筛选与 Self Time 设置。`PlayerLoop` 两侧均在 300 帧内出现 899 次，本文只将其称为平均标记耗时，不直接等同于主线程帧时间或换算成 FPS。多个渲染标记可能存在父子包含、跨线程累计关系，下列表格中的耗时节省不相加。依据：U:3 / D:3；[导出字段与数据来源](D:/Unity/Projects/My_3DProject/Library/PackageCache/com.unity.performance.profile-analyzer@1.4.2/Editor/ProfileAnalyzerExportWindow.cs:127)。

## 3. 渲染与整体性能

### 3.1 平均耗时

下表中的标记两侧均覆盖 300 帧，采用同一 `Mean Time` 口径。

| 指标 | 未烘焙 U | 烘焙 D | 耗时下降 | 证据 |
|---|---:|---:|---:|---|
| `PlayerLoop` | 5.076592 | 2.984138 | **41.2%** | U:3 / D:3 |
| `Inl_UniversalRenderTotal`，URP 渲染总标记 | 3.449742 | 1.629718 | **52.8%** | U:5 / D:5 |
| `Inl_ScriptableRenderContext.Submit`，渲染提交 | 2.366294 | 0.891706 | **62.3%** | U:7 / D:7 |
| `RenderLoop.ScheduleDraw`，绘制调度 | 1.897031 | 0.431031 | **77.3%** | U:11 / D:12 |
| `RenderLoop.DrawSRPBatcher` | 0.920540 | 0.207079 | **77.5%** | U:16 / D:28 |
| `CullScriptable`，剔除相关 | 0.449285 | 0.188152 | **58.1%** | U:15 / D:27 |
| `SceneCulling`，场景剔除 | 0.175347 | 0.068246 | **61.1%** | U:30 / D:45 |
| `MainLightShadow`，主光阴影 | 0.716035 | 0.345989 | **51.7%** | U:17 / D:13 |
| `SRPBatcher.Flush` | 0.777064 | 0.267585 | **65.6%** | U:24 / D:19 |

`PlayerLoop` 平均少用 **2.092454 ms**，URP 渲染总标记平均少用 **1.820024 ms**。渲染相关标记是本次最明显的改善区域；这两个差值存在包含关系，不能据此精确拆分各项优化的贡献。依据：U:3、5 / D:3、5。

### 3.2 中位数与波动

| 指标 | 未烘焙 U | 烘焙 D | 变化 | 证据 |
|---|---:|---:|---|---|
| `PlayerLoop` 中位数 | 3.5856 | 2.9777 | 下降 **17.0%** | U:3 / D:3 |
| URP 渲染总标记中位数 | 1.9745 | 1.6202 | 下降 **17.9%** | U:5 / D:5 |
| 渲染提交中位数 | 1.0230 | 0.9011 | 下降 **11.9%** | U:7 / D:7 |
| `RenderLoop.DrawSRPBatcher` 中位数 | 0.3163 | 0.1813 | 下降 **42.7%** | U:16 / D:28 |
| `CullScriptable` 中位数 | 0.3363 | 0.1846 | 下降 **45.1%** | U:15 / D:27 |
| `MainLightShadow` 中位数 | 0.2875 | 0.3617 | 上升 **25.8%** | U:17 / D:13 |
| `Shadows.DrawSRPBatcher` 中位数 | 0.1458 | 0.2443 | 上升 **67.6%** | U:35 / D:21 |
| `SRPBatcher.Flush` 中位数 | 0.2288 | 0.2664 | 上升 **16.4%** | U:24 / D:19 |
| `PlayerLoop` 样本最大值 | 12.2432 | 4.0006 | 下降 **67.3%** | U:3 / D:3 |
| URP 渲染总标记样本最大值 | 10.2249 | 2.1507 | 下降 **79.0%** | U:5 / D:5 |
| `PlayerLoop` 四分位距 IQR | 4.0006 | 0.3735 | 下降 **90.7%** | U:3 / D:3 |

`PlayerLoop` 中间 50% 样本的区间从 **3.1305～7.1311 ms** 收窄至 **2.7779～3.1514 ms**。结合最大值下降，可以判断本次样本的高耗时与波动明显收敛；均值降幅明显大于中位数降幅，不能把 41.2% 解读为每帧或每个行为阶段都快了 41.2%。依据：U:3 / D:3 的均值、中位数、最大值与上下四分位数。

阴影部分体现了取舍：主光阴影平均耗时下降 51.7%，但中位数上升 25.8%；其最大值从 **2.7562 ms 降至 0.5249 ms**。本次结果支持“阴影平均开销和高耗时样本降低”，不支持“典型阴影帧也更快”。具体阶段和原因在汇总表中没查到。依据：U:17 / D:13。

### 3.3 批处理标记调用次数

| 标记 | 未烘焙 U | 烘焙 D | 次数下降 | 证据 |
|---|---:|---:|---:|---|
| `RenderLoop.DrawSRPBatcher` | 2,175 | 665 | **69.4%** | U:16 / D:28 |
| `Shadows.DrawSRPBatcher` | 4,663 | 2,192 | **53.0%** | U:35 / D:21 |
| `SRPBatcher.Flush` | 14,707 | 10,725 | **27.1%** | U:24 / D:19 |

批处理相关标记调用减少，与渲染调度和提交平均耗时下降相互印证。这些是 `Count Total`，不能把它们写成 Draw Call 数量或降幅；同视角实际 Batches 的截图对比见第 3.4 节。依据：各行字段及第 2 节、S-U / S-D。

### 3.4 同视角 Game View Stats 对比

下表按优化前、优化后排列。截图中的 k 为千数量级缩写，Tris、Verts 的比例基于显示值计算，保留约数；每项证据均为 S-U / S-D 的 Graphics 区域。

| 指标 | 未烘焙 S-U | 烘焙 S-D | 变化 |
|---|---:|---:|---|
| Batches | 5,511 | 989 | 减少 **82.1%** |
| SetPass calls | 65 | 39 | 减少 **40.0%** |
| Tris | 296.9k | 271.8k | 减少约 **8.5%** |
| Verts | 293.8k | 279.7k | 减少约 **4.8%** |
| Shadow casters | 2,886 | 547 | 减少 **81.0%** |
| CPU main | 18.5 ms | 9.3 ms | 下降 **49.7%** |
| CPU render thread | 4.9 ms | 3.1 ms | 下降 **36.7%** |
| Graphics 显示 FPS | 54.1 | 107.3 | 上升 **98.3%**，约为原来的 **1.98 倍** |

截图证据：

![优化前，同视角 Game View Statistics](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/VoxelPlatformStats_unbaked.png)

![优化后，同视角 Game View Statistics](D:/Unity/Projects/My_3DProject/docs/Unity性能分析/VoxelPlatformStats_baked.png)

字段口径采用 Unity 2022.3 Manual 的 The Rendering Statistics window：Batches 是 Unity 在该帧处理的绘制批次数；Tris、Verts 是绘制的三角形和顶点数；SetPass calls 记录渲染 Pass 切换；Shadow casters 记录投影物体数量。Graphics 给出当前 FPS；CPU main 的帧处理时间包含应用帧更新和编辑器工作，CPU render thread 记录 Game View 帧的渲染处理时间，不包含编辑器自身工作。这两张截图没有显示 GPU 耗时。官方说明：`https://docs.unity3d.com/2022.3/Documentation/Manual/RenderingStatistics.html`。

这组计数中，Batches 减少 4,522，降幅显著大于 Tris 的约 8.5%。结合分块网格合并和源层级剥离实现，结果支持本次收益主要体现在减少渲染对象管理与绘制批次，几何量下降是另一部分收益。Tris 是该视角的绘制计数，包含场景其他对象，不能据此反推出平台资产自身减少了多少三角形。依据：S-U / S-D、第 5.1～5.2 节。

Shadow casters 从 2,886 降至 547，支持“参与投影的物体数量减少”。合并后的 Renderer 继承阴影设置，这一变化与源对象合并方向一致，不能解释为统一关闭平台阴影，也不能直接换算为阴影 GPU 耗时下降 81.0%。依据：S-U / S-D、第 5.1 节。

两侧 Saved by batching 均为 0。这里的网格合并通过 `Mesh.CombineMeshes` 预先生成合并对象，是否减少绘制批次应结合 Batches 的实测变化判断；不能由 Saved by batching 为 0 推断本次合批没有效果。依据：S-U / S-D、[网格合并实现](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs:119)。

截图中显示的 FPS 接近翻倍，CPU main 与 render thread 的读数也更低。这些是两个截图时刻的显示值，持续平均 FPS 的提升幅度没查到，不作为简历中的稳定帧率成绩；简历优先使用 Batches、SetPass 的同视角计数变化，以及 CSV 的 300 帧耗时统计。依据：S-U / S-D、第 2 节。

## 4. 物理性能

| 指标 | 未烘焙 U | 烘焙 D | 变化 | 证据 |
|---|---:|---:|---|---|
| `Physics.Simulate` 累计耗时 | 36.512900 | 17.742400 | 下降 51.4% | U:37 / D:35 |
| `Physics.Simulate` 调用次数 | 283 | 156 | 下降 44.9% | U:37 / D:35 |
| 包含 `Physics.Simulate` 的帧数 | 268 | 156 | 下降 41.8% | U:37 / D:35 |
| `Physics.Simulate` 单次平均耗时 | 0.129021 | 0.113733 | 下降 **11.8%** | U:37 / D:35 |
| `PhysX.ScScene.broadPhase` 粗筛单次平均耗时 | 0.017970 | 0.009147 | 下降 **49.1%** | U:111 / D:133 |
| `PhysX.ScScene.rigidBodyNarrowPhase` 窄相单次平均耗时 | 0.002250 | 0.002347 | 上升 **4.3%** | U:255 / D:247 |

两个窗口各 300 帧，但物理模拟调用次数不同；因此累计耗时下降 51.4% 同时包含“调用变少”和“每次变便宜”两部分。以单次平均衡量，本次物理模拟改善为 11.8%，不能宣称物理算法效率提高 51.4%。调用次数差异的具体原因在 CSV 中没查到。依据：上表及 `Total Time / Count Total`。

粗筛改善明显，窄相没有同步改善。这与“减少独立 Collider、仍保留完整碰撞网格”的实现方向相符，但两份汇总表不足以证明每个物理子阶段变化的独立成因。依据：上表；[完整网格与分块碰撞构建](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs:60)。

## 5. 烘焙具体优化了什么

### 5.1 把逐方块渲染表示改成分块、按材质合并

代码在每个空间分块内按材质分组，通过 `Mesh.CombineMeshes` 生成表面对象。参与烘焙的源 Cube 随后被剥离，运行时以合并表面表示完整平台，使 Renderer 管理和绘制提交工作从逐方块转向分块与材质组。依据：[材质分组](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs:84)、[网格合并](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs:119)、[源层级剥离](D:/Unity/Projects/My_3DProject/Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs:149)。

本次剔除平均耗时下降 58.1%、渲染提交下降 62.3%，普通绘制的 SRP Batcher 标记调用减少 69.4%，与上述机制的预期方向一致。这是本次数据支持最充分的一组收益；各措施独立贡献未做隔离测试，不能分别归因。依据：第 3.1、3.3 节。

生成 Renderer 会继承源对象的投影与接收阴影设置；此处“烘焙”指平台几何和数据预生成。代码没有通过这个合并步骤统一关闭阴影，阴影收益的表述仍应保留第 3.2 节的中位数变化。依据：[渲染配置继承](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs:129)。

### 5.2 六邻域剔除内部面，减少视觉几何

代码查询六个方向的邻块，生成可见面掩码；完全遮挡的方块不生成视觉表面，其余方块根据掩码过滤三角形索引。同一源网格与掩码组合复用缓存的网格变体。依据：[邻块查询与变体缓存](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs:43)、[完全遮挡时跳过](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs:92)。

这是内部面剔除，代码保留原顶点属性并替换索引，没有实现相邻共面四边形的贪心合并。同视角截图的总绘制三角形从 296.9k 降至 271.8k，减少约 8.5%，方向与剔面机制相符；其中平台资产自身的三角形降幅和对应 GPU 耗时收益没查到。依据：[三角形过滤与网格复制](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs:71)、S-U / S-D、第 3.4 节。

### 5.3 按分块合并碰撞体，减少粗筛对象

碰撞构建将分块内活动方块的完整网格合并，每个非空分块创建一个静态非凸 MeshCollider。视觉剔面与碰撞网格分别构建，碰撞仍保留完整方块几何。依据：[碰撞构建](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs:54)。

本次粗筛单次耗时下降 49.1%、物理模拟单次耗时下降 11.8%，与减少独立 Collider 的方向一致；窄相单次耗时上升 4.3%。因此可总结为“碰撞表示合并，粗筛及整体单次模拟开销下降”，不能写成“所有碰撞阶段加速”或“碰撞三角形减少 49.1%”。依据：第 4 节及上述构建代码。

### 5.4 编辑器预计算、复用网格并剥离源层级

烘焙阶段完成源几何采集、可见面处理、网格合并，并保存方块记录与分块网格；运行时恢复索引、复用已保存网格，不重新采集源 Cube。依据：[烘焙保存](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformBaking.cs:34)、[运行时恢复](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs:37)。

源层级剥离会移除参与烘焙的 Cube 容器及其预切碎块，带排除标记的对象保留。碎块实例和 `FragmentsRuntime` 容器在首次破坏时才创建，所以未破坏平台无需保留每块 Cube 的预切碎块实例层级。依据：[剥离与排除规则](D:/Unity/Projects/My_3DProject/Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs:149)、[碎块按需创建](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Fragments/VoxelPlatformFragments.cs:22)。

这些机制把几何生成工作移到编辑器，减少运行场景需要承载的源对象层级；资源加载、索引恢复与碰撞初始化仍有成本。启动时长、对象数量和驻留内存的实际降幅在本次数据中没查到，不能由稳态标记均值代替。依据：上述保存、恢复和剥离代码；CSV 导出字段。

### 5.5 已有机制中，本次没有量化收益的部分

| 机制 | 本次能得出的结论 | 依据 |
|---|---|---|
| 无源对象时跳过变换与启停轮询 | 有短路逻辑；`VoxelPlatformRenderer.LateUpdate` 两侧中位数均为 0.0015 ms，未显示可量化下降 | [轮询短路](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs:192)，U:280 / D:277 |
| 网格变体缓存 | 能复用相同网格、掩码的结果；独立生成计时没查到 | [缓存命中](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs:61)，CSV 字段 |
| 脏分块去重、受影响区域局部重建 | 属于变化和破坏后的更新机制；本次没有破坏，不能宣称破坏性能提高了多少 | [脏分块集合](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs:16)、[统一重建](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs:188)，测试者说明 |
| 碎块延迟实例化 | 未破坏时不创建碎块实例；破坏瞬间的实例化和刚体模拟性能未测 | [按需实例化](D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Scene/VoxelPlatform/Fragments/VoxelPlatformFragments.cs:22)，测试者说明 |

## 6. GC 与结论边界

### 6.1 托管分配

| 项目 | 未烘焙 U | 烘焙 D | 下降比例 | 证据 |
|---|---:|---:|---:|---|
| `PlayerLoop` GC 累计分配 | 507,826 B | 364,023 B | 28.3% | U:3 / D:3 |
| `PlayerLoop` GC 分配次数 | 4,182 | 3,302 | 21.0% | U:3 / D:3 |
| `AutoStartClient.Update` GC 累计分配 | 156,582 B | 78,291 B | 50.0% | U:339 / D:350 |
| `LogStringToConsole` GC 累计分配 | 41,812 B | 20,906 B | 50.0% | U:13 / D:15 |

GC 总分配下降是观测事实，但网络连接与日志路径也有分配差异，不能全部归因于平台烘焙。父子标记的分配可能重复覆盖，不把这些行相加或从总量中扣除来推算“纯平台 GC”。本次简历量化成绩采用渲染与 `PlayerLoop` 耗时。依据：上表及第 2 节。

### 6.2 适用范围

- 结果是本次两个各 300 帧的编辑器 Profiler 窗口的比较。`EditorLoop` 平均值也从 13.655280 ms 降至 7.055645 ms，但包含编辑器工作，不作为游戏帧时间成绩。依据：U:2、3 / D:2、3。
- 行为阶段的独立耗时、多轮采样的误差范围、P95/P99 与完整逐帧时间序列没查到；无法据此证明不同测试轮次和行为阶段具有相同降幅。依据：U:1 / D:1。
- 同视角截图补充了 Batches、SetPass、绘制三角形和顶点、Shadow casters，以及 Stats 显示的 CPU 时间与 FPS。它们与 CSV 标记统计采用不同口径，分别作为截图计数、截图时刻读数、300 帧耗时统计记录。依据：第 2、3.4 节。
- GPU 耗时、完整场景 Renderer/Collider 数量、平台资产自身的三角形数量、加载时长和有效内存数值没查到。`GPU Frame Time`、`Total Used Memory` 的时间列均为零，这不是 GPU 开销或内存占用为零的证据；截图中的 Screen 23.7 MB 也不作为完整场景显存占用。依据：U:1、550、613 / D:1、553、616，S-U / S-D、第 3.4 节字段口径。
- 本次同时启用了多项烘焙措施，且物理调用量、网络与日志分配也有差异。因此“41.2%”是这两个窗口的整体观测降幅，各项措施的独立收益没查到。依据：第 3～6 节。

## 7. 简历用语

下列表述的实现依据见第 5 节，数值依据见第 3 节；测试范围限定为六平台、无破坏场景。耗时采用编辑器 Profiler 各 300 帧统计，Batches、SetPass 采用同视角 Game View Stats 截图。

### 完整表述

> 实现 Unity 体素平台编辑器烘焙管线，结合空间分块、按材质网格合并、六邻域内部面剔除与分块 MeshCollider，并剥离源对象层级、按需实例化碎块。在六平台无破坏场景中，同视角 Batches 从 5,511 降至 989，减少 82.1%，SetPass calls 减少 40.0%；前后各 300 帧编辑器 Profiler 统计中，PlayerLoop 平均标记耗时从 5.08 ms 降至 2.98 ms，下降 41.2%，URP 渲染总标记平均耗时下降 52.8%。

### 精简表述

> 实现 Unity 体素平台烘焙优化，通过分块合批、六邻域剔面及碰撞体合并，在六平台无破坏场景中将同视角 Batches 从 5,511 降至 989，减少 82.1%；前后各 300 帧编辑器 Profiler 统计的 PlayerLoop 平均标记耗时下降 41.2%。

面试展开可补充：同视角总绘制三角形减少约 8.5%，剔除平均耗时下降 58.1%，粗筛单次耗时下降 49.1%；并说明合并网格与碰撞表示的职责、阴影中位数的取舍，以及 Stats 计数与 Profiler 标记调用次数的区别。依据：第 3～5 节。
