# Multiplayer 开发记录

## 2026-10-14：Networking 脚本按职责分层并重命名

- 目录重组：`Assets/Core/Scripts/Networking/` 的 6 个脚本由平铺改为 `Connection/`、`Character/Sync/`、`Character/Presentation/` 三层存放，脚本与 `.meta` 成对移动，四个受跟踪脚本的 GUID 保持不变。
- 重命名：`NetworkCharacterSync` 为 `CharacterIdSync`，`NetworkCharacterMovementSync` 为 `CharacterMotionSync`，`NetworkCharacterPresentationService` 为 `CharacterPresentationService`，`CharacterPresentation` 为 `CharacterPresentationState`；`AutoStartClient` 与 `NetworkCharacterManager` 名称不变。
- 命名空间跟随目录：`Core.Scripts.Networking.Connection`、`Core.Scripts.Networking.Character.Sync`、`Core.Scripts.Networking.Character.Presentation`。
- 调用方同步：`CharacterSelectButton` 的 `using` 指向表现命名空间；`NetworkCharacterPresentationTests` 的类型名字符串与全限定名映射同步更新。
- 文档同步：`Project_Overview.md` 代码模块地图、`Multiplayer_Guide.md`、`Sync_Protocol.md`、`Dedicated_Server.md`、`Project_Guide.md`、`Mirror_KCP_Config.md`、`Character_Guide.md`、`README.md`、`StateMachine/README.md` 与 `docs/开发计划/联机系统教学/` 的标识符与路径已更新。
- 验证：全仓检索旧类名与旧路径，运行时代码、测试与权威文档无残留。
- 计数：`2/5 → 3/5`。

## 2026-10-11：客户端启动上报默认角色编号

- 默认编号来源由服务器单方配置改为客户端本机配置：`NetworkCharacterSync.OnStartLocalPlayer` 新增 `ReportLocalDefaultCharacter`，读取本机 `NetworkCharacterManager.defaultCharacterId` 并经现有 `CommandSetCharacter` 上行一次；`OnStartServer` 写入的服务器默认编号作为客户端未上报或上报编号无效时的兜底值。
- 上报前置条件为本机已连接服务器且该对象为本机玩家，组件测试与纯服务器不产生 `Command` 调用。
- `CommandSetCharacter` 拒收未通过校验的编号时输出 `Debug.LogWarning`，日志包含上报编号与服务器保留编号。
- 表现路径不变：服务器写入 `characterId` 后仍由 SyncVar hook 与启动回调驱动客户端装配；上报值与服务器默认值不同时客户端替换一次视觉实例。
- 验证：Unity 完成 `NetworkCharacterSync.cs` 重新导入与 `Assembly-CSharp` 编译，编译输出无错误。
- 计数：`0/5 → 1/5`。

## 2026-10-11：Multiplayer 域权威文档通篇复核

- 核对：逐份阅读 `Router.md`、`Multiplayer_Guide.md`、`Sync_Protocol.md`、`Dedicated_Server.md`，对照 `Assets/Core/Scripts/Networking/` 四个脚本与两个 Prefab 的实际根组件列表。
- 修正 1（组件列表过期）：`NetworkPlayer.prefab` 实际根组件为 7 个，`Multiplayer_Guide.md` 与 `Sync_Protocol.md` 均补齐末尾的 `NetworkCharacterMovementSync`。
- 修正 2（章节定性过期）：运动表现同步已实装，`Sync_Protocol.md` 由「运动表现同步（Proposal）」移入「现有同步字段」的第三个子节「运动表现」，`Router.md` 与 `Dedicated_Server.md` 的引用同步更正。
- 修正 3（分层图缺项）：`Sync_Protocol.md` 的协议分层图补上 `NetworkCharacterMovementSync 运动快照`。
- 修正 4（远程动画描述过期）：`Multiplayer_Guide.md` 更正为远程角色由收到的运动快照驱动动画，首个快照到达前为 Idle。
- 修正 5（说明缺项）：`Multiplayer_Guide.md` 的「当前组件」与「源码字段说明」两节各补一条 `NetworkCharacterMovementSync`。
- 修正 6（代码注释残骸）：`NetworkCharacterSync.cs` 中 `OnCharacterMovementStateChanged` 的 5 行注释残骸已删除；`PlayerCharacterController.LateUpdate` 里指向未接入运动状态的注释一并更正。
- 复核一致的部分：`AutoStartClient` 的编辑器/构建开关、批处理与活动服务端跳过、真实时间重试间隔；`NetworkManager.prefab` 的四个组件；角色装配九步流程、`presentations` 复用规则、表现模式与相机收拢、`CommandSetCharacter` 上下行链路、专用服务器的 Headless 与构建事实。
- 计数：`5/5 → 0/5` 重置，进入下一轮。

## 2026-10-11：运动快照拆分与异常丢弃日志

- 快照类型拆分：新增 `Core.Scripts.Character.Animation.CharacterMotionSnapshot`，含三个可传输字段，作为网络传输的唯一格式；`NetworkCharacterMovementSync` 的 `OnDeserialize` 构造快照，`PlayerCharacterController.ReceiveRemoteMotion` 接收快照。线上格式为 1 字节状态 + 2 个 `float` 方向分量 + 1 个 `float` 权重，字节布局未变。
- 类型分离使网络能提供的字段与表现层需要的字段在类型上区分，`CharacterMotion` 增加本地专有字段时远程角色不会取到默认值。
- 异常丢弃日志：越界快照按失败项区分原因输出 `Debug.LogWarning`，状态编号超上限、方向长度越界、权重越界分别可辨，并按 1 秒间隔限流。
- 验证：Unity Console 0 错误 0 警告；`Core.EditorTests` 27 个测试全部通过。
- 文档与计数：`Sync_Protocol.md` 的字段说明与校验说明已同步。Multiplayer 维护计数 `4/5 → 5/5`。

