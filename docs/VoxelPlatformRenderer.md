# 体素平台：优化原理与使用

平台把完整方块转换为数据和合并网格，被破坏时才创建对应的碎块。编辑布局、烘焙成品、运行状态分别承担不同职责。

## 1. 平台由什么组成

**平台根**就是挂着 `VoxelPlatformRenderer` 的 GameObject。组件保存这一层平台的数据，提供破坏和恢复接口；下面的视觉、碰撞和碎块都相对它摆放。

```text
平台根（VoxelPlatformRenderer）
├─ CombinedSurface       合并后的 MeshRenderer，按分块、材质划分
├─ CombinedCollision     合并后的 MeshCollider，按分块划分
├─ CenterDisk            第一层保留的独立方块区，带烘焙排除标记
└─ FragmentsRuntime      第一次破坏时创建，收纳碎块实例
```

平台有两种方块表示：

| 表示 | 保存什么 | 编辑方式 |
|---|---|---|
| 独立方块 | 实际的 Cube 对象，包含 `Intact` 和 `Fragments` | 在 Hierarchy 中直接修改 Cube |
| 烘焙方块 | 一条方块记录，以及合并网格中的部分几何 | 修改完整源布局，再烘焙 |

`_Authoring.prefab` 保存可逐块编辑的源布局；配对的运行 Prefab 保存烘焙结果。场景平台也可以拥有自己的烘焙结果。第一层中央的独立区通过祖先节点上的 `VoxelPlatformBakeExclusion` 保留，排除范围由层级关系决定。

组件里的 `bakedBlocks` 记录每块的位置、矩阵、资源与初始状态；`bakedChunks` 记录每个分块的表面、碰撞对象及网格引用。启用时把方块记录复制为运行状态，破坏修改副本；重新加载烘焙状态会复原布局。

