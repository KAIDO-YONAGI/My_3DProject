# My_SceneAndUIFrameWork

基于 Unity `SceneManager` 的多场景 Additive 加载与 UI 画布管理框架。运行时不依赖 Addressables、玩法脚本或音频系统；输入使用 Input System（`com.unity.inputsystem`），UI 使用 UGUI。

## 包含内容

```text
FrameWork/
  Scripts/Core/       单例基类与通用枚举
  Scripts/SO/         场景数据（GameSceneSO）与事件通道（SceneLoad/SceneLoaded/Void/ToggleCanvas）
  Scripts/Scene/      SceneChanger（多场景组切换）、InitialLoad、PersistentSceneRegistry、TimeManager
  Scripts/UI/         UIManager、CanvasFocusStack、ICanvasManager、按钮（切场/重试/ESC 关闭）
  Scripts/UI/SystemCanvasManagers/  ESCMenuManager、GameOverCanvasManager、ExitManager
  Teleport/           关卡传送触发器
  Samples/Scenes/     InitialScene（启动）、PersistentScene（常驻模板）、LevelScene（关卡示例）
  Samples/SOAssets/   事件通道与场景数据模板资产
  Samples/Input/      SampleInput.inputactions + ESC 的 InputActionReference
  Samples/Prefabs/    Teleport.prefab
```

导入 `.unitypackage` 不会自动修改 Build Settings。

## 运行 Sample

1. 确认项目已安装 `com.unity.inputsystem`（Active Input Handling 设为 Input System Package）。
2. 在 `File > Build Settings` 手动加入并按此顺序排列：
   - `Assets/FrameWork/Samples/Scenes/InitialScene.unity`（设为启动场景）
   - `Assets/FrameWork/Samples/Scenes/PersistentScene.unity`
   - `Assets/FrameWork/Samples/Scenes/LevelScene.unity`
3. 打开并运行 `InitialScene`：`InitialLoad` 以 Additive 加载 `PersistentScene`（并注册进常驻注册表），随后其中常驻的 `SceneChanger` 加载 `LevelScene`。
4. 按 `ESC` 打开/关闭暂停面板（走 UIManager 焦点栈 + ToggleESCEvent）；进入关卡右侧的 Teleport 触发器会切回常驻场景组。

## PersistentScene 配置模板说明（重点）

`PersistentScene` 是跨关卡常驻场景的配置模板，导入后可直接对照修改：

| 对象 | 组件 | 关键配置 |
|---|---|---|
| `Managers/SceneChanger` | SceneChanger | `First Scene To Load` = 初始内容场景组（LevelSceneSO）；`Load Event SO` = SceneLoadEvent；`Retry Event SO` = RetryRequestEvent；`Scene Loaded Event` = SceneLoadedEvent；`Player` 指向玩家 Transform；`Fade Canva` 指向 FadeCanvas 的 CanvasGroup；`Objects To Unable While Menu Or Reset` 列玩家对象 |
| `Managers/UIManager` | UIManager | `Load Event SO` 与 SceneChanger 同一资产（切场时复位全部面板）；`Toggle Canvas Events` 注册 ToggleESCEvent/ToggleGameOverEvent 等面板事件；`Input Bindings` 把 CanvasToToggle.ESC 映射到 `Samples/Input/UI_ESCPress`（InputActionReference）；`Mutex Canvases` 填参与互斥的面板枚举 |
| `FadeCanvas` | Canvas + CanvasGroup | 排序 999；SceneChanger 用它做黑屏过渡（alpha 0→1→0） |
| `ESC_Canvas/ESC_Panel` | ESCMenuManager | `ESCGroup` 指向 ESC_Canvas 的 CanvasGroup；订阅 ToggleESCEvent（开关）与 SceneLoadedEvent（切场复位）；Menu 场景打开时自动屏蔽 |
| `GameOver_Canvas/GameOver_Panel` | GameOverCanvasManager | 不可 ESC 关闭 + 阻塞全局输入；面板上的 Retry 按钮挂 `RetryButton.HandleRetry` 广播 RetryRequestEvent |
| `EventSystem` | InputSystemUIInputModule | UGUI 输入走 Input System；`Actions Asset` 可留空使用默认，或绑定 SampleInput |

## 事件管线模板

