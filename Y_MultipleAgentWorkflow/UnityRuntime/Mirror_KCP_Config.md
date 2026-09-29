# Mirror 与 KCP 联机配置文档

文档 ID：`UNITY-NETCFG`
状态：`Active`
最后核验：`2026-09-29`
适用版本：Unity `2022.3.62f3c1` + Mirror `96.11.2`（Asset Store 版，本地插件）

本文档是当前工程网络链路的配置事实与操作手册。历史自研 TCP/UDP 栈见 `Networking/Networking_Guide.md`（Retired）。

---

## 1. 当前配置总览

| 配置项 | 当前值 | 位置 |
|---|---|---|
| Mirror 版本 | 96.11.2 | `Assets/Mirror/`（本地插件，gitignored） |
| Transport | KCP（`kcp2k.KcpTransport`） | `LobbyScene` → `NetworkManager` 对象 |
| 服务器端口 | `7777`（UDP） | KcpTransport.port |
| DualMode | `true`（IPv6 DualMode socket，兼容 IPv4 映射） | KcpTransport.DualMode |
| debugLog | `true`（联调期开启，发布前关闭） | KcpTransport.debugLog |
| 玩家 Prefab | `Assets/Core/Prefabs/Player_Network.prefab` | NetworkManager.playerPrefab |
| autoCreatePlayer | `true` | NetworkManager |
| 场景切换 | offline/online 均为空（阶段一单场景） | NetworkManager |
| Build 场景 0 | `Assets/Core/Scenes/LobbyScene.unity` | Build Settings |
| Build 场景 1 | `Assets/Core/Scenes/MultiplayerSampleScene.unity` | Build Settings |
| runInBackground | `true` | PlayerSettings |
| 服务器构建 | Windows x64，`subtarget=server`（headless） | Build Profiles / 构建脚本 |

## 2. Mirror 安装方式

Mirror 以**本地插件**形式存在，不入库（`.gitignore` 忽略 `/Assets/Mirror/`、`/Assets/Plugins/`）。因此换机器或误删后需要重新导入：

1. Asset Store（Package Manager → My Assets → Mirror）重新下载导入，或从 GitHub（github.com/MirrorNetworking/Mirror）下载对应 release 的 `.unitypackage`。
2. 本机备用源：Asset Store 缓存解压包 `D:/Unity/Temp/Mirror-96.11.2-extracted`（hash 命名的 Asset Store 包格式，`pathname` 文件记录 `Assets/...` 原始路径，可脚本还原）。
3. 导入后确认编译产物存在：`Library/ScriptAssemblies/` 下应有 `Mirror.dll`、`Mirror.Components.dll`、`Mirror.Transports.dll`、`kcp2k.dll`。
4. 注意：Mirror 自带 `ScriptTemplates/` 目录与搬运工具 `MoveToAssetsFolder.cs`；工程 `Assets/ScriptTemplates/` 若已有旧残留会产生 `CS0101` 重复定义编译错误，保留一份即可（2026-09-29 已删除旧残留）。

## 3. 场景配置（LobbyScene）

场景路径 `Assets/Core/Scenes/LobbyScene.unity`，层级：

```text
LobbyScene
├─ NetworkManager          ← Mirror.NetworkManager + NetworkManagerHUD + KcpTransport + AutoStartServerBuild
├─ Ground                  ← 30×1×30 立方体地面（BoxCollider）
└─ Directional Light
```

### NetworkManager 对象组件

- `Mirror.NetworkManager`：PlayerPrefab 指向 `Player_Network.prefab`；`transport` 字段必须指向同对象上的 `KcpTransport`（曾出现组件添加后 transport 引用为空导致连接失败，脚本里通过 `nm.transport = GetComponent<KcpTransport>()` 已修复并保存）。
- `Mirror.NetworkManagerHUD`：编辑器/客户端内显示 Server/Host/Client 手动按钮（左上角），调试用。
- `kcp2k.KcpTransport`：见下表。
- `AutoStartServerBuild`：见第 5 节。

