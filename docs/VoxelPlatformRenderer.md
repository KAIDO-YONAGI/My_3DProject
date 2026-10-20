# 体素平台合批、碰撞与破碎

本文讲一块圆形石材平台从方块摆进场景到被炸碎的完整过程，以及过程中每一步为什么这样做。项目结构与职责以 [Project 权威文档](../Y_MultipleAgentWorkflow/Project/Project_Guide.md) 和 [Scene 权威文档](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md) 为准，本文提供平台自身的使用说明。

## 阅读指引

第一次读按 1 到 3 节顺序看，这三节建立整体画面。已经理解原理、只想知道怎么调用，直接跳到第 5、6 节。

| 想解决的问题 | 看哪一节 |
|---|---|
| 为什么不直接堆方块 | 第 1 节 |
| 一个方块从头到尾经历了什么 | 第 2 节 |
| 剔除、合并、碰撞具体怎么算 | 第 3 节 |
| 炸一次为什么只重算几个分块 | 第 4 节 |
| 游戏里怎么破坏、恢复方块 | 第 5 节 |
| 怎么整片炸、怎么接碎块表现 | 第 6 节 |
| 怎么改方块布局、重新烘焙 | 第 7 节 |
| 每次操作花多少代价 | 第 8 节 |

## 1. 为什么要做平台

### 1.1 直觉方案的问题

最直观的做法是把每个方块做成一个 GameObject：带 Transform、MeshRenderer、BoxCollider，下面再挂一组预切碎块。一块圆盘摆满方块就是几千个 GameObject，五层就是上万。

这种做法在四个环节同时变贵：

| 环节 | 成本来源 |
|---|---|
| 进入 Play | 每个对象都要反序列化、注册组件、创建物理形状 |
| 每帧渲染 | 每个 Renderer 都要单独提交绘制 |
| 可见三角形 | 相邻方块贴合的两个面完全被挡住，却照样绘制 |
| 物理查询 | 每个方块一个 Collider，查询要遍历上千个形状 |

### 1.2 平台的做法

平台把这四个环节的成本逐项消除：

```text
一个方块一个对象
  ↓ 剔除接触面
相邻方块之间 2 个面不画，一个内部方块从 12 个三角形降到 0
  ↓ 按分块按材质合并
几百个 Renderer 变成几十个 Renderer
  ↓ 按分块合并碰撞
几百个 BoxCollider 变成几十个 MeshCollider
  ↓ 烘焙成资产
合并计算在编辑器里做完，进 Play 只加载
  ↓ 方块只留数据
几千个 GameObject 变成几千条结构体记录
  ↓ 碎块按需创建
完整状态下不生成任何碎块对象
```

### 1.3 规模对比

以下是 `Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25.prefab` 的实际规模：

| 项 | 数量 |
|---|---|
| 方块记录 | 约 2000 条 |
| 分块 | 44 个 |
| 合并后的表面对象 | 164 个 |
| 合并后的碰撞对象 | 44 个 |

6 层平台放在 `Assets/Core/Scenes/VoxelArena.unity` 里，合计约 11700 条方块记录、264 个分块、约 1000 个表面对象和 264 个碰撞对象。

未合并时，同样是这 11700 个方块各自需要 Transform、Renderer、Collider，碎块节点还要再按方块倍增。

## 2. 一个方块的完整旅程

这一节是全流程主线，后面每个阶段都会展开。先建立全局印象。

```text
① 摆放
   编辑器里放下 Cube_ 节点，Intact 显示石头，Fragments 藏着碎块

② 烘焙（编辑器，一次）
   遍历所有 Intact → 算出每块的格子坐标和变换
   → 剔除相邻面 → 按分块按材质合并 → 合并碰撞
   → 图存进 Prefab，方块节点删除

③ 加载（进入 Play，每层一次）
   读 Prefab 里的方块记录 → 建索引 → 打开已合并好的网格

④ 破坏（游戏中，每次命中）
   找到方块 → 标记为已破坏 → 生成碎块 → 重算受影响的几个分块

⑤ 恢复（游戏中，按需）
   找到方块 → 标记为未破坏 → 删除碎块 → 重算受影响的几个分块

⑥ 清理（碎块超时或平台停用）
   销毁碎块 → 或整体清空运行对象
```

### 2.1 阶段①：摆放

编辑器里看到的是普通方块。每个 `Cube_` 节点是 Cube Prefab 的实例，里面有两层：

```text
Cube_X_<x>_Z_<z>
  Intact      完整网格 + BoxCollider，启用
  Fragments   16 个预切片 Node_###，默认停用
```

`Intact` 是完整状态的外观与碰撞。`Fragments` 是预切好的碎块，保持停用所以不产生开销，破坏时才打开。

这个阶段没有平台组件在起作用，摆放就是普通 Prefab 编辑。

### 2.2 阶段②：烘焙

烘焙是编辑器里执行一次的操作，把“一堆方块对象”变成“一份数据加几张合并网格”。它做六件事：

| 步骤 | 做什么 | 产物 |
|---|---|---|
| 收集 | 找到所有名为 `Intact` 的对象，记下它的网格、材质和变换 | 方块列表 |
| 落格 | 用方块中心算出格子坐标和分块坐标 | 每块一个整数键 |
| 建索引 | 把方块按格子坐标放进字典 | 占据索引 |
| 剔除 | 查六方向邻居，决定这块哪些面要画 | 每块一个六位掩码 |
| 合并 | 同一分块、同一材质的方块合成一个网格 | 每个组合一个表面网格 |
| 存档 | 方块记录写进组件字段，网格写进资产 | Prefab |

