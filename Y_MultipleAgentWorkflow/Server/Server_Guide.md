# 服务端核心、注册与广播

文档 ID：`SERVER-GUIDE`
状态：`Retired`
最后核验：`2026-09-29`

> **本文档描述的独立 `LocalServer`（net10.0 UDP 控制台服务端）已于 2026-09-29 退役删除**（提交 `5c2b019`，归档 tag `v0.2-selfbuilt-net`）。`LocalServer/` 目录已不存在，本文件保留作为 git 历史的解读参考，不作为当前事实依据。

## 退役原因

- 服务端形态切换为 **Unity Dedicated Server Build**（`docs/plan/07-阶段七` 落地），与客户端共享玩法模拟程序集；独立控制台工程与该目标冲突。
- 旧服务端是纯转发管道（`Move` 客户端权威、无校验、无结算），新架构要求服务器权威的固定 Tick 模拟。

## 历史设计存档（仅参考 git 历史）

- `ServerCore.Main`：绑定 `127.0.0.1:8888`，串行接收循环，按换行拆分多消息。
- `ServerClientRegistry`：`ConcurrentDictionary`，以 `IP:port` 为键。
- `ServerNetHandler`：处理 `Enter/Move/Leave`；`Attack` 枚举存在但无处理分支。
- `ServerMessageBroadcaster` / `ServerSocketSender`：广播与共享锁串行发送。
- 无心跳、无超时、无认证、无长度与速率限制。

## 替代文档

阶段七落地后，本知识域将重写为 Mirror NetworkManager 服务器生命周期、玩家注册与 Server Build 的权威文档。
