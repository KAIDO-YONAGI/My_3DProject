# Scene 开发记录

## 2026-10-11：记录场景光照与地形图层现状

- 背景：工程从内置渲染管线迁到 URP 14.0.12（见 `../Project/DeveloperLog.md` 的 `接入 URP 14.0.12 并实装三渲二着色器` 条）。本次只核实并记录场景侧事实，不改场景内容。
- 光照：四个场景均无烘焙光照数据，也无反射探针；两个玩法场景各含一组光照探针。环境光模式为 Skybox（`m_AmbientMode: 0`，`InitialScene` 为 `3`）。因此迁 URP **不需要重烘焙光照**。
- 地形：`MultiplayerSampleScene` 与 `MultiplayerSampleScene 1` 各有一个 Terrain，两者引用同一份 `Assets/Core/Terrain/Terrain_0_0_46643ae4-e124-42e3-9349-a7306c6d6a4a.asset`。该 TerrainData 内不含任何外部资源 guid，即**没有绘制任何 TerrainLayer**；项目里唯一的 `NewLayer.terrainlayer` 也没有被任何场景或资产引用。地形是裸的。
- 植被：9 个草地材质（原用已废弃的 `Nature/SpeedTree` 内置着色器）未被任何场景、Prefab 或 TerrainLayer 引用，草地不会渲染出来。6 个树与枝材质已随迁移换成三渲二着色器。
- 未完成：未在编辑器中打开场景核对 URP 下的实际渲染表现，未验证地形与植被在 URP 下的出画效果。本次为事实核实与文档记录，不改变场景实现，维护计数保持 `0/5`。

记录已完成任务的证据、受影响文件与资源、验证结果、结论和维护计数变化。
