# 05 Mirror 接入

创建下面两个文件。`NetworkManager` 是连接与对象创建的管理器，`NetworkIdentity` 是网络对象身份组件，`NetworkBehaviour` 是可以访问网络身份和权限的脚本基类。`SyncVar` 是由服务器同步给客户端的字段，本章用它传递业务编号、回合、代次与类别。

`NetworkMessage` 是 Mirror 的自定义消息接口。可靠通道传递完整基线和爆炸事件，可丢弃通道传递运动包。客户端输入通过连接对应的玩家身份进入服务器；服务器从 `conn.identity` 找到玩家，操作目标由服务器计算。

Mirror 的 Weaver 是编译期代码生成器，为消息和同步字段生成读写代码。把文件导入 Unity 后，确认 Console 编译与 Weaver 处理完成。

```csharp file=Unity/MvpNetwork.cs
using Mirror;
using UnityEngine;

namespace Mvp
{
    public struct MvpInput : NetworkMessage
    {
        public uint Round, Sequence;
        public float X, Z;
        public byte Buttons;
    }
    public struct MvpBaseline : NetworkMessage { public byte[] Bytes; }
    public struct MvpMotion : NetworkMessage { public byte[] Bytes; }
    public struct MvpRecover : NetworkMessage { public uint Round; }
    public struct MvpExplosion : NetworkMessage
    {
        public uint Round, Event, Tick, Source;
        public Vector3 Position;
    }

    public sealed class MvpNetwork : NetworkManager
    {
        public MvpWorld World;
        public MvpView View;
        public MvpBody BodyPrefab;

        public override void OnStartServer()
        {
            base.OnStartServer();
            World.Begin(this);
            NetworkServer.RegisterHandler<MvpInput>((conn, msg) =>
            {
                // 身份从已认证连接取得，客户端消息只表达操作意图。
                if (conn.isReady && conn.identity != null &&
                    conn.identity.TryGetComponent<MvpBody>(out var body))
                    World.ReceiveInput(body.Id, msg);
            });
            NetworkServer.RegisterHandler<MvpRecover>((conn, msg) =>
            {
                if (conn.isReady && conn.identity != null)
                    World.Recover(conn.identity.GetComponent<MvpBody>(), msg.Round);
            });
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var body = Instantiate(BodyPrefab);
            World.AddPlayer(body);
            NetworkServer.AddPlayerForConnection(conn, body.gameObject);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null) World.RemovePlayer(conn.identity.GetComponent<MvpBody>());
            base.OnServerDisconnect(conn);
        }

        public override void OnStopServer()
        {
            World.End();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            View.ResetClient();
            NetworkClient.RegisterHandler<MvpBaseline>(msg => View.ReceiveBaseline(msg.Bytes));
            NetworkClient.RegisterHandler<MvpMotion>(msg => View.ReceiveMotion(msg.Bytes));
            NetworkClient.RegisterHandler<MvpExplosion>(msg => View.ReceiveExplosion(msg));
        }

        public override void OnStopClient()
        {
            View.ResetClient();
            MvpBody.ClientBodies.Clear();
            base.OnStopClient();
        }
    }
}
```

```csharp file=Unity/MvpBody.cs
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Mvp
{
    public sealed class MvpBody : NetworkBehaviour
    {
        [SyncVar] public uint Id;
        [SyncVar] public uint Round;
        [SyncVar] public ushort Generation = 1;
        [SyncVar] public BodyKind Kind;
        public static readonly Dictionary<uint, MvpBody> ClientBodies = new Dictionary<uint, MvpBody>();

        public override void OnStartClient()
        {
            ClientBodies[Id] = this;
        }
        public override void OnStopClient()
        {
            ClientBodies.Remove(Id);
        }
        void OnCollisionEnter(Collision collision)
        {
            if (isServer) FindObjectOfType<MvpWorld>().Impact(this, collision);
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (isServer) FindObjectOfType<MvpWorld>().CharacterImpact(this, hit);
        }
    }
}
```

所有类别使用同一个网络 Prefab。服务器按类别添加刚体、角色控制器与碰撞形状，客户端按完整基线建立显示形状。对象创建可能晚于运动包，客户端保留状态并在网络对象到达后建立显示。

本章检查：Prefab 根上只有一个 `NetworkIdentity`；`MvpBody` 字段在 `Spawn` 和 `AddPlayerForConnection` 前填写；连接建立后每个客户端只有一个拥有权限的玩家对象。

下一章：[服务器世界](06-服务器世界.md)。
