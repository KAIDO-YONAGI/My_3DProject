# Mirror + KCP 配置指南

从零开始，把 Mirror 和 KCP Transport 配置到 Unity 工程并跑通"服务器 + 双客户端"的完整步骤。当前工程已完成全部配置，本文用于复现配置过程、在新工程重做一遍、或配置被破坏后按步骤恢复。

工程当前配置值的事实来源：`Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md`。

## 1. 安装 Mirror

Mirror 是纯资源包，两种来源任选：

**Asset Store 路线**

1. Unity 编辑器 → Window → Package Manager → 左上角下拉选 **My Assets**。
2. 搜索 **Mirror**，点击 Download → Import，导入全部内容。
3. 导入完成后工程出现 `Assets/Mirror/` 目录。

**GitHub 路线**

1. 打开 github.com/MirrorNetworking/Mirror 的 Releases。
2. 下载对应版本的 `Mirror-xx.x.x.unitypackage`。
3. 编辑器菜单 Assets → Import Package → Custom Package，导入全部内容。

**验证安装**

1. 等待编译完成，Console 无报错。
2. 菜单 Tools → Mirror，能弹出 Mirror 相关菜单项。
3. 磁盘检查 `Library/ScriptAssemblies/` 存在 `Mirror.dll`、`Mirror.Components.dll`、`Mirror.Transports.dll`、`kcp2k.dll`。

**可能的坑**

- 工程里已有 `Assets/ScriptTemplates/` 目录（旧 Mirror 或手动复制过的残留）时，Mirror 自带的 `MoveToAssetsFolder.cs` 会与之产生 `CS0101` 重复定义编译错误。二选一保留：只留 Mirror 自带的那份。
- Mirror 版本差异会导致 API 变化，本文步骤基于 96.11.2。

## 2. 创建 NetworkManager 对象

打开目标场景（工程中为 `LobbyScene`）：

1. Hierarchy 右键 → **Create Empty**，命名 `NetworkManager`。
2. 选中该对象，Inspector → Add Component → 搜索 `NetworkManager`（Mirror 的那个），添加。
3. 添加后 Mirror 自动在对象上带出 `NetworkManagerHUD` 组件（运行时左上角显示 Server/Host/Client 按钮的调试界面）。

一个场景只需要一个 NetworkManager；它跨场景存活，其他场景无需重复放置。

## 3. 配置 KCP Transport

1. 选中 `NetworkManager` 对象，Inspector → Add Component → 搜索 `KCP Transport`，添加（组件全名 `kcp2k.KcpTransport`）。
2. 回到 `NetworkManager` 组件，检查 **Transport** 字段是否自动填上了 `KcpTransport`。为空时手动把Hierarchy 里该对象拖进 Transport 槽位。
3. KcpTransport 参数保持默认即可跑通本机联调：

| 参数 | 值 | 作用 |
|---|---|---|
| Port | 7777 | 服务器监听的 UDP 端口，双端必须一致 |
| DualMode | ✔ | 同时接受 IPv4/IPv6 连接 |
| NoDelay | ✔ | 关闭发送延迟 |
| Timeout | 10000 | 对端静默 10 秒判定掉线 |
| debugLog | 联调期勾上 | Console 打印 `[KCP]` 握手与连接日志，定位问题后取消 |

**坑：Transport 字段为空。** 用脚本 AddComponent 或旧序列化迁移时，NetworkManager 的 Transport 引用可能悬空，表现为 StartServer 直接抛空引用。在 Inspector 里手动拖一次并保存场景即可。

## 4. 制作并注册玩家 Prefab

Mirror 按玩家 Prefab 在每台客户端上生成玩家对象。

1. 场景里搭一个玩家对象：空物体或角色模型，按需添加：
   - `NetworkIdentity`（必须，Mirror 对象的身份组件）
   - `NetworkTransformReliable`（同步 Transform 到远端；阶段七会替换为自研快照）
   - 移动相关组件（工程的 `Player_Network` 用 CharacterController + 自研 `NetworkPlayerController`，脚本内用 `isLocalPlayer` 判断：本地玩家才读键盘，远端实例不读输入）
   - 相机子物体时，加一个脚本在非本地玩家上禁用 Camera/AudioListener，避免多相机冲突
