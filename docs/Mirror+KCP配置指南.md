# Mirror + KCP 配置指南

- 工程里已有 `Assets/ScriptTemplates/` 目录（旧 Mirror 或手动复制过的残留）时，Mirror 自带的 `MoveToAssetsFolder.cs` 会与之产生 `CS0101` 重复定义编译错误。二选一保留：只留 Mirror 自带的那份。
- Mirror 版本差异会导致 API 变化，本文步骤基于 96.11.2。

## 先懂：构建和场景的关系

构建 = 把 Build Settings 场景表从 0 号场景开始打包成 exe。exe 启动后加载 0 号场景，场景里的对象实例化，NetworkManager 组件的 `Start()` 执行，服务器的能力全部来自这个组件随场景被加载。

由此得出三条规则：

1. 服务器构建的 0 号场景里必须有挂好 NetworkManager 的对象，其他场景排在后面即可，参与构建但启动时不加载。
2. 连接只认 IP 和端口，握手包里没有场景信息，两端加载不同场景照样连通。
3. 构建之后在编辑器里改的场景，exe 感知到，改完必须重新构建。

## 创建 NetworkManager 对象

打开目标场景：

1. Hierarchy 右键 → **Create Empty**，命名 `NetworkManager`。
2. 选中该对象，Inspector → Add Component → 搜索 `NetworkManager`（Mirror 的那个），添加。
3. 添加后 Mirror 自动在对象上带出 `NetworkManagerHUD` 组件（运行时左上角显示 Server/Host/Client 按钮的调试界面）。

一个场景只需要一个 NetworkManager；它跨场景存活，其他场景无需重复放置。

## 配置 KCP Transport

1. 选中 `NetworkManager` 对象，Inspector → Add Component → 搜索 `KCP Transport`，添加（组件全名 `kcp2k.KcpTransport`）。
2. 回到 `NetworkManager` 组件，检查 **Transport** 字段是否自动填上了 `KcpTransport`。为空时手动把Hierarchy 里该对象拖进 Transport 槽位。
3. KcpTransport 参数保持默认即可跑通本机联调：

| 参数 | 值 | 作用 |
|---|---|---|
| Port | 7777 | 服务器监听的 UDP 端口，双端必须一致 |
| DualMode | ✔ | 同时接受 IPv4/IPv6 连接 |
| NoDelay | ✔ | 关闭发送延迟 |
| Timeout | 10000 | 对端静默 10 秒判定掉线 |
| FastResend | 2 | 收到 2 次重复确认立即重传，抗丢包 |
| SendWindowSize / ReceiveWindowSize | 4096 / 4096 | 滑动窗口，决定单连接吞吐上限 |
| MaxRetransmit | 40 | 重传 40 次仍无确认判定连接死亡 |
| debugLog | 联调期勾上 | Console 打印 `[KCP]` 握手与连接日志，定位问题后取消 |

**坑：Transport 字段为空。** 用脚本 AddComponent 或旧序列化迁移时，NetworkManager 的 Transport 引用可能悬空，表现为 StartServer 直接抛空引用。在 Inspector 里手动拖一次并保存场景即可。

## 制作并注册玩家 Prefab

玩家对象存在两份：Prefab 资产是模板，躺在 Project 窗口；运行时实例是 Mirror 生成的，出现在每台机器的场景里。配置工作只针对模板。

1. 场景里搭一个玩家对象：空物体或角色模型，按需添加：
   - `NetworkIdentity`：网络身份牌。连接建立时分配 netId（全网唯一编号），并记录对象属于哪个连接。没有它 NetworkManager 拒绝注册这个 Prefab。
   - `NetworkTransformReliable`：把本地 position/rotation 编码成消息发出，远端收到后写回自己的 Transform。没有它对象能出现但位置永远不动。
   - 移动相关组件（工程的 `Player_Network` 用 CharacterController + 自研 `NetworkPlayerController`，脚本第一行 `if (!isLocalPlayer) return;`）。`isLocalPlayer` 由 NetworkIdentity 判定：本机连接拥有的实例为 true。同一个 Prefab 生成五个实例，每台机器上只有一个是 localPlayer，其余是位置由网络消息驱动的副本。缺这个守卫，每台机器上的所有玩家都响应同一份键盘。
   - 相机子物体时，加一个脚本在非本地玩家上禁用 Camera/AudioListener，避免多相机冲突（工程中的 `LocalPlayerCamera`）。
2. 把对象从 Hierarchy 拖进 Project 面板生成 Prefab，然后**删除场景里的实例**。
3. 选中 `NetworkManager` 对象，把 Prefab 拖进 **Player Prefab** 槽位。