## 2026-10-10：清理运动同步残留记录

- `NetworkCharacterSync.cs` 中 `OnCharacterMovementStateChanged` 的半截 `SyncVar` 注释残骸已删除，序列化字段与同步行为未变；`characterId` 的 `SyncVar` 与 `CommandSetCharacter` 链路未动。
- `NetworkPlayer.prefab` 上的残留运动同步组件记录已删除，`NetworkCharacterSync` 为根节点最后一个组件。
- 验证：`Assets/Core` 缺失脚本 GUID 归零。维护计数保持 `4/5`。

## 2026-10-10：清理残留组件记录并撤回错误表述

- `Networking_Guide.md` 兼容性规则一节记录 Prefab 残留记录已清理，根节点为 6 个组件、末尾 `NetworkCharacterSync`。
- 维护计数保持 `1/5`。

## 2026-10-10：记录角色装配边界与运动同步暂缓

- `AutoStartClient`、`NetworkCharacterManager`、`NetworkCharacterSync` 位于 `Assets/Core/Scripts/Networking/`。视觉角色装配到带 NetworkTransform 的 NetworkPlayer 根下，视觉子预制件不加网络同步组件。
- 控制器远程动画回退 Idle，Guide 不将运动状态协议视为已接入能力。
- 维护计数保持 `4/5`。

## 2026-10-10：运动表现快照方案标记为 Proposal

- 当前协议为 Mirror 变换同步和角色编号，运动状态同步暂缓，未引入可用协议。
- Guide 记录未来状态 `byte`、局部方向 `Vector2`、权重 `float`、可靠 ClientToServer、时间轴、校验、缓冲与 Host 分流要求，标记为 Proposal。
- 兼容边界要求未来根节点末尾追加组件、同版本客户端与服务器配套构建。计数保持 `1/5`。

## 2026-10-10：澄清五态代码与服务器职责

- 客户端五态代码状态机不改变当前客户端权威位移；纯专用服务器控制器不读取输入或执行本地运动。
- 运动表现转发暂缓，没有已接入的服务端角色运动状态缓冲；后续校验与转发职责链接到 Protocol 的 Proposal。
- 计数保持 `0/5`。

## 2026-10-08：角色控制器身份查询改为一次性缓存

- 控制器缓存自身网络身份组件及缺失结果；身份在首次初始化前配置，本地/远程归属属性实时读取。网络子模型和 Animator 的延迟装配不受影响。
- 测试覆盖缺失缓存、已有身份和 Animator 后挂载；PlayMode 网络任务成功，完成 12 项、未报告失败，EditMode 8/8；控制台无错误或警告。
- 维护计数 `3/5 -> 4/5`。

## 2026-10-08：单机相机路径取消全局查找

- `SetStandaloneCameraEnabled` 为实例方法，直接切换配置的 Camera、ThirdPersonCamera、AudioListener；场景扫描与父级网络身份检查已移除。动态网络表现的装配缓存逻辑不变。
- EditMode 场景与连接配置测试 8/8；PlayMode 网络与相机测试任务成功，完成 49 项、未报告失败。
- 计数 `2/5 -> 3/5`。

## 2026-10-08：单机角色切换移除全局查找

- `ApplyPresentationMode`、`SetStandalonePlayerEnabled` 为实例方法，通过 `standalonePlayer` 场景序列化引用调用 `SetActive`，角色全局查找和父级身份查询已移除。
- 绑定场景引用并重新加载场景后，EditMode 7/7、网络表现 PlayMode 9/9 通过。
- 计数 `1/5 -> 2/5`。

## 2026-09-30：连接状态与单机入口分流修复

- 联机表现以 `NetworkClient.isConnected=true` 为启用条件。
- 已连接后禁用 PersistentScene 的场景单机角色，消除两个网络玩家之外的第三个角色。
- 未连接和重试阶段保留单机角色、输入、相机及物理更新。

## 2026-09-30：双编辑器联机与后加载相机修复

- 通过重建后的 `Server_12_7` 专用服务器验证两个 Unity 编辑器同时连接，KCP 7777 握手和玩家生成均成功。
- 修正本地角色 Prefab 根引用和出生点高度，网络角色不再从地形下方生成。
- `NetworkCharacterManager` 增加 `SceneManager.sceneLoaded` 处理，解决 Additive 玩法场景的非网络相机在联机状态下仍启用的问题。

## 2026-09-30：修复联机模型切换后的 Animator 失效

- `PersistentScene/Managers/NetworkCharacterManager` 按 `NetworkCharacterSync` 同步的 `characterId` 分流加载：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`，网络根对象不持有固定角色视觉。
- 模型销毁前先清空 `PlayerCharacterController` 的旧动画驱动，模型实例化后再绑定新 `Animator`。
- Host 模式实测 `characterId 0 -> 1 -> 0`，角色持续可见，控制台无 `MissingReferenceException`。

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。

## 2026-09-29：建立协议模块权威入口

- 依据当前 Mirror 组件和玩家 Prefab 建立协议边界文档。
- 协议由 Mirror 内置消息和 NetworkTransform 序列化承载，没有项目自定义协议源码。

## 2026-09-29：建立专用服务器模块权威入口

- 依据 PersistentScene、Mirror NetworkManager、KCP 配置和 Server_7_0 构建建立模块文档。
- Server 模块由 Mirror Headless 配置承载，没有独立服务器源码目录。
