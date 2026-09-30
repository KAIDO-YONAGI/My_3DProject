# Mirror 与 KCP 工程配置

文档 ID：`UNITY-NETCFG`
状态：`Active`
最后核验：`2026-09-30`

## 配置总览

| 配置项 | 当前值 | 位置 |
|---|---|---|
| Mirror 版本 | 96.11.2 | `Assets/Mirror/` |
| Transport | `kcp2k.KcpTransport` | `PersistentScene` → `NetworkManager` 对象 |
| 服务器端口 | `7777`（UDP） | KcpTransport.port |
| DualMode | `true` | KcpTransport.DualMode |
| debugLog | `true` | KcpTransport.debugLog，压测与发布前关闭 |
| 玩家 Prefab | `Assets/Core/Prefabs/Player_Network.prefab` | NetworkManager.playerPrefab |
| autoCreatePlayer | `true` | NetworkManager |
| 场景切换 | `onlineScene = 空`，由 `SceneChanger` 通过 Additive 加载 | `PersistentScene` |
| NetworkManager 生命周期 | `dontDestroyOnLoad = false` | `PersistentScene` 持有 |
| Build 场景 | `PersistentScene`、`MultiplayerSampleScene`，两者均启用 | Build Settings |
| runInBackground | `true` | PlayerSettings |
| 客户端连接地址 | `127.0.0.1` | NetworkManager.networkAddress + AutoStartClient.connectAddress |

## Mirror 安装

Mirror 以本地插件形式存在，`.gitignore` 忽略 `/Assets/Mirror/` 与 `/Assets/Plugins/`。换机器或误删后重新导入：

1. Asset Store 的 Package Manager → My Assets → Mirror 重新下载导入，或从 GitHub github.com/MirrorNetworking/Mirror 下载 release 的 `.unitypackage`。
2. 本机备用源：Asset Store 缓存解压包 `D:/Unity/Temp/Mirror-96.11.2-extracted`，hash 目录下的 `pathname` 文件记录 `Assets/...` 原始路径，可按清单还原。
3. 导入后检查 `Library/ScriptAssemblies/` 下存在 `Mirror.dll`、`Mirror.Components.dll`、`Mirror.Transports.dll`、`kcp2k.dll`。
4. Mirror 自带 `ScriptTemplates/` 目录与搬运工具 `MoveToAssetsFolder.cs`。`Assets/ScriptTemplates/` 下同时存在两份时产生 `CS0101` 重复定义编译错误，保留一份。

## PersistentScene 联机入口与单机入口

```text
PersistentScene
├─ Managers
│  ├─ NetworkManager
│  │  ├─ Mirror.NetworkManager
│  │  ├─ kcp2k.KcpTransport
│  │  ├─ Mirror.NetworkManagerHUD
│  │  └─ AutoStartClient
│  ├─ TimeManager
│  └─ SceneChanger
└─ LocalPlayer
   └─ 本地角色 Prefab（包含本地 Camera）
```

NetworkManager 的 `transport` 字段指向同对象上的 `KcpTransport`。`PersistentScene` 是项目自己的常驻场景，由 `SceneChanger.firstSceneToLoad` 引用 `GameSceneSO`，以 Additive 模式加载 `MultiplayerSampleScene`。本地单机角色和本地 Camera 直接配置在 `PersistentScene` 中，Play 时不会通过运行时 Instantiate 生成。

Mirror 的 `onlineScene` 必须保持为空，`dontDestroyOnLoad` 必须关闭。这样 Mirror 只负责连接、身份、网络玩家 Prefab 和同步，不会绕过项目框架切换场景，也不会把 NetworkManager 脱离 `PersistentScene`。`NetworkManager.playerPrefab` 仍指向 `Assets/Core/Prefabs/Player_Network.prefab`，不得指向本地视觉模型 Prefab。

单机开发时直接打开 `Assets/Core/Scenes/PersistentScene.unity` 后按 Play：编辑器可见的 `LocalPlayer` 保留在常驻场景，`SceneChanger` additive 加载玩法场景。需要切换模型时修改 `LocalPlayer` 的 Prefab 或其引用，不改 Mirror 的 `playerPrefab`。

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

## 当前玩家 Prefab

```text
Player_Network
├─ Mirror.NetworkIdentity
├─ Mirror.NetworkTransformReliable        SyncDirection = ClientToServer
├─ CharacterController
├─ PlayerCharacterController              联机时仅本地玩家采集输入
└─ NetworkCharacterSync                    SyncVar characterId，只同步角色编号
```

