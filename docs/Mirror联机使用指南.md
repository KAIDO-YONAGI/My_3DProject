# Mirror 联机使用指南

本指南面向日常联调：启动服务器、开客户端、确认连接、排查问题。工程配置的事实来源是 `Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md`。

## 启动流程

1. 启动服务器：

```text
D:/Unity/Releases/3D_MultiplayerGame/Server/Server_3_0/My_3DProject.exe -batchmode -nographics -logFile server.log
```

2. 双开客户端：运行两次

```text
D:/Unity/Releases/3D_MultiplayerGame/Client/Client_3_0/My_3DProject.exe
```

3. 确认连接：`server.log` 出现两条 `Server: OnConnected`；客户端窗口出现两个白色胶囊玩家，WASD 移动、空格跳跃，远端玩家位置同步。

4. 结束联调：任务管理器结束全部 `My_3DProject.exe`，释放 7777 端口。

## 编辑器内测试

打开 `Assets/Core/Scenes/LobbyScene.unity`，Play 后用左上角 NetworkManagerHUD 选择：

- Server Only：本机起服，再开客户端构建连它。
- Host：本机起服并自带一个本地玩家，第二个客户端构建连进来。
- Client：作为客户端连接 `networkAddress`（填 `127.0.0.1`）。

## 构建新版本

- 修改了脚本或场景后，客户端与服务器构建分别重新生成，改动才会进入 exe。
- 客户端：Build Profiles → Windows，输出到 `D:/Unity/Releases/3D_MultiplayerGame/Client/Client_3_0/`。
- 服务器：Build Profiles → Windows Server，输出到 `D:/Unity/Releases/3D_MultiplayerGame/Server/Server_3_0/`。

## 常见问题

| 现象 | 处理 |
|---|---|
| 客户端连不上，无 KCP 日志 | 服务器日志确认有 `Server listening on port 7777`；客户端地址确认是 `127.0.0.1` |
| 客户端发送握手后无响应，约 10 秒断开重连 | 地址填了 `localhost`，改为 `127.0.0.1`：`localhost` 解析为 IPv6 `::1`，服务器 KCP socket 降级为 IPv4 绑定时握手包丢失 |
| 服务器启动即 SocketException | 上一轮进程占用 7777，任务管理器结束全部 `My_3DProject.exe` 后重启 |
| 构建产物完全没有网络行为 | 检查 `My_3DProject_Data/Managed/` 下是否有 `Mirror.dll`、`kcp2k.dll`；缺失说明构建源工程未装 Mirror |
| 服务器日志刷 Shader ERROR | 无头服务器的正常输出，忽略 |
| 服务器无响应但进程存在 | headless 进程在后台持续运行，属正常；确认它是否监听用 `netstat -ano | findstr 7777` |

## 换机部署

1. 新机器安装 Unity 2022.3.62f3c1 后打开工程，按 `Mirror_KCP_Config.md` 的安装章节重新导入 Mirror。
2. 分别构建服务器与客户端。
3. 客户端 Inspector 中把 `AutoStartServerBuild.connectAddress` 改成服务器局域网 IPv4 地址，重新构建客户端。
4. 服务器侧确认防火墙放行 UDP 7777 入站。