完成后源方块节点被删除，场景里只剩合并网格和一份数据。

### 2.3 阶段③：加载

进入 Play 时，平台组件读烘焙数据并重建运行状态：

```text
读方块记录 → 还原每块的矩阵、材质、格子坐标、碎块 Prefab 引用
          → 重建占据索引和分块表
          → 把已合并的表面和碰撞从隐藏改为启用
```

这一步不做任何几何计算，只是加载。这是烘焙的意义所在。

### 2.4 阶段④：破坏

玩家打中平台时发生七件事：

```text
射线命中合并碰撞
  → 反查落在哪个方块（遍历包围盒找最近的）
  → 该方块标记为已破坏，移出占据索引
  → 实例化它的碎块 Prefab，打开 Fragments，碎块飞散
  → 找到受影响的分块集合：自己所在的分块 + 六个邻居所在的分块
  → 每个受影响分块：重新剔除邻居、重新合并网格、重新合并碰撞
```

### 2.5 阶段⑤：恢复

恢复是破坏的逆操作，路径完全相同：标记为未破坏、加回占据索引、删除碎块实例、重算受影响分块。

### 2.6 阶段⑥：清理

碎块有自己的生命周期。`FragmentBurst` 到期后销毁整组碎块，刚体一起回收。平台停用则清空全部运行对象，只能在下次启用时从烘焙数据重新加载。

## 3. 三个关键机制

上面的旅程里有两处需要展开：为什么能判断“这个面被挡住了”，为什么“重算只重算几个分块”。

### 3.1 格子：把浮点坐标变成字典键

剔除和查找都需要快速回答“这个位置有没有方块”。直接比较浮点坐标不可靠，所以平台把坐标转成两个整数键：

```text
位置键 = RoundToInt(方块中心 × 10000)
分块键 = FloorToInt(方块中心 ÷ 8)
```

位置键保留到万分之一，半格布局和不同高度都落在不同键上。分块键把空间切成 8 格见方的立方块，负坐标向下取整，左右两侧边界对称。

两个键花在同一个方块上是这样的：

```text
方块中心 (0.5, 0, 2.5)
  位置键  (5000, 0, 25000)
  分块键  (0, 0, 0)

方块中心 (0.5, 0, 9.5)
  位置键  (5000, 0, 95000)
  分块键  (0, 0, 1)     ← 跨过了 z = 8 的边界，属于另一个分块
```

有了键之后，判断邻居变成一次字典查询：

```text
邻居的键 = 自己的键 + 方向 × 10000
```

### 3.2 剔除：什么样的方块能遮挡

不是所有方块都能参与剔除。平台要求同时满足四条：

1. 源网格是标准单位 Cube，24 顶点 36 索引，包围盒正好是 1×1×1 且居中。
2. 变换后的三个轴保持单位长度，并且对齐平台坐标轴。
3. 变换没有镜像。
4. 邻居中心与自己恰好相差 1 个单位。

四条都满足，才在占据索引里登记。这条规则的效果是：整齐排列的方块互相遮挡，有缝隙、有高差、做过缩放或旋转的方块保持完整表面。

判定完邻居后，每个面有一位，组成六位掩码：

```text
位 0 = 右(+X)   位 1 = 左(-X)
位 2 = 上(+Y)   位 3 = 下(-Y)
位 4 = 前(+Z)   位 5 = 后(-Z)

111111 = 63  全可见
110111 = 55  上面被挡，其余五面要画
000000 = 0   六面全被挡，这块完全不画
```

### 3.3 剔除的收益

场景里的质变发生在内部方块：

| 方块位置 | 掩码 | 三角形 |
|---|---|---|
| 孤立方块 | 63 | 12 |
| 两个相邻方块 | 各 55 | 各 10 |
| 完全埋住的内部方块 | 0 | 0，只留碰撞 |

一层实心圆盘里绝大多数方块是内部方块，掩码为 0，表面网格里根本没有它们，但碰撞网格里保留，所以站上去不会掉下去。

### 3.4 六位掩码怎么变成网格

掩码不是 63 时，需要一份剔掉面的网格。平台按“源网格 + 掩码”缓存变体：

```text
第一次遇到 (Cube_stone, 掩码 55)
  → 复制源网格
  → 逐三角形判断法线属于哪一面
  → 剔除掉被挡面的三角形
  → 保留顶点、UV、法线，只改索引
  → 存入缓存

第二次遇到同样的组合
  → 直接从缓存取，不再复制
```

一块平台里几千个方块，绝大多数共享同样的石材和同样的掩码，所以变体只需要几十份。

掩码为 63 时直接使用源网格本身，连复制都省掉。

### 3.5 合并：分块内按材质打包

有了每个方块的“带变换的变体网格”，合并就是把它们拼起来：

```text
同一个分块内
  遍历所有启用方块
    → 算出掩码，掩码为 0 的跳过
    → 按材质分进不同的组
  每个材质组
    → Mesh.CombineMeshes 一次合并
    → 得到一个 MeshFilter + MeshRenderer
```

合并后的对象数量只取决于“分块数 × 材质种类数”，与方块数量无关。这是 Renderer 从上千降到几十的原因。

