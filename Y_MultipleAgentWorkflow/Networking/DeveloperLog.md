# Networking 开发记录

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

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 当前网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。
- 明确 `AutoStartClient`、`NetworkPlayerController`、`LocalPlayerCamera` 的职责和旧新构建混用风险。

## 2026-09-30：修复联机模型切换后的 Animator 失效

- `PersistentScene/Managers/NetworkCharacterManager` 按 `NetworkCharacterSync` 同步的 `characterId` 分流加载：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`，网络根对象不再持有固定角色视觉。
- 模型销毁前先清空 `PlayerCharacterController` 的旧动画驱动，模型实例化后再绑定新 `Animator`。
- Host 模式实测 `characterId 0 -> 1 -> 0`，角色持续可见，控制台无 `MissingReferenceException`。

## 2026-09-30：双编辑器联机与后加载相机修复

- 通过重建后的 `Server_12_7` 专用服务器验证两个 Unity 编辑器同时连接，KCP 7777 握手和玩家生成均成功。
- 修正本地角色 Prefab 根引用和出生点高度，避免网络角色从地形下方生成后持续下落。
- `NetworkCharacterManager` 增加 `SceneManager.sceneLoaded` 处理，解决 Additive 玩法场景的非网络相机在联机状态下仍保持启用的问题。

## 2026-09-30：连接状态与单机入口分流修复

- 联机表现不再读取连接尝试阶段也会变化的 `NetworkClient.active`，改为只在 `NetworkClient.isConnected=true` 时启用。
- 已连接后禁用 PersistentScene 的场景单机角色，消除两个网络玩家之外的第三个角色。
- 未连接和重试阶段保留单机角色、输入、相机及物理更新，避免 `No cameras rendering` 和角色重力暂停。
