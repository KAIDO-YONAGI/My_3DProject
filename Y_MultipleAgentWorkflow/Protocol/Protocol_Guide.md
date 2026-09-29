# 客户端与服务端通信协议

文档 ID：`PROTOCOL-GUIDE`
状态：`Retired`
最后核验：`2026-09-29`

> **本文档描述的文本协议（`Enter/Move/Leave/Attack`，`|` 与 `,` 分隔、`\n` 终止、UTF-8 UDP 数据报）随自研网络栈于 2026-09-29 退役删除**（提交 `5c2b019`，归档 tag `v0.2-selfbuilt-net`）。双端协议代码已不存在于工作区，本文件保留作为 git 历史的解读参考，不作为当前事实依据。

## 历史格式存档（仅参考 git 历史）

| 类型 | 客户端到服务端 | 服务端到客户端 | 退役时状态 |
|---|---|---|---|
| `Enter` | `Enter|modelID,health,damage\n` | `Enter|address,modelID,health,damage\n` | 已接通 |
| `Move` | `Move|x,y,z\n` | `Move|address,x,y,z\n` | 已接通 |
| `Leave` | `Leave|\n` | `Leave|address\n` | 已接通 |
| `Attack` | `Attack|address\n` | `Attack|addressdamage\n` | 未接通且不对称 |

已知设计缺陷：无版本/长度/校验/序号字段；浮点未固定 `InvariantCulture`；顶层解析要求恰好两段；双端多消息拆分行为不对称。

## 新协议方向

新协议按 `docs/plan/` 计划在阶段七设计，核心变化：

- 输入方向：`InputFrame`（玩家标识、输入序号、目标 Tick、移动向量、动作位），经 Mirror Command/NetworkMessage 上传。
- 状态方向：自定义快照（服务器 Tick、快照序号、已处理输入序号、玩家/道具/回合状态）走不可靠通道；低频状态用 SyncVar；离散事件走可靠通道。
- 序列化：Mirror NetworkWriter/Reader 二进制序列化，替代文本协议。

新协议双端接通前，不得在任何文档中标记为"已接通"。
