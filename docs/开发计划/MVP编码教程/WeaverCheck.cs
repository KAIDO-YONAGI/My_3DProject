using System;
using System.IO;
using System.Linq;
using Mirror.Weaver;
using Mono.CecilX;

// 此程序在独立验证目录运行，检查教程程序集经过真实 Weaver 后的输出。
static class WeaverCheck
{
    public static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("需要程序集路径与引用清单");
        int errors = 0;
        ILPostProcessorFromFile.ILPostProcessFile(args[0], File.ReadAllLines(args[1]),
            warning => Console.WriteLine("Weaver warning: " + warning),
            error => { ++errors; Console.Error.WriteLine(error); });
        if (errors != 0) return 1;
        using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
        var generated = assembly.MainModule.Types.Single(t =>
            t.Namespace == "Mirror" && t.Name == "GeneratedNetworkCode");
        foreach (string message in new[] { "MvpInput", "MvpBaseline", "MvpMotion", "MvpRecover", "MvpExplosion" })
        {
            if (generated.Methods.Count(m => m.Name.Contains(message)) < 2)
                throw new Exception("消息读写生成缺失: " + message);
        }
        var body = assembly.MainModule.Types.Single(t => t.FullName == "Mvp.MvpBody");
        foreach (string method in new[] { "SerializeSyncVars", "DeserializeSyncVars" })
            if (!body.Methods.Any(m => m.Name == method)) throw new Exception("同步字段生成缺失: " + method);
        Console.WriteLine("PASS: Mirror Weaver / 5 类消息读写 / SyncVar 生成");
        return 0;
    }
}