`PersistentScene/Managers/NetworkCharacterManager` 是编辑器可见的联机角色逻辑中心，配置 `localCharacterPrefabs[]`、`characterPrefabs[]` 和 `defaultCharacterId`。两个数组按下标对齐：本地拥有者从 `CharactersForLocal` 加载带本地相机的模型，远程拥有者从 `CharactersForSync` 加载不带相机的模型。`PlayerCharacterController` 保持普通 `MonoBehaviour`，通过同对象上可选的 `NetworkIdentity` 判断本地玩家。位置由本地拥有者驱动并经 `NetworkTransformReliable` 上传到服务端，再广播给其他客户端。`NetworkCharacterSync` 只同步 `characterId`，每个客户端由场景管理器按角色编号选择对应类别的视觉 Prefab；本地单机 `LocalPlayer` 仍是独立入口。

`PersistentScene` 不覆盖 `NetworkManager.playerPrefab`。Mirror 的玩家 Prefab 只在 `Assets/Core/Prefabs/NetworkManager.prefab` 配置一次，必须是 `Assets/Core/Prefabs/Player_Network.prefab`，不能直接指向 `CharactersForSync` 中的视觉 Prefab。

## 启动模式分流

- 专用服务器使用 `HeadlessStartMode = AutoStartServer`，批处理启动后自动监听 7777。
- `AutoStartClient` 在 Unity 编辑器和普通客户端构建中连接 `127.0.0.1`，断开后每 3 秒重试。
- `AutoStartClient` 在 `Application.isBatchMode` 或本机已启动 Server/Client 时不重复发起连接。

## 连接地址

客户端连接地址使用 `127.0.0.1`。`localhost` 的 DNS 解析结果为 `::1`，服务器 KCP socket 在部分机器上为纯 IPv4 绑定，发往 `::1` 的握手包到不了服务器，客户端表现为发送 hello 后 10 秒超时。跨机联机填写服务器 IPv4 地址。

## 构建

### 客户端

- 平台 Windows x64，构建包含 `PersistentScene` 与 `MultiplayerSampleScene`。
- 输出：`D:/Unity/Releases/3D_MultiplayerGame/Client/Client_5_0/My_3DProject.exe`。
- 校验：`My_3DProject_Data/Managed/` 含 `Mirror.dll`、`kcp2k.dll`。

### 专用服务器

- Build Profiles 选择 Windows Server，构建产物无图形设备。
- 启动方式：运行 `Server_7_0/My_3DProject.exe`，HeadlessStartMode = AutoStartServer 使其自动监听 7777。
- 启动成功标志：日志出现 `Server listening on port 7777`。
- 服务器日志中的 `Shader ... not supported` ERROR/WARNING 来自 Null 图形设备，属正常输出。
- 输出：`D:/Unity/Releases/3D_MultiplayerGame/Server/Server_7_0/My_3DProject.exe`。

从零复现整套配置的步骤与常见配置错误速查见 `docs/Mirror+KCP配置学习指南.md`。

## 最近验证证据

- 客户端 `Client_5_0` 与服务器 `Server_7_0` 均构建成功，结果为 0 error、1 warning。
- `Server_7_0 + Unity Editor Play` 已验证连接和 Ready 正常。
- 本地玩家 `娜娜莉（华丽飞踢）(Clone)` 的状态为 `local=True`、`owned=True`，移动启用，`SyncDirection=ClientToServer`。
- Host 模式下已验证角色 `characterId 0 -> 1 -> 0` 连续切换，角色持续可见，控制台为 0 error；角色替换前后的 `Animator` 重绑定已覆盖销毁时序。
- 注入前进输入后角色移动约 `2.82m`，等待 2 秒后没有被服务端位置拉回；Unity Console 为 0 error，用户确认角色可以移动。
- 验证结束后服务器进程已停止，UDP 7777 无监听，编辑器已退出 Play。

## 风险

- 端口占用：KCP 服务器绑定失败会抛出 `SocketException`，启动前确认 7777 端口干净。
- `debugLog` 每条 KCP 事件都写日志，压测与发布构建关闭。
- 客户端与服务端必须来自同一次组件布局；修改玩家身上的 NetworkBehaviour 列表后只重启一端，会触发 `OnDeserialize` / `EndOfStreamException`。
- 当前 `PlayerCharacterController` 不是 NetworkBehaviour，不得仅为判断本地玩家而改变其基类，否则会污染单机 Prefab 并改变 Mirror 组件序列。
- 当前联机为最小可玩形态：Mirror 组件直接同步。阶段七按 `docs/plan/` 引入固定 Tick、`InputFrame`、自定义快照插值与预测校正后，NetworkTransformReliable 与直连输入被替换，本文档同步更新。

## 维护触发

修改 Mirror 版本、Transport 参数、NetworkManager 配置、玩家 Prefab 组件、连接地址或构建输出时更新本文档。
