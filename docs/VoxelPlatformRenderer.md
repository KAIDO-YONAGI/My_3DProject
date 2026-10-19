# 体素平台合批、碰撞与破碎接入

本文说明圆形石材平台的资源结构、三种接入用法和内部机制。项目结构与职责以 [Project 权威文档](../Y_MultipleAgentWorkflow/Project/Project_Guide.md) 和 [Scene 权威文档](../Y_MultipleAgentWorkflow/Scene/Scene_Guide.md) 为准，本文提供平台自身的使用说明。

| 要做的操作 | 对应章节 |
|---|---|
| 游戏里破坏、恢复方块 | 第 3 节 |
| 改方块布局、重新烘焙 | 第 4 节 |
| 给碎块挂表现脚本 | 第 5 节 |
| 查合批与剔除原理 | 第 6 节 |

## 1. 平台解决什么开销

平台的开销分为持续运行和进入、退出 Play 两部分。

持续运行涉及渲染提交、顶点与像素处理、阴影、物理查询和脚本更新。把方块按材质合并可以减少 Renderer 与渲染提交，剔除完整方块之间的接触面可以减少需要绘制的三角形。实际绘制次数还受相机、材质 Pass、阴影和渲染管线影响，合并后的 Renderer 数量与最终 Draw Call 数量是两个不同的量。

进入和退出 Play 涉及场景对象反序列化、组件注册、物理形状创建、脚本初始化与对象销毁。停用的 `Fragments/Node_###` 仍然属于场景对象，仍有 Transform、Renderer、Collider 的数据与生命周期成本。只隐藏 Renderer 或执行静态合批，这些对象仍然保留。

平台用三项互相配合的处理承担这部分开销：

1. 在编辑器中生成分块表面与分块碰撞网格，把合并工作放在资源制作阶段。
2. 运行资源中的完整方块用数据表示，场景只保存合并后的视觉与碰撞对象。
3. 某个方块被破坏时，按该方块的数据实例化预切 Cube，再启用它的碎块。

启用运行平台时，组件读取方块数据并建立索引，复用已经保存的网格。Unity 仍需加载资源、启用 Renderer 并注册 MeshCollider，启动成本与平台数量、资源规模和物理初始化相关。

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

`CenterDisk` 与 `OuterDisk` 是布局分组名，子节点在烘焙后清空的分组会被删除。每个 `Cube_` 节点是 Cube Prefab 的嵌套实例，`Intact` 保存完整网格与 BoxCollider，默认停用的 `Fragments` 保存预切碎块与凸 MeshCollider。

四种 `Cube_` Prefab 共用同一套预切模型，各自保存石材外表面材质；碎块切口复用 `stone` 材质。每个 `Node_###` 的局部位置保存它在 Cube 容器中的装配位置。

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
    Fragments_<位置键>
      Intact
      Fragments
        Node_###
