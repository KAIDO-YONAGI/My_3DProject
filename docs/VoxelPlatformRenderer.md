# 体素平台合批、碰撞与破碎接入

本文说明圆形石材平台的资源结构、合批原理、运行流程和破碎脚本接法。项目结构与职责以 [Project 权威文档](../Y_MultipleAgentWorkflow/Project/Project_Guide.md) 和 [Scene 权威文档](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md) 为准，本文提供操作步骤与示例。

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
| `VoxelPlatform/Rendering/VoxelPlatformGeometry.cs` | 单位 Cube 判定、六方向邻面判定和可见面网格缓存 |
| `VoxelPlatform/Rendering/VoxelPlatformMeshBuilder.cs` | 分块视觉合并、表面烘焙对象复用及临时网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformCollisionBuilder.cs` | 分块碰撞合并、碰撞烘焙对象复用及碰撞网格释放 |
| `VoxelPlatform/Collision/VoxelPlatformHitResolver.cs` | 从合并碰撞的射线命中反查方块中心 |
| `VoxelPlatform/Fragments/VoxelPlatformFragments.cs` | 查找原 Cube Prefab、延迟创建碎块、应用容器变换与销毁实例 |
| `VoxelPlatform/Baking/VoxelPlatformBaking.cs` | 编辑器内生成烘焙记录与收集需要保存的网格 |

`Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs` 负责编辑器菜单、烘焙资产保存、编辑版与运行版转换和源层级移除。

运行模块围绕一个 `VoxelPlatformRenderer` 实例协作。场景只挂入口组件，各职责类由入口及其运行状态持有。

## 6. 编辑与烘焙步骤

### 修改平台 Prefab

1. 打开 `Platform_Circle_R25_Authoring.prefab`，编辑方块布局、材质和高度。
2. 保存编辑用 Prefab。Prefab 烘焙入口读取磁盘上保存的编辑版内容。
3. 在 `VoxelPlatformRenderer` 组件菜单中执行 `Bake Platform Surface`。
4. 使用生成的 `Platform_Circle_R25.prefab` 作为运行资源。

编辑用 Prefab 保存源 Cube，运行用 Prefab 保存方块数据和合并对象。烘焙网格写入 `Assets/Art/MinecraftClassic/Generated/`。Prefab 输出文件名包含路径散列，同名但不同目录的平台有各自的网格资产。

### 转换场景内的自定义布局

1. 在场景中放置保留 `Intact` 的编辑用平台，并完成各层布局。
2. 使用组件的 `Bake Platform Surface` 转换单个平台，或使用 `My_3DProject/Platform/Bake Active Scene Surfaces` 转换当前场景中的源平台。
3. 保存场景。场景批量菜单包含保存操作，单个平台的上下文菜单把场景标记为需要保存。

转换会解包平台外层 Prefab 实例，保存该层独立的烘焙网格，移除源 Cube，并清理已经为空的 `CenterDisk`、`OuterDisk`。场景布局的源数据应在编辑用资源中保留。

已经转换的场景平台具有自己的烘焙数据。更新运行用 Prefab 与更新这些独立场景平台是两项资源操作。修改独立场景布局时，从相应编辑用布局生成场景平台。

`Rebuild Platform Surface` 用于源对象编辑预览，它与保存资产的烘焙菜单具有不同职责。保留源对象时，`Rebuild()` 收集并生成预览网格；运行资源调用 `Rebuild()` 会重新加载已有烘焙状态。

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

`Assets/Tests/Editor/PlatformOptimization/VoxelPlatformRendererTests.cs` 覆盖邻面剔除、旋转与高度、间隙与重叠、跨分块移动、破坏恢复、源网格保留、编辑版与运行版层级、烘焙网格复用、合并碰撞、原 Cube 引用、延迟碎块、碎块位置、碰撞命中定位、Prefab Stage 烘焙与同名资产隔离。

在 Unity Test Runner 的 EditMode 中选择 `VoxelPlatformRendererTests` 执行。

场景验证关注以下行为：

1. 六层平台保持独立根对象，正常启用时表面和碰撞同时有效。
2. 破坏前由合并对象显示完整平台，破坏目标块后洞口与碰撞同步变化。
3. 新建碎块保持原 Cube 的装配位置与材质，`Fragments` 上的表现脚本收到启用回调。
4. 恢复同一局部中心后，完整面和碰撞恢复，对应碎块实例被清理。
5. 停用再启用平台后恢复烘焙布局，各平台的状态独立。
