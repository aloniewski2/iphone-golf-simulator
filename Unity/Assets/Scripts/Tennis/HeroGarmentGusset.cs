using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    // Opt-in source candidate. Invoke after IK and after the sleeve shape weights
    // are set: HeroGarmentGusset.Apply(look.female, look.transform, top).
    // Each renderer owns its deforming mesh. Full and LOD have separate profiles.
    internal static class HeroGarmentGusset
    {
        [Serializable] sealed class Profile
        {
            public int sourceVertexCount, vertexCount;
            public string[] bones;
            public float[] sourceVertices, sourceNormals, sourceWeights, weights;
            public int[] sourceTriangles, sourceIndices, boneIndices, triangles, triangleSourceFaces;
            public int[] freeVertices, aliases, contactTriangles;
            public int[] proxyBoneIndices, proxyTriangles;
            public float[] proxyBoneLocalPositions, proxyWeights, proxyRestPositions;
            public float clearance = .003f, maximumDisplacement = .04f;
            public float rayMinimum = -.012f, rayMaximum = .05f, contactStep = .008f;
            public int contactIterations = 6;
            public int cageCount;
            public int[] cageIndices;
            public float[] cageWeights, cageStrengths;
            public float[] authoredVertices, bodyWeights, bodyOffsets;
            public int[] bodyIndices, mirroredVertices;
        }
        sealed class Shape
        {
            public float[] frameWeights;
            public Vector3[][] deltas, normalDeltas, tangentDeltas;
        }
        static readonly bool Enabled = Environment.GetEnvironmentVariable("VISUAL_UNDERARM_GUSSET") == "1";
        static readonly Dictionary<int, Profile> profiles = new();
        sealed class RendererStates
        {
            public Transform owner;
            public readonly Dictionary<Mesh, State> sources = new();
        }
        static readonly Dictionary<SkinnedMeshRenderer, RendererStates> states = new();
        static readonly Dictionary<Mesh, State> ownedMeshes = new();
        static readonly bool Telemetry = Environment.GetEnvironmentVariable("VISUAL_UNDERARM_GUSSET_STATS") == "1";
        static long applyCalls, skippedCalls, applyTicks, maximumApplyTicks, applyAllocatedBytes, buildAllocatedBytes, buildTicks, maximumBuildTicks;
        static int builds, activations, fullCalls, lodCalls, maximumFreeVertices;

        // LOD must retain canonical, immutable sources, never a transient deforming mesh.
        public static Mesh CanonicalSource(Mesh mesh)
            => mesh && ownedMeshes.TryGetValue(mesh, out var state) ? state.source : mesh;

        internal static void BeginCaptureStats()
        {
            applyCalls = skippedCalls = applyTicks = maximumApplyTicks = applyAllocatedBytes = buildAllocatedBytes = buildTicks = maximumBuildTicks = 0;
            builds = activations = fullCalls = lodCalls = maximumFreeVertices = 0;
        }
        internal static HeroGarmentGussetLifetime.CaptureStats CaptureStats()
        {
            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            return new HeroGarmentGussetLifetime.CaptureStats {
                enabled = Telemetry, calls = applyCalls, skippedCalls = skippedCalls,
                solves = applyCalls - skippedCalls, fullCalls = fullCalls, lodCalls = lodCalls,
                builds = builds, meshActivations = activations, maximumFreeVertices = maximumFreeVertices,
                meanApplyMs = applyCalls > 0 ? applyTicks * ticksToMs / applyCalls : 0,
                maximumApplyMs = maximumApplyTicks * ticksToMs,
                totalBuildMs = buildTicks * ticksToMs, maximumBuildMs = maximumBuildTicks * ticksToMs,
                managedApplyBytes = applyAllocatedBytes, managedBuildBytes = buildAllocatedBytes,
                retainedRendererCaches = states.Count, retainedMeshes = ownedMeshes.Count
            };
        }
        internal static void EndCaptureStats()
        {
            if (Telemetry) Debug.Log("[HeroGarmentGussetStats] " + JsonUtility.ToJson(CaptureStats()));
        }

        internal static void Release(Transform owner)
        {
            var remove = new List<SkinnedMeshRenderer>();
            foreach (var entry in states)
            {
                if (entry.Value.owner != owner) continue;
                if (entry.Key) entry.Key.sharedMesh = CanonicalSource(entry.Key.sharedMesh);
                foreach (var state in entry.Value.sources.Values)
                {
                    ownedMeshes.Remove(state.mesh);
                    if (state.mesh) UnityEngine.Object.Destroy(state.mesh);
                }
                remove.Add(entry.Key);
            }
            foreach (var renderer in remove) states.Remove(renderer);
        }

        static Profile Load(int count)
        {
            if (profiles.TryGetValue(count, out var p)) return p;
            string detail = count == 13932 ? "Full" : count == 11533 ? "LOD" : null;
            if (detail == null) throw new InvalidOperationException("Unverified gusset source vertex count: " + count);
            var asset = Resources.Load<TextAsset>("Tennis/Correctives/UnderarmGusset_Female_" + detail);
            if (!asset) throw new InvalidOperationException("Missing gusset " + detail + " profile; capture must stop");
            p = JsonUtility.FromJson<Profile>(asset.text);
            if (p == null || p.sourceVertexCount != count || p.vertexCount <= count)
                throw new InvalidOperationException("Invalid gusset profile");
            profiles[count] = p;
            return p;
        }

        public static void Apply(bool female, Transform heroRoot, SkinnedMeshRenderer renderer)
        {
            if (!Enabled || !female || !renderer || !renderer.sharedMesh) return;
            if (!heroRoot) throw new InvalidOperationException("Gusset requires its hero owner");
            var source = CanonicalSource(renderer.sharedMesh);
            if (!states.TryGetValue(renderer, out var rendererStates))
            {
                rendererStates = new RendererStates { owner = heroRoot };
                states.Add(renderer, rendererStates);
                if (!heroRoot.GetComponent<HeroGarmentGussetLifetime>())
                    heroRoot.gameObject.AddComponent<HeroGarmentGussetLifetime>().owner = heroRoot;
            }
            if (!rendererStates.sources.TryGetValue(source, out var state))
            {
                long allocatedBefore = Telemetry ? GC.GetAllocatedBytesForCurrentThread() : 0;
                long buildStarted = Telemetry ? Stopwatch.GetTimestamp() : 0;
                state = new State(renderer, source, Load(source.vertexCount));
                rendererStates.sources.Add(source, state);
                ownedMeshes.Add(state.mesh, state);
                if (Telemetry)
                {
                    long elapsed = Stopwatch.GetTimestamp() - buildStarted;
                    builds++; buildTicks += elapsed; maximumBuildTicks = Math.Max(maximumBuildTicks, elapsed);
                    buildAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                }
            }
            if (renderer.sharedMesh != state.mesh)
            {
                state.Activate(renderer);
                if (Telemetry) activations++;
            }
            long started = Telemetry ? Stopwatch.GetTimestamp() : 0;
            long applyAllocatedBefore = Telemetry ? GC.GetAllocatedBytesForCurrentThread() : 0;
            bool solved = state.Apply(heroRoot, renderer);
            if (Telemetry)
            {
                long elapsed = Stopwatch.GetTimestamp() - started;
                applyCalls++; if (!solved) skippedCalls++; applyTicks += elapsed; maximumApplyTicks = Math.Max(maximumApplyTicks, elapsed);
                applyAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - applyAllocatedBefore;
                if (state.source.vertexCount == 13932) fullCalls++; else lodCalls++;
                maximumFreeVertices = Math.Max(maximumFreeVertices, state.FreeVertexCount);
            }
        }

        sealed class State
        {
            public readonly Mesh mesh, source;
            public int FreeVertexCount => p.freeVertices.Length;
            readonly float[] savedWeights, poseWeights;
            bool havePose;
            readonly Profile p;
            readonly Vector3[] basis, normalBasis, shaped, shapedNormals, shapedTangents, posed, raw, geom;
            readonly Vector4[] tangentBasis, outputTangents;
            readonly Vector3[] delta, updates, output, outputNormals, aliasSums, proxy, proxyNormals;
            readonly bool[] free;
            readonly int[] aliasCounts, normalTriangles, solveVertices;
            readonly Vector4[] realPalette, dualPalette;
            readonly float[] counts;
            readonly Matrix4x4[] skinMatrices, boneToRoot, bindposes;
            readonly Transform[] bones;
            readonly BoneWeight[] bindings;
            readonly Shape[] shapes;
            
            readonly Bounds bounds;

            Vector3 Interpolate(Vector3[] values, int i)
            {
                int q = i * 3;
                return values[p.sourceIndices[q]] * p.sourceWeights[q]
                    + values[p.sourceIndices[q+1]] * p.sourceWeights[q+1]
                    + values[p.sourceIndices[q+2]] * p.sourceWeights[q+2];
            }

            public State(SkinnedMeshRenderer r, Mesh canonicalSource, Profile profile)
            {
                p = profile; bones = r.bones;
                source = canonicalSource;
                if (!source.isReadable || source.subMeshCount < 1)
                    throw new InvalidOperationException("Gusset requires a verified readable Top with triangle submeshes");
                var positions = source.vertices;
                var sourceNormals = source.normals;
                var sourceTriangles = source.triangles;
                if (p.sourceVertices.Length != positions.Length * 3 || p.sourceTriangles.Length != sourceTriangles.Length)
                    throw new InvalidOperationException("Gusset source geometry mismatch");
                for (int i = 0; i < positions.Length; i++)
                    if ((positions[i] - Read(p.sourceVertices, i)).sqrMagnitude > 2.5e-9f)
                        throw new InvalidOperationException("Gusset source basis moved at " + i);
                for (int i = 0; i < sourceTriangles.Length; i++)
                    if (sourceTriangles[i] != p.sourceTriangles[i])
                        throw new InvalidOperationException("Gusset source topology changed at " + i);
                if (bones.Length != p.bones.Length)
                    throw new InvalidOperationException("Gusset bone count mismatch");
                for (int i = 0; i < p.bones.Length; i++)
                    if (!bones[i] || bones[i].name != p.bones[i])
                        throw new InvalidOperationException("Gusset bone order mismatch at " + i);
                int n = p.vertexCount;
                basis = new Vector3[n]; normalBasis = new Vector3[n]; bindings = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    basis[i] = Read(p.authoredVertices, i);
                    normalBasis[i] = Interpolate(sourceNormals, i).normalized;
                    if(p.mirroredVertices[i]!=0)normalBasis[i].x=-normalBasis[i].x;
                    int j = i * 4;
                    bindings[i] = new BoneWeight {
                        boneIndex0=p.boneIndices[j], boneIndex1=p.boneIndices[j+1],
                        boneIndex2=p.boneIndices[j+2], boneIndex3=p.boneIndices[j+3],
                        weight0=p.weights[j], weight1=p.weights[j+1], weight2=p.weights[j+2], weight3=p.weights[j+3]
                    };
                }
                mesh = new Mesh { name=source.name+" (underarm gusset)", hideFlags=HideFlags.DontSave,
                    indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
                mesh.MarkDynamic(); mesh.vertices=basis; mesh.normals=normalBasis;
                var uvs = new List<Vector4>();
                for (int channel = 0; channel < 8; channel++)
                {
                    uvs.Clear(); source.GetUVs(channel, uvs); if (uvs.Count != positions.Length) continue;
                    var mapped = new List<Vector4>(n);
                    for (int i = 0; i < n; i++) { int q=i*3; mapped.Add(uvs[p.sourceIndices[q]]*p.sourceWeights[q]+uvs[p.sourceIndices[q+1]]*p.sourceWeights[q+1]+uvs[p.sourceIndices[q+2]]*p.sourceWeights[q+2]); }
                    mesh.SetUVs(channel, mapped);
                }
                var tangents=source.tangents;
                tangentBasis=new Vector4[n];outputTangents=new Vector4[n];
                if (tangents.Length==positions.Length)
                {
                    var result=new Vector4[n];
                    for (int i=0;i<n;i++) {int q=i*3;result[i]=tangents[p.sourceIndices[q]]*p.sourceWeights[q]+tangents[p.sourceIndices[q+1]]*p.sourceWeights[q+1]+tangents[p.sourceIndices[q+2]]*p.sourceWeights[q+2];}
                    for(int i=0;i<n;i++)if(p.mirroredVertices[i]!=0){result[i].x=-result[i].x;result[i].w=-result[i].w;}
                    mesh.tangents=result;Array.Copy(result,tangentBasis,n);
                }
                var colors=source.colors;
                if (colors.Length==positions.Length)
                {
                    var result=new Color[n];
                    for(int i=0;i<n;i++){int q=i*3;result[i]=colors[p.sourceIndices[q]]*p.sourceWeights[q]+colors[p.sourceIndices[q+1]]*p.sourceWeights[q+1]+colors[p.sourceIndices[q+2]]*p.sourceWeights[q+2];}
                    mesh.colors=result;
                }
                bindposes=source.bindposes; mesh.bindposes=bindposes;
                // The CPU evaluates the original 53-bone skin and shapes only for the
                // patch and its pinned boundary. All other vertices keep GPU skinning.
                // A rigid output binding transports the patch through one well-conditioned
                // bone. Inverting a blended skin matrix can produce metre-scale rest inputs
                // near an elbow/shoulder fold even for a 40 mm physical correction.
                var outputBindings=new BoneWeight[n];
                Array.Copy(bindings,outputBindings,n);
                foreach(int i in p.freeVertices)outputBindings[i]=new BoneWeight{boneIndex0=0,weight0=1};
                mesh.boneWeights=outputBindings; InstallTriangles(source, mesh, p);
                shapes=new Shape[source.blendShapeCount]; savedWeights=new float[shapes.Length]; poseWeights=new float[shapes.Length];
                var dv=new Vector3[positions.Length];var dn=new Vector3[positions.Length];var dt=new Vector3[positions.Length];
                for(int s=0;s<shapes.Length;s++)
                {
                    int frames=source.GetBlendShapeFrameCount(s);
                    var shape=new Shape{frameWeights=new float[frames],deltas=new Vector3[frames][],normalDeltas=new Vector3[frames][],tangentDeltas=new Vector3[frames][]};shapes[s]=shape;
                    for(int f=0;f<frames;f++)
                    {
                        source.GetBlendShapeFrameVertices(s,f,dv,dn,dt);
                        var vd=new Vector3[n];var nd=new Vector3[n];var td=new Vector3[n];
                        for(int i=0;i<n;i++) if(p.bodyOffsets[i]==0 && p.mirroredVertices[i]==0){vd[i]=Interpolate(dv,i);nd[i]=Interpolate(dn,i);td[i]=Interpolate(dt,i);}
                        shape.frameWeights[f]=source.GetBlendShapeFrameWeight(s,f);shape.deltas[f]=vd;shape.normalDeltas[f]=nd;shape.tangentDeltas[f]=td;
                        // Preserve the named weight controls for HeroGarmentPoseCorrectives,
                        // while the GPU receives no duplicate shape position/normal deltas.
                        var gpuPositions=(Vector3[])vd.Clone();var gpuNormals=(Vector3[])nd.Clone();var gpuTangents=(Vector3[])td.Clone();
                        foreach(int i in p.freeVertices){gpuPositions[i]=Vector3.zero;gpuNormals[i]=Vector3.zero;gpuTangents[i]=Vector3.zero;}
                        mesh.AddBlendShapeFrame(source.GetBlendShapeName(s),shape.frameWeights[f],gpuPositions,gpuNormals,gpuTangents);
                    }
                }
                bounds=source.bounds;bounds.Expand(.20f);mesh.bounds=bounds;
                shaped=new Vector3[n];shapedNormals=new Vector3[n];shapedTangents=new Vector3[n];posed=new Vector3[n];raw=new Vector3[n];geom=new Vector3[n];
                delta=new Vector3[n];updates=new Vector3[n];output=new Vector3[n];outputNormals=new Vector3[n];counts=new float[n];free=new bool[n];
                foreach(int i in p.freeVertices)free[i]=true;
                var incident=new List<int>();
                for(int f=0;f<p.triangles.Length;f+=3)
                    if(free[p.triangles[f]]||free[p.triangles[f+1]]||free[p.triangles[f+2]])
                    {incident.Add(p.triangles[f]);incident.Add(p.triangles[f+1]);incident.Add(p.triangles[f+2]);}
                normalTriangles=incident.ToArray();
                var solve=new HashSet<int>(normalTriangles);
                foreach(int i in p.freeVertices) {
                    solve.Add(i);
                    if(p.bodyOffsets[i]==0)for(int k=0;k<3;k++)solve.Add(p.sourceIndices[i*3+k]);
                }
                solveVertices=new int[solve.Count];solve.CopyTo(solveVertices);
                if(p.bodyIndices==null||p.bodyWeights==null||p.bodyOffsets.Length!=n)
                    throw new InvalidOperationException("Missing coherent anatomical band mapping");
                realPalette=new Vector4[bones.Length];dualPalette=new Vector4[bones.Length];
                Array.Copy(basis,output,n);Array.Copy(normalBasis,outputNormals,n);Array.Copy(tangentBasis,outputTangents,n);
                int aliases=0;foreach(int i in p.aliases)aliases=Math.Max(aliases,i+1);
                aliasSums=new Vector3[aliases];aliasCounts=new int[aliases];foreach(int i in p.aliases)aliasCounts[i]++;
                skinMatrices=new Matrix4x4[bones.Length];boneToRoot=new Matrix4x4[bones.Length];
                proxy=new Vector3[p.proxyWeights.Length/4];proxyNormals=new Vector3[proxy.Length];
                Debug.Log($"[HeroGarmentGusset] verified source {positions.Length}, result {n} vertices / {p.triangles.Length/3} triangles, connected anatomical band, posed surface normal offset, no contact solver");
            }

            public void Activate(SkinnedMeshRenderer r)
            {
                // Apply is called after the current source's shape weights are set.
                // Preserve them across a sharedMesh assignment, with no allocation.
                for (int s = 0; s < savedWeights.Length; s++) savedWeights[s] = r.GetBlendShapeWeight(s);
                r.sharedMesh = mesh;
                for (int s = 0; s < savedWeights.Length; s++) r.SetBlendShapeWeight(s, savedWeights[s]);
            }

            void GeometricNormals(Vector3[] vertices, Vector3[] normals)
            {
                Array.Clear(normals,0,normals.Length);
                for(int f=0;f<normalTriangles.Length;f+=3)
                {int a=normalTriangles[f],b=normalTriangles[f+1],c=normalTriangles[f+2];var area=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);normals[a]+=area;normals[b]+=area;normals[c]+=area;}
                foreach(int i in p.freeVertices)normals[i]=Normalize(normals[i]);
            }
            void Synchronize()
            {
                Array.Clear(aliasSums,0,aliasSums.Length);
                foreach(int i in p.freeVertices)aliasSums[p.aliases[i]]+=delta[i];
                foreach(int i in p.freeVertices){delta[i]=aliasSums[p.aliases[i]]/aliasCounts[p.aliases[i]];posed[i]=raw[i]+delta[i];}
            }
            void AddShape(Shape s,float w)
            {
                if(Mathf.Abs(w)<1e-7f||s.deltas.Length==0)return;
                int upper=0;while(upper<s.frameWeights.Length-1&&w>s.frameWeights[upper])upper++;
                int lower=upper-1;float low=lower<0?0:s.frameWeights[lower];
                float t=(w-low)/(s.frameWeights[upper]-low);
                var a=lower<0?null:s.deltas[lower];var b=s.deltas[upper];
                var na=lower<0?null:s.normalDeltas[lower];var nb=s.normalDeltas[upper];
                var ta=lower<0?null:s.tangentDeltas[lower];var tb=s.tangentDeltas[upper];
                foreach(int i in solveVertices) {
                    shaped[i]+=a==null?b[i]*t:a[i]*(1-t)+b[i]*t;
                    shapedNormals[i]+=na==null?nb[i]*t:na[i]*(1-t)+nb[i]*t;
                    shapedTangents[i]+=ta==null?tb[i]*t:ta[i]*(1-t)+tb[i]*t;
                }
            }
            public bool Apply(Transform root,SkinnedMeshRenderer r)
            {
                var rootInverse=root.worldToLocalMatrix;var bind=bindposes;
                bool changed = !havePose;
                for (int b = 0; b < skinMatrices.Length; b++)
                {
                    var current = rootInverse * bones[b].localToWorldMatrix;
                    // Matrix4x4 == uses an approximate comparison in Unity.
                    // Exact element equality is required for a deterministic reuse.
                    for (int k = 0; k < 16; k++) if (current[k] != boneToRoot[b][k]) changed = true;
                    boneToRoot[b] = current; skinMatrices[b] = current * bind[b];
                }
                for (int s = 0; s < shapes.Length; s++)
                {
                    float current = r.GetBlendShapeWeight(s);
                    if (current != poseWeights[s]) changed = true;
                    poseWeights[s] = current;
                }
                if (!changed) return false;
                havePose = false;
                foreach(int i in solveVertices) {shaped[i]=basis[i];shapedNormals[i]=normalBasis[i];shapedTangents[i]=new Vector3(tangentBasis[i].x,tangentBasis[i].y,tangentBasis[i].z);}
                for(int s=0;s<shapes.Length;s++)AddShape(shapes[s],poseWeights[s]);
                foreach(int i in solveVertices)
                {
                    var w=bindings[i];var v=shaped[i];raw[i]=skinMatrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+skinMatrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+skinMatrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+skinMatrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;posed[i]=raw[i];
                }
                // Cut points evaluate the original triangle barycentrically in posed
                // space, so their retained source face has exactly the same edge.
                foreach(int i in p.freeVertices)if(p.bodyOffsets[i]==0)
                    posed[i]=Interpolate(raw,i);
                Array.Clear(proxyNormals,0,proxyNormals.Length);
                for(int i=0;i<proxy.Length;i++) {
                    Vector3 value=Vector3.zero;
                    for(int k=0;k<4;k++) {int j=i*4+k;value+=boneToRoot[p.proxyBoneIndices[j]].MultiplyPoint3x4(Read(p.proxyBoneLocalPositions,j))*p.proxyWeights[j];}
                    proxy[i]=value;
                }
                for(int f=0;f<p.proxyTriangles.Length;f+=3) {
                    int a=p.proxyTriangles[f],b=p.proxyTriangles[f+1],c=p.proxyTriangles[f+2];
                    var area=Vector3.Cross(proxy[b]-proxy[a],proxy[c]-proxy[a]);
                    proxyNormals[a]+=area;proxyNormals[b]+=area;proxyNormals[c]+=area;
                }
                for(int i=0;i<proxyNormals.Length;i++)proxyNormals[i]=Normalize(proxyNormals[i]);
                foreach(int i in p.freeVertices)if(p.bodyOffsets[i]>0) {
                    Vector3 value=Vector3.zero,normal=Vector3.zero;
                    for(int k=0;k<3;k++){int j=i*3+k;value+=proxy[p.bodyIndices[j]]*p.bodyWeights[j];normal+=proxyNormals[p.bodyIndices[j]]*p.bodyWeights[j];}
                    posed[i]=value+Normalize(normal)*p.bodyOffsets[i];
                }
                GeometricNormals(posed,geom);
                foreach(int i in p.freeVertices)shapedTangents[i]=SkinVector(skinMatrices,bindings[i],shapedTangents[i]);
                var rootOutputInverse=skinMatrices[0].inverse;
                foreach(int i in p.freeVertices) {
                    output[i]=rootOutputInverse.MultiplyPoint3x4(posed[i]);
                    var normal=Normalize(rootOutputInverse.MultiplyVector(geom[i]));
                    var tangent=rootOutputInverse.MultiplyVector(shapedTangents[i]);
                    tangent=Normalize(tangent-normal*Vector3.Dot(normal,tangent));
                    outputNormals[i]=normal;outputTangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,tangentBasis[i].w);
                }
                mesh.vertices=output;mesh.normals=outputNormals;mesh.tangents=outputTangents;
                mesh.bounds=bounds;
                havePose = true;
                return true;
            }
        }
        static void InstallTriangles(Mesh source, Mesh result, Profile p)
        {
            // Snapshot sourceTriangles is Unity's concatenation of every source submesh.
            // Authored parents map each subdivision child back to that exact aggregate face.
            if (p.triangleSourceFaces == null || p.triangleSourceFaces.Length * 3 != p.triangles.Length)
                throw new InvalidOperationException("Gusset missing verified subdivision source-face mapping");
            var sourceFaceSubmesh = new int[p.sourceTriangles.Length / 3];
            var submeshTriangles = new List<int>[source.subMeshCount];
            int sourceFace = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles || source.GetIndexCount(sub) % 3 != 0)
                    throw new InvalidOperationException("Gusset source submesh topology is not triangles at " + sub);
                int count = (int)source.GetIndexCount(sub) / 3;
                if (sourceFace + count > sourceFaceSubmesh.Length)
                    throw new InvalidOperationException("Gusset aggregate submesh face count mismatch");
                for (int i = 0; i < count; i++) sourceFaceSubmesh[sourceFace++] = sub;
                submeshTriangles[sub] = new List<int>();
            }
            if (sourceFace != sourceFaceSubmesh.Length)
                throw new InvalidOperationException("Gusset aggregate submesh face count mismatch");
            for (int f = 0; f < p.triangleSourceFaces.Length; f++)
            {
                int parent = p.triangleSourceFaces[f];
                if (parent < 0 || parent >= sourceFaceSubmesh.Length)
                    throw new InvalidOperationException("Gusset subdivision source-face mapping out of range at " + f);
                var destination = submeshTriangles[sourceFaceSubmesh[parent]];
                for (int k = 0; k < 3; k++) destination.Add(p.triangles[f * 3 + k]);
            }
            result.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < submeshTriangles.Length; sub++) result.SetTriangles(submeshTriangles[sub], sub, false);
        }
        static Vector3 Read(float[] data,int i)=>new Vector3(data[i*3],data[i*3+1],data[i*3+2]);
        static Vector3 Normalize(Vector3 v){float n=v.magnitude;return n>1e-12f?v/n:Vector3.zero;}
        static Vector3 SkinVector(Matrix4x4[] matrices,BoneWeight w,Vector3 v)
            =>matrices[w.boneIndex0].MultiplyVector(v)*w.weight0+matrices[w.boneIndex1].MultiplyVector(v)*w.weight1
              +matrices[w.boneIndex2].MultiplyVector(v)*w.weight2+matrices[w.boneIndex3].MultiplyVector(v)*w.weight3;

        static Vector4 DualPart(Vector4 q,Vector3 t)
        {
            var v=new Vector3(q.x,q.y,q.z);var d=(t*q.w+Vector3.Cross(t,v))*.5f;
            return new Vector4(d.x,d.y,d.z,-Vector3.Dot(t,v)*.5f);
        }
        static Vector3 Translation(Vector4 r,Vector4 d)
        {
            var rv=new Vector3(r.x,r.y,r.z);var dv=new Vector3(d.x,d.y,d.z);
            return (dv*r.w-rv*d.w-Vector3.Cross(dv,rv))*2;
        }
        static Vector3 Rotate(Vector4 q,Vector3 v)
        {
            var r=new Vector3(q.x,q.y,q.z);return v+Vector3.Cross(r,Vector3.Cross(r,v)+v*q.w)*2;
        }
        static void BlendDual(Vector4[] real,Vector4[] dual,BoneWeight w,out Vector4 r,out Vector4 d)
        {
            var reference=real[w.boneIndex0];
            float b=w.weight1*(Vector4.Dot(reference,real[w.boneIndex1])<0?-1:1);
            float c=w.weight2*(Vector4.Dot(reference,real[w.boneIndex2])<0?-1:1);
            float e=w.weight3*(Vector4.Dot(reference,real[w.boneIndex3])<0?-1:1);
            r=reference*w.weight0+real[w.boneIndex1]*b+real[w.boneIndex2]*c+real[w.boneIndex3]*e;
            d=dual[w.boneIndex0]*w.weight0+dual[w.boneIndex1]*b+dual[w.boneIndex2]*c+dual[w.boneIndex3]*e;
            float length=Mathf.Sqrt(Vector4.Dot(r,r));
            if(length<1e-6f)throw new InvalidOperationException("Degenerate underarm dual quaternion");
            r/=length;d/=length;d-=r*Vector4.Dot(r,d);
        }
    }
}
