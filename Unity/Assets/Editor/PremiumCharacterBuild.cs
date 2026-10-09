using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GolfArcade.Tennis;

namespace GolfArcade.EditorTools
{
    public static class PremiumCharacterBuild
    {
        [Serializable] public class Row { public string sex, piece; public int sourceTriangles, matchTriangles, bones; public float maxBindError, minWeightSum, maxWeightSum; }
        [Serializable] public class Report { public string gate; public Row[] pieces; public string[] failures; }
        /// Import and prove the new match-distance garments against the installed high-detail
        /// body rig. No prefab body, rest skeleton, clip or native buffer is rewritten here.
        public static void Run()
        {
            var rows = new List<Row>(); var fail = new List<string>();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach(string sex in new[]{"Male","Female"}) {
                    foreach(string folder in new[]{"KitsLOD","KitsFitted"}) {
                        string path="Assets/Resources/Tennis/"+folder+"/"+sex+".fbx";
                        var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                        if(importer&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                    }
                    AssetDatabase.ImportAsset("Assets/Resources/Golf/Equipment/"+sex+".fbx",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                }
                foreach (string sex in new[] { "Male", "Female" })
                {
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/Customization/Player" + sex + ".prefab");
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/KitsLOD/" + sex + ".fbx");
                    if (!root || !source) { fail.Add(sex + ": missing garment or hero asset"); continue; }
                    var look = root.GetComponent<MatchHeroLook>();
                    var lows = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (lows.Length != 6) fail.Add(sex + ": expected six garment renderers, got " + lows.Length);
                    foreach (var low in lows)
                    {
                        var high = look.kit.FirstOrDefault(r => r && r.name == low.name);
                        if (!high) { fail.Add(sex + "/" + low.name + ": semantic piece missing"); continue; }
                        var row = new Row { sex = sex, piece = low.name, sourceTriangles = Count(high.sharedMesh), matchTriangles = Count(low.sharedMesh), bones = low.bones.Length, minWeightSum = 1, maxWeightSum = 1 };
                        var original = high.bones.Select((b,i) => new { b.name, matrix = high.sharedMesh.bindposes[i] }).ToDictionary(x => x.name, x => x.matrix);
                        for (int i = 0; i < low.bones.Length; i++)
                        {
                            if (!original.TryGetValue(low.bones[i].name, out var expected)) { fail.Add(sex + "/" + low.name + ": unknown bind bone " + low.bones[i].name); continue; }
                            var matrix = low.sharedMesh.bindposes[i];
                            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) row.maxBindError = Mathf.Max(row.maxBindError, Mathf.Abs(matrix[r,c] - expected[r,c]));
                        }
                        foreach (var w in low.sharedMesh.boneWeights)
                        {
                            float sum = w.weight0+w.weight1+w.weight2+w.weight3;
                            row.minWeightSum = Mathf.Min(row.minWeightSum, sum); row.maxWeightSum = Mathf.Max(row.maxWeightSum, sum);
                        }
                        if (row.maxBindError > .0002f) fail.Add(sex + "/" + low.name + ": bind matrix drift " + row.maxBindError);
                        if (row.minWeightSum < .999f || row.maxWeightSum > 1.001f) fail.Add(sex + "/" + low.name + ": weights not normalized");
                        if (low.sharedMesh.subMeshCount != high.sharedMesh.subMeshCount) fail.Add(sex + "/" + low.name + ": material submesh count changed");
                        rows.Add(row);
                    }
                    var fit=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/KitsFitted/"+sex+".fbx");
                    if(!fit)fail.Add(sex+": fitted sleeve asset missing");
                    else foreach(var piece in fit.GetComponentsInChildren<SkinnedMeshRenderer>(true))CheckExtra(sex,"SleeveFit/",piece,look.kit.FirstOrDefault(r=>r&&r.name==piece.name),rows,fail);
                    var golf=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Hero/golfer_"+(sex=="Female" ? "f":"m")+".fbx");
                    var clubs=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Golf/Equipment/"+sex+".fbx");
                    if(!golf||!clubs)fail.Add(sex+": golf equipment asset missing");
                    else foreach(var piece in clubs.GetComponentsInChildren<SkinnedMeshRenderer>(true))CheckExtra(sex,"Equipment/",piece,golf.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name==piece.name),rows,fail);
                }
                var report = new Report { gate = fail.Count == 0 ? "PASS: garment bind/role contract only" : "FAIL", pieces = rows.ToArray(), failures = fail.ToArray() };
                Directory.CreateDirectory("../proof/full-visual-overhaul/characters");
                File.WriteAllText("../proof/full-visual-overhaul/characters/unity-garment-contract.json", JsonUtility.ToJson(report,true));
                Debug.Log("[PremiumCharacterBuild] " + report.gate + " pieces=" + rows.Count + " failures=" + string.Join("; ",fail));
                if (Application.isBatchMode) EditorApplication.Exit(fail.Count == 0 ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }
        static void CheckExtra(string sex,string prefix,SkinnedMeshRenderer low,SkinnedMeshRenderer high,List<Row> rows,List<string> fail){
            if(!high){fail.Add(sex+"/"+prefix+low.name+": matching installed piece missing");return;}
            var row=new Row{sex=sex,piece=prefix+low.name,sourceTriangles=Count(high.sharedMesh),matchTriangles=Count(low.sharedMesh),bones=low.bones.Length,minWeightSum=1,maxWeightSum=1};
            var original=high.bones.Select((b,i)=>new{b.name,matrix=high.sharedMesh.bindposes[i]}).ToDictionary(x=>x.name,x=>x.matrix);
            for(int i=0;i<low.bones.Length;i++){
                if(!original.TryGetValue(low.bones[i].name,out var expected)){fail.Add(sex+"/"+prefix+low.name+": unknown bone "+low.bones[i].name);continue;}
                var matrix=low.sharedMesh.bindposes[i];for(int r=0;r<4;r++)for(int c=0;c<4;c++)row.maxBindError=Mathf.Max(row.maxBindError,Mathf.Abs(matrix[r,c]-expected[r,c]));
            }
            foreach(var w in low.sharedMesh.boneWeights){float sum=w.weight0+w.weight1+w.weight2+w.weight3;row.minWeightSum=Mathf.Min(row.minWeightSum,sum);row.maxWeightSum=Mathf.Max(row.maxWeightSum,sum);}
            if(row.maxBindError>.0002f)fail.Add(sex+"/"+prefix+low.name+": bind drift "+row.maxBindError);
            if(row.minWeightSum<.999f||row.maxWeightSum>1.001f)fail.Add(sex+"/"+prefix+low.name+": unnormalized weights");
            if(high.sharedMesh.subMeshCount!=low.sharedMesh.subMeshCount)fail.Add(sex+"/"+prefix+low.name+": material slot drift");rows.Add(row);
        }
        static int Count(Mesh mesh) { int n=0;for(int i=0;i<mesh.subMeshCount;i++)n+=(int)mesh.GetIndexCount(i)/3;return n; }
    }
}
