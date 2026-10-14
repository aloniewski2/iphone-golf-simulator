using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Local, continuous head correction. Body, neck base, weights, bindposes,
    /// topology and animation sources are retained. Blink endpoints follow the
    /// same mapping as the resting face, so the lid remains attached.
    public static class HeroFaceReferenceFit
    {
        static readonly Dictionary<Mesh, Mesh> cache = new();
        /// The orbit term lowers the eyes (and the face measured from them) by 10 mm; the bald-base numbers were taken on the finished face.
        const float Settle = -.010f;
        public static Mesh Prepare(SkinnedMeshRenderer source, SkinnedMeshRenderer body, SkinnedMeshRenderer features, string headName, bool female)
        {
            if (!source || !body || !features || !source.sharedMesh.isReadable) return source ? source.sharedMesh : null;
            var mesh = source.sharedMesh;
            if (cache.TryGetValue(mesh, out var ready) && ready) return ready;
            int hi = Array.FindIndex(body.bones, b => b && b.name == headName);
            int fi = Array.FindIndex(features.bones, b => b && b.name == headName);
            int si = Array.FindIndex(source.bones, b => b && b.name == headName);
            if (hi < 0 || fi < 0 || si < 0) return mesh;
            var rest = body.sharedMesh.bindposes[hi].inverse;
            var featureMap = rest * features.sharedMesh.bindposes[fi];
            var sourceMap = rest * mesh.bindposes[si];
            var fv = features.sharedMesh.vertices; var fn = features.sharedMesh.normals;
            var indices = new List<int>();
            for (int s = 0; s < features.sharedMaterials.Length; s++)
                if (features.sharedMaterials[s].name.Contains("Sclera")) indices.AddRange(features.sharedMesh.GetTriangles(s));
            if (indices.Count == 0) return mesh;
            Vector3 up = rest.MultiplyVector(Vector3.up).normalized;
            Vector3 forward = featureMap.inverse.transpose.MultiplyVector(indices.Aggregate(Vector3.zero, (sum, i) => sum + fn[i]));
            forward = (forward - up * Vector3.Dot(forward, up)).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized; up = Vector3.Cross(forward, right).normalized;
            Vector3 eye = featureMap.MultiplyPoint3x4(indices.Aggregate(Vector3.zero, (sum, i) => sum + fv[i]) / indices.Count);
            Vector3 ToFace(Vector3 p) { var d = sourceMap.MultiplyPoint3x4(p) - eye; return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward)); }
            Vector3 FromFace(Vector3 p) => sourceMap.inverse.MultiplyPoint3x4(eye + right * p.x + up * p.y + forward * p.z);
            float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.Clamp01((x - a) / (b - a)));
            var browVertices = new HashSet<int>(); var irisVertices = new HashSet<int>();
            if (source == features) for (int sub = 0; sub < features.sharedMaterials.Length; sub++) {
                string role = features.sharedMaterials[sub].name;
                if (role.Contains("Brow")) foreach (int i in mesh.GetTriangles(sub)) browVertices.Add(i);
                if (new[] { "Iris", "Pupil", "Limbal", "Catch" }.Any(role.Contains)) foreach (int i in mesh.GetTriangles(sub)) irisVertices.Add(i);
            }
            Vector3 Warp(Vector3 original, int vertex)
            {
                var p = ToFace(original);
                float head = Smooth(-.17f, -.12f, p.y) * (1 - Smooth(.16f, .20f, Mathf.Abs(p.x)));
                if (head < .00001f) return original;
                float front = Smooth(-.075f, -.045f, p.z);
                float eyeCenter = female ? .0453f : .0367f;
                float orbit = Smooth(-.085f, -.022f, p.y) * (1 - Smooth(.060f, .110f, p.y)) * front;
                float eyeRegion = Mathf.Exp(-Mathf.Pow((Mathf.Abs(p.x)-eyeCenter)/.027f,4)-Mathf.Pow(p.y/.018f,4)) * front;
                if(female && source==body && p.y>0){
                    float lid=Mathf.Exp(-Mathf.Pow((Mathf.Abs(p.x)-eyeCenter)/.025f,4)-Mathf.Pow((p.y-.010f)/.011f,4))*Smooth(0,.005f,p.y)*front;
                    p.y+=.0035f*lid;
                }
                float side = p.x / Mathf.Sqrt(p.x * p.x + .0001f);
                if (!female && !browVertices.Contains(vertex)) {
                    p.y += p.y * .32f * eyeRegion;
                    float irisRegion = Mathf.Exp(-Mathf.Pow((Mathf.Abs(p.x) - eyeCenter) / .030f, 4)) * eyeRegion;
                    p.x += (p.x - side * eyeCenter) * .20f * irisRegion;
                }
                float orbitalSides = Smooth(.012f, .030f, Mathf.Abs(p.x)) * Mathf.Exp(-Mathf.Pow(p.y/.050f,4));
                p.x -= side * (female ? .0028f : .0035f) * orbit * orbitalSides;
                p.y -= .010f * orbit;
                if (browVertices.Contains(vertex) && female) p.y = .0249f + (p.y - .0249f) * .88f;
                float mouth = Mathf.Exp(-Mathf.Pow((p.y + .079f) / .022f, 4)) * (1 - Smooth(.045f, .075f, Mathf.Abs(p.x))) * front;
                if (!female) p.x *= 1 + .06f * mouth;   // bald-base pass 2: was .16, the wide stretched grin
                if(source==body){
                    if(!female) {
                        // Open the existing auricle toward the front view while
                        // retaining its attachment and authored cartilage folds.
                        float ear=Smooth(.072f,.100f,Mathf.Abs(p.x))*(1-Smooth(-.085f,-.025f,p.z))
                            *Smooth(-.100f,-.060f,p.y)*(1-Smooth(.010f,.055f,p.y));
                        float angle=20*Mathf.Deg2Rad*ear;
                        float dx=Mathf.Abs(p.x)-.078f,dz=p.z+.090f;
                        p.x=Mathf.Sign(p.x)*(.078f+dx*Mathf.Cos(angle)-dz*Mathf.Sin(angle));
                        p.z=-.090f+dx*Mathf.Sin(angle)+dz*Mathf.Cos(angle);
                    }
                    // Bald-base pass 2026-10-12: a smaller, tidier auricle. Measured on this fitted head, the ear spans male y -.052..+.021,
                    // z -.118..-.071 (up to 26 mm proud of the skull) and female y -.052..+.040, z -.170..-.109 (31 mm). It shrinks toward its
                    // own centre on the skull side; a soft elliptical falloff lets the scalp around it slide along itself instead of denting.
                    // (Measured on the finished face: here y is Settle lower and the male x is before its 1.085 head scale.)
                    {
                        float yc=(female?-.003f:-.017f)+Settle,zc=female?-.138f:-.092f,root=female?.0888f:.0906f/1.085f;
                        float ry=female?.048f:.040f,rz=female?.032f:.026f,shrink=female?.16f:.14f;
                        float reach=Mathf.Sqrt(Mathf.Pow((p.y-yc)/ry,2)+Mathf.Pow((p.z-zc)/rz,2));
                        float earShrink=shrink*(1-Smooth(1f,1.6f,reach))*Smooth(root-.030f,root-.015f,Mathf.Abs(p.x));
                        if(earShrink>0){
                            p.y=yc+(p.y-yc)*(1-earShrink);p.z=zc+(p.z-zc)*(1-earShrink);
                            float lateral=Mathf.Abs(p.x)-root;
                            p.x-=Mathf.Sign(p.x)*earShrink*Smooth(0,.006f,lateral)*Mathf.Max(lateral,0);
                        }
                    }
                    // Restore the rounded malar volume below the outer eyes.
                    // A broad, continuous support avoids a hard cosmetic ridge.
                    float cheek=Mathf.Exp(-2*Mathf.Pow((Mathf.Abs(p.x)-(female?.058f:.052f))/.030f,2)
                        -2*Mathf.Pow((p.y+.049f)/.032f,2))*Smooth(-.055f,-.010f,p.z);
                    p.z+=(female?.0045f:.0035f)*cheek;
                    p.x+=side*.0012f*cheek;
                    if(female) {
                        float nose=Mathf.Exp(-Mathf.Pow(p.x/.029f,4)-Mathf.Pow((p.y+.055f)/.021f,4))*Smooth(.014f,.023f,p.z);
                        p.y+=.0055f*nose;
                        p.x*=1+.12f*nose;
                    }
                    // Round the nose's broad planes into a tip and separate alar
                    // wings. A partial blend retains the source identity and
                    // joins continuously into the untouched cheek surface.
                    float tip=Mathf.Exp(-2*Mathf.Pow(p.x/(female?.016f:.023f),2)-2*Mathf.Pow((p.y+(female?.043f:.049f))/(female?.015f:.019f),2));
                    float bridge=Mathf.Exp(-2*Mathf.Pow(p.x/(female?.0075f:.010f),2)-2*Mathf.Pow((p.y+.019f)/.035f,2));
                    float alar=Mathf.Exp(-2*Mathf.Pow((Mathf.Abs(p.x)-(female?.016f:.020f))/.009f,2)-2*Mathf.Pow((p.y+.053f)/.011f,2));
                    float targetNose=(female?.015f:.035f)+(female?.020f:.019f)*tip+(female?.008f:.014f)*bridge+.006f*alar;
                    float noseBlend=Mathf.Exp(-Mathf.Pow(p.x/.032f,4)-Mathf.Pow((p.y+.047f)/.032f,4))*Smooth(female?0:.020f,female?.028f:.052f,p.z);
                    noseBlend*=Smooth(-.074f,-.060f,p.y);
                    p.z=Mathf.Lerp(p.z,targetNose,noseBlend*(female?.25f:.55f));
                    float lipWidth=female?.034f:.041f;
                    float lipShape=Mathf.Pow(Mathf.Clamp01(1-p.x*p.x/(lipWidth*lipWidth)),2)*front;
                    float lower=Mathf.Exp(-Mathf.Pow((p.y+(female?.0875f:.084f))/.0070f,2));
                    p.z+=(female?.0025f:.0018f)*lower*lipShape;
                }
                // Male plate has a fuller cranium and mandible. Fade completely
                // above the neck base; no body or skeletal scaling.
                if (!female) p.x *= 1 + .085f * head;
                // Bald-base pass 2 (2026-10-12), in this finished face's own measurements (mm in comments, centred on the finished eyes;
                // this warp's frame is centred on the source eyes, which the orbit term above lowered by Settle):
                // eyes a little bigger and rounder, irises round and slightly larger, the female eyes 3 mm closer together,
                // a smaller, narrower nose, and the ears a second step smaller. Head only; every term fades to zero before the neck.
                {
                    float sx = Mathf.Sign(p.x), ax = Mathf.Abs(p.x);
                    // eye openings: male 39 x 15 centred (38.5, -1.5); female 58 x 32 centred (46.7, -0.6)
                    float ec = female ? .0467f : .0385f, ey = (female ? -.0006f : -.0015f) + Settle, hw = female ? .029f : .020f, hh = female ? .016f : .0075f;
                    float grow = female ? 1.0f : 1.08f, tall = female ? 1.20f : 1.55f, inward = female ? .003f : 0;
                    float dx = ax - ec, dy = p.y - ey, reach = Mathf.Sqrt(dx * dx / (hw * hw) + dy * dy / (hh * hh));
                    float eyeFront = Smooth(-.060f, -.035f, p.z);
                    float eyeWeight = (1 - Smooth(1.0f, 2.6f, reach)) * eyeFront;
                    if (irisVertices.Contains(vertex)) {
                        // the iris stays round: one uniform scale about its own centre (male (36.6, 1.4), female (42.6, 0.7))
                        float ic = female ? .0426f : .0366f, iy = (female ? .0007f : .0014f) + Settle, iris = female ? 1.06f : 1.16f;
                        ax = ic + (ax - ic) * iris; p.y = iy + (p.y - iy) * iris;
                    }
                    else if (!browVertices.Contains(vertex) && eyeWeight > 0) {
                        ax = ec + dx * (1 + (grow - 1) * eyeWeight); p.y = ey + dy * (1 + (tall - 1) * eyeWeight);
                    }
                    ax -= inward * (1 - Smooth(1.3f, 3.2f, reach)) * eyeFront * Smooth(.004f, .022f, ax);   // nothing crosses the midline
                    p.x = sx * ax;
                    if (source == body) {
                        // nose: narrower at the nostrils and a little less proud (only what stands in front of the cheek plane)
                        float noseFront = female ? Smooth(.004f, .016f, p.z) : Smooth(.031f, .045f, p.z);
                        float nose = Mathf.Exp(-Mathf.Pow(p.x / .028f, 4) - Mathf.Pow((p.y - Settle + (female ? .047f : .048f)) / .030f, 4)) * noseFront;
                        p.x *= 1 - (female ? .22f : .24f) * nose;
                        p.z -= (female ? .0040f : .0045f) * nose * Smooth(0, 1, nose);
                        // ears, second step: male 65 mm tall centred (y -15.2, z -93.2), female 95 mm (y -1.6, z -138.4)
                        float yc = (female ? -.0016f : -.0152f) + Settle, zc = female ? -.1384f : -.0932f, root = female ? .0892f : .0909f;
                        float ry = female ? .052f : .036f, rz = female ? .032f : .022f, shrink = female ? .16f : .14f;
                        float earReach = Mathf.Sqrt(Mathf.Pow((p.y - yc) / ry, 2) + Mathf.Pow((p.z - zc) / rz, 2));
                        float earShrink = shrink * (1 - Smooth(1f, 1.6f, earReach)) * Smooth(root - .030f, root - .015f, Mathf.Abs(p.x));
                        if (earShrink > 0) {
                            p.y = yc + (p.y - yc) * (1 - earShrink); p.z = zc + (p.z - zc) * (1 - earShrink);
                            float lateral = Mathf.Abs(p.x) - root;
                            p.x -= Mathf.Sign(p.x) * earShrink * Smooth(0, .006f, lateral) * Mathf.Max(lateral, 0);
                        }
                    }
                }
                return FromFace(p);
            }
            var before = mesh.vertices; var after = new Vector3[before.Length]; var normals = mesh.normals;
            var adjusted = (Vector3[])normals.Clone(); int changed = 0, belowHeadChanges = 0; float minimumJacobian = 1;
            const float epsilon = .0001f;
            for (int i = 0; i < before.Length; i++) {
                after[i] = Warp(before[i], i);
                if ((after[i] - before[i]).sqrMagnitude < 1e-14f) { after[i] = before[i]; continue; }
                changed++;
                if(ToFace(before[i]).y<=-.17f)belowHeadChanges++;
                var jacobian = Matrix4x4.identity;
                jacobian.SetColumn(0, (Warp(before[i] + Vector3.right * epsilon, i) - after[i]) / epsilon);
                jacobian.SetColumn(1, (Warp(before[i] + Vector3.up * epsilon, i) - after[i]) / epsilon);
                jacobian.SetColumn(2, (Warp(before[i] + Vector3.forward * epsilon, i) - after[i]) / epsilon);
                minimumJacobian=Mathf.Min(minimumJacobian,jacobian.determinant);
                adjusted[i] = jacobian.inverse.transpose.MultiplyVector(normals[i]).normalized;
            }
            ready = UnityEngine.Object.Instantiate(mesh); ready.name = mesh.name + " (reference face fit)"; ready.hideFlags = HideFlags.DontSave;
            ready.vertices = after; ready.normals = adjusted; ready.ClearBlendShapes();
            if (source == body) {
                // Distance to the actual open eyelid boundary travels with the
                // skinned surface, including every blink, instead of projecting
                // a stationary painted arc onto the face.
                var welded = new Dictionary<Vector3Int,int>();
                var alias = new int[before.Length]; var facePoints = new List<Vector3>();
                for(int i=0;i<before.Length;i++) {
                    var p=ToFace(before[i]); var key=new Vector3Int(Mathf.RoundToInt(p.x*1000000),Mathf.RoundToInt(p.y*1000000),Mathf.RoundToInt(p.z*1000000));
                    if(!welded.TryGetValue(key,out int id)){id=facePoints.Count;welded[key]=id;facePoints.Add(p);} alias[i]=id;
                }
                var edges=new Dictionary<(int,int),int>(); var triangles=mesh.triangles;
                void Edge(int a,int b){if(a>b)(a,b)=(b,a);edges.TryGetValue((a,b),out int count);edges[(a,b)]=count+1;}
                for(int t=0;t<triangles.Length;t+=3){Edge(alias[triangles[t]],alias[triangles[t+1]]);Edge(alias[triangles[t+1]],alias[triangles[t+2]]);Edge(alias[triangles[t+2]],alias[triangles[t]]);}
                var upper=new List<(Vector3,Vector3)>();var lower=new List<(Vector3,Vector3)>();
                foreach(var edge in edges)if(edge.Value==1){
                    var a=facePoints[edge.Key.Item1];var b=facePoints[edge.Key.Item2];var mid=(a+b)*.5f;
                    if(Mathf.Abs(mid.x)>.014f && Mathf.Abs(mid.x)<.085f && Mathf.Abs(mid.y)<.032f && mid.z>-.028f)
                        (mid.y>0?upper:lower).Add((a,b));
                }
                float Distance(Vector3 p,List<(Vector3,Vector3)> segments){float d=.1f;foreach(var edge in segments){var v=edge.Item2-edge.Item1;float t=Mathf.Clamp01(Vector3.Dot(p-edge.Item1,v)/Mathf.Max(v.sqrMagnitude,1e-12f));d=Mathf.Min(d,Vector3.Distance(p,edge.Item1+v*t));}return d;}
                var rim=new List<Vector2>(before.Length);
                foreach(var p in before){var f=ToFace(p);rim.Add(new Vector2(Distance(f,upper),Distance(f,lower)));}
                ready.SetUVs(2,rim);
            }
            for (int shape = 0; shape < mesh.blendShapeCount; shape++) for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++) {
                var dp = new Vector3[before.Length]; var dn = new Vector3[before.Length]; var dt = new Vector3[before.Length];
                mesh.GetBlendShapeFrameVertices(shape, frame, dp, dn, dt);
                for (int i = 0; i < dp.Length; i++) if (dp[i].sqrMagnitude > 1e-14f) dp[i] = Warp(before[i] + dp[i], i) - after[i];
                ready.AddBlendShapeFrame(mesh.GetBlendShapeName(shape), mesh.GetBlendShapeFrameWeight(shape, frame), dp, dn, dt);
            }
            if (source == features) {
                var eyeVertices = new HashSet<int>();
                for (int sub=0;sub<features.sharedMaterials.Length;sub++) {
                    string role=features.sharedMaterials[sub].name;
                    if(new[]{"Iris","Pupil","Limbal","Catch"}.Any(role.Contains))
                        foreach(int i in mesh.GetTriangles(sub))eyeVertices.Add(i);
                }
                void Expression(string name, HashSet<int> selected, Vector3 displacement) {
                    var delta=new Vector3[after.Length];
                    foreach(int i in selected)delta[i]=sourceMap.inverse.MultiplyVector(displacement);
                    ready.AddBlendShapeFrame(name,100,delta,new Vector3[after.Length],new Vector3[after.Length]);
                }
                Expression("Face_GazeRight",eyeVertices,right*.0018f);Expression("Face_GazeLeft",eyeVertices,-right*.0018f);
                Expression("Face_GazeUp",eyeVertices,up*.0018f);Expression("Face_GazeDown",eyeVertices,-up*.0018f);
                Expression("Face_BrowUp",browVertices,up*.001f);Expression("Face_BrowDown",browVertices,-up*.001f);
            }
            ready.RecalculateBounds(); cache[mesh] = ready;
            bool topologySame=mesh.triangles.SequenceEqual(ready.triangles),weightsSame=mesh.boneWeights.SequenceEqual(ready.boneWeights),bindsSame=mesh.bindposes.SequenceEqual(ready.bindposes);
            Debug.Log($"[HeroFaceReferenceFit] {(female ? "female" : "male")} {source.name}: moved={changed}; belowHeadChanges={belowHeadChanges}; minJacobian={minimumJacobian:R}; topologySame={topologySame}; weightsSame={weightsSame}; bindsSame={bindsSame}");
            if(belowHeadChanges!=0 || minimumJacobian<=0 || !topologySame || !weightsSame || !bindsSame)
                Debug.LogError("[HeroFaceReferenceFit] head-only deformation invariant failed");
            return ready;
        }
        public static void ApplyExpression(MatchHeroLook hero, float blink, Vector2 gaze, float brow)
        {
            var face=hero ? hero.face as SkinnedMeshRenderer : null;
            if(!face || !face.sharedMesh)return;
            void Weight(string name,float value){int index=face.sharedMesh.GetBlendShapeIndex(name);if(index>=0)face.SetBlendShapeWeight(index,Mathf.Clamp01(value)*100);}
            gaze=Vector2.ClampMagnitude(gaze,.0018f)*(1-Mathf.Clamp01(blink));
            Weight("Face_GazeRight",gaze.x/.0018f);Weight("Face_GazeLeft",-gaze.x/.0018f);
            Weight("Face_GazeUp",gaze.y/.0018f);Weight("Face_GazeDown",-gaze.y/.0018f);
            Weight("Face_BrowUp",brow/.001f);Weight("Face_BrowDown",-brow/.001f);
        }
    }
}