每个组的阴影、探针、Layer 配置取该组的第一个方块，所以同一分块、同一材质的方块要保持渲染配置一致。

### 3.6 碰撞：为什么要另一套网格

视觉网格剔除掉了被遮挡的面。如果碰撞也用它，玩家和物理引擎会从那些洞口掉进平台内部。

所以碰撞用另一套网格：

| | 视觉网格 | 碰撞网格 |
|---|---|---|
| 输入 | 剔除后的变体 | 完整方块网格 |
| 合并范围 | 分块 × 材质 | 分块 |
| 每分块对象数 | 材质种类数 | 1 个 |
| 碰撞体形状 | 不涉及 | 非凸 MeshCollider |

两套网格用同一批方块记录和同一组变换，所以位置严格一致，视觉上的洞口和物理上的洞口同步。

### 3.7 两个分界线的意义

平台在三个维度上切了界限，这三个界限决定了后面所有的性能特性：

```text
按方块切    → 可以单块破坏
按分块切    → 破坏时只重算局部
按材质切    → 绘制批次只与材质种类有关
```

## 4. 同一帧里到底发生了什么

这一节把机制换算成实际工作量，用“炸一个方块”来检验。

### 4.1 单个方块破坏

假设玩家打中一个位于分块中心的方块：

```text
受影响分块 = 自己所在分块 (1 个)

这个分块要做的：
  1. 遍历该分块内的方块，重新算掩码
  2. 按材质重新 CombineMeshes
  3. 重新合并碰撞网格，赋给 MeshCollider
  4. MeshCollider 换网格触发物理引擎重新烹制
```

代价是**一个分块**的重建，与平台总方块数无关。这就是分块存在的意义。

### 4.2 边界上的方块

如果打中的方块贴着分块边界，情况变成：

```text
受影响分块 = 自己所在分块 + 六个邻居所在的分块

平面边界上的方块      → 3 个分块
立方体角上的方块      → 8 个分块
```

判断依据是查占据索引：六个方向上如果有邻居，就把邻居所在分块也加进重建集合。集合用 HashSet 去重，所以同一个分块不会重建两次。

### 4.3 整片爆炸

`BreakBlocks` 和 `BreakBlocksIn` 的流程刻意分成两段：

```text
第一段：把本次所有目标方块逐个标记为已破坏、生成碎块
   （不重建任何网格）

第二段：占据索引重建一次
   → 收集所有受影响分块到同一个集合
   → 集合里每个分块只重建一次
```

如果换成一个个调用单块破坏，重叠区域的分块会被重建十几次。这就是批量接口存在的意义。

### 4.4 一次破坏的完整成本清单

| 工作 | 单块 | 批量（N 块） |
|---|---|---|
| 反查方块 | 遍历启用方块包围盒 | 每块一次 |
| 碎块实例化 | 1 次 | N 次 |
| 占据索引重建 | 1 次全量 | 1 次全量 |
| 掩码重算 | 分块内方块数 | 受影响分块内方块数 |
| 网格合并 | 受影响分块数 | 受影响分块数，已去重 |
| 碰撞烹制 | 受影响分块数 | 受影响分块数，已去重 |

批量接口把后两项从“按方块累加”变成“按去重后的分块计数”。

### 4.5 分块大小是一个权衡

`ChunkSize` 当前是 8。它同时影响两端：

```text
分块变小
  → 单次重建的分块更小，重算更快
  → 分块数量变多，Renderer 和 Collider 变多，绘制批次变多

分块变大
  → 分块数量变少，批次变少
  → 单次重建要处理更多方块，碰撞烹制更久
```

调整这个值需要重新烘焙整个平台。

## 5. 用法：游戏中破坏与恢复方块

### 5.1 需要的三样东西

游戏逻辑只需要平台组件、一次射线命中、方块在平台局部空间中的中心坐标。

### 5.2 接入步骤

1. 用射线取得 `RaycastHit`，LayerMask 覆盖合并碰撞所在的 Layer。
2. 从命中的碰撞体取平台：`hit.collider.GetComponentInParent<VoxelPlatformRenderer>()`。
3. 调用 `platform.BreakBlockAtHit(hit)`，一步完成定位与破坏。
4. 需要保存破坏位置时，改用 `TryGetBlockPosition` 取得局部中心，再调用 `BreakBlock`。
5. 恢复时用同一个平台与同一个局部中心调用 `RestoreBlock`。

### 5.3 接口

命名空间 `Core.Scripts.Scene.VoxelPlatform`，组件类 `VoxelPlatformRenderer`。

| 接口 | 作用 |
|---|---|
| `BreakBlockAtHit(RaycastHit)` | 定位命中方块并破坏 |
| `BreakBlock(Vector3)` | 按平台局部方块中心破坏 |
| `BreakBlocks(IReadOnlyList<Vector3>, float? collisionDuration = null)` | 按一组局部中心批量破坏，受影响分块只重建一次 |
| `BreakBlocksIn(IReadOnlyList<VoxelPlatformBlast>, float? collisionDuration = null)` | 按一组世界空间体积破坏本平台的方块 |
| `RestoreBlock(Vector3)` | 按同一局部中心恢复 |
| `SetBlockActive(Vector3, bool)` | 统一设置方块状态，破坏与恢复共用这一处 |
| `TryGetBlockPosition(RaycastHit, out Vector3)` | 从本平台合并碰撞的命中反查方块中心 |
| `RefreshChangedBlocks()` | 同步源对象的启停与变换变化 |
| `UseBakedSurface()` | 清理当前变化并加载烘焙状态 |
| `Rebuild()` | 从源对象重建表面与碰撞，没有源对象时重载烘焙状态 |
| `HasBakeSources` | 当前层级中是否存在参与收集的源方块 |
| `HasBakedSurface` | 是否存在完整的烘焙数据 |
| `RenderedTriangleCount`、`SurfaceRendererCount`、`CollisionColliderCount` | 当前运行中的三角形数量与渲染、碰撞对象数量 |
| `DebrisLayer`、`DebrisMask` | 碎块专属层的索引与位掩码 |