```

碎块实例中的 `Intact` 保持停用，`Fragments` 启用。每个实例保留原 Cube 的材质与碎块布局。恢复方块时销毁对应实例，平台停用时清理全部实例。

### 在新场景中放置平台

把 `Platform_Circle_R25.prefab` 放进场景就得到一个可破坏的完整平台，烘焙数据与其网格随 Prefab 一起加载，不需要额外挂接脚本或引用资产。

多层场景按层各放一个实例。每层平台拥有自己的方块数据与碎块分组，破坏一层不影响其他层。

需要新的方块布局时，按第 4 节从编辑用资源生成运行用 Prefab，再替换场景中的实例。

### 场景布局

`Assets/Core/Scenes/VoxelArena.unity` 的六层平台各自是独立根对象，每层拥有自己的方块数据、`CombinedSurface`、`CombinedCollision` 与碎块分组。对一层平台调用破坏接口，只更新这一层的运行状态。

顶层平台 `Platform_Circle_R25` 的 `CenterDisk` 保留独立方块，并挂有 `VoxelPlatformBakeExclusion`：

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

## 3. 用法：运行中破坏与恢复方块

游戏逻辑只需要三样东西：平台组件、一次射线命中、方块在平台局部空间中的中心坐标。

### 接入步骤

1. 用射线取得 `RaycastHit`，LayerMask 覆盖合并碰撞所在的 Layer。
2. 从命中的碰撞体取平台：`hit.collider.GetComponentInParent<VoxelPlatformRenderer>()`。
3. 调用 `platform.BreakBlockAtHit(hit)`，一步完成定位与破坏。
4. 需要保存破坏位置时，改用 `TryGetBlockPosition` 取得局部中心，再调用 `BreakBlock`。
5. 恢复时用同一个平台与同一个局部中心调用 `RestoreBlock`。

### 接口

命名空间 `Core.Scripts.Environment`，组件类 `VoxelPlatformRenderer`。

| 接口 | 作用 |
|---|---|
| `BreakBlockAtHit(RaycastHit)` | 定位命中方块并破坏 |
| `BreakBlock(Vector3)` | 按平台局部方块中心破坏 |
| `RestoreBlock(Vector3)` | 按同一局部中心恢复 |
| `SetBlockActive(Vector3, bool)` | 统一设置方块状态，破坏与恢复共用这一处 |
| `TryGetBlockPosition(RaycastHit, out Vector3)` | 从本平台合并碰撞的命中反查方块中心 |
| `RefreshChangedBlocks()` | 同步源对象的启停与变换变化 |
| `UseBakedSurface()` | 清理当前变化并加载烘焙状态 |
| `Rebuild()` | 从源对象重建表面与碰撞，没有源对象时重载烘焙状态 |
| `HasBakeSources` | 当前层级中是否存在参与收集的源方块 |
| `HasBakedSurface` | 是否存在完整的烘焙数据 |
| `RenderedTriangleCount`、`SurfaceRendererCount`、`CollisionColliderCount` | 当前运行中的三角形数量与渲染、碰撞对象数量 |

### 坐标与返回值约定

- 破坏、恢复、查询的参数都是平台局部空间的方块中心。射线的世界空间命中点落在表面，先经 `TryGetBlockPosition` 换算。
- 参数按位置键精度取整，传入近似中心即可命中。
- `TryGetBlockPosition` 只接受本平台 `CombinedCollision` 下的碰撞体。命中 `FragmentsRuntime` 中的碎块或其他碰撞体时返回 `false`。
- 找到方块就返回 `true`，方块已经处于目标状态同样返回 `true`。组件不报告状态是否真的改变，需要计分、播放一次性音效或扣除攻击次数的逻辑自行记录该方块的状态。
- 平台平移或旋转后，同一个局部中心仍然指向该平台中的同一方块。

### 示例

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

在已经取得 `platform` 与 `hit` 的地方，需要记录位置以便恢复时先定位再破坏：

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

## 4. 用法：改布局并重新烘焙

一层的方块有两种来源：层级中保留的源 Cube，以及组件上保存的烘焙数据。改布局前先确认目标区域属于哪一种来源，再按对应方式保存。

### 判断要改的区域

| 区域 | 层级中的识别方式 | 改哪里 | 保存方式 |
|---|---|---|---|
| 独立区 | 分组上挂有 `VoxelPlatformBakeExclusion`，下方保留 Cube、`Intact`、`Fragments` | 直接改分组里的 Cube 节点 | 保存场景或所属编辑用 Prefab |
| 烘焙区 | 显示与碰撞分别位于 `CombinedSurface`、`CombinedCollision`，方块由组件数据表示 | 改与该层布局对应的源 Cube | 改完烘焙，保存运行结果 |
| 整层变换 | 平台根节点 | 移动或旋转根节点 | 保存场景 |

当前场景中，顶层平台的 `CenterDisk` 是独立区，其余区域与另外五层是烘焙区。

### 改独立区方块

1. 在编辑模式打开 `Assets/Core/Scenes/VoxelArena.unity`，展开顶层平台根节点下的 `CenterDisk`。
2. 选中 `Cube_X_<x>_Z_<z>` 根节点做移动、旋转、删除或复制。整块编辑作用在 Cube 根节点上，`Intact` 与 `Fragments` 一起变换。
3. 增加方块时复制已有 Cube，或把 `Assets/Core/Prefabs/MinecraftClassic/` 下的 `Cube_stone.prefab`、`Cube_stonebrick.prefab`、`Cube_stonebrick_mossy.prefab`、`Cube_stonebrick_cracked.prefab` 放到 `CenterDisk` 下。
4. 保持 `Intact` 启用、`Fragments` 停用。更换石材时使用对应的 Cube Prefab，完整块与预切碎块的材质随之对应。
5. 保存场景。独立区不进入合并网格，重新烘焙不影响这些方块。

`VoxelPlatformBakeExclusion` 覆盖标记节点自身与全部子节点，标记组件的启停状态不改变排除范围。方块留在标记范围内就保持独立，移出标记范围后按新的祖先关系决定是否参与烘焙。

单独移动 `Intact` 会改变完整网格与它的碰撞，碎块仍停在 Cube 容器原来的装配位置。只替换 `Intact` 的材质只影响完整状态，碎块材质保存在 Cube Prefab 的 `Fragments` 上。

### 改烘焙区方块

1. 找到与目标层实际布局对应的编辑用资源。公共布局是 `Platform_Circle_R25_Authoring.prefab`；某层有独立的抬高、位移或挖空时，使用该层自己的编辑用资源。
2. 在编辑用资源里修改源 Cube 的位置、材质、启停与增删。把该层的抬高与位移同步写进源 Cube，重新生成前后的布局才一致。
3. 保存编辑用资源。
4. 执行 `Bake Platform Surface` 烘焙，保存运行结果。

烘焙以当前参与收集的完整源布局为输入，清空旧方块记录与分块记录后生成整套结果。源布局要包含该平台全部需要合并的方块，只补几个 Cube 就烘焙，生成结果只包含这几个方块。

### 整层移动与旋转

选择该层平台的 `VoxelPlatformRenderer` 根节点移动或旋转，让合并视觉、碰撞与独立区一起变换，然后保存场景。方块数据与碰撞命中定位都使用平台局部空间，根节点变换改变后接口参数不变。

### 预览布局

组件菜单 `Rebuild Platform Surface` 对应 `Rebuild()`：存在非排除源方块时，从当前源对象生成表面与碰撞预览，并隐藏源 Renderer，供检查邻面剔除与分块合并的结果。这个操作只改场景内的预览状态，不写烘焙数据。

停用再启用组件恢复源显示。已经烘焙、没有源方块的平台执行 `Rebuild()` 只重新加载烘焙状态。

### 烘焙入口

| 入口 | 适用对象 | 输入 | 输出 |
|---|---|---|---|
| 组件菜单 `Bake Platform Surface`，在场景中执行 | 场景里的平台 | 该平台的非排除源 Cube | 场景烘焙资产，清理源层级，场景标记为已修改 |
| 组件菜单 `Bake Platform Surface`，在 Prefab 编辑模式或资产上执行 | 平台 Prefab | 同目录的 `_Authoring` 源 Prefab | 运行用 Prefab，生成的网格作为子资产写入 |
| 菜单 `My_3DProject/Platform/Bake Active Scene Surfaces` | 当前活动场景的全部平台 | 存在非排除源方块的平台 | 逐个转换并保存整个活动场景 |

烘焙网格写入 `Assets/Art/MinecraftClassic/Generated/`。Prefab 的输出资产名包含运行资源路径的散列，同一路径再次烘焙替换该资产的内容；场景平台的输出资产名由场景名与平台对象的 `GlobalObjectId` 组成。

场景平台的资产名包含场景名，场景改名后重新烘焙会按新名字生成资产文件，旧文件留在目录中失去引用。

Prefab 模式下，烘焙从磁盘加载与运行 Prefab 配对的 `_Authoring` 资源，在隔离的预览场景中处理，再把结果写回运行 Prefab。配对资源不存在时，烘焙先复制调用方资源得到一份 `_Authoring` 副本，这份副本随后承担源布局的保存职责，因此它需要包含完整的源 Cube。

### 烘焙条件

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

烘焙区改动完整块的位置、网格、材质引用或渲染配置后，通过源布局重新烘焙。碎块的表现脚本挂在源 Cube Prefab 的 `Fragments` 上，接法见第 5 节。

### 重新编辑已有场景平台

场景平台在烘焙后解包为场景对象，参与烘焙的源 Cube 已被清理。改某一层时先准备源布局，再重新烘焙：

1. 打开与目标层实际布局对应的编辑用资源，完成逐块修改并保存。
2. 把该层需要的完整源 Cube 布局复制回平台根节点下，保持源分组与 Cube 相对平台根节点的变换与编辑布局一致。
3. 顶层平台只补回外围源布局，保留场景中现有的 `CenterDisk` 与手工调整。
4. 在该平台组件的菜单执行 `Bake Platform Surface`，或等全部源布局就绪后执行场景批量菜单。
5. 保存场景，检查改动位置的表面、碰撞与碎块位置。

单平台菜单要求存在参与收集的源方块，缺失时中止并提示。只有独立区的平台由场景批量菜单跳过，继续使用已有烘焙结果。

需要把某片烘焙区域改为独立编辑时，在源布局中恢复这片区域的 Cube，为它所在分组添加排除标记，再用包含全部其余合并方块的布局重新烘焙。

### 烘焙规则

- 重新烘焙整体替换该平台的方块记录与分块记录。
- 单平台菜单与批量菜单都在播放模式中止，单平台菜单在没有源方块时中止。
- 场景烘焙先解包平台的最外层 Prefab 实例，平台根节点不是该实例根节点时中止。
- 烘焙清理跳过排除区域，`CenterDisk`、`OuterDisk` 在子节点清空后被删除。

## 5. 用法：接入碎块表现脚本

破坏时的调用链：

```text
攻击逻辑
  -> BreakBlockAtHit 或 BreakBlock
  -> 完整方块状态关闭
  -> 从该方块的原 Cube Prefab 创建实例
  -> 应用保存的 Cube 容器变换
  -> 关闭 Intact，启用 Fragments
  -> 重建受影响分块的表面与碰撞
