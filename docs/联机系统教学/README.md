# 本项目的 Mirror 联机编写教学

联机代码从职责开始：谁接收输入，谁决定结果，哪些数据跨网络传递，收到数据后谁更新本地对象。

本项目使用 Unity 客户端和 Mirror 专用服务器。Mirror 是 Unity 网络框架，负责连接、网络对象和数据同步。专用服务器独立运行场景与服务器逻辑，客户端运行输入、相机和表现。

各章以客户端 A、客户端 B 和服务器 S 说明链路。A 操作玩家甲，B 操作玩家乙。“本地玩家”指当前客户端自己的玩家。上行指客户端向服务器发送，下行指服务器向客户端发送。

源码摘录对应项目实际实现。“扩展练习”用于说明新增功能的接入写法，组件位置、接口职责和配置与代码一起说明。投掷与爆炸章节采用扩展练习的方式给出设计和落地步骤。

框架规则结合 Mirror 官方文档说明，具体接口与组件行为结合工程内的 Mirror 源码核对。各章附有官方阅读入口。工程运行配置以 [Mirror 与 KCP 工程配置](/D:/Unity/Projects/My_3DProject/Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md) 为准。

## 按问题查阅

| 想弄清的问题 | 阅读章节 |
| --- | --- |
| 网络对象、本地对象、归属和数据决定权怎么划分？ | [01 网络对象与职责](01-网络对象与职责.md) |
| 按钮、输入、管理器怎样把数据交给网络层？ | [02 本地调用与网络交接](02-本地调用与网络交接.md) |
| 客户端调用 Command 时到底执行什么？角色切换的上下行怎么走？ | [03 角色切换完整链路](03-角色切换完整链路.md) |
| 项目玩家的位置、旋转和动画怎么同步？ | [04 玩家移动同步](04-玩家移动同步.md) |
| SyncVar、Command、ClientRpc、TargetRpc 怎么选？新状态怎么接 UI？ | [05 状态与事件的编码](05-状态与事件的编码.md) |
| 名单、公共机关、道具生成、连接级消息怎么写？ | [06 集合、公共对象与消息](06-集合公共对象与消息.md) |
| 特性怎么变成网络代码？方法签名、程序集和双端构建是什么关系？ | [07 代码生成与序列化](07-代码生成与序列化.md) |
| 运动轨迹该同步位置、速度，还是起点和时间？ | [08 运动轨迹与同步选择](08-运动轨迹与同步选择.md) |
| 本地预测球怎样对应服务器投掷物？怎样平滑切换？ | [09 投掷物预测与权威接管](09-投掷物预测与权威接管.md) |
| 弹跳、引信、爆炸、伤害和击退由谁处理？ | [10 爆炸物与命中裁决](10-爆炸物与命中裁决.md) |
| 初始化、解绑、同步范围和多客户端验证怎么安排？ | [11 生命周期与开发验证](11-生命周期与开发验证.md) |

## 阅读路径

初次编写联机功能：01 → 02 → 03 → 05 → 07 → 11。

理解项目运行链路：01 → 03 → 04 → 11。

接入投掷玩法：01 → 05 → 06 → 08 → 09 → 10 → 11。

排查操作没有产生结果：02 → 03 → 07 → 11。沿本地回调、发送入口、服务器校验、状态下行和表现更新逐段观察。

## 核心词汇

| 词汇 | 含义 |
| --- | --- |
| 网络实体 | 多个进程通过同一网络身份对应的游戏对象 |
| NetworkIdentity | Mirror 的网络身份组件，保存运行期对象编号和连接归属 |
| netId | 一个网络实体在本次网络生命周期中的编号 |
| NetworkBehaviour | 带有 Mirror 同步字段、远程调用和网络回调能力的 Unity 组件基类 |
| Authority | Mirror 的对象控制归属，决定连接与对象的权限关系 |
| 权威状态 | 游戏规则最终采用的状态，由该数据的决定者维护 |
| Command | 客户端向服务器提交对象操作的方法调用机制 |
| SyncVar | 参与 Mirror 状态同步的字段 |
| RPC | Remote Procedure Call，远程过程调用；ClientRpc 面向观察客户端，TargetRpc 面向指定连接 |
| Spawn | 建立网络对象生命周期，并向客户端提供对象与初始状态 |
| 观察者 | 服务器允许接收某个网络对象的客户端连接 |
| 快照 | 某一时刻的状态记录 |
| 插值 | 根据相邻快照计算中间显示状态 |
| 预测 | 客户端在确认到达前估算表现或运动结果 |
| 校正 | 用收到的权威状态调整预测结果 |
| 序列化 | 把数据编码成可传输的字节；反序列化将字节还原成数据 |
| Weaver | Mirror 的编译后程序集处理器，生成网络调用和序列化代码 |

## 工程入口

| 要查的事实 | 入口 |
| --- | --- |
| 玩家网络根与同步方向 | [NetworkPlayer.prefab](/D:/Unity/Projects/My_3DProject/Assets/Core/Prefabs/NetworkPlayer.prefab) |
| 角色请求与编号状态 | [NetworkCharacterSync.cs](/D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Networking/NetworkCharacterSync.cs) |
| 模型、动画和相机装配 | [NetworkCharacterManager.cs](/D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Networking/NetworkCharacterManager.cs) |
| 本地运动与远程动画 | [PlayerCharacterController.cs](/D:/Unity/Projects/My_3DProject/Assets/Core/Scripts/Character/Runtime/PlayerCharacterController.cs) |
| 变换快照同步 | [NetworkTransformReliable.cs](/D:/Unity/Projects/My_3DProject/Assets/Plugins/Mirror/Components/NetworkTransform/NetworkTransformReliable.cs) |
| 刚体的模拟侧控制 | [NetworkRigidbodyReliable.cs](/D:/Unity/Projects/My_3DProject/Assets/Plugins/Mirror/Components/NetworkRigidbody/NetworkRigidbodyReliable.cs) |
| 网络时间与显示时间线 | [NetworkTime.cs](/D:/Unity/Projects/My_3DProject/Assets/Plugins/Mirror/Core/NetworkTime.cs) |

Mirror 官方手册入口：[User Manual](https://mirror-networking.gitbook.io/docs/manual)。