### 5.4 坐标与返回值约定

- 破坏、恢复、查询的参数都是平台局部空间的方块中心。射线的世界空间命中点落在表面，先经 `TryGetBlockPosition` 换算。
- 参数按位置键精度取整，传入近似中心即可命中。
- `TryGetBlockPosition` 只接受本平台 `CombinedCollision` 下的碰撞体。命中碎块或其他碰撞体时返回 `false`。
- 找到方块就返回 `true`，方块已经处于目标状态同样返回 `true`。组件不报告状态是否真的改变，需要计分、播放一次性音效或扣除攻击次数的逻辑自行记录该方块的状态。
- `BreakBlocks` 与 `BreakBlocksIn` 返回实际从启用变为破坏状态的方块数量。
- 平台平移或旋转后，同一个局部中心仍然指向该平台中的同一方块。

### 5.5 示例

攻击逻辑的调用示例：

```csharp
using Core.Scripts.Scene.VoxelPlatform;
using UnityEngine;

public sealed class PlatformDamageExample : MonoBehaviour
{
    [SerializeField] private LayerMask platformMask = ~0;

    public bool TryBreak(Ray ray, float maxDistance)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, platformMask,
                QueryTriggerInteraction.Ignore))
            return false;

        VoxelPlatformRenderer platform = hit.collider.GetComponentInParent<VoxelPlatformRenderer>();
        return platform && platform.BreakBlockAtHit(hit);
    }
}
```

需要记录位置以便恢复时，先定位再破坏：

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

外部脚本通过上述接口驱动完整块状态，组件负责维护合并视觉、碰撞与碎块实例之间的一致性。

### 5.6 命中反查的原理与代价

射线打在合并碰撞上，物理引擎只告诉你是哪个 `MeshCollider` 和世界坐标点，不告诉你是哪个方块。反查靠遍历：

```text
命中点换到平台局部空间
  → 遍历平台所有启用方块
    → 把点换进该方块的局部空间
    → 求点到方块包围盒的距离
    → 取距离最小的那个（命中点在表面，通常只有一个距离为零）
```

代价是**与平台启用方块数成正比**，单层约 2000 次包围盒距离计算。这个成本发生在每次射击命中时，不在每帧。

## 6. 用法：整片破坏与碎块表现

### 6.1 按体积破坏

`VoxelPlatformDestruction` 是破坏体积的结算入口。调用方提交标记碰撞体或体积后立即结算，每个已启用平台只遍历一次方块、只重建一次受影响分块。

命名空间 `Core.Scripts.Scene.VoxelPlatform.Destruction`，静态类 `VoxelPlatformDestruction`。

| 接口 | 作用 |
|---|---|
| `Break(Collider marker, float? collisionDuration = null)` | 按单个标记碰撞体的世界体积破坏，返回实际破坏数量 |
| `Break(IReadOnlyList<Collider>, float? collisionDuration = null)` | 按一组标记碰撞体破坏 |
| `Break(Bounds worldBounds, float? collisionDuration = null)` | 按世界空间盒破坏 |
| `Break(Vector3 center, float radius, float? collisionDuration = null)` | 按世界空间球破坏 |
| `Break(IReadOnlyList<VoxelPlatformBlast>, float? collisionDuration = null)` | 按一组世界空间体积破坏 |

结算遍历在启用时登记过自身的全部平台。停用的平台运行数据已清空，不参与结算；已销毁的平台在下次结算时从登记表移除。

### 6.2 破坏体积怎么判

`VoxelPlatformBlast` 表示世界空间的破坏体积：

| 构造方式 | 判定形状 |
|---|---|
| 由标记碰撞体构造 `FromCollider` | 球形碰撞体按缩放后的真实半径；其余形状取世界包围盒 |
| `VoxelPlatformBlast(Bounds)` | 世界空间盒 |
| `VoxelPlatformBlast(Vector3, float)` | 世界空间球，半径为零时命中包含该点的方块 |

判定时方块一侧取旋转后的轴对齐包围盒。整数 90 度旋转时这个包围盒与方块本身完全重合，判定精确；非 90 度旋转时包围盒变大，可能多破坏边缘方块。

球体积用点到盒的距离判定，盒体积用相交判定。同时提交多份体积时，被任一体积覆盖的方块只破坏一次。

判定过程是逐个启用方块与逐个体积的两层循环，代价与“方块数 × 体积数”成正比。

### 6.3 触发器入口

`VoxelPlatformBlastTrigger` 让触发器负责开火、标记碰撞体负责划出破碎区域。玩家进入触发器时，先广播开局事件，再结算破坏、把 `platform` 的 `isKinematic` 置为 `false` 并启用 `fence`。

触发器要与静态平台产生物理事件，触发器自身或进入它的物体上需要有 Rigidbody。

