using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using NUnit.Framework;

namespace GolfArcade.PostcardCheck
{
    /// `tests <file.cs> [more.cs]`: compile NUnit test sources against this assembly (real game sources + the NUnit shim) with Unity's Roslyn,
    /// then run every [Test] / [TestCase] by reflection.
    static class TestRunner
    {
        static string Env(string key, Func<string> fallback) { var v = Environment.GetEnvironmentVariable(key); return string.IsNullOrEmpty(v) ? fallback() : v; }

        public static int Command(Args a)
        {
            var files = a.Positional.Skip(1).Select(Path.GetFullPath).ToList();
            if (files.Count == 0) { Console.Error.WriteLine("usage: tests <file.cs> [more.cs ...]"); return 2; }
            foreach (var f in files) if (!File.Exists(f)) { Console.Error.WriteLine("tests: no such file " + f); return 2; }

            string self = typeof(Program).Assembly.Location;
            string dotnet = Env("PC_DOTNET", () => Environment.ProcessPath);
            string fx = Env("PC_FXDIR", () => Path.GetDirectoryName(typeof(object).Assembly.Location));
            string csc = Env("PC_CSC", () => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dotnet), "..", "DotNetSdkRoslyn", "csc.dll")));
            string build = Env("PC_BUILD", () => Path.GetDirectoryName(self));
            string dir = Path.Combine(build, "tests"); Directory.CreateDirectory(dir);
            string stem = Path.GetFileNameWithoutExtension(files[0]).Replace('.', '_');
            string uniq = stem + "_" + Environment.ProcessId;
            string dll = Path.Combine(dir, uniq + ".dll"), rsp = Path.Combine(dir, uniq + ".rsp");

            var sb = new StringBuilder();
            sb.AppendLine($"-nologo -target:library -optimize+ -debug:portable -langversion:10 -nullable:disable -warn:3 -nowarn:CS0649,CS0169,CS0219,CS0414,CS8632");
            sb.AppendLine($"-out:{dll}");
            foreach (var f in Directory.GetFiles(fx, "*.dll")) if (!Path.GetFileName(f).Contains("Native")) sb.AppendLine($"-r:{f}");
            sb.AppendLine($"-r:{self}");
            foreach (var f in a.All("ref")) sb.AppendLine($"-r:{Path.GetFullPath(f)}");
            foreach (var f in files) sb.AppendLine(f);
            File.WriteAllText(rsp, sb.ToString());
            Console.Error.WriteLine($"+ {dotnet} {csc} @{rsp}   # {string.Join(" ", files.Select(Path.GetFileName))} against {Path.GetFileName(self)}");

            var psi = new ProcessStartInfo(dotnet) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(csc); psi.ArgumentList.Add("@" + rsp);
            using (var p = Process.Start(psi))
            {
                string so = p.StandardOutput.ReadToEnd(), se = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (so.Length > 0) Console.Error.Write(so);
                if (se.Length > 0) Console.Error.Write(se);
                if (p.ExitCode != 0) { Console.WriteLine($"GATE: TESTS_COMPILE FAIL - csc exit code {p.ExitCode} for {string.Join(", ", files.Select(Path.GetFileName))}"); return 4; }
            }
            Console.WriteLine($"GATE: TESTS_COMPILE PASS - {string.Join(", ", files.Select(Path.GetFileName))} compiled against the real sources + NUnit shim");
            if (a.Has("no-run")) { TryDelete(rsp); return 0; }
            var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
            int code = Run(asm, string.Join(", ", files.Select(Path.GetFileName)), a.Get("filter"));
            TryDelete(rsp); TryDelete(Path.ChangeExtension(dll, ".pdb")); TryDelete(dll);     // keep nothing but compile-failure leftovers
            return code;
        }

        static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

        sealed class Counts { public int Pass, Fail, Error, Ignored, Inconclusive; }

        static IEnumerable<(MethodInfo m, object[] args, string suffix, object expected, bool hasExpected)> Cases(MethodInfo m)
        {
            var tcs = m.GetCustomAttributes<TestCaseAttribute>().ToList();
            if (tcs.Count == 0) { yield return (m, Array.Empty<object>(), "", null, false); yield break; }
            foreach (var tc in tcs)
            {
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    object v = i < tc.Arguments.Length ? tc.Arguments[i] : null;
                    if (v != null && !ps[i].ParameterType.IsInstanceOfType(v) && v is IConvertible)
                        v = Convert.ChangeType(v, Nullable.GetUnderlyingType(ps[i].ParameterType) ?? ps[i].ParameterType, System.Globalization.CultureInfo.InvariantCulture);
                    args[i] = v;
                }
                yield return (m, args, tc.TestName ?? "(" + string.Join(",", tc.Arguments.Select(x => x == null ? "null" : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture))) + ")", tc.ExpectedResult, tc.ExpectedResult != null);
            }
        }

        public static int Run(Assembly asm, string label, string filter)
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var counts = new Counts();
            var clock = Stopwatch.StartNew();
            var types = asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetMethods(All).Any(m => m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any()))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
            foreach (var t in types)
            {
                if (t.GetCustomAttribute<IgnoreAttribute>() != null) { Console.WriteLine($"TEST IGNORE {t.FullName} (class: {t.GetCustomAttribute<IgnoreAttribute>().Reason})"); counts.Ignored++; continue; }
                object fixture;
                try { fixture = Activator.CreateInstance(t, true); }
                catch (Exception e) { Console.WriteLine($"TEST ERROR {t.FullName}: cannot construct the fixture: {Unwrap(e).Message}"); counts.Error++; continue; }
                var methods = t.GetMethods(All).Where(m => m.DeclaringType != typeof(object)).ToList();
                var once = methods.Where(m => m.GetCustomAttribute<OneTimeSetUpAttribute>() != null).ToList();
                var setups = methods.Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToList();
                var teardowns = methods.Where(m => m.GetCustomAttribute<TearDownAttribute>() != null).ToList();
                var onceDown = methods.Where(m => m.GetCustomAttribute<OneTimeTearDownAttribute>() != null).ToList();
                bool fixtureOk = true;
                foreach (var m in once) { try { m.Invoke(m.IsStatic ? null : fixture, null); } catch (Exception e) { Console.WriteLine($"TEST ERROR {t.FullName}.{m.Name} (OneTimeSetUp): {Unwrap(e).GetType().Name}: {Unwrap(e).Message}"); counts.Error++; fixtureOk = false; } }
                if (fixtureOk)
                    foreach (var tm in methods.Where(m => m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any()).OrderBy(m => m.Name, StringComparer.Ordinal))
                        foreach (var (m, args, suffix, expected, hasExpected) in Cases(tm))
                        {
                            string name = $"{t.FullName}.{m.Name}{suffix}";
                            if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                            var ign = m.GetCustomAttribute<IgnoreAttribute>();
                            if (ign != null) { Console.WriteLine($"TEST IGNORE {name} ({ign.Reason})"); counts.Ignored++; continue; }
                            var sw = Stopwatch.StartNew();
                            Exception failure = null;
                            try
                            {
                                foreach (var s in setups) s.Invoke(s.IsStatic ? null : fixture, null);
                                object ret = m.Invoke(m.IsStatic ? null : fixture, args);
                                if (hasExpected && !Eq.Equal(expected, ret, 0)) throw new AssertionException($"  Expected: {Eq.Show(expected)}\n  But was:  {Eq.Show(ret)}");
                            }
                            catch (Exception e) { failure = Unwrap(e); }
                            foreach (var td in teardowns) { try { td.Invoke(td.IsStatic ? null : fixture, null); } catch (Exception e) { failure ??= Unwrap(e); } }
                            string ms = $"({sw.Elapsed.TotalMilliseconds:F0} ms)";
                            switch (failure)
                            {
                                case null:
                                case SuccessException: Console.WriteLine($"TEST PASS   {name} {ms}"); counts.Pass++; break;
                                case IgnoreException ie: Console.WriteLine($"TEST IGNORE {name}: {ie.Message}"); counts.Ignored++; break;
                                case InconclusiveException ic: Console.WriteLine($"TEST INCONCLUSIVE {name}: {ic.Message}"); counts.Inconclusive++; break;
                                case AssertionException ae: Console.WriteLine($"TEST FAIL   {name} {ms}\n{Indent(ae.Message)}"); counts.Fail++; break;
                                default:
                                    var frames = (failure.StackTrace ?? "").Split('\n').Take(4).Select(l => l.TrimEnd());
                                    Console.WriteLine($"TEST ERROR  {name} {ms}: {failure.GetType().Name}: {failure.Message}\n{Indent(string.Join("\n", frames))}"); counts.Error++; break;
                            }
                        }
                foreach (var m in onceDown) { try { m.Invoke(m.IsStatic ? null : fixture, null); } catch (Exception e) { Console.WriteLine($"TEST ERROR {t.FullName}.{m.Name} (OneTimeTearDown): {Unwrap(e).Message}"); counts.Error++; } }
            }
            int run = counts.Pass + counts.Fail + counts.Error;
            bool ok = run > 0 && counts.Fail == 0 && counts.Error == 0;
            Console.WriteLine($"TESTS {label}: {run} run, {counts.Pass} passed, {counts.Fail} failed, {counts.Error} errored, {counts.Ignored} ignored, {counts.Inconclusive} inconclusive [{clock.Elapsed.TotalSeconds:F1}s]");
            Console.WriteLine($"GATE: UNIT_TESTS {(ok ? "PASS" : "FAIL")} - {label}: {counts.Pass}/{run} passed");
            return ok ? 0 : 1;
        }

        static Exception Unwrap(Exception e) => e is TargetInvocationException tie && tie.InnerException != null ? Unwrap(tie.InnerException) : e;
        static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => "      " + l.TrimEnd('\r')));
    }
}