2. 把对象从 Hierarchy 拖进 Project 面板生成 Prefab，然后**删除场景里的实例**（Mirror 运行时自己生成）。
3. 选中 `NetworkManager` 对象，把 Prefab 拖进 **Player Prefab** 槽位。
4. Player Prefab 必须带 NetworkIdentity，否则 NetworkManager 拒绝注册。

## 5. 配置 Build Settings

1. File → Build Profiles（或 Build Settings）→ Platform 选 **Windows** → Switch Platform。
2. **Scene List** 加入场景，0 号是构建启动场景（工程中为 `LobbyScene`）。Scene List 为空的构建没有可运行入口。
3. Player Settings → 勾选 **Run In Background**：服务器或后台窗口失焦后主循环照常跑，Mirror 的收发依赖主循环，失焦暂停会让握手包无人处理，表现成"客户端一直连不上"。

## 6. 打服务器构建

1. Build Profiles → Windows → **Server** 子目标（2022.3 的 Server Build），构建。
2. 产物无图形设备，运行日志出现 `Forcing GfxDevice: Null` 与一批 `Shader ... not supported` 报错，这是无头渲染的正常输出。
3. 服务器需要自动开服，二选一：
   - 写一个启动脚本，`Start()` 里判断 `Application.isBatchMode` 后调用 `NetworkManager.singleton.StartServer()`，挂到 NetworkManager 对象上（工程中的 `AutoStartServerBuild` 就是这个）；
   - 或每次手动在日志窗口之外用命令行带参控制。
4. 启动命令：

```text
My_3DProject.exe -batchmode -nographics -logFile server.log
```

启动成功的标志：日志出现 `Server listening on port 7777`。

## 7. 配置客户端连接

客户端构建需要知道服务器地址。两种方式：

- 运行时由启动脚本赋值：`NetworkManager.singleton.networkAddress = "服务器IP"; StartClient();`（工程中的 `AutoStartServerBuild` 默认 `127.0.0.1`，断线自动重连）；
- 或用 NetworkManagerHUD 的输入框手动填地址后点 Client。

**地址规则：本机填 `127.0.0.1`，跨机填服务器 IPv4。**

`localhost` 在 Windows 上解析成 IPv6 的 `::1`，服务器 KCP socket 在部分机器上实际是纯 IPv4 绑定，发往 `::1` 的握手包到不了服务器，客户端表现为发送 hello 后 10 秒超时循环。这是本工程实际踩过的坑，绑定地址一律写 IPv4 字面量。

## 8. 验证

1. 启动服务器，确认日志有 `Server listening on port 7777`。
2. 双开客户端构建。
3. 服务器日志出现两条 `Server: OnConnected`，每个客户端日志出现 `Client: OnConnected`。
4. 客户端画面各自出现玩家对象；移动本地玩家，另一台客户端能看到位置同步。
5. 排查手段：KcpTransport 勾上 debugLog，日志按 `[KCP]` 前缀过滤；客户端卡在 `sending handshake` 收不到回包，按第 7 节地址规则先查地址族。

## 9. 常见配置错误速查

| 现象 | 原因与处理 |
|---|---|
| CS0101 MoveToAssetsFolder 重复定义 | `Assets/ScriptTemplates/` 与 Mirror 自带目录重复，留一份 |
| StartServer 抛空引用 | NetworkManager 的 Transport 槽位为空，手动拖入 KcpTransport |
| 客户端 hello 发不出回包，10 秒超时循环 | 地址填了 `localhost`，改 `127.0.0.1` 或服务器 IPv4 |
| 服务器启动报 SocketException 端口占用 | 上一轮进程未退，任务管理器结束全部 `My_3DProject.exe` |
| 客户端完全无连接行为 | 检查场景里是否有 NetworkManager 对象；检查构建 Scene List 非空 |
| 后台窗口连不上 | Player Settings 勾 Run In Background |
| 构建产物无 Mirror | 当前构建的源工程没装 Mirror，看 `Managed/` 里有没有 `Mirror.dll` |
