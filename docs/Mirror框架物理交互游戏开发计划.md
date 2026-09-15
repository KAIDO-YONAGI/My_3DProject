# Mirror 3D 物理交互游戏技术选型与实施计划

> 文档状态：技术选型与依赖落地计划
> 编写日期：2026-09-15
> 适用项目：`D:\Unity\Projects\My_3DProject`
> 本文档范围：依赖、架构、同步方案、实施步骤和验收标准
> 本文档不包含具体玩法实现代码

## 1. 目标与边界

### 1.1 目标

为一个 2-4 人合作型 3D 物理交互游戏建立可执行的技术路线，首版重点验证：

- Mirror 网络连接和房间内玩家同步。
- Unity Headless/Dedicated Server。
- 服务器权威物理。
- 网络物体的 Spawn、Unspawn 和对象池复用。
- 2-3 种物理交互道具。
- 移动、交互、抓取或投掷等简洁动作。
- 多客户端、延迟、丢包和断线场景下的稳定性。

### 1.2 首版非目标

首版暂不实现：

- 客户端预测和服务器回滚。
- 确定性物理。
- 高速竞技战斗和命中补偿。
- 大地图兴趣管理。
- 匹配、账号、大厅和正式服务器编排。
- 复杂背包和道具组合系统。
- WebGL 传输。

## 2. 当前项目现状

### 2.1 Unity 与依赖

- Unity 版本：`2022.3.62f3c1`。
- Mirror 已按官方 release 方式落地到 `Assets/Mirror`，当前版本为 `v96.11.2`。
- Mirror 不作为 UPM 依赖写入 `Packages/manifest.json`；版本以 `Assets/Mirror/version.txt` 和版本化资源目录锁定。
- 当前项目已使用 Addressables、Terrain Tools、Timeline、UGUI、Visual Scripting 等 Unity 包。
- 项目已有 ParrelSync，可用于本地多客户端测试。
- Unity 物理模块已经存在，不需要额外引入物理引擎。

参考文件：

- [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt)
- [Packages/manifest.json](../Packages/manifest.json)

### 2.2 当前客户端网络

当前客户端使用自定义 TCP 连接和文本协议：

- [SyncCharacter.cs](../Assets/Core/Scripts/Client/SyncCharacter.cs) 每约 `0.05s` 发送本地位置。
- [PlayerManager.cs](../Assets/Core/Scripts/Client/PlayerManager.cs) 使用 `Instantiate` 创建远端玩家。
- 远端玩家通过直接修改 Transform 位置进行显示。
- 目前没有网络对象池、插值物理同步或服务器权威物理。
- [ClientProtocol.cs](../Assets/Core/Scripts/Client/Net/ClientProtocol.cs) 定义 Enter、Move、Leave、Attack 文本消息。

### 2.3 当前服务端

`LocalServer/` 是独立的 `.NET 10` TCP 服务端：

- [ServerCore.cs](../LocalServer/Scripts/ServerCore.cs) 负责 TCP 监听和连接生命周期。
- [ServerNetHandler.cs](../LocalServer/Scripts/ServerNetHandler.cs) 目前主要转发 Move、Enter、Leave 消息。
- 当前服务端不运行 Unity PhysX，不拥有物理世界，也不负责物理碰撞结果。

### 2.4 对迁移的直接影响

新玩法不能继续让以下两套系统同时控制同一批对象：

1. `LocalServer + ClientProtocol`。
2. `Mirror + Unity Server`。

新物理玩法采用 Mirror 后：

- Unity Headless Server 成为唯一权威服务器。
- `LocalServer` 保留为历史原型，不参与新玩法。
- 新玩法不再沿用客户端直接上传最终位置的协议。
- 旧网络代码暂不删除，待新样片通过验收后再单独决定是否迁移或清理。

## 3. 技术选型结论

