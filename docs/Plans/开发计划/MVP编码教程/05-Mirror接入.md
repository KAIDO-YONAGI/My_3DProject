# 05 给现有玩家接上 Mirror 请求

这一章给现有 `NetworkPlayer` 加一个组件：本地玩家按 E，把瞄准方向交给服务器；服务器再检查并执行平台破坏。先读懂这条链：

```text
本机 Update 读 E → CmdUse 发到服务器 → 服务器检查 → ServerBreak 修改平台
```

## 1. 在已有玩家上添加组件

先按下一节创建脚本，保存后等 Unity 编译。再打开 `Assets/Core/Prefabs/NetworkPlayer.prefab`，选中最外层 `NetworkPlayer`，添加 `ArenaPlayerActions` 组件。

脚本路径是：

```text
Assets/Core/Scripts/Gameplay/Mvp/ArenaPlayerActions.cs
```

命名空间使用 `Core.Scripts.Gameplay.Mvp`。保存 Prefab。不要删除或替换其 `NetworkIdentity`、`NetworkTransformReliable`、`PlayerCharacterController`、`CharacterMotionSync`、`NetworkAudioPlayer`。

## 2. 先建骨架，认清玩家状态

把下面代码放进新建的 `ArenaPlayerActions.cs`。后面每段代码都加到这个类里，放在类的最后一个 `}` 前；文件顶部的 using 和 namespace 只保留一份。

```csharp file=Assets/Core/Scripts/Gameplay/Mvp/ArenaPlayerActions.cs
using System;
using System.Collections.Generic;
using Core.Scripts.Character.Control;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Scripts.Gameplay.Mvp
{
    public sealed class ArenaPlayerActions : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnAliveChanged))]
        public bool Alive = true;

        [SyncVar]
        public uint RoundId;

        [SyncVar]
        public int Charges;

        public static readonly HashSet<ArenaPlayerActions> ServerPlayers = new();
        // 第 08 章由对局对象把正式使用规则接到这个入口。
        public static Func<ArenaPlayerActions, Vector3, bool> ServerUseRule;

        private double nextUseTime;
        private const float UseInterval = 0.35f;
        private const float DebugRange = 25f;
        private const float DebugBreakRadius = 1.2f;
    }
}
```

`Alive` 表示这个玩家是否仍在局内，`RoundId` 表示参加哪一局，`Charges` 是可用道具数量。它们由服务器修改，SyncVar 把结果传给客户端。`OnAliveChanged` 在第 6 节补上；本章各段补齐后再查看编译结果。

`ServerUseRule` 是一个能保存方法的变量。`Func<ArenaPlayerActions, Vector3, bool>` 表示这个方法接收“玩家、方向”，返回一个 `bool`。第 08 章执行 `ServerUseRule = ServerTryUse` 后，本章调用 `ServerUseRule(this, aimDirection)` 就等于把请求交给回合对象的 `ServerTryUse`。这里保存的是本机服务器上的方法引用，跨网络传输由 `CmdUse` 完成。

## 3. 本机只负责读取操作

把下面三个方法加到类里。服务器用集合记录已连接玩家；每台客户端的 `Update` 只处理属于自己的那个玩家对象。

```csharp
        public override void OnStartServer()
        {
            ServerPlayers.Add(this);
        }

        public override void OnStopServer()
        {
            ServerPlayers.Remove(this);
        }

        private void Update()
        {
            if (!isLocalPlayer || !Alive || Keyboard.current == null)
            {
                return;
            }

            if (!Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            UnityEngine.Camera viewCamera = UnityEngine.Camera.main;
            if (viewCamera != null)
            {
                // 这里只发送“朝哪里用”，不让客户端指定命中对象或结果。
                CmdUse(viewCamera.transform.forward);
            }
        }
```

一个客户端也能看见别人的 `ArenaPlayerActions`，所以 `isLocalPlayer` 判断不可省。否则本机按一次 E，会尝试通过场景中的每个玩家对象发请求。`Camera.main.transform.forward` 是当前镜头的朝向；服务器把它用作瞄准方向。

## 4. 服务器接到请求后做什么

把下面方法加到类里：