代码：[平台入口与生命周期](../Assets/Core/Scripts/Scene/VoxelPlatform/VoxelPlatformRenderer.cs#L39)、[数据结构](../Assets/Core/Scripts/Scene/VoxelPlatform/Data/VoxelPlatformData.cs#L39)、[排除区域识别](../Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L20)。

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

| 优化点 | 实现与收益 |
|---|---|
| 手动合批 | 同一空间分块内，按材质调用 `Mesh.CombineMeshes`，减少完整方块的 Renderer 和绘制提交 |
| 空间分块 | 将合批范围限制在邻近方块，保留较小的剔除范围，也限制破坏后的网格重建范围 |
| 紧邻面剔除 | 用占据索引查询六个方向，隐藏被邻块遮住的面，减少提交的三角形 |
| 网格变体缓存 | 同一源 Mesh、同一六面可见掩码复用网格，避免重复生成相同变体 |
| 分块碰撞 | 每个分块合并活动方块的完整网格，减少 Collider 对象数量 |
| 编辑器烘焙 | 提前完成几何采集、剔面和合并，运行时加载已保存结果 |
| 移除源层级 | 烘焙成品以方块记录代替逐块 GameObject，也移除其预切碎块层级，减少加载与组件初始化负担 |
| 延迟生成碎块 | 只为实际破坏的方块实例化碎块；完整平台不预建碎块实例 |
| 批量归并 | 一次调用统一更新占据索引，用 `HashSet` 去重受影响分块，每个分块重建一次 |
| 跳过源对象轮询 | 无源对象的烘焙平台直接跳过逐块 Transform、启停状态检查 |

空间分块服务于**剔除与更新的局部性**：附近几何放在一起，摄像机只看到局部区域时，可以按较小的 Renderer 范围剔除；打掉一块时，也只重建附近分块。分块越大，合并越充分，单次更新范围也越大。实际绘制次数还受材质 Pass、阴影等渲染流程影响。

这里的数据表是 `List` 和 `Dictionary`，方块记录是类对象。空间分组本身没有把记录改成连续的内存布局；当前明确实现的缓存是“源网格 + 可见面掩码”的网格变体缓存。

**烘焙解决的是提前计算和减少对象加载两个问题。** 只合并网格、仍保留全部源 Cube 与碎块层级，进入 Play 时仍需加载这些对象；运行时现算合并，又会把几何处理放进启动过程。烘焙成品直接加载网格和数据，仍有资源加载、索引构建和物理初始化成本。

实现边界：剔面通过替换三角形索引完成，保留原顶点属性；视觉没有做贪心面合并。碰撞使用完整方块网格，与剔面后的视觉网格分别生成，Collider 数量减少也不等于碰撞三角形同步减少。

代码：[视觉合批](../Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs#L76)、[剔面与变体缓存](../Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L43)、[碰撞合并](../Assets/Core/Scripts/Scene/VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs#L54)、[烘焙生成](../Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformBaking.cs#L14)、[源层级清理](../Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs#L149)。

## 3. 怎样定位方块和重建

### 坐标与索引

方块矩阵将“方块网格局部坐标”转换为“平台局部坐标”。位置键取变换后网格包围盒的中心，包含高度；游戏接口接收这个**平台局部中心**。

| 数据 | 内容与用途 |
|---|---|
| `Blocks` | 全部运行方块记录，供遍历和状态管理 |
| `Occupied` | 位置键 → 活动且可遮挡邻面的方块，供邻居查询 |
| `Chunks` | 分块键 → 该分块的方块列表，供局部重建 |

位置键是 `RoundToInt(局部中心 × PositionPrecision)`；分块键是各轴分别计算 `FloorToInt(局部中心 / ChunkSize)`。前者把小数中心量化为整数键，后者确定重建范围，尺寸单位属于平台局部空间。常量取值见 [VoxelPlatformGeometry.cs](../Assets/Core/Scripts/Scene/VoxelPlatform/Rendering/VoxelPlatformGeometry.cs#L11)。

这些键都是 `Vector3Int`，与 GameObject 名称无关。烘焙方块没有逐块对象引用，通过位置键找到的是方块记录；分块级的视觉和碰撞对象由烘焙分块记录引用。按中心查询先查 `Occupied`，未命中再扫描 `Blocks`，以覆盖已破坏或不参与遮挡的方块。

代码：[采集矩阵和键](../Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L42)、[运行表与查找](../Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs#L24)。

### 一次破坏的完整调用

```text
射线命中 CombinedCollision 下的 MeshCollider
  → VoxelPlatformRenderer.TryGetBlockPosition
  → HitResolver：命中点转平台局部，再转每块的网格局部，比较包围盒距离
  → 返回命中方块的局部中心
  → BreakBlock / BreakBlocks：方块 Active=false，按需生成碎块
  → 更新 Occupied
  → 收集自身分块和六向邻块所在分块，用 HashSet 去重
  → MeshBuilder.RebuildChunk：重新剔面、按材质合并视觉、重新合并碰撞
```

**重建**指替换整个受影响分块的几何：重新遍历该分块的活动方块，生成当前应有的视觉和碰撞网格。它没有直接从旧大网格里删除某一块的三角形。移除方块会露出邻块原本隐藏的面，所以跨分块的邻居也要纳入更新。

批量破坏在**同一调用栈内同步完成**，没有“本帧提交、下一帧处理”的队列。一次批量调用内，重叠目标只破坏一次、每个受影响分块只重建一次；连续调用多个单块接口，仍会连续重建。

当前还有全量成本：射线反查遍历平台活动方块并计算逆矩阵；占据索引更新遍历全部方块；体积破坏遍历方块与提交体积。大量破坏还会同时实例化碎块、生成网格并更新 MeshCollider，合批后的静态帧率不能代表破坏帧耗时。

代码：[命中反查](../Assets/Core/Scripts/Scene/VoxelPlatform/Collision/VoxelPlatformHitResolver.cs#L11)、[批量破坏](../Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs#L170)、[受影响分块与占据索引](../Assets/Core/Scripts/Scene/VoxelPlatform/Runtime/VoxelPlatformRuntime.cs#L235)。

## 4. 怎么编辑平台和烘焙

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
| 保存 | 方块与分块记录写入组件，生成网格作为子资产写入 `Assets/Art/MinecraftClassic/Generated/` |
| 清理 | 删除参与转换的源方块及其碎块层级，清理空的圆盘分组；排除区保留 |
| 场景入口 | 直接转换场景平台，外层平台 Prefab 实例会解包；场景批量入口跳过无源平台 |
| `Rebuild Platform Surface` | 有源布局时重建当前表面供预览；无源的烘焙平台重新加载现有烘焙状态 |
| `Bake Platform Surface` | 在编辑模式生成并保存烘焙成品，Prefab 分支读取已保存的编辑资源 |

移动或旋转**平台根**，所有局部数据和子对象一起跟随，无须重烘。改变参与烘焙的单块布局，需要重新烘焙。`CombinedSurface`、`CombinedCollision` 是生成结果，直接移动它们会破坏网格与方块记录的对应关系。

代码：[Prefab 与场景烘焙入口](../Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs#L14)、[输入筛选](../Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs#L27)、[整体替换记录](../Assets/Core/Scripts/Scene/VoxelPlatform/Baking/VoxelPlatformBaking.cs#L24)。

## 5. 接入破坏与碎片表现

### 游戏逻辑调用

单块攻击先从合并碰撞的命中取得平台和局部中心。保存这两个值，后续可调用 `platform.RestoreBlock(localCenter)` 恢复：

```csharp
using Core.Scripts.Scene.VoxelPlatform;
using UnityEngine;

// 放在已有射线命中处理函数中，hit 为有效的 RaycastHit。
var platform = hit.collider.GetComponentInParent<VoxelPlatformRenderer>();
if (platform && platform.TryGetBlockPosition(hit, out Vector3 localCenter))
{
    platform.BreakBlock(localCenter);
}
```

只需要立即破坏时可用 `BreakBlockAtHit(hit)`。已知多个局部中心时，用一次 `platform.BreakBlocks(localCenters)`；已知世界空间体积时，用 `platform.BreakBlocksIn(blasts)`。

跨平台的区域破坏交给 `VoxelPlatformDestruction`，将所有标记碰撞体一次传入：

```csharp
using Core.Scripts.Scene.VoxelPlatform.Destruction;

// regions 为 Collider 数组；collisionDuration 为本次碎块保持碰撞的时长。
int broken = VoxelPlatformDestruction.Break(regions, collisionDuration);
```

该入口遍历已登记的启用平台，返回实际破坏数，也支持世界 `Bounds`、球或 `VoxelPlatformBlast` 列表。体积判定使用方块的世界轴对齐包围盒；球形标记使用缩放后的半径，其他标记取世界包围盒。

`VoxelPlatformBlastTrigger` 将触发事件接到同一入口；区域字段留空时收集子碰撞体，再退到自身碰撞体。它还承接开局事件、平台刚体与围栏行为，使用时应按当前组件职责配置引用。

代码：[公开平台接口](../Assets/Core/Scripts/Scene/VoxelPlatform/VoxelPlatformRenderer.cs#L118)、[跨平台破坏](../Assets/Core/Scripts/Scene/VoxelPlatform/Destruction/VoxelPlatformDestruction.cs#L21)、[触发器](../Assets/Core/Scripts/Scene/VoxelPlatform/Destruction/VoxelPlatformBlastTrigger.cs#L49)。

### 碎块脚本挂在哪里

在原始 Cube Prefab 中保留以下约定：

```text
Cube 根
├─ Intact                 完整方块
└─ Fragments（初始停用）   挂 FragmentBurst 或自己的破碎表现脚本
   └─ 预切碎片             网格、Renderer、凸 MeshCollider
```

破坏时，`VoxelPlatformFragments.Spawn` 实例化烘焙记录引用的 Cube Prefab，挂到 `FragmentsRuntime`，按保存的 **Cube 容器矩阵**摆位，关闭 `Intact`，最后启用 `Fragments`。因此表现脚本在 `Fragments.OnEnable` 中可以读取已就位的世界坐标，创建或接管刚体并施力。

平台负责移除完整块、更新合并视觉与碰撞，以及在恢复和平台清理时回收碎块实例；表现脚本负责碎片的运动、碰撞时序和寿命。自定义表现替换 `FragmentBurst` 时，应避免两套脚本同时控制刚体。修改同一源 Prefab 的表现可供后续实例使用；改变方块引用的碎块 Prefab 或容器变换，则通过源布局重新烘焙更新记录。

现有 `FragmentBurst` 的碰撞语义：

| 调用方式 | 碎块碰撞 |
|---|---|
| 省略 `collisionDuration` | 按 Prefab 的 `escapeTime` 先关闭碰撞，时间结束后恢复 |
| 显式传入 `collisionDuration` | 生成时有碰撞，经过指定时长后关闭 |

时长覆盖只对现有 `FragmentBurst` 生效，在启用 `Fragments` 前写入；自定义脚本需要自行接收同等参数。碎块实例递归设置为 `Debris` 层，攻击射线可用 `VoxelPlatformRenderer.DebrisMask` 排除。现有寿命逻辑销毁 `Fragments` 分组，外层 Cube 实例在恢复或平台清理时回收。

代码：[实例装配与启用顺序](../Assets/Core/Scripts/Scene/VoxelPlatform/Fragments/VoxelPlatformFragments.cs#L22)、[刚体、碰撞与寿命](../Assets/Core/Scripts/Scene/VoxelPlatform/Fragments/FragmentBurst.cs#L26)。

项目级职责见 [Project Guide](../Y_MultipleAgentWorkflow/Project/Project_Guide.md#圆形平台资源)，场景中的区域划分见 [Scene Guide](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md#minecraftclassicplatform)。