### KcpTransport 关键参数（Mirror 96 默认值）

| 参数 | 默认值 | 说明 |
|---|---|---|
| `port` | 7777 | 服务器 UDP 监听端口 |
| `DualMode` | true | IPv6 socket + DualMode，同时接受 IPv4 映射地址 |
| `NoDelay` | true | 关闭 Nagle 类延迟，实时游戏保持默认 |
| `Interval` | 10 ms | KCP 内部 tick 间隔 |
| `SendWindowSize` / `ReceiveWindowSize` | 4096 / 4096 | Mirror 调大过的窗口（原版 kcp 32/128） |
| `MaximizeSocketBuffers` | true | 自动放大 OS socket 缓冲 |
| `Timeout` | 10000 ms | 对端无数据判定超时 |
| `FastResend` | 0 | 快速重传阈值（0=关闭） |
| `CongestionWindow` | false | 拥塞窗口关闭（实时游戏默认关闭） |
| `MTU` | 1200（Kcp.MTU_DEF） | 本 transport 固定，不可配置 |
| `debugLog` | true（联调期） | 打印 `[KCP] ...` 握手/连接日志；发布构建建议关闭 |

## 4. 玩家 Prefab（Player_Network）

路径 `Assets/Core/Prefabs/Player_Network.prefab`，必须注册到 NetworkManager.playerPrefab 且带 `NetworkIdentity`：

```text
Player_Network
├─ Transform（位置 (0, 0.55, 0)）
├─ Mirror.NetworkIdentity        ← Prefab 注册的前提
├─ Mirror.NetworkTransformReliable  ← 位置同步（阶段七将替换为自定义快照）
├─ CharacterController           ← 移动碰撞
├─ NetworkPlayerController       ← 自研：isLocalPlayer 才读输入，服务器执行 Move
└─ PlayerCameraRig（子对象）
   ├─ Camera + AudioListener     ← 远端实例由 LocalPlayerCamera 禁用
   └─ LocalPlayerCamera          ← 自研：仅本地玩家启用相机，LateUpdate 跟随
```

原则：`NetworkPlayerController` 用 `isLocalPlayer` 守卫输入；相机/AudioListener 只在本地实例激活，避免多监听器冲突。

## 5. 启动模式分流（AutoStartServerBuild）

脚本 `Assets/Core/Scripts/Networking/AutoStartServerBuild.cs`，挂在 NetworkManager 对象上：

- **Server Build**（`Application.isBatchMode`）：自动 `StartServer()`；端口被占用抛 `SocketException` 时按 `serverRetryInterval`（2s）轮询重试。之前"连不上"的一类原因就是上一轮测试进程未死透占着 7777，`Start()` 只尝试一次且失败静默。
- **客户端构建**（非 batch、非编辑器）：自动 `StartClient()` 连接 `connectAddress`，断开后按 `clientRetryInterval`（3s）重连。
- **Unity 编辑器**：不自动启动，用 HUD 手动选 Host/Server/Client，便于同机多开测试。

## 6. 客户端连接地址：必须用 `127.0.0.1`，不要用 `localhost`

**这是本次排查到的连接失败根因。**

- KCP 客户端把 `localhost` 经 DNS 解析，Windows 上通常得到 `::1`（IPv6）优先；
- 服务器 KCP `DualMode=true` 时理论上 ::1 也能到达，但在部分机器上 DualMode socket 创建会降级为纯 IPv4（`0.0.0.0`），此时 ::1 的握手包永远到不了服务器，客户端表现为发 hello 后无响应、10 秒超时断开重连；
- 服务器与客户端两侧开启 `KcpTransport.debugLog` 后，可从日志直接判定：
  - 客户端 `[KCP] Client: connect to ...` → `sending handshake` → `received initial cookie` → `received hello` → `Client: OnConnected`；
  - 服务器 `[KCP] ServerConnection: received hello` → `Server: added connection(id)` → `Server: OnConnected(id)`；
  - 卡在 `sending handshake` 重复出现即地址族不匹配。