```csharp
        [Command]
        private void CmdUse(Vector3 aimDirection)
        {
            if (!Alive || NetworkTime.time < nextUseTime || !IsFinite(aimDirection))
            {
                return;
            }

            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            nextUseTime = NetworkTime.time + UseInterval;
            aimDirection.Normalize();

            // 对局开始后，统一交给回合规则检查状态、道具与攻击范围。
            if (ServerUseRule != null)
            {
                ServerUseRule(this, aimDirection);
                return;
            }

            // 回合系统尚未接入时，这条射线仅用于检查网络平台是否可命中。
            Vector3 origin = transform.position + Vector3.up;
            if (Physics.Raycast(origin, aimDirection, out RaycastHit hit, DebugRange))
            {
                PlatformChunk chunk = hit.collider.GetComponentInParent<PlatformChunk>();
                chunk?.ServerBreak(hit.point, DebugBreakRadius);
            }
        }
```

Mirror 把 `CmdUse` 从拥有者客户端送到这个玩家的服务器实例。服务器先检查玩家存活、间隔时间和方向数值，再把方向归一化。`nextUseTime` 保存下一次允许使用的服务器时间，例如当前是 `10.0` 秒，使用后就设成 `10.35` 秒。

第 08 章会给 `ServerUseRule` 赋值。之后每次请求都交给对局规则验证；即使规则拒绝，也会直接 `return`，不会绕回下面的练习射线。接回合系统前，这条射线让你在第 06 章生成平台后就能用 E 检查破坏链。

## 5. 服务器怎样改变玩家状态

以下三个方法也加进类中。道具数量只在服务器增减，客户端读取同步后的结果：

```csharp
        [Server]
        public void ServerSetAlive(bool alive)
        {
            Alive = alive;
        }

        [Server]
        public void ServerAddCharge()
        {
            Charges = Mathf.Min(Charges + 1, 5);
        }

        [Server]
        public bool ServerConsumeCharge()
        {
            if (Charges <= 0)
            {
                return false;
            }

            Charges--;
            return true;
        }
```

`ServerConsumeCharge` 用返回值说明是否扣除成功。数量为零就返回 `false`，调用者据此停止破坏；这比客户端先判断库存再发“扣掉一个”的结果更容易维持一致。

## 6. 新回合复位和淘汰

最后加入以下方法，完成这个类：

```csharp
        [Server]
        public void ServerReset(Vector3 spawnPosition, uint roundId)
        {
            Alive = true;
            RoundId = roundId;
            Charges = 0;
            // 传送也要通知客户端并清空插值，不能只改服务器的 Transform。
            GetComponent<NetworkTransformReliable>()
                .ServerTeleport(spawnPosition, transform.rotation);
        }

        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            PlayerCharacterController controller = GetComponent<PlayerCharacterController>();
            if (controller != null)
            {
                controller.enabled = newValue;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
```

`ServerTeleport` 来自当前玩家已有的 `NetworkTransformReliable`。它通知客户端传送并清理旧位置插值。玩家正常移动仍采用 ClientToServer；新回合的位置由服务器明确复位。

`OnAliveChanged` 只开关本机运动控制器，镜头和网络对象仍保留。第 07 章再补清除运动速度的定向 RPC，避免角色带着上一局的速度重生。

## 7. 在 Unity 中检查

保存完整脚本，等 Unity 编译结束，先看 Console 第一条 Error。然后打开 `NetworkPlayer.prefab`，把本章组件加到最外层，与原来的 `NetworkIdentity` 同一层并保存。

第 04 章已做好平台网络 Prefab，第 06 章把它生成到场景里。本章先检查脚本编译通过、玩家根上有组件；生成场地后就能用 E 检查破坏。完整的双客户端操作见第 11 章。

第 08 章在服务器生成对局对象，并把 `ServerUseRule` 指向它的检查方法。它收到方向后会再次验证当前回合和库存，只有全部条件通过才消耗道具、改变平台。

## 本章的接口选择

`[Command]` 适合“拥有者提出操作，服务器检查后执行”的请求，例如使用道具。它不是让客户端直接改服务器数据的快捷写法。SyncVar 适合让所有客户端看到当前值，例如 `Alive` 和 `Charges`。平台格子集合由 `PlatformChunk.Cells` 保存，根物体位置由 `NetworkTransformReliable` 同步。API 的选择和使用位置见 [Mirror 网络 API 总览](Mirror网络API总览.md)。

本工程现有角色位置仍由拥有者的 `NetworkTransformReliable` 上报。服务器规则使用服务器当前收到的位置，因此 MVP 已有基本服务端裁决，但不包含严格移动作弊校验。这里没有把整个角色移动链改成服务器模拟。