**生成机制的运作方式**：客户端连上后发 AddPlayerRequest，服务器收到后 Instantiate playerPrefab 字段指向的模板，分配 netId，再向所有客户端广播 SpawnMessage（含 prefabId、netId、坐标）。每台客户端收到 SpawnMessage，按 prefabId 找到自己的同一份模板，各自 Instantiate，靠相同的 netId 把全网副本关联成同一个逻辑对象。场景实例绕过这条链，没有 netId，各放各的还和网络生成的重复，所以要删。

**出生位置**：NetworkManager 的 `playerSpawnMethod` 选 Random（随机挑出生点）或 RoundRobin（轮流使用）。出生点是场景里挂 `NetworkStartPosition` 组件的对象（Mirror 自带，Add Component 搜得到），摆几个空物体各挂一个即可；一个都没有时默认用原点。

## 配置 Build Settings

1. File → Build Profiles（或 Build Settings）→ Platform 选 **Windows** → Switch Platform。
2. **Scene List** 加入场景，0 号是构建启动场景，服务器与客户端都从它启动。Scene List 为空的构建没有可运行入口；表里挂着已删除的场景会在构建时被跳过，及时清理保持表和磁盘一致。
3. Player Settings → 勾选 **Run In Background**：服务器或后台窗口失焦后主循环照常跑，Mirror 的收发依赖主循环，失焦暂停会让握手包无人处理，表现成"客户端一直连不上"。

## 打服务器构建

1. Build Profiles → Windows → **Server** 子目标（2022.3 的 Server Build），构建。
2. 产物无图形设备，运行日志出现 `Forcing GfxDevice: Null` 与一批 `Shader ... not supported` 报错，这是无头渲染的正常输出。
3. Server 子目标的包里定义了 `UNITY_SERVER` 宏，Mirror 据此认定自己是服务器。自动开服用 NetworkManager 自带的 **Headless Start Mode** 参数：Inspector 里把它从 `DoNothing` 改成 **AutoStart Server**。判定逻辑在 NetworkManager 的 `Start()` 里：`Utils.IsHeadless()` 为 true 时按此字段调用 `StartServer()` 或 `StartClient()`。Server 包下 IsHeadless 恒为 true，双击 exe 即开服。
4. 启动成功的标志：日志出现 `Server listening on port 7777`。

Headless Start Mode 的三个值：`DoNothing` 什么都不做；`AutoStartServer` 无头进程自动开服；`AutoStartClient` 无头进程自动向 `networkAddress` 连接。编辑器里默认不触发，勾上 `editorAutoStart` 才在 Play 时生效。

## 配置客户端连接

客户端构建需要知道服务器地址。两种方式：

- NetworkManager 的 **Headless Start Mode** 设为 `AutoStart Client`，`networkAddress` 填服务器 IP，客户端 exe 双击自动连接；
- 或用 NetworkManagerHUD 的输入框手动填地址后点 Client。

**地址规则：本机填 `127.0.0.1`，跨机填服务器 IPv4。**

`localhost` 在 Windows 上解析成 IPv6 的 `::1`，服务器 KCP socket 在部分机器上实际是纯 IPv4 绑定，发往 `::1` 的握手包到不了服务器，客户端表现为发送 hello 后 10 秒超时循环。这是本工程实际踩过的坑，绑定地址一律写 IPv4 字面量。

## 验证

1. 启动服务器，确认日志有 `Server listening on port 7777`。
2. 双开客户端构建。
3. 服务器日志出现两条 `Server: OnConnected`，每个客户端日志出现 `Client: OnConnected`。
4. 客户端画面各自出现玩家对象；移动本地玩家，另一台客户端能看到位置同步。
5. 排查手段：KcpTransport 勾上 debugLog，日志按 `[KCP]` 前缀过滤；客户端卡在 `sending handshake` 收不到回包，按地址规则先查地址族。

## 常见配置错误速查

| 现象 | 原因与处理 |
|---|---|
| CS0101 MoveToAssetsFolder 重复定义 | `Assets/ScriptTemplates/` 与 Mirror 自带目录重复，留一份 |
| StartServer 抛空引用 | NetworkManager 的 Transport 槽位为空，手动拖入 KcpTransport |
| 客户端 hello 发不出回包，10 秒超时循环 | 地址填了 `localhost`，改 `127.0.0.1` 或服务器 IPv4 |
| 服务器启动报 SocketException 端口占用 | 上一轮进程未退，任务管理器结束全部服务器 exe |
| 服务器日志停在 ThreadLog，无 listening | Headless Start Mode 还是 DoNothing，改成 AutoStart Server 后重新构建 |
| 客户端完全无连接行为 | 检查场景里是否有 NetworkManager 对象；检查构建 Scene List 非空且与磁盘一致 |
| 后台窗口连不上 | Player Settings 勾 Run In Background |
| 构建产物无 Mirror | 当前构建的源工程没装 Mirror，看 `Managed/` 里有没有 `Mirror.dll` |