| 资产 | 类型 | 用途 |
|---|---|---|
| `SceneLoadEvent` | SceneLoadEventSO | 切场请求总线：Teleport/ButtonSceneToggler/RetryManager/SaveDataManager/SceneChanger/UIManager 共用；`RequestSceneLoad` 先同步广播（订阅方收尾）再执行切换 |
| `SceneLoadedEvent` | SceneLoadedEventSO | 整组加载完成广播：面板复位（ESC/GameOver 关闭）、存档自动读档等订阅它 |
| `RetryRequestEvent` | VoidEventSO | 死亡重试：RetryButton 广播，常驻 SceneChanger 订阅后整组重载当前场景组 |
| `ToggleESCEvent` / `ToggleGameOverEvent` | ToggleCanvasEventSO | 面板开关通道：UIManager 经焦点栈驱动开关与 focus 刷新；面板（ICanvasManager）订阅它实现显隐 |

新增面板的标准流程：建 `ToggleCanvasEventSO`（选 `CanvasToToggle` 枚举）→ 面板实现 `ICanvasManager` 并订阅该事件 + SceneLoadedEvent → 把事件资产加进 UIManager 的 `Toggle Canvas Events` → 需要按键唤起就加一条 `Input Bindings`（新建 InputActionReference 指到你的 `.inputactions`）。

## 架构

- **SceneChanger**：常驻单例，切换的唯一入口是 `RequestSceneLoad(List<GameSceneSO>, Vector3, bool)`——先广播 `loadEventSO` 让订阅方同步收尾，再执行「淡入 → 整组卸载旧内容场景 → 按顺序 Additive 加载 → 玩家落位 → 淡出 → 广播 `sceneLoadedEvent`」。加载前校验 `Application.CanStreamedLevelBeLoaded`，场景未入 Build Settings 时报错跳过。
- **多场景组**：每次请求针对一个完整 Additive 场景组（`List<GameSceneSO>`）；组内首个场景作为组标识（`GetCurrentGameScene()` 返回它），`GetCurrentScenes()` 返回整组副本供 Retry 整组重载。
- **常驻保护**：`InitialLoad` 启动时 Additive 加载并注册 `PersistentSceneRegistry` 中的常驻场景；`SceneChanger` 卸载时跳过注册场景。
- **GameSceneSO**：每个可加载场景一个资产，拖入 `.unity` 自动同步 `sceneName`，配置 `SceneType`（Location/Menu）和默认出生点 `initialPosition`；`SaveKey` 取场景文件 GUID，可作存档稳定标识。
- **玩法解耦**：框架版 SceneChanger 的回血改为 `OnBeforeSceneUnload()` 虚钩子、输入禁用为空实现，宿主项目继承 `SceneChanger` 重写即可接入玩法。

## 关卡场景配置

放进关卡场景的框架内容只有 **Teleport**（组件类名 `SceneToggler`）：触发对象需带 `Player` Tag，双方有 `Collider2D` 且至少一方有 `Rigidbody2D`。玩家、Camera、EventSystem、`SceneChanger`、`UIManager` 和全局 UI 应由 PersistentScene 统一持有，不要复制进关卡场景。

## Additive 与协作开发

1. `InitialScene` 只负责加载常驻场景；`PersistentScene` 集中放置玩家、全局管理器、Camera 和跨关卡 UI。
2. 每个关卡使用独立 `.unity` 场景；不要让多人同时修改同一个 `.unity` 文件。
3. 普通切场与 Retry 必须使用相同的固定顺序场景组（`sceneToLoad` / Retry 配置）。
4. Build Settings 由集成人员集中维护，框架不会自动改写构建列表。

## UI 面板配置

实现 `ICanvasManager` 的面板需要把自己的 `ToggleCanvasEventSO` 同时加入 `UIManager.Toggle Canvas Events`。

- `Close On Escape`：仅决定该面板位于顶层时，ESC 能否关闭它。
- `Blocks Global Input`：面板打开时吞掉 ESC、快捷键和 `RequestCanvasToggle` 请求。
- `RequestCanvasClose`：显式关闭入口，不受上述两个选项限制。
- `Mutex Canvases`：列在 UIManager 此列表中的面板互相互斥。
- 面板排序由 `CanvasFocusStack` 按打开顺序维护，不需要手动竞争 `sortingOrder`。
