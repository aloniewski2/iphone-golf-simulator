using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

// Minimal [Test] runner: loads the EditMode test assembly and its dependencies from one folder
// and runs every public [Test] method (no [UnityTest], no fixtures needed by these tests).
static class Runner
{
    static int Main(string[] args)
    {
        string dir = System.IO.Path.GetFullPath(args[0]);
        var ctx = AssemblyLoadContext.Default;
        ctx.Resolving += (c, name) =>
        {
            string p = System.IO.Path.Combine(dir, name.Name + ".dll");
            return System.IO.File.Exists(p) ? c.LoadFromAssemblyPath(p) : null;
        };
        var asm = ctx.LoadFromAssemblyPath(System.IO.Path.Combine(dir, "GolfArcade.Tests.dll"));
        int pass = 0, fail = 0;
        foreach (var type in asm.GetTypes().OrderBy(t => t.Name))
        {
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute")))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; }
                catch (TargetInvocationException e)
                {
                    fail++;
                    var inner = e.InnerException;
                    Console.WriteLine($"FAIL {type.Name}.{m.Name}: {inner.GetType().Name}: {inner.Message.Trim()}");
                }
            }
        }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