**结论：LAN/本机联调一律填 `127.0.0.1`；跨机联机填服务器 IPv4 地址。** 发布给别人的客户端把 Inspector 里 `AutoStartServerBuild.connectAddress` 改成目标地址即可。

## 7. 构建配置

### 客户端构建

- 平台 Windows x64；Build Settings 场景：`LobbyScene`（0 号，启动场景）+ `MultiplayerSampleScene`。
- 输出参考：`D:/Unity/Releases/3D_MultiplayerGame/Client/Client_3_0/My_3DProject.exe`。
- 校验：`My_3DProject_Data/Managed/` 必须含 `Mirror.dll`、`kcp2k.dll` 等，缺失即构建源工程没装 Mirror。

### 专用服务器构建（Dedicated Server）

- 使用 **Build Profiles → Windows Server**（或 `EditorUserBuildSettings` 的 server subtarget），构建产物运行时无图形设备，日志出现 `Forcing GfxDevice: Null` 与一批 `Shader ... not supported` ERROR/WARNING——**这些是无头服务器的正常输出，不是故障**。
- 启动命令：`My_3DProject.exe -batchmode -nographics -logFile server.log`。
- 启动成功标志：日志出现 `[AutoStartServerBuild] Server started on port 7777` 与 `Server listening on port 7777`。
- 排查卡死/不监听：先 `netstat -ano | findstr 7777` 确认端口是否被上一轮进程占用（headless 服务器 Ctrl+C 未必杀干净，任务管理器结束 `My_3DProject.exe`）。
- 服务器构建同样必须包含 Mirror 程序集（`Server_3_0/Managed/` 里检查），旧 `Server_2_0` 就是没有 Mirror 的空壳。

### 本机三端联调流程

1. 启动服务器：`Server_3_0/My_3DProject.exe -batchmode -nographics -logFile server.log`。
2. 双开客户端：两次运行 `Client_3_0/My_3DProject.exe`（客户端自动连 127.0.0.1:7777）。
3. 验证：`server.log` 出现两条 `Server: OnConnected`；各客户端窗口出现两个白色 capsule 玩家，WASD 移动、空格跳跃，且远端玩家位置同步。
4. 结束后用任务管理器结束全部 `My_3DProject.exe`，避免端口残留。

## 8. 已知问题与边界

- **SocketException 端口占用**：KCP 服务器绑定失败会抛异常；`AutoStartServerBuild` 已做轮询重试，但仍建议启动前确认端口干净。
- **debugLog 性能**：开启后每条 KCP 事件都走 Debug.Log，仅用于联调，压测/发布构建关闭。
- **NetworkManagerHUD**：客户端构建里 HUD 仍显示（阶段一调试用）；它只在点击时生效，不会自动抢占连接。
- **MultiplayerSampleScene**：已剥离全部网络组件，仅作为地形/角色美术基底；联机演示一律用 LobbyScene。
- **阶段七计划**：本配置是最小可玩联机（Mirror 组件直同步）。固定 Tick、`InputFrame`、自定义快照插值与预测校正按 `docs/plan/06-08` 替换 NetworkTransformReliable 与直连输入，届时本文档同步重写。

## 9. 快速排障表

| 现象 | 先查 | 再查 |
|---|---|---|
| 客户端连不上，无任何 KCP 日志 | 服务器是否真的在监听（日志 `Server listening`） | 客户端 `connectAddress` 是否 127.0.0.1 |
| 客户端 hello 发出但无 cookie 回包 | 服务器端口被占（netstat 7777） | 地址族不匹配（localhost→::1 问题，见第 6 节） |
| 服务器启动即 SocketException | 上一轮进程未退（任务管理器杀 My_3DProject.exe） | 防火墙/其他程序占用 7777 |
| 构建后完全无网络行为 | 构建产物 Managed/ 有无 Mirror.dll | 场景里 NetworkManager 是否在 Build 场景 0 |
| 服务器日志刷 Shader ERROR | 无头服务器正常现象 | 无需处理 |
