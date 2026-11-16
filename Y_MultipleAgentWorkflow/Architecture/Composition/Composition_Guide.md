# 组合与空值处理 Guide

文档 ID：`ARCH-COMPOSITION-GUIDE`
状态：`Active`
最后更新：`2026-11-16`

## 维护规则

本文件对应的实现、约束或规则发生变化时，按已核验事实更新相关章节，证据记录在[开发日志](DeveloperLog.md)。修改小标题时同步文档内目录；计数与维护周期遵循[工作流维护规则](../../Workflow/Workflow_Guide.md#维护规则)。

## 文档内目录

- [1. 触发线索](#1-触发线索)
- [2. 当前实现](#2-当前实现)
  - [2.1 单例基类与两种访问语义](#21-单例基类与两种访问语义)
  - [2.2 序列化引用校验](#22-序列化引用校验)
  - [2.3 空值分类与判定](#23-空值分类与判定)
  - [2.4 允许暂缺的初始化期与生命周期路径](#24-允许暂缺的初始化期与生命周期路径)
  - [2.5 订阅、退订与就绪标记](#25-订阅退订与就绪标记)
- [3. 约定与硬边界](#3-约定与硬边界)
  - [3.1 禁止 `Instance?.Method()`](#31-禁止-instancemethod)
  - [3.2 必填引用不在调用点判空](#32-必填引用不在调用点判空)
  - [3.3 断言只在编辑器与开发构建生效](#33-断言只在编辑器与开发构建生效)
  - [3.4 可选引用必须写明留空语义](#34-可选引用必须写明留空语义)
  - [3.5 `RequiredInstance` 的使用边界](#35-requiredinstance-的使用边界)
  - [3.6 集合容器与元素分别判定](#36-集合容器与元素分别判定)

先读目录，再按任务读取相关章节，避免整份指南载入上下文。

权威范围：单例访问、序列化引用校验、空值分类与初始化时序约定。具体业务链路归各业务域 Guide，本文件不复制字段配置与场景接线。

## 1. 触发线索

| 线索 | 指向 |
|---|---|
| `单例`、`YSingleton`、`Instance`、`RequiredInstance`、单例为 null | §2.1、§3.5 |
| `序列化引用`、Inspector 拖引用、资产缺失、Awake 校验 | §2.2、§3.2 |
| `Debug.Assert`、断言、数组元素、集合校验 | §2.2、§3.6 |
| `空值`、可选引用、留空、运行时注入 | §2.3、§3.4 |
| `初始化顺序`、`OnSingletonInitialized`、管理器尚未注册 | §2.4 |
| `订阅`、`退订`、就绪标记、重复判空 | §2.5 |
| `?.`、`Instance?.`、防御性兜底 | §3.1、§3.2 |

## 2. 当前实现

### 2.1 单例基类与两种访问语义

`YSingleton<T>` 位于 `Assets/Core/Scripts/Common/YSingleton.cs`。`Awake` 先做重复实例判定，命中时停用组件、`Destroy(this)` 后直接返回；合法实例写入静态字段并调用钩子 `OnSingletonInitialized()`；`OnDestroy` 只在当前实例匹配时复位。`Instance` 只读、无惰性创建。

两种访问语义：

- `Instance`：允许为空。供初始化期、销毁期、未连接、批处理、组件测试隔离等允许暂缺的路径使用，调用点保留判空并写明原因。
- `RequiredInstance`：必需依赖。未注册或已销毁时抛出带类型名的 `InvalidOperationException`，调用点不再判空。

当前单例：`SceneChanger`、`UIManager`、`TimeManager`、`AudioPlayerManager`、`NetworkCharacterManager`、`VoxelPlatformDestruction`。

### 2.2 序列化引用校验

必填的序列化引用（组件、配置资产、事件资产、数组元素）在 `Awake` 或 `OnSingletonInitialized` 中校验一次，形态为：

```csharp
Debug.Assert(loadEventSO != null,
    $"{nameof(loadEventSO)} 未指定：场景切换请求无法广播。", this);
```

消息由字段名与缺失后果组成；数组按下标逐个断言。校验后调用点直接解引用，不再判空。

校验入口按组件类型划分：

- 单例组件在 `OnSingletonInitialized` 校验：`SceneChanger`、`UIManager`、`AudioPlayerManager`、`NetworkCharacterManager`、`VoxelPlatformDestruction`。
- 普通组件在 `Awake` 校验：`SceneToggler`、`InitialLoad`、`VoxelPlatformRegister`、`VoxelPlatformBlastTrigger`、`NetworkBlastManager`、`NetworkAudioPlayer`、`CharacterMotionSync`、`AudioTriggerManager`、`Attack`，以及画布管理器与按钮组件。
- 角色视觉在装配期由 `CharacterVisual.TryValidate` 校验。

### 2.3 空值分类与判定

四类空值，处理方式不同：

| 类别 | 判定 | 处理 | 示例 |
|---|---|---|---|
| 必填配置缺失 | 自有场景或 Prefab 应配置，缺失会让对应功能静默失效 | `Awake`/`OnSingletonInitialized` 断言，调用点直接使用 | `SceneChanger.firstSceneToLoad`、`VoxelPlatformRegister.registerEvent`、`UIManager.toggleCanvasEvents` |
| 可留空配置 | `Tooltip` 或注释写明留空语义 | 不做断言，保留判空 | `VoxelPlatformBlastTrigger.gameStartEventSO`、`UIManager.inputBindings`、`SceneChanger.transitionImagesDuringFade` |
| 运行时注入 | 由表现层或网络装配在运行时赋值 | 不做断言，按装配时序处理 | `ThirdPersonCamera.target`、`PlayerCharacterController.inputSpace`、`CharacterBoneFollower.headBone` |
| 运行时状态 | 未连接、玩家未生成、对象池为空、批处理、场景切换中 | 保留判空并注释原因 | `AudioPlayerManager` 的网络转发缓存、`FloatingSignboard` 的 `Camera.main`、`PlayerCharacterController` 的延迟挂载 |

判定顺序：先看字段在自有场景或 Prefab 中是否已配置且缺失后果是否静默；再看 `Tooltip` 或注释是否已声明留空语义；两者都不成立时按必填处理。

### 2.4 允许暂缺的初始化期与生命周期路径

以下路径使用 `Instance` 并在调用点注释原因，不得改为 `RequiredInstance`：

- `UIManager.Report`：单例尚未注册时静默跳过，面板在自身的 `Awake` 期间上报初始状态。
- `ICanvasManager.RefreshCanvaOrder`：`UIManager` 未注册时退化为 `UIManager.DefaultOrder`。
- `CharacterIdSync.OnStopClient`：停止流程可能先于常驻场景管理器销毁。
- `AudioPlayerManager.ForwardAudioRequestToNetworkPlayer`：未连接或本地玩家尚未生成。
- `PlayerCharacterController`：网络模型运行时挂载，运动驱动与动画驱动延迟初始化。

### 2.5 订阅、退订与就绪标记

事件订阅在 `OnEnable` 中无条件执行，前提是引用已在 `Awake` 或初始化钩子中断言，因此订阅点不写"非空才订阅"。

退订与订阅成对出现在 `OnDisable` 或 `OnDestroy`。订阅是否发生由布尔就绪标记记录，不再对资产做第二次判空，示例为 `VoxelPlatformDestruction.subscribed`。

## 3. 约定与硬边界

### 3.1 禁止 `Instance?.Method()`

`?.` 不遵循 Unity 的销毁语义，销毁中的对象仍会被判定为非空；这种写法同时吞掉单例缺失。单例访问按 §2.1 的两种入口书写，`?.` 只用于纯 C# 事件与普通 C# 对象。

### 3.2 必填引用不在调用点判空

必填引用只在 §2.2 的校验点断言一次。调用点不写 `if (引用 != null)`、不写类型过滤、不写"为空就跳过"的分支。删除守卫后缺失会在首次使用处抛出空引用，缺失位置由断言消息指出。

### 3.3 断言只在编辑器与开发构建生效

`Debug.Assert` 在未定义 `UNITY_ASSERTIONS` 的发布构建中被剥离，资产校验只在编辑器与开发构建执行；发布前的检查在编辑器或开发构建中完成。`RequiredInstance` 抛出的异常不受该限制。

### 3.4 可选引用必须写明留空语义

可选引用在 `Tooltip` 或源码注释中写明留空后的行为；未写明留空语义的空引用按必填处理。可选集合只断言已配置元素，不断言容器非空。

### 3.5 `RequiredInstance` 的使用边界

必需依赖指常驻场景单例，缺失时对应功能没有替代输入，且调用不处于销毁期或初始化等待期。新增调用点先判定属于必需依赖还是允许暂缺，再选择入口；不得把允许暂缺的路径改成 `RequiredInstance`，也不得把必需依赖降级为可空判空。

### 3.6 集合容器与元素分别判定

必填集合断言容器非空与元素非空；可留空集合只断言已配置元素；元素为空一律断言，不做静默过滤。容器是否允许为空由字段语义决定，并在 `Tooltip` 或注释中写明。