| 领域 | 选型 | 结论 |
|---|---|---|
| Unity | `2022.3.62f3c1` | 暂不升级 Unity，减少迁移变量 |
| 网络框架 | Mirror | 负责客户端、Host、Dedicated Server 和网络对象生命周期 |
| Mirror 版本 | 以 `v96.11.2` 为首选锁定版本 | 导入后必须在当前 Unity 版本完成编译验证 |
| 正式传输层 | KCP | 作为桌面客户端和 Dedicated Server 的默认传输 |
| 本地备用传输 | Telepathy | 用于排查 TCP 环境问题和简单本地调试 |
| 网络故障测试 | Latency Simulation Transport | 模拟延迟、丢包和乱序 |
| 服务端形态 | Unity Headless/Dedicated Server | 与客户端共用玩法代码和物理规则 |
| 权威模型 | Server Authority | 客户端发送意图，服务器决定结果 |
| 物理引擎 | Unity PhysX | 不引入第三方物理引擎 |
| 物理同步 | `NetworkTransform` + 必要的自定义状态 | 首版不依赖实验性的完整物理预测方案 |
| 对象池 | `UnityEngine.Pool.ObjectPool<T>` | 不引入第三方对象池 |
| 网络对象池 | Mirror Custom Spawn/Unspawn Handler | 客户端和服务端分别维护池 |
| 输入 | 暂不新增 Input System | 先复用当前角色控制器的输入入口 |
| 多客户端测试 | ParrelSync | 复用项目已有工具 |
| 资源加载 | 保留现有 Addressables | 首版不让 Addressables 介入动态网络 Spawn |

