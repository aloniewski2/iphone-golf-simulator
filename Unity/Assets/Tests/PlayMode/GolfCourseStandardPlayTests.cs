using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    public class GolfCourseStandardPlayTests
    {
        [UnityTest,Timeout(300000)]
        public IEnumerator UpgradePreservesGroundObstaclesAndHazardsAcrossTheCatalog()
        {
            yield return SceneManager.LoadSceneAsync("Golf");
            var game=Object.FindFirstObjectByType<GolfGame>();
            game.ChooseHoles(0); game.Play();
            yield return null;
            try
            {
                foreach(int number in new[]{7,12,13,14,15,16,17,18,19,20,21,22,23})
                {
                    GolfCourseLook.Enabled=false; game.JumpToHole(number);
                    yield return null;yield return null;Physics.SyncTransforms();
                    var before=Snapshot(game.CurrentHole); var obstacles=game.CurrentHole.Obstacles.ToArray();
                    int colliders=HoleView.Current.GetComponentsInChildren<MeshCollider>().Length;
                    GolfCourseLook.Enabled=true; game.JumpToHole(number);
                    yield return null;yield return null;Physics.SyncTransforms();
                    CollectionAssert.AreEqual(before,Snapshot(game.CurrentHole),"ground and lies "+number);
                    CollectionAssert.AreEqual(obstacles,game.CurrentHole.Obstacles,"obstacles "+number);
                    Assert.AreEqual(colliders,HoleView.Current.GetComponentsInChildren<MeshCollider>().Length);
                    var look=HoleView.Current.GetComponent<GolfCourseLook>(); Assert.IsNotNull(look);
                    var grass=look.Get(GolfCourseLook.Surface.Fairway);
                    Assert.AreEqual("GolfArcade/GolfGround",grass.shader.name);
                    Assert.AreNotEqual(Texture2D.whiteTexture,grass.GetTexture("_BaseMap"));
                    Assert.IsNotNull(grass.GetTexture("_BumpMap"));
                    if(number is 16 or 19 or 21 or 22 or 23)
                    {
                        // Both implementations retain the original gameplay source.
                        var original=HoleView.Current.ModelNode("TREES");
                        var prefab=Resources.Load<GameObject>($"Course/hole_{number:00}");
                        var authored=prefab.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="TREES");
                        Assert.AreSame(authored.sharedMesh,original.GetComponent<MeshFilter>().sharedMesh,"original tree collision source "+number);
                        var resort=HoleView.Current.GetComponentInChildren<GolfResortDress>();
                        if(resort&&resort.ReplacedAuthoredPlants)
                        {
                            // Current botanical replacement deliberately skips the old
                            // Palm.Rebuild path, so its removal counters remain zero.
                            Assert.IsFalse(original.GetComponent<Renderer>().enabled,"legacy crowns hidden "+number);
                            Assert.Greater(resort.PlantCount,0,"replacement plants "+number);
                            Assert.Greater(resort.TriangleCount,0,"replacement geometry "+number);
                            var palmLods=resort.GetComponentsInChildren<LODGroup>(true).Where(g=>g.name.EndsWith("_PALM_LOD")).ToArray();
                            Assert.Greater(palmLods.Length,0,"authored palm replacement LODs "+number);
                            foreach(var group in palmLods)
                            {
                                var levels=group.GetLODs();Assert.AreEqual(2,levels.Length,"near/far palms "+number);
                                foreach(var level in levels)
                                {
                                    Assert.Greater(level.renderers.Length,0,"nonempty palm LOD "+number);
                                    foreach(var renderer in level.renderers)
                                    {
                                        Assert.IsNotNull(renderer);
                                        var filter=renderer.GetComponent<MeshFilter>();Assert.IsNotNull(filter);
                                        var mesh=filter.sharedMesh;Assert.IsNotNull(mesh);
                                        Assert.Greater(mesh.vertexCount,0,"palm vertices "+number);
                                        Assert.IsTrue(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"finite palm geometry "+number);
                                        var indices=mesh.triangles;Assert.Greater(indices.Length,0,"palm triangles "+number);
                                        Assert.IsTrue(indices.All(i=>i>=0&&i<mesh.vertexCount),"valid palm indices "+number);
                                        var leaf=renderer.sharedMaterial;Assert.IsNotNull(leaf);
                                        Assert.AreEqual("GolfArcade/GolfBotanical",leaf.shader.name);
                                        Assert.AreEqual(0,leaf.GetFloat("_Cull"));
                                        Assert.AreEqual(1,leaf.GetFloat("_Leaf"));
                                        Assert.AreEqual(1,leaf.GetFloat("_VertexPalette"));
                                        var colors=mesh.colors;
                                        Assert.AreEqual(mesh.vertexCount,colors.Length,"authored vertex palette "+number);
                                        // Paint stores linear RGB and GolfBotanical multiplies it
                                        // by _BaseColor directly. The readable-palette threshold
                                        // is in display (sRGB) space, not the vertex buffer space.
                                        var tint=leaf.GetColor("_BaseColor");
                                        var albedos=colors.Select(c=>new Color(c.r*tint.r,c.g*tint.g,c.b*tint.b,1)).ToArray();
                                        var displayAlbedos=albedos.Select(c=>c.gamma).ToArray();
                                        Debug.Log($"[PalmPalette] hole {number} {mesh.name}: linear G {albedos.Min(c=>c.g):F6}..{albedos.Max(c=>c.g):F6}; sRGB G {displayAlbedos.Min(c=>c.g):F6}..{displayAlbedos.Max(c=>c.g):F6}");
                                        Assert.IsTrue(displayAlbedos.Any(c=>c.g>.20f&&c.g>c.r),"readable green palm palette "+number);
                                    }
                                }
                            }
                        }
                        else
                        {
                            var palms=HoleView.Current.GetComponentInChildren<GolfCoursePalms>();
                            Assert.IsNotNull(palms,"palm repair "+number);
                            Assert.Greater(palms.PalmCount,0);
                            Assert.Greater(palms.RemovedFrondTriangles,0);
                            if(number==19) Assert.AreEqual(18960,palms.RemovedFrondTriangles,"38 palm crowns only; cliff-top jungle bushes stay intact");
                            var visual=original.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="GOLF_PALM_VISUAL");
                            foreach(var v in visual.sharedMesh.vertices)
                                Assert.IsTrue(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z),"finite palm geometry");
                            var leaf=visual.GetComponent<Renderer>().sharedMaterials[1];
                            Assert.AreEqual(0,leaf.GetFloat("_Cull"));
                            Assert.AreEqual(1,leaf.GetFloat("_Foliage"));
                            Assert.Greater(leaf.GetColor("_BaseColor").g,.20f,"readable frond palette");
                        }
                    }
                    if(number==20)
                    {
                        var sails=HoleView.Current.GetComponentsInChildren<Transform>().First(t=>t.name=="SAILS_SPIN");
                        Assert.IsTrue(sails.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
                        Assert.IsNotNull(game.CurrentHole.Windmill);
                    }
                    yield return null;
                    yield return null; // allow deferred visual material and mesh disposal
                    foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
                        if(material.name.StartsWith("Course "))Assert.IsTrue(material.name.StartsWith("Course "+number+" "),"retained course material "+material.name);
                    Assert.AreEqual(1,Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name.StartsWith("Course mowing ")));
                }
                game.enabled=false;
                foreach(var h in Course.Course.Meadow().Holes)
                {
                    Object.DestroyImmediate(HoleView.Current.gameObject);
                    var view=HoleView.Build(h,game.transform);
                    Assert.IsNotNull(view.GetComponent<GolfCourseLook>(),"procedural hole "+h.Number);
                    Assert.IsTrue(view.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="GolfArcade/GolfGround"));
                    yield return null;
                }
            }
            finally { GolfCourseLook.Enabled=true; }
        }

        static List<double> Snapshot(Hole hole)
        {
            var result=new List<double>();
            foreach(var p in hole.Centerline)
                for(int x=-2;x<=2;x++) for(int z=-2;z<=2;z++)
                {
                    var q=new CoursePoint(p.X+x*3,p.D+z*3);
                    result.Add(hole.Ground(q)); result.Add(hole.Surface.Height(q)); result.Add((int)hole.LieAt(q));
                }
            foreach(var h in hole.Hazards) { result.Add((int)h.Kind);result.Add(h.X);result.Add(h.Distance);result.Add(h.Width);result.Add(h.Length); }
            return result;
        }
    }
}
