# Architecture.Composition Router

文档 ID：`BUS-ARCHITECTURE-COMPOSITION`
状态：`Active`
维护计数：`1/5`
最后更新：`2026-11-16`

## 维护规则

任务路由、权威文档或职责边界变化时更新本文，详细文档索引在所属功能域 Router 就近维护。证据记录在[开发日志](DeveloperLog.md)；计数与维护周期遵循[工作流维护规则](../../Workflow/Workflow_Guide.md#维护规则)。

## 任务线索

| 线索 | 权威文档 |
|---|---|
| `单例`、`YSingleton`、`Instance`、`RequiredInstance`、单例为 null | `Composition_Guide.md` §2.1、§3.5 |
| `序列化引用`、Awake 校验、`Debug.Assert`、数组元素 | `Composition_Guide.md` §2.2、§3.6 |
| `空值`、可选引用、留空、运行时注入 | `Composition_Guide.md` §2.3、§3.4 |
| `初始化顺序`、`OnSingletonInitialized`、允许暂缺路径 | `Composition_Guide.md` §2.4 |
| `订阅`、`退订`、就绪标记、重复判空 | `Composition_Guide.md` §2.5 |
| `Instance?.`、防御性兜底、调用点判空 | `Composition_Guide.md` §3.1、§3.2 |

## 下级导航

| 子类 | Router |
|---|---|
| 无 | 无 |

## 主要证据路径

- `Assets/Core/Scripts/Common/YSingleton.cs`
- `Assets/Core/Scripts/` 下的校验点与单例调用点
- `Y_MultipleAgentWorkflow/RULES.md`

## 并发资源

- `workflow:Architecture.Composition`
- `path:Y_MultipleAgentWorkflow/Architecture/Composition`

`Assets/Core/Scripts/` 以只读证据源登记；写入该目录需取得对应业务域的租约。

## 能力边界

`Composition_Guide.md` 是本域唯一权威文档，覆盖单例访问语义与 `RequiredInstance` 边界、序列化引用校验时机与断言形态、空值四分类、初始化期允许暂缺清单、订阅退订写法与硬边界。可据此判定新增组件是否需要 Awake 断言、单例引用应使用哪种入口、空值是否合法。
