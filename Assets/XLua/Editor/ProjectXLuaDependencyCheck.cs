using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using XLua;

/// <summary>
/// 验证 Lua 虚拟机、原生库、自定义模块加载与 C# 互调。
/// </summary>
public static class ProjectXLuaDependencyCheck
{
    [MenuItem("My_3DProject/Lua/验证 xLua 环境")]
    public static void Run()
    {
        using (var lua = new LuaEnv())
        {
            lua.AddLoader((ref string moduleName) =>
                moduleName == "dependency_probe"
                    ? Encoding.UTF8.GetBytes("return { value = 42 }")
                    : null);

            var values = lua.DoString(@"
                local probe = require('dependency_probe')
                return 6 * 7, CS.System.Math.Abs(-9),
                    CS.UnityEngine.Application.unityVersion, probe.value, _VERSION
            ", "xLua dependency check");

            if (values.Length != 5 || Convert.ToInt32(values[0]) != 42 ||
                Convert.ToInt32(values[1]) != 9 ||
                (string)values[2] != Application.unityVersion ||
                Convert.ToInt32(values[3]) != 42)
            {
                throw new InvalidOperationException("xLua 依赖验证失败。");
            }

            lua.Tick();
            Debug.Log($"[xLua dependency check] PASS: {values[4]}, " +
                $"Unity {values[2]}, arithmetic, C# interop, custom loader.");
        }
    }
}
