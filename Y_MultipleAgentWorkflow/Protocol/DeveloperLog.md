# Protocol 开发记录

## 2026-09-29：建立协议模块权威入口

- 依据当前 Mirror 组件和玩家 Prefab 建立协议边界文档。
- 明确当前没有项目自定义协议源码，当前协议由 Mirror 内置消息和 NetworkTransform 序列化承载。
- 将 InputFrame、固定 Tick、状态快照和预测校正标记为 Proposal。