```

平台模块负责预切几何的创建、装配位置与实例生命周期。刚体分组、爆炸力、断裂判定、失去支撑的连通分量、粒子与碎片寿命由碎块脚本负责。

### 挂载位置

把表现脚本挂在四种源 `Cube_` Prefab 的 `Fragments` 节点上，并保持该节点默认停用。平台创建实例后先设置父节点与容器变换，再启用 `Fragments`，脚本在 `OnEnable` 中取得的 `Node_###` 已经就位。Cube 根节点的 `Awake` 在实例创建时执行，适合缓存引用；依赖装配位置的初始化放在 `Fragments.OnEnable` 中。

脚本位于源 Cube Prefab 上，延迟创建的实例自动携带该脚本。

### 示例

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

源碎块的 MeshCollider 使用凸形状，平台与碎块采用常规正缩放，物理表现以实际 Rigidbody 与 Collider 组合为准。

示例的速度方向来自碎块相对容器的位置。需要定向冲击时，控制器根据攻击信息计算世界空间受力；需要整组断裂时，控制器把连通碎块交给共同的 Rigidbody，并用方块标识与 `Node_###` 标识维护映射。

### 所有权与清理

碎块留在该方块的运行实例下时，`RestoreBlock` 与平台停用会连同刚体一起清理。控制器把碎块移出该实例后，接管这些碎块的销毁与恢复。长期驻留的残骸与对象池同样由控制器管理。

