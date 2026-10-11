# Scene 开发记录

## 2026-10-11：记录场景光照与地形图层现状

- 光照：四个场景均无烘焙光照数据，也无反射探针；两个玩法场景各含一组光照探针。环境光模式为 Skybox，`m_AmbientMode: 0`，`InitialScene` 为 `3`。
- 地形：`MultiplayerSampleScene` 与 `MultiplayerSampleScene 1` 各有一个 Terrain，两者引用同一份 `Assets/Core/Terrain/Terrain_0_0_46643ae4-e124-42e3-9349-a7306c6d6a4a.asset`。该 TerrainData 内不含任何外部资源 guid，没有绘制任何 TerrainLayer；项目里唯一的 `NewLayer.terrainlayer` 也未被任何场景或资产引用。
- 植被：9 个草地材质未被任何场景、Prefab 或 TerrainLayer 引用，草地不渲染。6 个树与枝材质使用三渲二着色器。
- 维护计数保持 `0/5`。

记录已完成任务的证据、受影响文件与资源、验证结果、结论和维护计数变化。
