using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace GolfArcade.EditorTools {
 public static class ClothCompileProbe {
  static string Out => Environment.GetEnvironmentVariable("CLOTH_COMPILE_OUT") ?? "../work/cloth-overhaul/compile";
  public static void Run() {
   Directory.CreateDirectory(Out);var shader=Shader.Find("GolfArcade/TennisCloth");
   var lines=new System.Collections.Generic.List<string>();
   foreach(var m in typeof(ShaderUtil).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name.Contains("ShaderData")||m.Name.Contains("Compile")||m.Name.Contains("Stat")))lines.Add(m.ToString());
   var method=typeof(ShaderUtil).GetMethod("GetShaderData",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
   if(method!=null){var data=method.Invoke(null,new object[]{shader});lines.Add("DATA "+data.GetType());foreach(var m in data.GetType().GetMethods())lines.Add(m.ToString());
    var sub=data.GetType().GetMethod("GetSubshader")?.Invoke(data,new object[]{0});
    if(sub!=null){lines.Add("SUB "+sub.GetType());foreach(var m in sub.GetType().GetMethods())lines.Add(m.ToString());var pass=sub.GetType().GetMethod("GetPass")?.Invoke(sub,new object[]{0});if(pass!=null){lines.Add("PASS "+pass.GetType());foreach(var m in pass.GetType().GetMethods())lines.Add(m.ToString());}}
   }
   foreach(var msg in ShaderUtil.GetShaderMessages(shader))lines.Add(msg.severity+" "+msg.message);
   File.WriteAllLines(Path.Combine(Out,"shader_api.txt"),lines);EditorApplication.Exit(0);
  }
  static void Dump(object value, string label, System.Collections.Generic.List<string> log, int depth=0) {
   if(value==null){log.Add(label+"=null");return;}
   var type=value.GetType();log.Add(label+" type="+type.FullName);
   foreach(var f in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)) {
    var v=f.GetValue(value);if(v is byte[] b){File.WriteAllBytes(Path.Combine(Out,label+"_"+f.Name+".bin"),b);log.Add(f.Name+" bytes="+b.Length);}
    else if(v is string text){File.WriteAllText(Path.Combine(Out,label+"_"+f.Name+".txt"),text);log.Add(f.Name+" chars="+text.Length);}
    else if(f.FieldType.IsPrimitive||f.FieldType.IsEnum)log.Add(f.Name+"="+v);
    else if(depth<2)Dump(v,label+"_"+f.Name,log,depth+1);
   }
   foreach(var p in type.GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.GetIndexParameters().Length==0))try{log.Add(p.Name+"="+p.GetValue(value));}catch{}
  }
  public static void CompileMetal() {
   Directory.CreateDirectory(Out);var log=new System.Collections.Generic.List<string>();
   foreach(var file in new[]{"Assets/Resources/Tennis/Shaders/TennisCloth.shader","Assets/Editor/ClothBaseline.shader"}) {
    var shader=AssetDatabase.LoadAssetAtPath<Shader>(file);if(!shader)continue;
    var pass=ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
    string tag=file.Contains("Baseline")?"before":"after";
    foreach(var keywords in new[]{Array.Empty<string>(),new[]{"_MAIN_LIGHT_SHADOWS","_ADDITIONAL_LIGHTS","_SHADOWS_SOFT_LOW","_SCREEN_SPACE_OCCLUSION","_LIGHT_LAYERS"}}) {
     string id=tag+(keywords.Length==0?"_plain":"_court");
     var result=pass.CompileVariant(ShaderType.Fragment,keywords,ShaderCompilerPlatform.Metal,BuildTarget.iOS);
     Dump(result,id,log);
    }
    foreach(var method in typeof(ShaderUtil).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name.Contains("SRPBatcher"))) {
     log.Add(method.ToString());var ps=method.GetParameters();
     if(ps.Length==2&&ps[0].ParameterType==typeof(Shader)&&ps[1].ParameterType==typeof(int))try{log.Add(tag+" "+method.Name+"="+method.Invoke(null,new object[]{shader,0}));}catch(Exception e){log.Add(e.Message);}
    }
    var compat=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(Shader),typeof(int)},null);
    var reason=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(Shader),typeof(int),typeof(int)},null);
    if(compat!=null&&reason!=null) { int code=(int)compat.Invoke(null,new object[]{shader,0});log.Add(tag+" SRP reason="+reason.Invoke(null,new object[]{shader,0,code})); }
    foreach(var message in ShaderUtil.GetShaderMessages(shader))log.Add(tag+" "+message.severity+" "+message.message);
   }
   File.WriteAllLines(Path.Combine(Out,"compiled_metal.txt"),log);
   if(Environment.GetEnvironmentVariable("CLOTH_BUILD_AFTER")=="1")BuildIOS();else EditorApplication.Exit(0);
  }
  public static void BuildIOS() {
   Directory.CreateDirectory(Out);
   if(!Application.dataPath.Contains("work/cloth-overhaul/isolation"))throw new InvalidOperationException("Profile builds must use the isolated project.");
   PlayerSettings.enableFrameTimingStats=true;
   PlayerSettings.bundleVersion=Environment.GetEnvironmentVariable("CLOTH_PROFILE_VERSION")??"29.2.0";
   PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS,"com.aloniewski.GolfArcade.ClothProof");
   PlayerSettings.productName="Cloth Proof";
   var opts=new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Tennis.unity"},locationPathName=Path.Combine(Out,"iOS"),target=BuildTarget.iOS,options=BuildOptions.Development};
   var rep=BuildPipeline.BuildPlayer(opts);File.WriteAllText(Path.Combine(Out,"build_summary.txt"),rep.summary.result+"\n"+rep.summary.totalTime+"\n"+rep.summary.totalErrors);
   EditorApplication.Exit(rep.summary.result==BuildResult.Succeeded?0:1);
  }
 }
}