## 6. 原理：合批与邻面剔除

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

`VoxelPlatform/Rendering/VoxelPlatformGeometry.cs` 定义这两个量，当前分别为 `10000` 和 `8`。负坐标使用向下取整，平台左右两侧的分块边界保持一致。

占据索引只收录启用且满足遮挡条件的单位方块。位置键包含高度，同一水平坐标上的不同高度属于独立位置。索引通过键赋值建立，布局要保证每个位置只放置一个方块，同位置重复源几何会形成重叠表面。

### 六方向可见面

满足下列条件的方块参与邻面遮挡：

- 源网格具有标准单位 Cube 的顶点、索引和包围盒结构。
- 变换后的三个轴保持单位长度，并对齐平台局部坐标轴。
- 变换保持正向朝向。
- 相邻方块的中心恰好相差一个单位。

算法检查六个面，把每个面映射到平台局部空间中的邻居位置。邻居启用且满足遮挡条件时，该面从表面网格中剔除。直角旋转的 Cube 通过变换后的方向寻找邻居。

六个可见面组成六位掩码。完整可见的方块复用源 Mesh，部分可见的方块按掩码生成网格变体，变体按源 Mesh 与掩码缓存。网格变体保留源顶点属性，只筛选三角形索引。

方块之间有间隙、发生重叠、具有高度差或采用其他形状时，算法根据实际接触条件保留外露面。六面都被邻居遮挡的完整方块保留数据与碰撞，视觉表面为空。

