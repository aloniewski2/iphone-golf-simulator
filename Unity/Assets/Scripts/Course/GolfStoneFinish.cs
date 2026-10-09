using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Softens separate boulder renderers after obstacle extraction. Source
    /// MeshFilters, collider geometry and object activity stay authoritative.
    public sealed class GolfStoneFinish : MonoBehaviour
    {
        readonly List<Mesh> owned = new();
        public float MaximumDisplacement { get; private set; }
        public int AddedTriangles { get; private set; }

        public static void Apply(GameObject root)
        {
            if (root.GetComponent<GolfStoneFinish>()) return;
            var owner = root.AddComponent<GolfStoneFinish>();
            foreach (var source in root.GetComponentsInChildren<MeshFilter>())
            {
                string name = source.name.ToUpperInvariant();
                if (!(name.StartsWith("ROCK") || name.StartsWith("BOULDER"))) continue;
                if (!source.sharedMesh || !source.sharedMesh.isReadable || source.sharedMesh.blendShapeCount > 0) continue;
                if (!source.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                owner.Round(source, renderer);
            }
            Debug.Log($"[GolfStoneFinish] {owner.owned.Count} separate boulder render clones, {owner.AddedTriangles} added triangles, maximum {owner.MaximumDisplacement:F3} yd; original MeshFilters/colliders retained");
        }

        void Round(MeshFilter source, MeshRenderer renderer)
        {
            var original = source.sharedMesh; var input = original.vertices;
            if (input.Length > 24000 || original.GetTopology(0) != MeshTopology.Triangles) return;
            var positions = new List<Vector3>(); var lookup = new Dictionary<Vector3Int, int>();
            var remap = new int[input.Length];
            Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            for (int i = 0; i < input.Length; i++)
            {
                var key = Key(input[i]);
                if (!lookup.TryGetValue(key, out int index)) { index = positions.Count; positions.Add(input[i]); lookup.Add(key, index); }
                remap[i] = index;
            }
            var neighbours = new HashSet<int>[positions.Count];
            for (int i = 0; i < neighbours.Length; i++) neighbours[i] = new HashSet<int>();
            var edges = new Dictionary<ulong, List<int>>();
            ulong Edge(int a, int b) => ((ulong)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            void Link(int a, int b, int opposite)
            {
                neighbours[a].Add(b); neighbours[b].Add(a); var key = Edge(a, b);
                if (!edges.TryGetValue(key, out var list)) { list = new List<int>(); edges.Add(key, list); }
                list.Add(opposite);
            }
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                var indices = original.GetTriangles(sub);
                for (int t = 0; t < indices.Length; t += 3)
                {
                    int a = remap[indices[t]], b = remap[indices[t + 1]], c = remap[indices[t + 2]];
                    Link(a, b, c); Link(b, c, a); Link(c, a, b);
                }
            }
            var output = new List<Vector3>(positions); var mids = new Dictionary<ulong, int>();
            // A restrained partial Loop relaxation retains geological planes.
            // The local cap is transformed and recorded in world units below.
            float cap = Mathf.Min(original.bounds.size.x, Mathf.Min(original.bounds.size.y, original.bounds.size.z)) * .06f;
            Vector3 Limit(Vector3 from, Vector3 to) => from + Vector3.ClampMagnitude(to - from, cap);
            for (int i = 0; i < positions.Count; i++)
            {
                int count = neighbours[i].Count; if (count < 3) continue;
                float beta = count == 3 ? 3f / 16 : 3f / (8 * count); Vector3 sum = Vector3.zero;
                foreach (int other in neighbours[i]) sum += positions[other];
                var smooth = positions[i] * (1 - count * beta) + sum * beta;
                output[i] = Limit(positions[i], Vector3.Lerp(positions[i], smooth, .32f));
            }
            int Mid(int a, int b)
            {
                var key = Edge(a, b); if (mids.TryGetValue(key, out int old)) return old;
                var middle = (positions[a] + positions[b]) * .5f; var point = middle;
                var opposite = edges[key];
                if (opposite.Count == 2)
                {
                    var smooth = (positions[a] + positions[b]) * .375f + (positions[opposite[0]] + positions[opposite[1]]) * .125f;
                    point = Limit(middle, Vector3.Lerp(middle, smooth, .55f));
                }
                int index = output.Count; output.Add(point); mids.Add(key, index);
                MaximumDisplacement = Mathf.Max(MaximumDisplacement, source.transform.TransformVector(point - middle).magnitude);
                return index;
            }
            var triangles = new List<int>[original.subMeshCount]; int oldTriangles = 0;
            for (int sub = 0; sub < triangles.Length; sub++)
            {
                var indices = original.GetTriangles(sub); oldTriangles += indices.Length / 3; triangles[sub] = new List<int>(indices.Length * 4);
                for (int t = 0; t < indices.Length; t += 3)
                {
                    int a = remap[indices[t]], b = remap[indices[t + 1]], c = remap[indices[t + 2]];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    triangles[sub].AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
            }
            for (int i = 0; i < positions.Count; i++) MaximumDisplacement = Mathf.Max(MaximumDisplacement, source.transform.TransformVector(output[i] - positions[i]).magnitude);
            var mesh = new Mesh { name = "Resort rounded source boulder render", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(output); mesh.subMeshCount = triangles.Length;
            for (int sub = 0; sub < triangles.Length; sub++) mesh.SetTriangles(triangles[sub], sub);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh); AddedTriangles += oldTriangles * 3;
            var go = new GameObject("RESORT_ROUNDED_BOULDER"); go.transform.SetParent(source.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var replacement = go.AddComponent<MeshRenderer>(); replacement.sharedMaterials = renderer.sharedMaterials;
            replacement.shadowCastingMode = renderer.shadowCastingMode; replacement.receiveShadows = renderer.receiveShadows;
            renderer.enabled = false;
        }
        void OnDestroy() { foreach (var mesh in owned) if (mesh) Destroy(mesh); }
    }
}
