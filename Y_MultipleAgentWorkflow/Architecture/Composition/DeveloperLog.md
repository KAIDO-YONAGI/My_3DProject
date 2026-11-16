# Architecture.Composition 开发记录

## 维护规则

按项目约定记录带日期的任务事实、验证证据与维护计数，现行结论同步到对应 Guide。了解现状优先读取 Guide；需要任务历史时，只定位并读取最近一天或两天的相关条目，禁止整份日志载入上下文。只有任务需要追溯历史时才按日期、标题或关键词定向读取更早条目。读取范围不改变存储保留，删除具体历史条目须经用户明确确认。纯文档维护不增加业务计数。

## 2026-11-16：建立组合与空值处理约定

- 单例基类 `YSingleton<T>` 新增 `RequiredInstance`：未注册或已销毁时抛出带类型名的 `InvalidOperationException`；`Instance` 保留允许暂缺语义。
- 必填序列化引用统一改为 `Debug.Assert`，消息保留字段名与缺失后果；覆盖 `SceneChanger`、`SceneToggler`、`InitialLoad`、`VoxelPlatformRegister`、`VoxelPlatformBlastTrigger`、`VoxelPlatformDestruction`、`AudioPlayerManager`、`AudioTriggerManager`、`Attack`、`NetworkBlastManager`、`NetworkCharacterManager`、`CharacterMotionSync`、`CharacterIdSync`、`UIManager`、`ESCMenuManager`、`GameOverCanvasManager`、`RetryButton`、`ESCButton`、`CharacterSelectButton`、`ButtonSceneToggler`。
- 删除已校验字段上的调用点判空与静默过滤；`VoxelPlatformDestruction` 用 `subscribed` 就绪标记替代 `OnDestroy` 中的资产二次判空。
- 必需依赖迁移到 `RequiredInstance`：`TimeManager`、`SceneChanger`、`VoxelPlatformDestruction`、`UIManager`、`NetworkCharacterManager`、`AudioPlayerManager`；`UIManager.Report`、`ICanvasManager.RefreshCanvaOrder`、`CharacterIdSync.OnStopClient` 等允许暂缺路径保留 `Instance` 并注明原因。
- 验证：以 Unity 2022.3.62f3 的 Roslyn 与 `Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp` 编译 `Assembly-CSharp`，退出码 `0`，仅保留既有 `AutoStartClient` 未使用字段警告；场景与 Prefab 中 `SceneChanger`、`InitialLoad`、`VoxelPlatformRegister`、`VoxelPlatformBlastTrigger`、`Attack`、`AudioPlayerManager`、`NetworkCharacterManager`、`NetworkBlastManager`、`CharacterMotionSync` 的必填引用逐项核对为非空。工程由已打开的 Unity 编辑器持有锁，未运行 EditMode 与 PlayMode 测试。
- 维护计数：`0/5 -> 1/5`。
