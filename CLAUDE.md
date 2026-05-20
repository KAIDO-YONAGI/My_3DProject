# CLAUDE.md

## 项目概述

基于 Unity 的多人联机 3D 项目，客户端和服务端在同一仓库中。

## 项目结构

### 客户端 (Unity)
- 脚本: `Assets/Core/Scripts/`
  - `Client/` — 玩家管理、角色同步等客户端逻辑
  - `Net/` — 网络连接管理
  - `Events/` — 事件系统
- 场景: `Assets/Core/Scenes/`
- 事件资产: `Assets/Core/EventSOs/`

### 服务端 (独立 C# 项目)
- 路径: `LocalSever/`
- 脚本: `LocalSever/Scripts/`
  - `EchoServer.cs` — 服务器主逻辑
  - `ServerProtocol.cs` — 通信协议定义

## 约定

- 客户端和服务端共享协议定义，修改协议时需同步两端。
