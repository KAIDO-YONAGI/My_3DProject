# Mirror 与 KCP 工程配置

文档 ID：`UNITY-NETCFG`
状态：`Active`
最后核验：`2026-09-29`

## 配置总览

| 配置项 | 当前值 | 位置 |
|---|---|---|
| Mirror 版本 | 96.11.2 | `Assets/Mirror/` |
| Transport | `kcp2k.KcpTransport` | `LobbyScene` → `NetworkManager` 对象 |
| 服务器端口 | `7777`（UDP） | KcpTransport.port |
| DualMode | `true` | KcpTransport.DualMode |
| debugLog | `true` | KcpTransport.debugLog，压测与发布前关闭 |
| 玩家 Prefab | `Assets/Core/Prefabs/Player_Network.prefab` | NetworkManager.playerPrefab |
| autoCreatePlayer | `true` | NetworkManager |
| 场景切换 | offline/online 均为空，阶段一单场景 | NetworkManager |
| Build 场景 | `LobbyScene`（0 号）、`MultiplayerSampleScene`（1 号） | Build Settings |
| runInBackground | `true` | PlayerSettings |
| 客户端连接地址 | `127.0.0.1` | `AutoStartServerBuild.connectAddress` |

## Mirror 安装

Mirror 以本地插件形式存在，`.gitignore` 忽略 `/Assets/Mirror/` 与 `/Assets/Plugins/`。换机器或误删后重新导入：

1. Asset Store 的 Package Manager → My Assets → Mirror 重新下载导入，或从 GitHub github.com/MirrorNetworking/Mirror 下载 release 的 `.unitypackage`。
2. 本机备用源：Asset Store 缓存解压包 `D:/Unity/Temp/Mirror-96.11.2-extracted`，hash 目录下的 `pathname` 文件记录 `Assets/...` 原始路径，可按清单还原。
3. 导入后检查 `Library/ScriptAssemblies/` 下存在 `Mirror.dll`、`Mirror.Components.dll`、`Mirror.Transports.dll`、`kcp2k.dll`。
4. Mirror 自带 `ScriptTemplates/` 目录与搬运工具 `MoveToAssetsFolder.cs`。`Assets/ScriptTemplates/` 下同时存在两份时产生 `CS0101` 重复定义编译错误，保留一份。

## LobbyScene 层级

```text
LobbyScene
├─ NetworkManager          Mirror.NetworkManager + NetworkManagerHUD + KcpTransport + AutoStartServerBuild
├─ Ground                  30×1×30 立方体地面，带 BoxCollider
└─ Directional Light
```

NetworkManager 的 `transport` 字段指向同对象上的 `KcpTransport`。`NetworkManagerHUD` 用于编辑器和客户端内手动选择 Server/Host/Client。

## KcpTransport 参数

以下为 Mirror 96 的默认值。

| 参数 | 值 | 说明 |
|---|---|---|
| `port` | 7777 | 服务器 UDP 监听端口 |
| `DualMode` | true | IPv6 socket + DualMode，兼容 IPv4 映射地址 |
| `NoDelay` | true | 降低发送延迟 |
| `Interval` | 10 ms | KCP 内部 tick 间隔 |
| `SendWindowSize` / `ReceiveWindowSize` | 4096 / 4096 | Mirror 调整过的窗口 |
| `MaximizeSocketBuffers` | true | 自动放大 OS socket 缓冲 |
| `Timeout` | 10000 ms | 对端无数据判定超时 |
| `FastResend` | 0 | 快速重传阈值 |
| `CongestionWindow` | false | 实时游戏默认关闭 |
| `MTU` | 1200 | 固定值 |
| `debugLog` | true | 打印 `[KCP] ...` 握手与连接日志，联调期开启 |

## 玩家 Prefab Player_Network

```text
Player_Network
├─ Transform（位置 (0, 0.55, 0)）
├─ Mirror.NetworkIdentity
├─ Mirror.NetworkTransformReliable        位置同步组件，阶段七改为自定义快照
├─ CharacterController
├─ NetworkPlayerController                isLocalPlayer 才读输入，服务器执行移动
└─ PlayerCameraRig
   ├─ Camera + AudioListener              远端实例禁用
   └─ LocalPlayerCamera                   仅本地玩家启用相机，LateUpdate 跟随
```

## 启动模式分流

`AutoStartServerBuild` 按运行环境分流启动行为：

- Server Build 检测到 `Application.isBatchMode` 后自动 `StartServer()`，端口占用抛 `SocketException` 时按 `serverRetryInterval` 每 2 秒轮询重试。
- 客户端构建自动 `StartClient()` 连接 `connectAddress`，断开后按 `clientRetryInterval` 每 3 秒重连。
- Unity 编辑器保持手动模式，通过 HUD 选择 Host/Server/Client。

## 连接地址

客户端连接地址使用 `127.0.0.1`。`localhost` 的 DNS 解析结果为 `::1`，服务器 KCP socket 在部分机器上为纯 IPv4 绑定，发往 `::1` 的握手包到不了服务器，客户端表现为发送 hello 后 10 秒超时。跨机联机填写服务器 IPv4 地址。

## 构建

### 客户端

- 平台 Windows x64，场景 0 号为 LobbyScene。
- 输出：`D:/Unity/Releases/3D_MultiplayerGame/Client/Client_3_0/My_3DProject.exe`。
- 校验：`My_3DProject_Data/Managed/` 含 `Mirror.dll`、`kcp2k.dll`。

### 专用服务器

- Build Profiles 选择 Windows Server，构建产物无图形设备。
- 启动命令：`My_3DProject.exe -batchmode -nographics -logFile server.log`。
- 启动成功标志：日志出现 `Server listening on port 7777` 与 `[AutoStartServerBuild] Server started on port 7777`。
- 服务器日志中的 `Shader ... not supported` ERROR/WARNING 来自 Null 图形设备，属正常输出。
- 输出：`D:/Unity/Releases/3D_MultiplayerGame/Server/Server_3_0/My_3DProject.exe`。

本机三端联调的操作步骤见 `docs/Mirror联机使用指南.md`。

## 风险

- 端口占用：KCP 服务器绑定失败抛 `SocketException`，`AutoStartServerBuild` 已做轮询重试，启动前确认 7777 端口干净。
- `debugLog` 每条 KCP 事件都写日志，压测与发布构建关闭。
- `MultiplayerSampleScene` 作为地形与角色美术基底，联机演示使用 LobbyScene。
- 当前联机为最小可玩形态：Mirror 组件直接同步。阶段七按 `docs/plan/` 引入固定 Tick、`InputFrame`、自定义快照插值与预测校正后，NetworkTransformReliable 与直连输入被替换，本文档同步更新。

## 维护触发

修改 Mirror 版本、Transport 参数、NetworkManager 配置、玩家 Prefab 组件、连接地址或构建输出时更新本文档。
