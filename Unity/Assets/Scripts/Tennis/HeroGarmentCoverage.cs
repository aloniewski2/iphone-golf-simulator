using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Omit only the body faces authored as covered by this particular polo.
    /// The source body and every vertex attribute remain intact. Profiles match
    /// physical triangles, rather than relying on an importer's vertex order.
    public static class HeroGarmentCoverage
    {
        [Serializable] sealed class Profile
        {
            public int garmentVertexCount, expectedRemovedFaceCount;
            public float[] removedTriangleRootPositions;
        }

        const float CellSize = .00025f, MatchTolerance = .00004f;
        static readonly Dictionary<(Mesh, bool, bool), Mesh> prepared = new();

        sealed class FaceMap
        {
            public readonly List<Vector3> points = new();
            public readonly HashSet<(int, int, int)> faces = new();
            readonly Dictionary<Vector3Int, List<int>> cells = new();
            public Bounds bounds;

            static Vector3Int Cell(Vector3 p) => Vector3Int.FloorToInt(p / CellSize);

            public int Find(Vector3 p)
            {
                var cell = Cell(p);
                int closest = -1;
                float distance = MatchTolerance * MatchTolerance;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                    if (cells.TryGetValue(cell + new Vector3Int(x, y, z), out var candidates))
                        foreach (int candidate in candidates)
                        {
                            float d = (points[candidate] - p).sqrMagnitude;
                            if (d <= distance) { closest = candidate; distance = d; }
                        }
                return closest;
            }

            int Intern(Vector3 p)
            {
                int found = Find(p);
                if (found >= 0) return found;
                int id = points.Count;
                points.Add(p);
                var cell = Cell(p);
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<int>();
                list.Add(id);
                if (id == 0) bounds = new Bounds(p, Vector3.zero);
                else bounds.Encapsulate(p);
                return id;
            }

            public FaceMap(float[] coordinates)
            {
                for (int i = 0; i < coordinates.Length; i += 9)
                {
                    int a = Intern(new Vector3(coordinates[i], coordinates[i + 1], coordinates[i + 2]));
                    int b = Intern(new Vector3(coordinates[i + 3], coordinates[i + 4], coordinates[i + 5]));
                    int c = Intern(new Vector3(coordinates[i + 6], coordinates[i + 7], coordinates[i + 8]));
                    if (a == b || b == c || a == c)
                        throw new InvalidOperationException("Polo coverage profile contains a collapsed face");
                    faces.Add(Key(a, b, c));
                }
                bounds.Expand(MatchTolerance * 2);
            }
        }

        static (int, int, int) Key(int a, int b, int c)
        {
            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);
            return (a, b, c);
        }

        public static void Apply(MatchHeroLook hero)
        {
            if (!hero || !hero.body || hero.kit == null) return;
            // Outfit coverage is a bounded repair; preserve an explicit control path.
            // The remaining female underarm fit is handled independently.
            string review = Environment.GetEnvironmentVariable("VISUAL_GARMENT_COVERAGE");
            if (review == "0") return;
            var top = hero.kit.FirstOrDefault(r => r && r.name == "Kit_Top" && r.enabled);
            var source = hero.body.sharedMesh;
            if (!top || !top.sharedMesh || !source || !source.isReadable) return;
            var key = (source, hero.female, hero.golfKit);
            if (prepared.TryGetValue(key, out var cached)) { hero.body.sharedMesh = cached; return; }
            string resource = "Tennis/GarmentCoverage/" + (hero.golfKit ? "Golf_" : "Tennis_") + (hero.female ? "Female" : "Male");
            var text = Resources.Load<TextAsset>(resource);
            if (!text) return;
            var profile = JsonUtility.FromJson<Profile>(text.text);
            if (profile?.removedTriangleRootPositions == null || profile.expectedRemovedFaceCount <= 0 ||
                profile.removedTriangleRootPositions.Length != profile.expectedRemovedFaceCount * 9 ||
                top.sharedMesh.vertexCount != profile.garmentVertexCount)
            {
                Debug.LogWarning("[HeroGarmentCoverage] Profile/outfit mismatch; original body retained: " + resource);
                return;
            }

            var map = new FaceMap(profile.removedTriangleRootPositions);
            var vertices = source.vertices;
            var toRoot = hero.transform.worldToLocalMatrix * hero.body.transform.localToWorldMatrix;
            var ids = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = toRoot.MultiplyPoint3x4(vertices[i]);
                ids[i] = map.bounds.Contains(p) ? map.Find(p) : -1;
            }
            var matched = new HashSet<(int, int, int)>();
            var retained = new List<int>[source.subMeshCount];
            int removed = 0;
            for (int sub = 0; sub < retained.Length; sub++)
            {
                int[] indices = source.GetTriangles(sub);
                var keep = retained[sub] = new List<int>(indices.Length);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    var face = Key(ids[a], ids[b], ids[c]);
                    if (ids[a] >= 0 && ids[b] >= 0 && ids[c] >= 0 && map.faces.Contains(face))
                    { matched.Add(face); removed++; }
                    else { keep.Add(a); keep.Add(b); keep.Add(c); }
                }
            }
            // A changed source must be reviewed, never partially masked by a
            // profile whose anatomical correspondence is no longer complete.
            if (matched.Count != map.faces.Count)
            {
                Debug.LogWarning($"[HeroGarmentCoverage] Correspondence mismatch; original body retained: {resource}, {matched.Count}/{map.faces.Count} physical faces");
                return;
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + " (polo coverage)";
            mesh.hideFlags = HideFlags.DontSave;
            for (int sub = 0; sub < retained.Length; sub++) mesh.SetTriangles(retained[sub], sub, false);
            prepared[key] = mesh;
            hero.body.sharedMesh = mesh;
            Debug.Log($"[HeroGarmentCoverage] {resource}: {matched.Count}/{map.faces.Count} physical faces matched; {removed} rendered faces omitted; source vertices/weights/normals/UVs/morphs/bounds exact");
        }
    }
}
