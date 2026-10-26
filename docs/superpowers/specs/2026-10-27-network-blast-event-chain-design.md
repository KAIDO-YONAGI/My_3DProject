# NetworkBlastManager 爆炸事件链路设计

## 目标

通过 ScriptableObject 事件管线将本地攻击产生的爆炸请求提交给服务器，并由服务器通知其他观察者执行爆炸结算。

## 结构与数据流

- 新增 `BlastEventSO` 与 `BlastMessage`。消息包含来源 `GameObject`、爆炸中心、伤害值和水平半径。
- `Attack` 保留本地即时爆炸结算，并在成功命中后发布爆炸消息。
- `NetworkBlastManager` 仅由本地拥有者订阅；事件来源必须是该网络玩家对象，客户端 Ready 后才调用现有 Command。
- 服务器经 `ClientRpc(includeOwner = false)` 将爆炸请求转给观察者；远端按爆炸中心和半径执行结算，本地拥有者不重复结算。
- 禁用组件或本地玩家身份结束时解除订阅。

## 失败处理

缺失 SO 引用时记录组件错误；来源不匹配或客户端未 Ready 时忽略网络提交。

## 验证范围

检查事件发布、拥有者订阅与清理、Ready 门控、Command/RPC 所有者排除和相关 Multiplayer 文档。
