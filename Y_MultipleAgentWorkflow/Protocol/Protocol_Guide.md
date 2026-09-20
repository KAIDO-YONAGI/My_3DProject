# 客户端与服务端通信协议

文档 ID：`PROTOCOL-GUIDE`
状态：`Active`
最后核验：`2026-09-20`

## 基础格式

- 传输：UDP 数据报。
- 编码：UTF-8 文本。
- 类型与参数分隔符：`|`。
- 参数字段分隔符：`,`。
- 消息终止符：`\n`。
- 消息类型名称区分大小写，线上发送枚举名称字符串。
- 当前没有版本、长度、校验和、序号或时间戳字段。

## 已实现消息

| 类型 | 客户端到服务端 | 服务端到客户端 | 状态 |
|---|---|---|---|
| `Enter` | `Enter|modelID,health,damage\n` | `Enter|address,modelID,health,damage\n` | 已接通 |
| `Move` | `Move|x,y,z\n` | `Move|address,x,y,z\n` | 已接通 |
| `Leave` | `Leave|\n` | `Leave|address\n` | 已接通 |
| `Attack` | `Attack|address\n` | 当前方法生成 `Attack|addressdamage\n` | 未接通且不对称 |

## 解析约束

- 双端顶层解析都要求拆分后恰好有两个部分，额外的 `|` 会导致消息被丢弃。
- `Enter` 要求精确字段数量并使用 `int.TryParse`。
- 客户端 `Move` 要求 `address + 3` 个坐标字段，并使用 `float.TryParse`。
- 服务端不解析移动坐标，只转发客户端原始移动参数。
- 数值格式没有指定 `InvariantCulture`；浮点小数格式与逗号字段分隔符存在区域设置冲突风险。

## 身份语义

服务端把 UDP 远端端点字符串作为 `address` 和玩家 ID。客户端把本地 Socket 端点字符串作为自身 ID，并用它过滤自己的移动回包。

## 已确认不对称

1. `Attack` 没有服务端处理分支，客户端同步入口也没有注册监听。
2. `PackAttacked` 把 `address` 与 `damage` 直接拼接，客户端解析器却把整个负载当作玩家 ID。
3. 服务端支持在一个数据报中按换行拆分多条消息；客户端只移除数据报末尾换行，不拆分内部多消息。
4. 客户端对 `modelID` 有 Prefab 索引范围检查，服务端没有对应约束。

## 变更规则

修改协议时必须同时核对并更新：

- 客户端打包、解析和监听注册。
- 服务端打包、解析和处理分支。
- 调用方、广播策略和错误处理。
- 本文档、`Client_Guide.md`、`Server_Guide.md` 和必要的网络文档。

未经双端验证，不得把新增类型标记为“已接通”。
