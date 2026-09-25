using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Reads what stands on a modelled hole off its meshes, for the physics: every tree, bush,
    /// rock and building as an Obstacle. The course builders merge each kind into one mesh
    /// (TREES, SHRUBS, ROCKS) or keep them apart (Hole 7's TREE_PINE_LARGE.012 and so on); either
    /// way each connected piece of a mesh is a part — a trunk, a tier of branches, a boulder —
    /// and the parts standing over the same spot are one thing.
    public static class ObstacleScan
    {
        public static Obstacle[] From(Transform model)
        {
            var found = new List<Obstacle>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.gameObject.activeInHierarchy || !mf.sharedMesh.isReadable) continue;
                var kind = KindOf(mf.name);
                if (kind == null) continue;
                if (kind == ObstacleKind.Wall)
                {
                    var b = mf.GetComponent<Renderer>() is Renderer r ? r.bounds : WorldBounds(mf);
                    found.Add(Obstacle.Round(ObstacleKind.Wall, b.center.x, b.center.z, b.min.y, b.max.y, 0.5 * Mathf.Min(b.size.x, b.size.z)));
                    continue;
                }
                foreach (var group in Standing(Pieces(mf)))
                {
                    if (kind == ObstacleKind.Tree) found.Add(TreeOf(group));
                    else
                    {
                        var b = Union(group);
                        double radius = 0.5 * Mathf.Max(b.size.x, b.size.z);
                        if (radius < 0.25) continue;   // pebbles and flowers: nothing to stop a ball
                        found.Add(Obstacle.Round(kind.Value, b.center.x, b.center.z, b.min.y, b.max.y, radius));
                    }
                }
            }
            return found.ToArray();
        }

        /// What a mesh is by its name; null for everything the ball goes through or rests on.
        static ObstacleKind? KindOf(string name)
        {
            name = name.ToUpperInvariant();
            if (name.StartsWith("TREE")) return ObstacleKind.Tree;
            if (name.StartsWith("BUSH") || name.StartsWith("SHRUB")) return ObstacleKind.Bush;
            if (name.StartsWith("ROCK")) return ObstacleKind.Rock;   // (not CLIFF_ROCK: the cliffs' own boulders are down in the sea)
            if (name.StartsWith("LIGHTHOUSE") || name.StartsWith("TOWER") || name.StartsWith("CLUBHOUSE")) return ObstacleKind.Wall;
            return null;
        }

        /// A tree from its parts: the thin ones up the middle are the trunk, the rest the crown —
        /// a pine's when it stands taller than it is wide, round otherwise.
        static Obstacle TreeOf(List<Bounds> parts)
        {
            var all = Union(parts);
            float widest = 0;
            foreach (var p in parts) widest = Mathf.Max(widest, Half(p));
            float trunk = 0, crownBase = float.MaxValue, radius = 0;
            Vector3 middle = all.center;
            foreach (var p in parts)
            {
                if (Half(p) < 0.35f * widest) trunk = Mathf.Max(trunk, Half(p));
                else
                {
                    crownBase = Mathf.Min(crownBase, p.min.y);
                    if (Half(p) >= radius) { radius = Half(p); middle = p.center; }
                }
            }
            if (trunk <= 0 || crownBase == float.MaxValue)
            {
                // one piece: the lower third is trunk
                radius = widest; crownBase = all.min.y + 0.3f * all.size.y;
                trunk = Mathf.Clamp(0.1f * widest, 0.1f, 0.35f);
            }
            bool cone = (all.max.y - crownBase) > 1.25f * 2 * radius;
            return Obstacle.Tree(middle.x, middle.z, all.min.y, all.max.y, radius, crownBase, Mathf.Clamp(trunk, 0.08f, 0.5f), cone);
        }

        static float Half(Bounds b) => 0.25f * (b.size.x + b.size.z);

        static Bounds Union(List<Bounds> parts)
        {
            var b = parts[0];
            for (int i = 1; i < parts.Count; i++) b.Encapsulate(parts[i]);
            return b;
        }

        /// Parts standing over the same spot together: a trunk and the tiers of branches over it
        /// are one tree, the tree beside it another.
        static List<List<Bounds>> Standing(List<Bounds> parts)
        {
            parts.Sort((a, b) => Half(b).CompareTo(Half(a)));   // the big ones set the places
            var groups = new List<List<Bounds>>();
            var centres = new List<(Vector2 at, float half)>();
            foreach (var p in parts)
            {
                var at = new Vector2(p.center.x, p.center.z);
                int into = -1;
                for (int g = 0; g < groups.Count && into < 0; g++)
                {
                    float gap = (centres[g].at - at).magnitude;
                    if (gap < Mathf.Min(1.2f, 0.5f * Mathf.Max(centres[g].half, Half(p)))) into = g;
                }
                if (into < 0) { groups.Add(new List<Bounds> { p }); centres.Add((at, Half(p))); }
                else groups[into].Add(p);
            }
            return groups;
        }

        /// The connected pieces of a mesh, in world space. Flat-shaded low-poly meshes give every
        /// face its own corners, so corners are joined where they coincide before the pieces are
        /// found.
        static List<Bounds> Pieces(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var weld = new int[verts.Length];
            var at = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < verts.Length; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 1000f);
                if (!at.TryGetValue(key, out int w)) { w = at.Count; at[key] = w; }
                weld[i] = w;
            }
            var parent = new int[at.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = Find(weld[tris[t]]), b = Find(weld[tris[t + 1]]), c = Find(weld[tris[t + 2]]);
                parent[b] = a; parent[Find(c)] = a;
            }
            var toWorld = mf.transform.localToWorldMatrix;
            var pieces = new Dictionary<int, Bounds>();
            for (int i = 0; i < verts.Length; i++)
            {
                int root = Find(weld[i]);
                var p = toWorld.MultiplyPoint3x4(verts[i]);
                if (pieces.TryGetValue(root, out var b)) { b.Encapsulate(p); pieces[root] = b; }
                else pieces[root] = new Bounds(p, Vector3.zero);
            }
            return new List<Bounds>(pieces.Values);
        }

        static Bounds WorldBounds(MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds;
            var toWorld = mf.transform.localToWorldMatrix;
            var w = new Bounds(toWorld.MultiplyPoint3x4(b.center), Vector3.zero);
            foreach (var x in new[] { -1, 1 }) foreach (var y in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
                w.Encapsulate(toWorld.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            return w;
        }
    }
}
