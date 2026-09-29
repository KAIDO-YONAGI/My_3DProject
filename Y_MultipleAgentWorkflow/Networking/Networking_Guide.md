# UDP 网络与异步模型

文档 ID：`NETWORK-GUIDE`
状态：`Retired`
最后核验：`2026-09-29`

> **本 Guide 描述的自研 UDP 传输栈已于 2026-09-29 退役删除**（提交 `5c2b019`，归档 tag `v0.2-selfbuilt-net`）。`UdpConnection`、`NetManager` 及其线程模型不再存在于工作区，本文件保留作为 git 历史的解读参考，不作为当前事实依据。

## 退役原因

- 自研 UDP 缺失握手、确认、重传、序号、心跳、超时清理等能力（原文档"当前不具备的能力"一节），全部属于业界已解决问题。
- 项目网络层切换至 Mirror KCP（本地插件，不入库）；固定 Tick、快照插值、预测校正等同步模型按 `docs/plan/` 八阶段计划自研。
- 三代网络栈演进（自研 TCP → 自研 UDP → Mirror 传输 + 自研同步模型）作为求职叙事保留在 git 历史与 tag 中。

## 历史设计存档（仅参考 git 历史）

- `NetManager`：DontDestroyOnLoad 单例，ConcurrentQueue 桥接异步回调与 Unity 主线程。
- `UdpConnection`：Volatile/Interlocked 状态标志、SemaphoreSlim 发送串行化、async 接收循环。
- 地址固定 `127.0.0.1:8888`，玩家 ID 为 UDP 端点字符串。
- 无认证、无可靠性、无心跳；接收异常关闭后不发布断线事件。

## 替代文档

Mirror 传输与后续自研同步模型落地后，本知识域将重写为 Mirror KCP 传输配置与自研快照/插值/预测的权威文档。当前项目网络链路状态见 `../Project_Overview.md`。