Mirror 官方传输文档列出了 KCP、Telepathy 和 Latency Simulation 等传输方式。KCP 用于正式 UDP 通信，Telepathy 作为 TCP 备用，Latency Simulation 用于非理想网络条件测试。
参考：[Mirror Transports](https://mirror-networking.gitbook.io/docs/manual/transports)

## 4. Mirror 版本与依赖锁定策略

### 4.1 版本策略

当前已安装并锁定 Mirror `v96.11.2`。依赖阶段已确认：

1. 官方 release 资源可以导入当前项目。
2. Unity `2022.3.62f3c1` 已生成 `Mirror.dll`、`Mirror.Components.dll`、`Mirror.Transports.dll` 等程序集。
3. Mirror Weaver/ILPostProcessor 已被 Unity 执行。

以下验证仍属于后续框架阶段：

1. 启动 Host。
2. 启动 Unity Headless Server。
3. KCP 和 Telepathy 完成最小连接测试。
4. `NetworkServer.Spawn`、客户端 Spawn Handler 和 Unspawn Handler 运行。

如果 `v96.11.2` 在当前项目中存在导入或编译问题：

- 不直接切换到 `master`。
- 优先选择 Mirror 官方 release 中与 Unity 2022.3 兼容的最近稳定版本。
- 在本文档中记录实际使用的 release、commit 或 package hash。
- 不混用不同 release 的 Mirror 核心和 Transport。

### 4.2 安装方式

本次使用 Mirror 官方 release 的固定版本 Unitypackage，文件名为
`Mirror-96.11.2.unitypackage`，导入后的资源目录为 `Assets/Mirror`。

依赖锁定记录：

- Mirror release：`v96.11.2`。
- Unitypackage SHA-256：`509c3439dac5c2b21bb767e853d6c1d13bb3a22fd9e29518cb39efe32628e201`。
- KCP、Telepathy、Latency Simulation、NetworkTransform：随同 Mirror release 一起安装，不单独混用其他版本。
- `Packages/manifest.json` 和 `Packages/packages-lock.json`：未新增 Mirror UPM 条目。
- Unity 导入后生成的 `Assets/ScriptTemplates` 和 `MirrorExamplesPipelineConverted.txt` 属于本次官方资源导入产生的工程资源/转换标记。

不直接引用：

- Mirror `master` 分支。
- 未锁定 commit 的 Git URL。
- 来历不明的第三方 Mirror 修改版。
- 与当前 Mirror release 不匹配的 Transport 版本。

安装完成后需要记录：

- Mirror release 或 commit。
- KCP 版本或随 Mirror release 搭载的版本。
- Telepathy 版本或随 Mirror release 搭载的版本。
- Unity 编辑器版本。
- 安装方式。

### 4.3 当前依赖验证结果

已完成：

- `Assets/Mirror/version.txt` 显示 `96.11.2`。
- 已确认 `Assets/Mirror/Transports/KCP`、`Telepathy`、`Latency` 存在。
- 已确认 `Assets/Mirror/Components/NetworkTransform` 存在。
- Unity 编辑器已生成 Mirror 相关程序集，当前未发现 Mirror C# 编译错误。

未在依赖安装阶段完成：

- Dedicated Server 构建和启动。
- KCP/Telepathy 的端到端连接。
- 网络对象 Spawn/Unspawn 和对象池回收。
- 物理同步与延迟、丢包条件下的运行时验收。

## 5. 总体架构

### 5.1 进程关系

```text
                 +---------------------------+
                 | Unity Headless Server     |
                 | Mirror NetworkServer      |
                 | Unity PhysX               |
                 | 权威玩家和道具状态        |
                 +-------------+-------------+
                               |
                         KCP / Telepathy
                               |
          +--------------------+--------------------+
          |                                         |
+---------v----------+                    +---------v----------+
| Unity Client A     |                    | Unity Client B     |
| Mirror NetworkClient|                    | Mirror NetworkClient|
| 输入与表现          |                    | 输入与表现          |
+--------------------+                    +--------------------+
```

### 5.2 服务器职责

服务器负责：

- 玩家连接、断开和场景状态。
- 玩家输入验证和移动结果。
- 动态物体 Spawn、Unspawn 和销毁。
- Rigidbody 物理模拟。
- 推动、抓取、投掷和机关操作。
- 交互距离、状态、冷却和权限验证。
- 向客户端同步权威快照。

### 5.3 客户端职责

客户端负责：

- 采集本地输入。
- 发送移动和交互意图。
- 显示本地玩家和远端玩家。
- 对远端物体进行插值。
- 播放交互提示、动画和音效。
- 显示服务器校正结果。

客户端不能把最终 Transform 当作服务器结果提交。

## 6. 网络同步方案

### 6.1 玩家输入

玩家客户端发送：

- 移动方向。
- 动作按钮。
- 交互目标 `netId`。
- 输入序号或时间戳。

服务器接收后：

1. 验证连接和对象权限。
2. 验证移动或交互参数。
3. 执行角色移动或动作。
4. 同步最终状态。

首版不直接同步客户端提交的位置。

### 6.2 动态物理对象

动态物理对象的推荐规则：

- 服务器 Rigidbody 为真实动态刚体。
- 服务器执行 AddForce、AddTorque、抓取和释放。
- 客户端远端对象默认为 Kinematic 表现代理。
- 客户端通过快照插值显示服务器状态。
- 必要时额外同步线速度、角速度、睡眠状态和持有状态。

首版初始参数建议：

- Unity Physics Fixed Timestep：`0.02s`。
- 网络状态发送频率：先从 `20-30Hz` 开始。
- 动作请求：可靠通道。
- 高频状态快照：根据 Transport 和 Mirror 配置使用适合的非可靠通道。
- 插值缓冲：先保留 2-3 个快照，再根据实际延迟调节。

这些数值是首轮测试起点，不应在未测试前视为最终性能参数。

### 6.3 物理同步原则

不追求所有客户端 PhysX 结果完全一致。

采用：

```text
服务器运行真实物理
        ↓
服务器生成状态快照
        ↓
客户端缓存快照
        ↓
客户端插值显示
```

如果未来出现本地操作延迟明显，再单独评估：

- 玩家移动预测。
- 输入序号和服务器校正。
- 局部表现预测。
- 回滚或延迟补偿。

预测和回滚不属于首版基础依赖。

## 7. 对象池与网络对象生命周期

### 7.1 两层对象池

服务端对象池：

- 管理真实网络物理对象。
- 负责预热、取出、重置和回收。
- 服务器决定何时 Spawn 和 Unspawn。

客户端对象池：

- 接收到 Spawn 时从池中取对象。
- 接收到 Unspawn 时归还对象。
- 不由客户端自行创建权威网络对象。

Unity `ObjectPool<T>` 负责本地对象复用；Mirror Custom Spawn/Unspawn Handler 负责把池生命周期接入网络对象生命周期。

参考：

- [Unity ObjectPool<T>](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)
- [Mirror Custom Spawn Functions](https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions)

### 7.2 回收重置清单

每次对象回收必须重置：

- 位置和旋转。
- 线速度和角速度。
- Rigidbody `isKinematic`、睡眠和碰撞检测状态。
- 父节点。
- Collider、Renderer 和粒子效果。
- 抓取者和持有状态。
- 道具冷却和临时状态。
- NetworkTransform 插值缓存。
- 服务器和客户端的本地引用。

### 7.3 初始容量

首版建议：

- 每种可复用道具预热 4 个。
- 每种道具最大容量先设为 16 个。
- 容量不足时输出明确警告。
- 不允许因为池耗尽而静默创建大量对象。

最终容量根据实际场景中的同时存在数量调整。

## 8. 首版道具范围

### 8.1 可推动箱子

验证：

- 服务器 Rigidbody。
- 推动和碰撞。
- 多客户端位置同步。
- 睡眠和唤醒。

### 8.2 可抓取投掷物

验证：

- 目标选择。
- 抓取状态。
- 服务器确认持有者。
- 释放和施加冲量。
- 超出场景范围后的回收和重新 Spawn。

### 8.3 拉杆或按钮

验证：

- 服务器验证交互距离。
- 状态同步。
- 所有客户端表现一致。
- 不需要复杂物理预测。

## 9. 建议目录边界

后续搭建基础框架时建议使用以下目录：

```text
Assets/
  Core/
    Scenes/
      PhysicsPrototypeScene.unity
    Prefabs/
      Network/
      Physics/
    Scripts/
      Network/
        Mirror/
      Physics/
      Pooling/

docs/
  mirror-physics-interaction-plan.md
```

建议职责：

- `Scripts/Network/Mirror/`：NetworkManager、玩家网络组件、消息和权限。
- `Scripts/Physics/`：服务器物理对象和交互规则。
- `Scripts/Pooling/`：本地池、网络 Spawn Handler 和重置流程。
- `Prefabs/Network/`：带 NetworkIdentity 的玩家和道具 Prefab。
- `PhysicsPrototypeScene.unity`：与旧多人场景隔离的验证场景。

## 10. 后续实施阶段

### 阶段一：依赖导入与编译验证

工作内容：

- 导入固定版本 Mirror。
- 确认 KCP 和 Telepathy。
- 创建最小 NetworkManager。
- 创建最小 Host 场景。
- 创建 Unity Headless Server 构建入口。

完成标准：

- Editor 无 Mirror 编译错误。
- Host 可以启动。
- Dedicated Server 可以启动。
- 一个客户端可以连接和断开。

### 阶段二：玩家网络骨架

工作内容：

- 创建网络玩家 Prefab。
- 配置 NetworkIdentity。
- 完成玩家 Spawn、离开和 Late Join。
- 将输入发送改为意图，而不是最终 Transform。

完成标准：

- 2-4 个客户端可以同时进入。
- 客户端只能控制自己的玩家。
- Late Join 可以看到已存在玩家。
- 玩家离开后对象正确清理。

### 阶段三：网络物理对象

工作内容：

- 创建网络物理道具 Prefab。
- 服务器创建并控制 Rigidbody。
- 客户端显示插值结果。
- 添加位置、旋转和必要物理状态同步。

完成标准：

- 服务器碰撞结果为最终结果。
- 客户端不会长期独立模拟同一个权威刚体。
- 远端道具不会出现明显跳变或无限抖动。

### 阶段四：对象池

工作内容：

- 建立服务端对象池。
- 建立客户端对象池。
- 接入 Custom Spawn/Unspawn Handler。
- 完成完整的重置协议。

完成标准：

- 同一个道具可以连续 Spawn、Unspawn 和再次 Spawn。
- 重用后不会继承旧速度、旧父节点和旧交互状态。
- 不会出现重复对象、残留对象或错误 netId。

### 阶段五：三类交互道具

工作内容：

- 实现箱子、投掷物、拉杆/按钮的最小版本。
- 所有交互走服务器验证。
- 增加客户端交互提示和基础表现。

完成标准：

- 2-4 人同时操作时结果一致。
- 断线和重新加入后道具状态正确。
- 道具回收后可以再次使用。

### 阶段六：网络条件与性能验证

工作内容：

- 使用 Latency Simulation Transport。
- 测试延迟、丢包和乱序。
- 记录服务器 Tick、网络发送频率和池使用量。
- 检查场景重新加载和断线重连。

完成标准：

- 0ms、80ms、150ms 延迟下仍可正常交互。
- 低比例丢包下不会破坏网络对象生命周期。
- 4 人和约 12 个动态物理对象时服务器仍能稳定运行。
- 所有失败场景都有明确日志。

## 11. 测试矩阵

必须覆盖：

- Host 模式。
- 独立 Headless Server。
- 2 个客户端。
- 4 个客户端。
- Late Join。
- 主动离开。
- 非正常断开。
- 断线后重新连接。
- 道具运动中玩家离开。
- 道具连续回收和重用。
- 场景重新加载。
- 延迟。
- 丢包。
- 乱序。
- 池容量不足。
- 网络对象重复 Spawn。

## 12. 验收标准

技术框架验收：

- Mirror 版本已固定并记录。
- KCP 默认连接正常。
- Telepathy 备用连接正常。
- Unity Headless Server 可以独立运行。
- 2-4 个客户端可以进入同一场景。
- 玩家 Spawn、Leave 和 Late Join 正常。
- 动态物理对象由服务器控制。
- 客户端可以平滑显示远端物理对象。
- 对象池支持多次 Spawn/Unspawn。
- 回收对象没有旧状态残留。
- 延迟和丢包测试可以复现。
- 旧 `LocalServer` 没有参与新玩法。

文档验收：

- 其他开发者只看本文档即可知道要安装什么。
- 其他开发者不需要重新决定服务器形态。
- 其他开发者不需要重新决定物理权威归属。
- 其他开发者不需要重新决定对象池接入方式。
- 所有首版非目标均有明确记录。

## 13. 风险与替代方案

### 13.1 Mirror 与 Unity 版本兼容

风险：固定 Mirror release 可能在当前 Unity 2022.3 项目中出现导入、编译或序列化问题。

处理：

- 导入后先做空场景编译。
- 再做 Host 和 Headless Server 冒烟测试。
- 不在未验证前迁移现有多人场景。
- 如果需要更换 Mirror release，必须更新本文档的版本记录。

### 13.2 服务器权威物理的操作延迟

风险：抓取、推动和投掷可能在高延迟下感觉迟钝。

处理顺序：

1. 先优化输入发送和状态插值。
2. 再增加本地动作表现。
3. 最后评估局部预测。

首版不直接引入完整回滚系统。

### 13.3 PhysX 非确定性

风险：不同客户端独立模拟会产生不同结果。

处理：

- 服务器运行唯一真实物理。
- 客户端远端动态物体作为表现代理。
- 服务器结果覆盖客户端显示。
- 不把客户端碰撞结果作为权威数据。

### 13.4 对象池残留状态

风险：对象重用后继承旧速度、旧状态或旧插值缓存。

处理：

- 为所有池对象定义统一 Reset 流程。
- 回收时清理物理、表现和网络状态。
- 增加连续重复 Spawn/Unspawn 测试。

## 14. 未来升级路线

只有首版服务器权威方案通过验收后，才考虑：

1. 玩家移动预测。
2. 输入序号和服务器校正。
3. 更细粒度的速度和角速度同步。
4. 高速投掷物优化。
5. Interest Management。
6. 房间和匹配系统。
7. NAT 穿透或 Relay。
8. Steam、Edgegap 或其他在线服务。
9. 正式日志、监控和服务器编排。

## 15. 参考资料

### Mirror 官方

- [Mirror 官方仓库](https://github.com/MirrorNetworking/Mirror)
- [Mirror 官方文档](https://mirror-networking.gitbook.io/docs)
- [Mirror Transports](https://mirror-networking.gitbook.io/docs/manual/transports)
- [KCP Transport](https://mirror-networking.gitbook.io/docs/manual/transports/kcp-transport)
- [Telepathy Transport](https://mirror-networking.gitbook.io/docs/manual/transports/telepathy-transport)
- [Latency Simulation Transport](https://mirror-networking.gitbook.io/docs/manual/transports/latency-simulaton-transport)
- [Custom Spawn Functions](https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions)
- [Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)
- [Network Rigidbody](https://mirror-networking.gitbook.io/docs/manual/components/network-rigidbody)
- [Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management)

### Unity 官方

- [Unity ObjectPool<T>](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)
- [Unity Physics 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/PhysicsOverview.html)

### 网络同步与物理理论

- [Gaffer On Games：Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/)
- [Gaffer On Games：State Synchronization](https://gafferongames.com/post/state_synchronization/)
- [Gabriel Gambetta：Client-Side Prediction and Server Reconciliation](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html)
- [CodeSmile：Write Better Netcode](https://codesmile.de/2024/07/23/1-introduction-write-better-netcode/)

## 16. 实施边界确认

本计划完成后，后续基础框架搭建可以开始，但仍保持以下边界：

- 不修改旧 `LocalServer` 的业务逻辑。
- 不把 Mirror 和旧 TCP 协议混合到同一玩法场景。
- 不实现完整游戏玩法。
- 不在首版引入预测、回滚和确定性物理。
- 不增加未经验证的第三方网络物理插件。
- 所有新增网络和物理模块先在独立样片场景中验证。
