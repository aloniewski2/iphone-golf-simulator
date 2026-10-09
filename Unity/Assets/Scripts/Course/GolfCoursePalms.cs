using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Replaces the legacy palm's disconnected cylinders and leaf blobs for rendering only.
    /// The original TREES filter stays intact for ObstacleScan; the replacement is batched
    /// after collision extraction and disposed with its course.
    public sealed class GolfCoursePalms : MonoBehaviour
    {
        sealed class Source
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public int trunk, frond;
        }
        sealed class Palm
        {
            public Vector3 foot, crown;
            public float height, radius;
            public readonly HashSet<int> vertices = new();
        }
        readonly List<Source> sources = new();
        readonly List<Mesh> owned = new();
        public int PalmCount { get; private set; }
        public int RemovedFrondTriangles { get; private set; }
        public int AddedTriangles { get; private set; }
        public Vector3 FirstCrown { get; private set; }
        bool rebuilt;

        public static void Prepare(GameObject model)
        {
            if (model.GetComponent<GolfCoursePalms>()) return;
            GolfCoursePalms owner = null;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.name.StartsWith("TREES") || !r.TryGetComponent<MeshFilter>(out var mf)
                    || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                int trunk = -1, frond = -1;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    string name = mats[i].name.Replace(" (Instance)", "").Split('.')[0];
                    if (name == "MAT_PALM_TRUNK") trunk = i;
                    if (name == "MAT_PALM_FROND") frond = i;
                }
                if (trunk < 0 || frond < 0) continue;
                if (!owner) owner = model.AddComponent<GolfCoursePalms>();
                owner.sources.Add(new Source { filter = mf, renderer = r, trunk = trunk, frond = frond });
            }
        }

        public void Rebuild()
        {
            if (rebuilt) return;
            rebuilt = true;
            foreach (var source in sources) Rebuild(source);
            Debug.Log($"[GolfCoursePalms] {PalmCount} continuous palms, {RemovedFrondTriangles} detached leaf triangles replaced, {AddedTriangles} new triangles; original obstacle meshes retained");
        }

        void Rebuild(Source source)
        {
            var original = source.filter.sharedMesh;
            var points = original.vertices;
            for (int i = 0; i < points.Length; i++) points[i] = source.filter.transform.TransformPoint(points[i]);
            var candidates = new List<Palm>();
            var plants = new List<(Vector3 foot,float height,float reach)>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("PLANT_")) continue;
                var parts = t.name.Split('_');
                if (parts.Length < 5 || !int.TryParse(parts[3],out int cm) || !int.TryParse(parts[4],out int reach)) continue;
                float scale = Mathf.Abs(t.lossyScale.x)/100;
                plants.Add((t.position,cm*scale,reach*scale));
                if (parts[2] == "T") candidates.Add(new Palm { foot = t.position });
            }
            if (candidates.Count == 0) return;
            int Nearest(Vector3 p, List<Palm> plants)
            {
                int result = -1; float best = float.MaxValue;
                for (int i = 0; i < plants.Count; i++)
                {
                    var d = p - plants[i].foot; float distance = d.x * d.x + d.z * d.z;
                    if (distance < best) { best = distance; result = i; }
                }
                return result;
            }
            // Only palm-trunk vertices seed a replacement. Other tree markers, shrubs and
            // the jungle bushes that share MAT_PALM_FROND retain their original geometry.
            foreach (int v in original.GetTriangles(source.trunk)) candidates[Nearest(points[v], candidates)].vertices.Add(v);
            var palms = new List<Palm>();
            foreach (var palm in candidates)
            {
                if (palm.vertices.Count < 20) continue;
                float top = palm.foot.y;
                foreach (int v in palm.vertices) top = Mathf.Max(top, points[v].y);
                palm.height = top - palm.foot.y;
                if (palm.height < 6) continue;
                // The old seven cylinders all point straight up. The final ring starts at
                // lean*(6/7)^2; extrapolate to the crown at lean, joining the same endpoints.
                var ring = new HashSet<Vector3>();
                foreach (int v in palm.vertices)
                {
                    var p = points[v];
                    if (p.y > top - .01f) ring.Add(p);
                    if (p.y < palm.foot.y + .05f)
                        palm.radius = Mathf.Max(palm.radius, new Vector2(p.x - palm.foot.x, p.z - palm.foot.z).magnitude);
                }
                if (ring.Count < 3 || palm.radius <= .01f) continue;
                var center = Vector3.zero; foreach (var p in ring) center += p; center /= ring.Count;
                var lean = center - palm.foot; lean.y = 0;
                palm.crown = palm.foot + lean * (49f / 36f) + Vector3.up * palm.height;
                palms.Add(palm);
            }
            if (palms.Count == 0) return;
            var keep = Instantiate(original); keep.name = original.name + " without legacy palms"; owned.Add(keep);
            var removedTrunks = new HashSet<int>();
            foreach (var p in palms) removedTrunks.UnionWith(p.vertices);
            var trunk = new List<int>(); var oldTrunk = original.GetTriangles(source.trunk);
            for (int i = 0; i < oldTrunk.Length; i += 3)
                if (!removedTrunks.Contains(oldTrunk[i])) { trunk.Add(oldTrunk[i]); trunk.Add(oldTrunk[i+1]); trunk.Add(oldTrunk[i+2]); }
            keep.SetTriangles(trunk, source.trunk);
            var fronds = new List<int>(); var oldFronds = original.GetTriangles(source.frond);
            for (int i = 0; i < oldFronds.Length; i += 3)
            {
                var a = points[oldFronds[i]]; var b = points[oldFronds[i+1]]; var c = points[oldFronds[i+2]];
                var center = (a+b+c)/3;
                int owner = -1; float gap = float.MaxValue;
                for(int p=0;p<plants.Count;p++)
                {
                    var plant=plants[p]; var delta=center-plant.foot;
                    float distance=new Vector2(delta.x,delta.z).magnitude;
                    if(distance>plant.reach+.6f || delta.y<-.8f || delta.y>plant.height+.6f || distance>=gap)continue;
                    owner=p;gap=distance;
                }
                // A bush can stand on a high cliff beside a lower palm. Height relative to
                // that palm alone would incorrectly remove the bush's shared-material leaves.
                bool isCrown=false;
                if(owner>=0)foreach(var palm in palms)
                    if((palm.foot-plants[owner].foot).sqrMagnitude<.001f
                        && Mathf.Min(a.y,b.y,c.y)>palm.foot.y+palm.height*.5f){isCrown=true;break;}
                if (isCrown) { RemovedFrondTriangles++; continue; }
                fronds.Add(oldFronds[i]); fronds.Add(oldFronds[i+1]); fronds.Add(oldFronds[i+2]);
            }
            keep.SetTriangles(fronds,source.frond);
            Visual(source,keep,source.renderer.sharedMaterials,"GOLF_TREE_RETAINED_VISUAL");
            var vertices = new List<Vector3>(); var wood = new List<int>(); var leaves = new List<int>();
            foreach (var p in palms)
            {
                if (PalmCount++ == 0) FirstCrown = p.crown;
                BuildPalm(p,vertices,wood,leaves);
            }
            for (int i = 0; i < vertices.Count; i++) vertices[i] = source.filter.transform.InverseTransformPoint(vertices[i]);
            var mesh = new Mesh { name = "Connected course palms", indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
            owned.Add(mesh); mesh.SetVertices(vertices); mesh.SetTriangles(wood,0); mesh.SetTriangles(leaves,1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AddedTriangles += (wood.Count+leaves.Count)/3;
            var mats = source.renderer.sharedMaterials;
            Visual(source,mesh,new[]{mats[source.trunk],mats[source.frond]},"GOLF_PALM_VISUAL");
            source.renderer.enabled = false;
        }

        void Visual(Source source,Mesh mesh,Material[] materials,string name)
        {
            var go = new GameObject(name); go.layer = source.renderer.gameObject.layer;
            go.transform.SetParent(source.filter.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = materials;
            r.shadowCastingMode = source.renderer.shadowCastingMode; r.receiveShadows = source.renderer.receiveShadows;
        }

        static void BuildPalm(Palm palm,List<Vector3> vertices,List<int> wood,List<int> leaves)
        {
            var lean = palm.crown - palm.foot; lean.y = 0;
            const int rings = 14, sides = 10, fronds = 9, sections = 14;
            int start = vertices.Count;
            for (int k = 0; k <= rings; k++)
            {
                float t = k/(float)rings;
                var center = palm.foot + lean*t*t + Vector3.up*(palm.height*t);
                float radius = palm.radius*Mathf.Lerp(1,.61f,t)*(1+.035f*Mathf.Sin(k*2.4f));
                for (int j = 0; j < sides; j++)
                {
                    float angle = j*Mathf.PI*2/sides;
                    vertices.Add(center + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);
                    if (k == 0) continue;
                    int a = start+(k-1)*sides+j, b = start+(k-1)*sides+(j+1)%sides;
                    Quad(wood,a,a+sides,b,b+sides);
                }
            }
            // A cap sits under the attached fronds and closes the trunk when viewed from above.
            for (int j=1;j<sides-1;j++) { wood.Add(start+rings*sides); wood.Add(start+rings*sides+j+1); wood.Add(start+rings*sides+j); }
            float phase = Mathf.Atan2(lean.z,lean.x);
            for (int f = 0; f < fronds; f++)
            {
                float angle = phase+f*Mathf.PI*2/fronds+.10f*Mathf.Sin(f*7.1f);
                var direction = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var across = Vector3.Cross(direction,Vector3.up);
                float length = palm.height*(.43f+.035f*Mathf.Sin(f*2.7f));
                int first = vertices.Count;
                for (int k = 0; k <= sections; k++)
                {
                    float t = k/(float)sections;
                    var center = palm.crown + direction*(length*t) + Vector3.up*(length*(.8f*t-1.3f*t*t));
                    float width = palm.radius*1.8f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*t)),.75f);
                    if (k%2 == 0 && k>2) width *= .72f; // attached, feathered leaf edges
                    float fold = width*.20f;
                    vertices.Add(center-across*width-Vector3.up*fold);
                    vertices.Add(center);
                    vertices.Add(center+across*width-Vector3.up*fold);
                    if (k == 0) continue;
                    int a = first+(k-1)*3;
                    Quad(leaves,a,a+1,a+3,a+4); Quad(leaves,a+1,a+2,a+4,a+5);
                }
            }
        }

        static void Quad(List<int> triangles,int a,int b,int c,int d)
        { triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(c);triangles.Add(b);triangles.Add(d); }

        void OnDestroy()
        {
            foreach(var mesh in owned) if(mesh) { if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh); }
        }
    }
}
