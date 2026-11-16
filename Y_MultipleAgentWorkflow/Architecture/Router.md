# Architecture 知识域路由

文档 ID：`BUS-ARCHITECTURE`
状态：`Active`
维护计数：`1/5`
最后更新：`2026-11-16`

## 维护规则

任务路由、权威文档或职责边界变化时更新本文，详细文档索引在所属功能域 Router 就近维护。证据记录在[开发日志](DeveloperLog.md)；计数与维护周期遵循[工作流维护规则](../Workflow/Workflow_Guide.md#维护规则)。

## 任务路由

| 任务 | 权威文档 |
|---|---|
| 单例、`YSingleton`、`Instance`、`RequiredInstance`、单例为 null | `Composition/Composition_Guide.md` |
| 序列化引用校验、Awake 断言、`Debug.Assert`、数组元素校验 | `Composition/Composition_Guide.md` |
| 空值分类、可选引用、留空语义、运行时注入 | `Composition/Composition_Guide.md` |
| 初始化顺序、`OnSingletonInitialized`、允许暂缺的单例路径 | `Composition/Composition_Guide.md` |
| 订阅、退订、就绪标记、重复判空 | `Composition/Composition_Guide.md` |
| 程序集拆分、`asmdef` 与预定义程序集引用约束 | `../Project/Project_Guide.md` |

## 下级导航

| 子类 | Router |
|---|---|
| `Composition` | `Composition\Router.md` |

## 主要证据路径

- `Assets/Core/Scripts/Common/YSingleton.cs`
- `Assets/Core/Scripts/` 下各组件 `Awake`、`OnAwake` 与 `OnSingletonInitialized` 校验点
- `Y_MultipleAgentWorkflow/RULES.md`

## 并发资源

- `workflow:Architecture`
- `path:Y_MultipleAgentWorkflow/Architecture`

本域是文档域，不持有产品代码与资产目录的写资源。代码改动路由到对应业务域 Router，并按其资源取租约。

## 能力边界

维护跨功能的组合约定：单例访问语义、序列化引用的校验时机与断言形态、空值分类、初始化顺序与允许暂缺的单例路径、订阅与退订写法。具体业务行为归对应业务域 Guide。