| 序列化字段 | 作用 |
|---|---|
| `platform` | 进入触发器时解除运动学的平台刚体 |
| `fence` | 进入触发器时启用的围栏对象 |
| `regions` | 划出破碎区域的标记碰撞体，留空时使用子物体上的碰撞体 |
| `collisionDuration` | 本次破坏的碎块碰撞时长 |
| `oneShot` | 只结算一次，平台重建碰撞体后不会重复破坏 |
| `gameStartEventSO` | 玩家进入时广播的开局事件，留空时只做破坏结算 |

脚本经由 `VoxelPlatformDestruction.Break` 结算，不依赖每帧轮询。

### 6.4 碎块怎么产生

破坏一个方块时，平台做四件事：

```text
1. 从方块记录取出原 Cube 的 Prefab 引用
2. 实例化，命名为 Fragments_<位置键>，挂到 FragmentsRuntime 下
3. 用烘焙时保存的 Cube 容器变换摆好位置和朝向
4. 把整个实例的层级换到 Debris 层，然后关闭 Intact、打开 Fragments
```

`Fragments` 打开的瞬间，挂在它上面的 `FragmentBurst` 收到 `OnEnable`，开始处理碎块。

### 6.5 碎块的生命周期

`FragmentBurst` 挂在四种 Cube Prefab 的 `Fragments` 节点上，控制碎块的抛出、碰撞和销毁：

| 序列化字段 | 作用 |
|---|---|
| `outwardSpeed` | 每个碎块离开容器中心的速度，单位米每秒 |
| `upwardSpeed` | 附加在碎块上的向上速度 |
| `escapeTime` | 碎块飞离原方块体积前关闭碰撞的时长 |
| `lifetime` | 碎块分组从启用到销毁的时长 |

碎块碰撞的时机由三个参数决定：

| 条件 | 碎块碰撞 |
|---|---|
| 本次破坏指定了 `collisionDuration` | 生成即带碰撞，到期后关闭碰撞并保留到 `lifetime` 结束 |
| 未指定且 `escapeTime` 大于零 | 生成时关闭碰撞，`escapeTime` 后恢复碰撞 |
| 未指定且 `escapeTime` 为零 | 生成即带碰撞 |

生成时关闭碰撞的原因是：预切片初始正好填满原方块的体积，与相邻方块零距离贴面，带着碰撞生成会瞬间把碎块卡住炸飞。

`OverrideCollisionDuration` 由平台在打开 `Fragments` 之前调用，用于写入本次破坏指定的时长。

### 6.6 挂自定义表现脚本

把表现脚本挂在四种源 `Cube_` Prefab 的 `Fragments` 节点上，并保持该节点默认停用。

平台创建实例后先设置父节点与容器变换，再启用 `Fragments`，所以脚本在 `OnEnable` 中取得的 `Node_###` 已经就位。Cube 根节点的 `Awake` 在实例创建时执行，适合缓存引用；依赖装配位置的初始化放在 `Fragments.OnEnable` 中。

需要自行控制散落方向时，可以用下面的写法替换 `FragmentBurst`：

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

源碎块的 MeshCollider 使用凸形状，平台与碎块采用常规正缩放，物理表现以实际 Rigidbody 与 Collider 组合为准。

需要定向冲击时，控制器根据攻击信息计算世界空间受力；需要整组断裂时，控制器把连通碎块交给共同的 Rigidbody，并用方块标识与 `Node_###` 标识维护映射。

### 6.7 所有权与清理

碎块留在该方块的运行实例下时，`RestoreBlock` 与平台停用会连同刚体一起清理。控制器把碎块移出该实例后，接管这些碎块的销毁与恢复。长期驻留的残骸与对象池同样由控制器管理。

## 7. 用法：改布局并重新烘焙

一层的方块有两种来源：层级中保留的源 Cube，以及组件上保存的烘焙数据。改布局前先确认目标区域属于哪一种来源，再按对应方式保存。

### 7.1 判断要改的区域

| 区域 | 层级中的识别方式 | 改哪里 | 保存方式 |
|---|---|---|---|
| 独立区 | 分组上挂有 `VoxelPlatformBakeExclusion`，下方保留 Cube、`Intact`、`Fragments` | 直接改分组里的 Cube 节点 | 保存场景或所属编辑用 Prefab |
| 烘焙区 | 显示与碰撞分别位于 `CombinedSurface`、`CombinedCollision`，方块由组件数据表示 | 改与该层布局对应的源 Cube | 改完烘焙，保存运行结果 |
| 整层变换 | 平台根节点 | 移动或旋转根节点 | 保存场景 |

`VoxelArena.unity` 中，顶层平台的 `CenterDisk` 是独立区，其余区域与另外五层是烘焙区。

独立区存在的意义是保留一小片可以直接编辑、可以直接交互的方块，不必每次都走烘焙流程。它不进入合并网格，代价是这部分方块仍按独立对象渲染和碰撞。

### 7.2 改独立区方块

1. 在编辑模式打开 `Assets/Core/Scenes/VoxelArena.unity`，展开顶层平台根节点下的 `CenterDisk`。
2. 选中 `Cube_X_<x>_Z_<z>` 根节点做移动、旋转、删除或复制。整块编辑作用在 Cube 根节点上，`Intact` 与 `Fragments` 一起变换。
3. 增加方块时复制已有 Cube，或把 `Assets/Core/Prefabs/MinecraftClassic/` 下的 `Cube_stone.prefab`、`Cube_stonebrick.prefab`、`Cube_stonebrick_mossy.prefab`、`Cube_stonebrick_cracked.prefab` 放到 `CenterDisk` 下。
4. 保持 `Intact` 启用、`Fragments` 停用。更换石材时使用对应的 Cube Prefab，完整块与预切碎块的材质随之对应。
5. 保存场景。独立区不进入合并网格，重新烘焙不影响这些方块。

