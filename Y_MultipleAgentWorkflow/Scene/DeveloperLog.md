# Scene 开发记录

## 2026-10-19：碎块脱离窗口

- 现象：碎块可见之后仍停在原方块体积里，聚成方块状一坨，外观上像方块没被打掉。
- 根因：16 个预切片正好填满原方块的体积，与四周相邻方块零距离贴面。刚体一启用就被挤住，只会在原位相互挤压，飞不出来。
- 修复：`FragmentBurst` 增加 `escapeTime`。启用瞬间关闭预切片的 `MeshCollider`，按脱离时长计时后恢复，碎块先以无碰撞状态飞出原体积，再回到场景碰撞里下落。
- 验证：`Assets/Core/Scripts/Environment` 的 12 个源文件以 Unity 2022.3.62f3c1 的 `UnityEngine.CoreModule`、`UnityEngine.PhysicsModule` 编译，0 错误 0 警告。四个 Cube Prefab 的组件块补入 `escapeTime`，文档锚点仍为 90 个。
- 维护计数：`3/5 → 4/5`。

## 2026-10-19：接入碎块表现脚本

- 背景：碎块矩阵退回逻辑修好摆位后，碎块可见但停在方块原位，把破坏出的洞填住；射线还会命中 `Node_###`，导致后续破坏反查失败。原因是四个源 Cube Prefab 的 `Fragments` 节点上没有任何脚本，16 个预切片只是静态几何。
- 实现：新增 `Assets/Core/Scripts/Environment/VoxelPlatform/Fragments/FragmentBurst.cs`。`OnEnable` 遍历子节点，为带 `MeshCollider` 的预切片添加或复用 `Rigidbody`，按「相对容器中心的外法线 + 向上分量」施加 `VelocityChange`，到达 `lifetime` 后销毁整个碎块分组；编辑模式不参与。
- 挂载：`FragmentBurst` 加入 `Cube_stone`、`Cube_stonebrick`、`Cube_stonebrick_mossy`、`Cube_stonebrick_cracked` 四个 Prefab 的 `Fragments` 节点，该节点保持默认停用。烘焙记录的 `FragmentPrefab` 指向这四个资产，六层平台与共享运行 Prefab 自动继承。
- 验证：`Assets/Core/Scripts/Environment` 的 12 个源文件以 Unity 2022.3.62f3c1 的 `UnityEngine.CoreModule`、`UnityEngine.PhysicsModule` 编译，0 错误 0 警告。四个 Prefab 的 90 个文档锚点与 71 条组件引用一一对应，无重复锚点，脚本均挂在 `Fragments` 节点上。
- 边界：每个破坏方块产生 16 个刚体与凸 MeshCollider，同时活动的碎块数量由 `lifetime` 约束。
- 维护计数：`2/5 → 3/5`。

## 2026-10-19：修复烘焙碎块不可见

- 现象：破坏方块后外表面出现缺口，但看不到碎块。Console 报 `Look rotation viewing vector is zero`，堆栈落在 `VoxelPlatformFragments.ApplyLocalMatrix`。
- 根因：`VoxelArena` 的烘焙数据里 11776 条方块记录有 9880 条的 `FragmentMatrix` 全零。零矩阵让碎块实例的 `localScale` 变成 `(0,0,0)`，`Quaternion.LookRotation` 同时因前向为零报错，碎块被创建却不可见。
- 修复：`VoxelPlatformFragments` 取矩阵前先判退化，任一基向量为零时退回 `block.Matrix`；`ApplyLocalMatrix` 只在基向量不共线时写入旋转。
- 数据现状：`Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25.prefab` 的 1976 条记录全部有效，`VoxelArena` 的六层平台保留旧数据，由退化退回逻辑给出正确位置与朝向。
- 验证：`Assets/Core/Scripts/Environment` 的 11 个源文件以 Unity 2022.3.62f3c1 的 `UnityEngine.CoreModule`、`UnityEngine.PhysicsModule` 编译，0 错误 0 警告。
- 维护计数：`1/5 → 2/5`。

## 2026-10-11：记录场景光照与地形图层现状

- 光照：四个场景均无烘焙光照数据，也无反射探针；两个玩法场景各含一组光照探针。环境光模式为 Skybox，`m_AmbientMode: 0`，`InitialScene` 为 `3`。
- 地形：`MultiplayerSampleScene` 与 `MultiplayerSampleScene 1` 各有一个 Terrain，两者引用同一份 `Assets/Core/Terrain/Terrain_0_0_46643ae4-e124-42e3-9349-a7306c6d6a4a.asset`。该 TerrainData 内不含任何外部资源 guid，没有绘制任何 TerrainLayer；项目里唯一的 `NewLayer.terrainlayer` 也未被任何场景或资产引用。
- 植被：9 个草地材质未被任何场景、Prefab 或 TerrainLayer 引用，草地不渲染。6 个树与枝材质使用三渲二着色器。
- 维护计数保持 `0/5`。

记录已完成任务的证据、受影响文件与资源、验证结果、结论和维护计数变化。
