# Protocol 开发记录

## 2026-10-10：运动表现快照方案标记为 Proposal

- 当前协议仍为 Mirror 变换同步和角色编号；运动状态同步已按用户最新要求暂缓，未引入可用协议。
- Guide 记录未来状态 `byte`、局部方向 `Vector2`、权重 `float`、可靠 ClientToServer、时间轴、校验、缓冲与 Host 分流要求，明确所有内容为 Proposal。
- 兼容边界要求未来根节点末尾追加组件、同版本客户端与服务器配套构建；未进行协议或联机验收。纯文档维护，计数保持 `1/5`。

## 2026-09-29：建立协议模块权威入口

- 依据当前 Mirror 组件和玩家 Prefab 建立协议边界文档。
- 明确当前没有项目自定义协议源码，当前协议由 Mirror 内置消息和 NetworkTransform 序列化承载。
- 将 InputFrame、固定 Tick、状态快照和预测校正标记为 Proposal。