`VoxelPlatformBakeExclusion` 覆盖标记节点自身与全部子节点，标记组件的启停状态不改变排除范围。方块留在标记范围内就保持独立，移出标记范围后按新的祖先关系决定是否参与烘焙。

单独移动 `Intact` 会改变完整网格与它的碰撞，碎块仍停在 Cube 容器原来的装配位置。只替换 `Intact` 的材质只影响完整状态，碎块材质保存在 Cube Prefab 的 `Fragments` 上。

### 7.3 改烘焙区方块

1. 找到与目标层实际布局对应的编辑用资源。公共布局是 `Platform_Circle_R25_Authoring.prefab`；某层有独立的抬高、位移或挖空时，使用该层自己的编辑用资源。
2. 在编辑用资源里修改源 Cube 的位置、材质、启停与增删。把该层的抬高与位移同步写进源 Cube，重新生成前后的布局才一致。
3. 保存编辑用资源。
4. 执行 `Bake Platform Surface` 烘焙，保存运行结果。

烘焙以当前参与收集的完整源布局为输入，清空旧方块记录与分块记录后生成整套结果。源布局要包含该平台全部需要合并的方块，只补几个 Cube 就烘焙，生成结果只包含这几个方块。

### 7.4 整层移动与旋转

选择该层平台的 `VoxelPlatformRenderer` 根节点移动或旋转，让合并视觉、碰撞与独立区一起变换，然后保存场景。方块数据与碰撞命中定位都使用平台局部空间，根节点变换改变后接口参数不变。

### 7.5 预览布局

组件菜单 `Rebuild Platform Surface` 对应 `Rebuild()`：存在非排除源方块时，从当前源对象生成表面与碰撞预览，并隐藏源 Renderer，供检查邻面剔除与分块合并的结果。这个操作只改场景内的预览状态，不写烘焙数据。

停用再启用组件恢复源显示。已经烘焙、没有源方块的平台执行 `Rebuild()` 只重新加载烘焙状态。

### 7.6 烘焙入口

| 入口 | 适用对象 | 输入 | 输出 |
|---|---|---|---|
| 组件菜单 `Bake Platform Surface`，在场景中执行 | 场景里的平台 | 该平台的非排除源 Cube | 场景烘焙资产，清理源层级，场景标记为已修改 |
| 组件菜单 `Bake Platform Surface`，在 Prefab 编辑模式或资产上执行 | 平台 Prefab | 同目录的 `_Authoring` 源 Prefab | 运行用 Prefab，生成的网格作为子资产写入 |
| 菜单 `My_3DProject/Platform/Bake Active Scene Surfaces` | 当前活动场景的全部平台 | 存在非排除源方块的平台 | 逐个转换并保存整个活动场景 |

烘焙网格写入 `Assets/Art/MinecraftClassic/Generated/`。Prefab 的输出资产名包含运行资源路径的散列，同一路径再次烘焙替换该资产的内容；场景平台的输出资产名由场景名与平台对象的 `GlobalObjectId` 组成。

场景平台的资产名包含场景名，场景改名后重新烘焙会按新名字生成资产文件，旧文件留在目录中失去引用。

Prefab 模式下，烘焙从磁盘加载与运行 Prefab 配对的 `_Authoring` 资源，在隔离的预览场景中处理，再把结果写回运行 Prefab。配对资源不存在时，烘焙先复制调用方资源得到一份 `_Authoring` 副本，这份副本随后承担源布局的保存职责，因此它需要包含完整的源 Cube。

### 7.7 烘焙条件

| 检查项 | 收集规则 |
|---|---|
| 源节点 | 完整网格所在 MeshFilter 的对象名为 `Intact`，Cube 容器保留 `Fragments` 与原 Prefab 引用 |
| 网格与材质 | 源 Mesh 可读、只有一个子网格；MeshRenderer 只有一个有效材质，`lightmapIndex` 为负值 |
| 完整块状态 | 启用 `Intact` 与它的 Renderer，收集器按当前启用状态决定初始显示 |
| 方块位置 | 每个局部中心只放一个源方块，按实际高度摆放，名称中的坐标只用于识别 |
| 邻面剔除 | 标准单位 Cube 按平台局部轴对齐并紧邻时剔除接触面；间隙、非单位缩放与其他朝向按实际几何保留表面 |
| 破碎装配 | 布局变换作用在 Cube 容器上，完整块与碎块一起移动 |
| 排除范围 | 独立区放在标记节点下，参与合并的完整源布局位于标记范围之外 |

源几何满足这些条件才进入烘焙记录。源层级清理沿用同一套识别规则，按 `Intact` 命名与排除范围确定容器。四种 Cube Prefab 已具备这些结构，自定义方块按同样条件准备。

烘焙区改动完整块的位置、网格、材质引用或渲染配置后，通过源布局重新烘焙。碎块的表现脚本挂在源 Cube Prefab 的 `Fragments` 上，接法见第 6 节。

### 7.8 重新编辑已有场景平台

