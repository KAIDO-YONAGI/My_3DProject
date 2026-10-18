# 专项设计索引

这些专题补充 API 选择、现有工程接入和机制边界，不另建一套竞技场、NetworkManager 或玩家移动框架。主线沿用现有 `InitialScene → PersistentScene → MultiplayerSampleScene`，并在现有 `NetworkPlayer` 上扩展玩法组件。

| 你要查的问题 | 专题 |
| --- | --- |
| `SyncList`、SyncVar、公共平台状态和网络对象怎样配合 | [集合、公共对象与消息](集合公共对象与消息.md) |
| 当前玩家和动态平台分别由谁模拟，选什么变换同步 | [运动轨迹与同步选择](运动轨迹与同步选择.md) |
| 格子平台怎样断开、分组并成为动态块 | [结构破坏与动态碎片](结构破坏与动态碎片.md) |
| 玩家、平台 Prefab、Spawn、晚加入和运行检查怎样衔接 | [生命周期与开发验证](生命周期与开发验证.md) |
| 怎样保留现有角色移动、相机、动画和音频 | [现有角色与本地表现接入](现有角色与本地表现接入.md) |
| 怎样把已同步状态绑定到 HUD、标签和观战视图 | [状态与界面绑定](状态与界面绑定.md) |
| 双人近距炸弹与 E 键挖掘如何由服务器裁决 | [爆炸物与命中裁决](爆炸物与命中裁决.md) |
| 何时值得为投掷物增加本地视觉预测 | [投掷物预测与权威接管](投掷物预测与权威接管.md) |
| Mirror Weaver 如何生成远程调用与序列化代码 | [代码生成与序列化](代码生成与序列化.md) |
| 何时评估批量位姿快照、基线和恢复机制 | [独立快照同步内核](独立快照同步内核.md) |

## 主线与专题的边界

主线保留现有玩家移动和 `NetworkTransformReliable.ClientToServer`，新增平台服务端物理、破坏、道具和胜负裁决。`ArenaPlayerActions`、`PlatformPassenger`、`ArenaPlayerPresentation` 扩展现有 `NetworkPlayer`；`PlatformChunk` 使用 Mirror 状态同步和服务器到客户端变换同步。专题中的独立快照内核是单独的进阶研究主题，主线先用现有 Mirror 机制完成玩法。

Mirror API 的精确选择、现有角色脚本例子和同步回调签名见 [Mirror 网络 API 总览](../Mirror网络API总览.md)。