### 分块内按材质合并

每个分块把启用方块按 Material 分组，使用方块变换与可见面网格执行 `Mesh.CombineMeshes`。每个有效材质组创建一个 MeshFilter 和 MeshRenderer。

一个组的 Layer、阴影和探针配置取该组的代表方块，同一分块、同一材质的源方块采用一致的渲染配置。透明材质和需要逐物体排序的表面适合使用独立的表现方案。

空间分块约束单次破坏时的重建范围。较小分块会增加 Renderer 和 Collider 数量，较大分块会增加局部重建与碰撞重新烹制的工作量。调整尺度后，通过编辑用平台重新烘焙。

## 7. 原理：视觉与碰撞的对应

`CombinedSurface` 保存剔除邻面的可见网格。`CombinedCollision` 按分块合并启用方块的完整网格，每个非空分块对应一个非凸 MeshCollider。

两者使用同一批方块记录和同一组完整方块变换，方块位置一致。视觉网格面向绘制，碰撞网格保留完整块的封闭几何。运行平台适合作为静态环境碰撞，运动碎块使用各自的凸 MeshCollider 与破碎脚本配置的 Rigidbody。

破坏或恢复方块时，组件同时更新：

1. 方块的启用状态与占据索引。
2. 该方块所在分块的表面与碰撞。
3. 六方向相邻占据方块涉及的分块。
4. 该方块对应的碎块实例。

边界方块移除后，邻块原本被遮挡的面重新生成。邻块处于另一分块时，同样进入本次更新范围。

## 8. 代码职责

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

运行模块围绕一个 `VoxelPlatformRenderer` 实例协作。组件承接渲染生命周期，各职责类由组件及其运行状态持有。

## 9. 生命周期与性能边界

组件停用会恢复源 Renderer 的原始渲染状态，清理临时表面、临时碰撞、面变体缓存与碎块实例，并收起烘焙对象。烘焙网格作为资产保留。再次启用运行平台时加载烘焙状态。跨停用、跨场景的破坏状态由游戏状态层保存，并在平台启用后按局部方块中心重新应用。

完整运行平台只有数据记录，源对象变化轮询只作用于保留源 Cube 的编辑布局，间隔为 0.05 秒。单次破坏要更新占据索引、合并受影响分块，并把新网格提交给 MeshCollider。命中反查遍历当前平台中的启用方块。

优化大范围爆炸时，关注同一帧的破坏次数与重复分块重建。大量独立刚体会把开销转移到物理与碎块 Renderer，控制同时活动的碎块数量、持续时间与阴影范围可以约束这部分成本。

帧率表现按来源判断：在 Profiler 中分别采样进入 Play、退出 Play 与破坏瞬间，同时核对垂直同步、目标帧率和显示刷新率。稳定帧率上限与 CPU、GPU 超时属于两类问题。

## 10. 验证入口

`Assets/Tests/Editor/PlatformOptimization/VoxelPlatformRendererTests.cs` 覆盖邻面剔除、旋转与高度、间隙与重叠、跨分块移动、破坏恢复、源网格保留、编辑版与运行版层级、烘焙网格复用、合并碰撞、原 Cube 引用、延迟碎块、碎块位置、碰撞命中定位、Prefab Stage 烘焙、同名资产隔离，以及独立编辑区的收集排除、清理保留和烘焙结果重新加载。

在 Unity Test Runner 的 EditMode 中选择 `VoxelPlatformRendererTests` 执行。

场景验证关注以下行为：

1. 六层平台保持独立根对象，正常启用时表面和碰撞同时有效。
2. 破坏前由合并对象显示完整平台，破坏目标块后洞口与碰撞同步变化。
3. 新建碎块保持原 Cube 的装配位置与材质，`Fragments` 上的表现脚本收到启用回调。
4. 恢复同一局部中心后，完整面和碰撞恢复，对应碎块实例被清理。
5. 停用再启用平台后恢复烘焙布局，各平台的状态独立。
6. 顶层 `CenterDisk` 的方块保持可单独编辑，外围合并网格与碰撞覆盖外围区域。