场景平台在烘焙后解包为场景对象，参与烘焙的源 Cube 已被清理。改某一层时先准备源布局，再重新烘焙：

1. 打开与目标层实际布局对应的编辑用资源，完成逐块修改并保存。
2. 把该层需要的完整源 Cube 布局复制回平台根节点下，保持源分组与 Cube 相对平台根节点的变换与编辑布局一致。
3. 顶层平台只补回外围源布局，保留场景中现有的 `CenterDisk` 与手工调整。
4. 在该平台组件的菜单执行 `Bake Platform Surface`，或等全部源布局就绪后执行场景批量菜单。
5. 保存场景，检查改动位置的表面、碰撞与碎块位置。

单平台菜单要求存在参与收集的源方块，缺失时中止并提示。只有独立区的平台由场景批量菜单跳过，继续使用已有烘焙结果。

需要把某片烘焙区域改为独立编辑时，在源布局中恢复这片区域的 Cube，为它所在分组添加排除标记，再用包含全部其余合并方块的布局重新烘焙。

### 7.9 烘焙规则

- 重新烘焙整体替换该平台的方块记录与分块记录。
- 单平台菜单与批量菜单都在播放模式中止，单平台菜单在没有源方块时中止。
- 场景烘焙先解包平台的最外层 Prefab 实例，平台根节点不是该实例根节点时中止。
- 烘焙清理跳过排除区域，`CenterDisk`、`OuterDisk` 在子节点清空后被删除。

## 8. 资源、层级与代码结构

### 8.1 编辑用平台

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

`CenterDisk` 与 `OuterDisk` 是布局分组名，子节点在烘焙后清空的分组会被删除。每个 `Cube_` 节点是 Cube Prefab 的嵌套实例，`Intact` 保存完整网格与 BoxCollider，默认停用的 `Fragments` 保存预切碎块与凸 MeshCollider，并挂有 `FragmentBurst`。

四种 `Cube_` Prefab 共用同一套预切模型，各自保存石材外表面材质；碎块切口复用 `stone` 材质。每个 `Node_###` 的局部位置保存它在 Cube 容器中的装配位置。

### 8.2 运行用平台

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

方块记录和分块记录内联序列化在 `VoxelPlatformRenderer` 上，合并网格作为平台资产路径下的子资产保存。平台因此不依赖外部资产引用就能独立加载。

破坏方块后增加以下运行层级：

```text
Platform_Circle_R25
  FragmentsRuntime
    Fragments_<位置键>
      Intact
      Fragments
        Node_###
```

碎块实例中的 `Intact` 保持停用，`Fragments` 启用。每个实例保留原 Cube 的材质与碎块布局。恢复方块时销毁对应实例，平台停用时清理全部实例。

### 8.3 在新场景中放置平台

把 `Platform_Circle_R25.prefab` 放进场景就得到一个可破坏的完整平台，烘焙数据与其网格随 Prefab 一起加载，不需要额外挂接脚本或引用资产。

多层场景按层各放一个实例。需要新的方块布局时，按第 7 节从编辑用资源生成运行用 Prefab，再替换场景中的实例。

### 8.4 场景布局

`Assets/Core/Scenes/VoxelArena.unity` 的六层平台各自是独立根对象。顶层平台 `Platform_Circle_R25` 的 `CenterDisk` 保留独立方块并挂有 `VoxelPlatformBakeExclusion`：

```text
Platform_Circle_R25
  CenterDisk
    Cube_X_<x>_Z_<z>
      Intact
      Fragments
        Node_###
  CombinedSurface
  CombinedCollision
```

标记覆盖所在节点及其全部子节点，源几何收集、邻面遮挡与烘焙清理共同跳过这些方块。顶层平台的合并数据覆盖外围方块，另外五层使用各自完整的烘焙数据。

场景中的 `GameStartTrigger` 持有 `VoxelPlatformBlastTrigger`，区域为四组 `BlastAreaBox` 与五组 `Blocks=N` 容器下的触发体。

### 8.5 代码职责

组件入口保留在 `Assets/Core/Scripts/Scene/VoxelPlatform/VoxelPlatformRenderer.cs`，协作类按功能放在同级子文件夹中，采用普通类组合：

| 文件 | 职责 |
|---|---|
| `VoxelPlatformRenderer.cs` | Unity 生命周期、公开接口、序列化数据入口、生成根节点与碎块层解析 |
| `VoxelPlatform/Data/VoxelPlatformData.cs` | 运行方块、烘焙方块、表面、碰撞与分块记录 |
| `VoxelPlatform/Runtime/VoxelPlatformRuntime.cs` | 方块索引、启用状态、批量破坏、源对象变化检查与受影响分块调度 |
| `VoxelPlatform/Baking/VoxelPlatformSourceCollector.cs` | 收集编辑用 Intact、计算变换和保存源渲染状态 |
| `VoxelPlatform/Baking/VoxelPlatformBakeExclusion.cs` | 标记保持独立编辑与交互的区域，几何收集和源对象清理共同跳过其子节点 |
| `VoxelPlatform/Rendering/VoxelPlatformGeometry.cs` | 单位 Cube 判定、六方向邻面判定和可见面网格缓存 |
| `VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs` | 分块视觉合并、表面烘焙对象复用及临时网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs` | 分块碰撞合并、碰撞烘焙对象复用及碰撞网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformHitResolver.cs` | 从合并碰撞的射线命中反查方块中心 |
| `VoxelPlatform/Fragments/VoxelPlatformFragments.cs` | 查找原 Cube Prefab、延迟创建碎块、应用容器变换、切换碎块层与销毁实例 |
| `VoxelPlatform/Fragments/FragmentBurst.cs` | 碎块刚体、抛出速度、碰撞窗口与分组生命周期 |
| `VoxelPlatform/Destruction/VoxelPlatformDestruction.cs` | 破坏体积结算入口、启用平台登记与逐平台单次结算 |
| `VoxelPlatform/Destruction/VoxelPlatformBlast.cs` | 世界空间破坏体积的构造与相交判定 |
| `VoxelPlatform/Destruction/VoxelPlatformBlastTrigger.cs` | 触发器驱动的破坏、围栏启用与开局事件广播 |
| `VoxelPlatform/Baking/VoxelPlatformBaking.cs` | 编辑器内生成烘焙记录与收集需要保存的网格 |

`Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs` 负责编辑器菜单、烘焙资产保存、编辑版与运行版转换和源层级移除。

运行模块围绕一个 `VoxelPlatformRenderer` 实例协作。组件承接渲染生命周期，各职责类由组件及其运行状态持有。运行状态按需创建，从未启用的平台不承担构造开销。

方块记录保存共享 Mesh、Material、完整方块变换、碎块容器变换、局部中心坐标、分块坐标、启用状态、碎块 Prefab 引用与渲染配置。完整方块变换把 `Intact` 的网格局部坐标转换到平台局部空间；碎块容器变换把 Cube 根节点转换到平台局部空间。两者分别服务于合并完整网格和放置预切 Cube，`Intact` 自身的局部偏移只应用一次。

烘焙记录中的完整方块依赖共享资源和数值数据。源场景的 Transform、MeshRenderer、GameObject 引用只属于编辑收集期间的运行记录。

## 9. 代价与调优

### 9.1 各类操作的成本

| 操作 | 实际工作 |
|---|---|
| 进入 Play | 读方块记录、建索引、启用已合并网格；不做几何计算 |
| 单块破坏、恢复 | 切状态、生成或删除碎块、重建该块与六个邻居所在分块 |
| 批量破坏 | 逐个切状态并生成碎块，占据索引重建一次，受影响分块去重后各重建一次 |
| 命中反查 | 遍历本平台启用方块做包围盒距离比较 |
| 单分块重建 | 掩码重算 + 网格合并 + 碰撞合并 + 碰撞烹制 |
| 源对象轮询 | 遍历方块检查变换与启停，间隔 0.05 秒；运行平台没有源组件，直接返回 |
| 平台停用 | 恢复源渲染状态，清理临时表面、临时碰撞、面变体缓存与碎块实例 |

### 9.2 场景级规模

六层平台的方块记录约一万二千条，分块约二百六十四个，表面对象约一千个，碰撞对象二百六十四个。进入 Play 时这部分成本是反序列化与索引建立，不做几何合并。

### 9.3 控制成本的做法

1. 用 `BreakBlocks` 或 `BreakBlocksIn` 提交整片破坏，同一帧的多次体积结算合并为一次分块重建。
2. 关注同一帧的破坏次数与重复分块重建，大范围爆炸集中在一次结算里提交。
3. 分块尺度决定单次重建的粒度，调整 `ChunkSize` 后通过编辑用平台重新烘焙。
4. 大量独立刚体会把开销转移到物理与碎块 Renderer，控制同时活动的碎块数量、`lifetime` 与阴影范围可以约束这部分成本。
5. 碎块统一换到 `Debris` 层，瞄准与破坏检测可以按层整体排除碎块碰撞体。
6. 命中反查是每次射击的固定成本，需要高频射击时优先用体积破坏代替逐射线反查。

帧率表现按来源判断：在 Profiler 中分别采样进入 Play、退出 Play 与破坏瞬间，同时核对垂直同步、目标帧率和显示刷新率。稳定帧率上限与 CPU、GPU 超时属于两类问题。

## 10. 验证入口

`Assets/Tests/Editor/PlatformOptimization/VoxelPlatformRendererTests.cs` 覆盖邻面剔除、旋转与高度、间隙与重叠、跨分块移动、破坏恢复、源网格保留、编辑版与运行版层级、烘焙网格复用、合并碰撞、原 Cube 引用、延迟碎块、碎块位置、碰撞命中定位、Prefab Stage 烘焙、同名资产隔离，以及独立编辑区的收集排除、清理保留和烘焙结果重新加载。

在 Unity Test Runner 的 EditMode 中选择 `VoxelPlatformRendererTests` 执行。

场景验证关注以下行为：

1. 六层平台保持独立根对象，正常启用时表面和碰撞同时有效。
2. 破坏前由合并对象显示完整平台，破坏目标块后洞口与碰撞同步变化。
3. 新建碎块保持原 Cube 的装配位置与材质，碎块上的 `FragmentBurst` 收到启用回调并抛出碎块。
4. 恢复同一局部中心后，完整面和碰撞恢复，对应碎块实例被清理。
5. 停用再启用平台后恢复烘焙布局，各平台的状态独立。
6. 顶层 `CenterDisk` 的方块保持可单独编辑，外围合并网格与碰撞覆盖外围区域。
7. 玩家进入 `GameStartTrigger` 后，开局事件广播一次，标记区域内的方块整片破坏，围栏启用。
