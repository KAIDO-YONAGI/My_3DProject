# Lua 开发依赖

xLua 来源为 Tencent/xLua 官方发布标签 `2.1.16_android_16kb`，提交 `cc2bfc0d79018c4a274b4fe2ff346c57586eefc1`。许可见 `LICENSE.TXT`。

- `Src/`：Lua 运行环境、C# 互调与编辑器代码生成器。
- `Editor/XLuaUnityDefaultConfig.cs`：官方 Unity 类型过滤配置。
- `Resources/xlua/util.lua.txt`：官方 Lua 辅助模块。
- `../Plugins/x86_64/xlua.dll`：Windows x64 原生库，导入范围为 Windows 编辑器与 Windows x64 Player。
- `Gen/`：`XLua/Generate Code` 菜单输出的互调代码，与运行库同属 `Assembly-CSharp`。

Unity 菜单 `My_3DProject/Lua/验证 xLua 环境` 检查原生库加载、Lua 运算、Lua 调用 C# 与自定义模块加载。

Rider 使用 EmmyLua2 和其依赖 LSP4IJ 编辑 `.lua` 文件。Lua 运行环境由 Unity 中的 xLua 提供。EmmyLua2 的断点调试需要在 Lua 启动逻辑中连接调试器；插件的安装与调试器接入分别配置。

业务脚本可保持 `.lua` 后缀用于编辑，通过 `LuaEnv.AddLoader` 提供模块字节；需要按 Unity `TextAsset` 导入和打包时使用 `.lua.txt`。入口 API 为 `LuaEnv`、`DoString`、`AddLoader`、`Tick` 与 `Dispose`。
