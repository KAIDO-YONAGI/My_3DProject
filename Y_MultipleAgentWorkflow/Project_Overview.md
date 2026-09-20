# 项目总览

文档 ID：`PROJ-OVERVIEW`
状态：`Active`
最后核验：`2026-09-20`

## 项目边界

本仓库包含两个运行部分：

- Unity 客户端：主要代码位于 `Assets/Core/Scripts/`，场景位于 `Assets/Core/Scenes/`，事件资产位于 `Assets/Core/EventSOs/`。
- 独立 C# UDP 服务端：位于 `LocalServer/`，目标框架为 `net10.0`，入口与业务脚本位于 `LocalServer/Scripts/`。

## 核心运行链

1. Unity 场景中的 `SyncCharacter` 请求 `NetManager` 建立到 `127.0.0.1:8888` 的 UDP 连接。
2. `UdpConnection` 设置默认远端并启动异步接收；该成功状态不代表服务端已响应。
3. 客户端发送 `Enter`，服务端以 UDP 远端端点字符串作为玩家 ID 注册状态。
4. 服务端向其他客户端广播新玩家，并把已有玩家逐个同步给新客户端。
5. 客户端约每 `0.05` 秒发送一次位置；服务端转发 `Move`；接收端在 Unity 主线程更新远端角色位置。
6. 客户端主动断开时发送 `Leave`；服务端移除注册并广播离开消息。

## 状态归属

- 客户端拥有本地角色 Transform、远端 Prefab 实例和本地消息消费队列。
- 服务端以远端端点为身份键，保存 `modelID`、`health`、`damage` 和进入状态。
- 当前移动坐标由客户端提供，服务端不解析或校验坐标，仅转发。
- `Attack` 已在双端协议枚举中定义，但当前业务链未完整接通。

## 权威边界

- 客户端行为：`Client/Client_Guide.md`
- 网络传输：`Networking/Networking_Guide.md`
- 服务端行为：`Server/Server_Guide.md`
- 线协议：`Protocol/Protocol_Guide.md`
- Unity 场景与资产：`UnityRuntime/UnityRuntime_Guide.md`

## 已确认的高风险边界

- UDP 无握手、确认、重传、顺序保证、身份认证或加密。
- 服务端和客户端均绑定本机地址配置，当前不是可直接跨机器部署的网络配置。
- 浮点文本格式未固定为 `InvariantCulture`，同时协议使用逗号分隔字段。
- Unity Build Settings 当前没有配置场景。
- 原有旧文档未纳入本权威库，本页结论只来自代码、项目配置和 Unity 序列化资产。
