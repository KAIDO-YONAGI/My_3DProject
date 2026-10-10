# Multiplayer 开发记录

## 2026-10-10：清理运动同步残留记录

- 删除 `NetworkCharacterSync.cs` 中 `OnCharacterMovementStateChanged` 的半截 `SyncVar` 注释残骸（5 行，语法不完整），不改变任何序列化字段与同步行为；`characterId` 的 `SyncVar` 与 `CommandSetCharacter` 链路未动。
- `NetworkPlayer.prefab` 上的残留运动同步组件记录已删除，`NetworkCharacterSync` 仍是根节点最后一个组件；未来接入运动同步组件时仍需在末尾追加并配套构建客户端与服务器。
- `Networking_Guide.md` 撤回“Prefab 尚保留 GUID 记录、属待网络工作处理的资产一致性问题”的表述，改为记录已清理。
- 验证：`Assets/Core` 缺失脚本 GUID 归零；未执行双客户端、Host 或专用服务器验收。本次仅注释与资产清理，不改变业务实现，维护计数保持 `4/5`。

## 2026-10-10：清理残留组件记录并撤回错误表述

- `Protocol_Guide.md` 兼容性规则一节改为记录 Prefab 残留记录已清理，并写明根节点当前 6 个组件、末尾 `NetworkCharacterSync`；重申该清理不代表运动状态协议已发布。
- `docs/网络同步速查手册.md` 第三节表格新增勘误：撤回把 `NetworkCharacterMovementSync` 列为根节点已有组件的表述，并标注该节的服务器/远端描述属设计方案而非当前实现。
- Proposal 一节的字段约定与约束未改。纯文档维护，不引入可用协议，维护计数保持 `1/5`。

## 2026-10-10：记录角色装配边界与运动同步暂缓

- `AutoStartClient`、`NetworkCharacterManager`、`NetworkCharacterSync` 保留在 `Assets/Core/Scripts/Networking/`。视觉角色继续装配到带 NetworkTransform 的 NetworkPlayer 根下，视觉子预制件不添加网络同步组件。
- 当前磁盘运动同步脚本已不存在，控制器远程动画仅回退 Idle；Guide 不再将运动状态协议视为已接入能力。Prefab 残留的缺失脚本 GUID 记录列为待网络工作处理，未擅自修改资产。
- 同步 Protocol、Server 与 UnityRuntime 的待接入边界；未执行新网络验收。本次纯文档维护，计数保持 `4/5`。

## 2026-10-10：运动表现快照方案标记为 Proposal

- 当前协议仍为 Mirror 变换同步和角色编号；运动状态同步已按用户最新要求暂缓，未引入可用协议。
- Guide 记录未来状态 `byte`、局部方向 `Vector2`、权重 `float`、可靠 ClientToServer、时间轴、校验、缓冲与 Host 分流要求，明确所有内容为 Proposal。
- 兼容边界要求未来根节点末尾追加组件、同版本客户端与服务器配套构建；未进行协议或联机验收。纯文档维护，计数保持 `1/5`。

## 2026-10-10：澄清五态代码与服务器职责

- 客户端五态代码状态机不改变当前客户端权威位移；纯专用服务器控制器不读取输入或执行本地运动。
- 运动表现转发暂缓，没有已接入的服务端角色运动状态缓冲；将后续校验与转发职责链接到 Protocol 的 Proposal。
- 未重新构建、部署或验证双客户端/Host/延迟。纯文档维护，计数保持 `0/5`。

## 2026-10-08：角色控制器身份查询改为一次性缓存

- 控制器缓存自身网络身份组件及缺失结果；身份必须在首次初始化前配置，本地/远程归属属性仍实时读取。网络子模型和 Animator 的延迟装配不受影响。
- 新增缺失缓存、已有身份和 Animator 后挂载测试。先验证旧代码的重复查询，再验证修复；PlayMode 网络任务成功，完成 12 项、未报告失败，EditMode 8/8；控制台无错误或警告。
- 未修改网络协议、Prefab 或服务器配置，未构建或进行双客户端联机验收。维护计数 `3/5 -> 4/5`。

## 2026-10-08：单机相机路径取消全局查找

- `SetStandaloneCameraEnabled` 改为实例方法，直接切换配置的 Camera、ThirdPersonCamera、AudioListener；不再扫描场景或检查父级网络身份。动态网络表现的装配缓存逻辑不变。
- EditMode 场景与连接配置测试 8/8；PlayMode 网络与相机测试任务成功，完成 49 项、未报告失败。未重新构建或执行双客户端联机验证。
- 同步 Client 与 UnityRuntime 的场景配置说明。维护计数 `2/5 -> 3/5`。

## 2026-10-08：单机角色切换移除全局查找

- `ApplyPresentationMode`、`SetStandalonePlayerEnabled` 改为实例方法，通过 `standalonePlayer` 场景序列化引用调用 `SetActive`，移除角色全局查找和父级身份查询；相机查找不变。
- 测试先确认缺失字段与实例入口导致失败；绑定场景引用并重新加载未保存修改为零的场景后，EditMode 7/7、网络表现 PlayMode 9/9 通过。
- 不涉及消息协议、Mirror 参数或网络 Prefab。维护计数 `1/5 -> 2/5`。

## 2026-09-30：连接状态与单机入口分流修复

- 联机表现不再读取连接尝试阶段也会变化的 `NetworkClient.active`，改为只在 `NetworkClient.isConnected=true` 时启用。
- 已连接后禁用 PersistentScene 的场景单机角色，消除两个网络玩家之外的第三个角色。
- 未连接和重试阶段保留单机角色、输入、相机及物理更新，避免 `No cameras rendering` 和角色重力暂停。

## 2026-09-30：双编辑器联机与后加载相机修复

- 通过重建后的 `Server_12_7` 专用服务器验证两个 Unity 编辑器同时连接，KCP 7777 握手和玩家生成均成功。
- 修正本地角色 Prefab 根引用和出生点高度，避免网络角色从地形下方生成后持续下落。
- `NetworkCharacterManager` 增加 `SceneManager.sceneLoaded` 处理，解决 Additive 玩法场景的非网络相机在联机状态下仍保持启用的问题。

## 2026-09-30：修复联机模型切换后的 Animator 失效

- `PersistentScene/Managers/NetworkCharacterManager` 按 `NetworkCharacterSync` 同步的 `characterId` 分流加载：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`，网络根对象不再持有固定角色视觉。
- 模型销毁前先清空 `PlayerCharacterController` 的旧动画驱动，模型实例化后再绑定新 `Animator`。
- Host 模式实测 `characterId 0 -> 1 -> 0`，角色持续可见，控制台无 `MissingReferenceException`。

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 当前网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。
- 明确 `AutoStartClient`、`NetworkPlayerController`、`LocalPlayerCamera` 的职责和旧新构建混用风险。

## 2026-09-29：建立协议模块权威入口

- 依据当前 Mirror 组件和玩家 Prefab 建立协议边界文档。
- 明确当前没有项目自定义协议源码，当前协议由 Mirror 内置消息和 NetworkTransform 序列化承载。
- 将 InputFrame、固定 Tick、状态快照和预测校正标记为 Proposal。

## 2026-09-29：建立专用服务器模块权威入口

- 依据 PersistentScene、Mirror NetworkManager、KCP 配置和 Server_7_0 构建建立模块文档。
- 明确当前 Server 模块由 Mirror Headless 配置承载，没有独立服务器源码目录。
- 记录固定 Tick、输入队列、状态快照和玩法权威结算仍为 Proposal。
