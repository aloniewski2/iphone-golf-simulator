using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Identity-only cosmetics for the locked Hero01. Only swaps meshes/materials and a prop on the
    /// existing sockets (Hat slot, Hand_L). Never touches the Animator, Avatar, racket socket,
    /// the gameplay TennisActor, timing windows, reach or speed.
    public sealed class HeroCosmetics : MonoBehaviour
    {
        public enum Hat { Visor, Cap, Sweatband }
        public ModularHeroLook look;
        [Header("Hat slot (skinned to Head)")] public GameObject visorAsset, capAsset, sweatbandAsset;
        public Material capMaterial, bandWhite, bandOrange;
        [Header("Outfit trim variant")] public Material clothDefault, clothTrim, pipingDefault, pipingTrim;
        [Header("Celebration prop (Hand_L socket)")] public Mesh trophyMesh; public Material trophyMaterial;
        public Vector3 trophyOrigin, trophyAxis, trophyNormal;   // left-hand geometric frame (W,F,N)
        public bool debugHotkeys = true, debugPanel = true;

        public Hat CurrentHat { get; private set; } = Hat.Visor;
        public bool Trim { get; private set; }
        public bool Trophy { get; private set; }
        public event Action Changed;
        Transform trophy; bool celebrating;

        /// Short cuts (bald / buzz / waves): the hats are sized for a head of hair (band radius ~21 cm vs a ~13.5 cm
        /// skull at band height), so on a bare skull they read as floating halos. HeroKit sets this before EquipHat.
        public bool shortHair;
        public const float ShortHatScale = .76f, ShortHatDrop = .01f;   // fitted to the rebuilt skull (r ~.152 at band height vs band .209)
        /// Scale every hat mesh about its band centre and seat it lower, through the head-bone bind pose (the hats are
        /// skinned 100% to the head), on per-renderer mesh copies so the shared assets never change.
        void FitToSkull(Transform slot)
        {
            var head = look.animator ? look.animator.GetBoneTransform(HumanBodyBones.Head) : null; if (!slot || !head) return;
            var rs = slot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // band centre: least-squares circle through the back half of the baked band, in head space
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var r in rs) { var m = new Mesh(); r.BakeMesh(m, true); foreach (var v in m.vertices) pts.Add(head.InverseTransformPoint(r.transform.TransformPoint(v))); Destroy(m); }
            if (pts.Count < 20) return;
            Vector3 up = head.InverseTransformDirection(look.transform.up).normalized, fwd = Vector3.ProjectOnPlane(head.InverseTransformDirection(look.transform.forward), up).normalized, right = Vector3.Cross(up, fwd);
            float mid = 0; foreach (var p in pts) mid += Vector3.Dot(p, fwd); mid /= pts.Count;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0; int n = 0; float h = 0;
            foreach (var p in pts)
            {
                if (Vector3.Dot(p, fwd) > mid) continue;                 // back half: no brim
                double x = Vector3.Dot(p, right), y = Vector3.Dot(p, fwd), z = -(x * x + y * y);
                sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y; sxz += x * z; syz += y * z; sz += z; n++; h += Vector3.Dot(p, up);
            }
            if (n < 10) return;
            var A = new Matrix4x4(); A.SetRow(0, new Vector4((float)sxx, (float)sxy, (float)sx, 0)); A.SetRow(1, new Vector4((float)sxy, (float)syy, (float)sy, 0));
            A.SetRow(2, new Vector4((float)sx, (float)sy, n, 0)); A.SetRow(3, new Vector4(0, 0, 0, 1));
            var sol = A.inverse.MultiplyVector(new Vector3((float)sxz, (float)syz, (float)sz));
            Vector3 c = right * (-sol.x / 2) + fwd * (-sol.y / 2) + up * (h / n);
            float s = ShortHatScale, drop = ShortHatDrop / Mathf.Max(1e-4f, head.lossyScale.y);
            var fit = Matrix4x4.Translate(c - up * drop) * Matrix4x4.Scale(Vector3.one * s) * Matrix4x4.Translate(-c);   // in head-bone space
            foreach (var r in rs)
            {
                var mesh = Instantiate(r.sharedMesh); mesh.name = r.sharedMesh.name + " (short hair)";
                var bp = mesh.bindposes; var bones = r.bones;
                for (int i = 0; i < bp.Length; i++) if (bones[i] == head) bp[i] = fit * bp[i];
                mesh.bindposes = bp; r.sharedMesh = mesh;
            }
        }

        public void EquipHat(Hat hat)
        {
            var asset = hat == Hat.Cap ? capAsset : hat == Hat.Sweatband ? sweatbandAsset : visorAsset;
            if (!asset) return;
            look.Equip(ModularHeroLook.Slot.Hat, asset);
            var slot = FindSlot(ModularHeroLook.Slot.Hat);
            if (slot && hat != Hat.Visor)
                foreach (var r in slot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    r.sharedMaterial = hat == Hat.Cap ? capMaterial : r.name.Contains("Stripe") ? bandOrange : bandWhite;
            // Hero V5 (Adnan, 2026-09-27): the visor is all white for now -- band and brim, every submesh
            if (slot && hat == Hat.Visor && bandWhite)
                foreach (var r in slot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = bandWhite; r.sharedMaterials = ms; }
            if (shortHair) { FitToSkull(slot); BuildHatLiner(null); }   // no hair under the band: nothing for a liner to hide
            else BuildHatLiner(slot);
            CurrentHat = hat; Changed?.Invoke();
        }

        /// A band hat (visor, sweatband) is a ring that stands a few mm off the hair in places; from behind
        /// the background showed through the slit between band and hair. A hair-coloured liner tube is fitted
        /// just inside the band (per angle: the band's own inner radius and height), so that slit always reads
        /// as hair. Rigid on the head bone like the hats. Named HatLiner so the locker export treats it as hat.
        void BuildHatLiner(Transform slot)
        {
            var head = look.animator ? look.animator.GetBoneTransform(HumanBodyBones.Head) : null; if (!head) return;
            foreach (Transform ch in head) if (ch.name == "HatLiner") { ch.name = "HatLiner (old)"; ch.gameObject.SetActive(false); Destroy(ch.gameObject); }   // every stale liner, not just the first
            if (!slot) return;
            Vector3 up = head.InverseTransformDirection(look.transform.up).normalized, fwd = head.InverseTransformDirection(look.transform.forward).normalized;
            fwd = Vector3.ProjectOnPlane(fwd, up).normalized; var right = Vector3.Cross(up, fwd);
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var r in slot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); r.BakeMesh(m, false);
                foreach (var v in m.vertices) pts.Add(head.InverseTransformPoint(r.transform.position + r.transform.rotation * v));
                Destroy(m);
            }
            if (pts.Count < 20) return;
            Vector3 c = Vector3.zero; foreach (var q in pts) c += q; c /= pts.Count;
            const int N = 40; var rMin = new float[N]; var y0 = new float[N]; var y1 = new float[N];
            for (int i = 0; i < N; i++) { rMin[i] = float.MaxValue; y0[i] = float.MaxValue; y1[i] = float.MinValue; }
            foreach (var q in pts)
            {
                var d = q - c; float y = Vector3.Dot(d, up); var flat = d - up * y;
                float a = Mathf.Atan2(Vector3.Dot(flat, right), Vector3.Dot(flat, fwd)); int k = ((int)Mathf.Floor((a + Mathf.PI) / (2 * Mathf.PI) * N) + N) % N;
                rMin[k] = Mathf.Min(rMin[k], flat.magnitude); y0[k] = Mathf.Min(y0[k], y); y1[k] = Mathf.Max(y1[k], y);
            }
            var verts = new System.Collections.Generic.List<Vector3>(); var tris = new System.Collections.Generic.List<int>();
            for (int k = 0; k < N; k++)
            {
                int k2 = (k + 1) % N; if (rMin[k] == float.MaxValue || rMin[k2] == float.MaxValue) continue;
                Vector3 P(int j, float y) { float a = (j + .5f) / N * 2 * Mathf.PI - Mathf.PI; return c + (fwd * Mathf.Cos(a) + right * Mathf.Sin(a)) * (rMin[j] * .985f) + up * y; }
                int b = verts.Count;
                verts.Add(P(k, y0[k])); verts.Add(P(k2, y0[k2])); verts.Add(P(k2, y1[k2])); verts.Add(P(k, y1[k]));
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            // close the top: a hair-coloured cap at the band's top edge, under the crown hair (seen only where the
            // crown dips below the band, i.e. exactly where the sky used to show through the band opening)
            { float yTop = 0; int nTop = 0; for (int k = 0; k < N; k++) if (y1[k] > float.MinValue) { yTop += y1[k]; nTop++; }
              if (nTop > 0) { yTop /= nTop; int ci = verts.Count; verts.Add(c + up * yTop);
                for (int k = 0; k < N; k++) { int k2 = (k + 1) % N; if (rMin[k] == float.MaxValue || rMin[k2] == float.MaxValue) continue;
                    float a1 = (k + .5f) / N * 2 * Mathf.PI - Mathf.PI, a2 = (k2 + .5f) / N * 2 * Mathf.PI - Mathf.PI; int b = verts.Count;
                    verts.Add(c + (fwd * Mathf.Cos(a1) + right * Mathf.Sin(a1)) * (rMin[k] * .985f) + up * y1[k]);
                    verts.Add(c + (fwd * Mathf.Cos(a2) + right * Mathf.Sin(a2)) * (rMin[k2] * .985f) + up * y1[k2]);
                    tris.AddRange(new[] { ci, b, b + 1 }); } } }
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Hat liner" }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Material hairMat = null;
            // the plain swept-hair material (HeroKit tints it the hair colour); the first "Hair*" renderer may now be
            // Hair_Bald (skin) or a detail-mapped buzz / waves, which turned the liner yellow
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "Hair_Default" && r.sharedMaterial) { hairMat = r.sharedMaterial; break; }
            if (!hairMat) foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name.StartsWith("Hair") && r.sharedMaterial) { hairMat = r.sharedMaterial; break; }
            // the hair shell's own material is one-sided now (Score80 D); the liner is a thin tube seen from either side
            if (hairMat && hairMat.HasProperty("_Cull") && hairMat.GetFloat("_Cull") != 0) { hairMat = new Material(hairMat) { name = hairMat.name + " (liner)" }; hairMat.SetFloat("_Cull", 0); }
            var go = new GameObject("HatLiner"); go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = hairMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetTrim(bool on)
        {
            foreach (var s in new[] { ModularHeroLook.Slot.Shirt, ModularHeroLook.Slot.Shorts })
            {
                var slot = FindSlot(s); if (!slot) continue;
                foreach (var r in slot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == (on ? clothDefault : clothTrim)) mats[i] = on ? clothTrim : clothDefault;
                        else if (mats[i] == (on ? pipingDefault : pipingTrim)) mats[i] = on ? pipingTrim : pipingDefault;
                    }
                    r.sharedMaterials = mats;
                }
            }
            Trim = on; Changed?.Invoke();
        }

        public void SetTrophy(bool on) { Trophy = on; EnsureTrophy(); Refresh(); Changed?.Invoke(); }
        /// Driven by the animation layer: the prop only shows while celebrating.
        public void SetCelebrating(bool on) { if (celebrating == on) return; celebrating = on; Refresh(); }
        void Refresh() { if (trophy) trophy.gameObject.SetActive(Trophy && celebrating); }

        public void ResetToDefault() { EquipHat(Hat.Visor); SetTrim(false); SetTrophy(false); }

        Transform FindSlot(ModularHeroLook.Slot s)
        {
            if (!look || !look.skeletonRoot) return null;
            foreach (Transform c in look.skeletonRoot) if (c.name == "Slot_" + s && c.gameObject.activeSelf) return c;
            return null;
        }

        void EnsureTrophy()
        {
            if (trophy || !trophyMesh) return;
            var bones = look.skeletonRoot.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) { foreach (var b in bones) if (b.name == n) return b; return null; }
            Transform hand = Find("Hand.L"), socket = Find("Hand_L") ?? hand;
            Transform index = Find("Index1.L"), middle = Find("Middle1.L"), middle2 = Find("Middle2.L"), pinky = Find("Pinky1.L"), thumb = Find("Thumb1.L");
            if (!hand || !index || !middle || !middle2 || !pinky || !thumb) return;
            // Same geometric hand frame as the finger grip, measured at bind (finger bones are never animated by clips).
            Vector3 L(Transform t) => hand.InverseTransformPoint(t.position);
            var f = (L(middle2) - L(middle)).normalized; var w = Vector3.ProjectOnPlane(L(pinky) - L(index), f).normalized;
            var n = Vector3.Cross(w, f).normalized; if (Vector3.Dot(n, L(thumb) - L(index)) < 0) n = -n;
            Vector3 Fr(Vector3 c) => hand.TransformDirection(w * c.x + f * c.y + n * c.z);
            var go = new GameObject("Cosmetic_Trophy (Hand_L)");
            trophy = go.transform; trophy.SetParent(socket, false);
            trophy.position = hand.position + Fr(trophyOrigin);
            trophy.rotation = Quaternion.LookRotation(Fr(trophyNormal), Fr(trophyAxis));
            var u = socket.lossyScale; trophy.localScale = new Vector3(1 / Mathf.Abs(u.x), 1 / Mathf.Abs(u.y), 1 / Mathf.Abs(u.z));
            go.AddComponent<MeshFilter>().sharedMesh = trophyMesh; go.AddComponent<MeshRenderer>().sharedMaterial = trophyMaterial;
            go.SetActive(false);
        }

        void Update()
        {
            if (!debugHotkeys) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) EquipHat(Hat.Cap);
            if (Input.GetKeyDown(KeyCode.Alpha2)) EquipHat(Hat.Sweatband);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTrim(!Trim);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetTrophy(!Trophy);
            if (Input.GetKeyDown(KeyCode.Alpha0)) ResetToDefault();
        }

        void OnGUI()
        {
            if (!debugPanel) return;
            GUILayout.BeginArea(new Rect(10, Screen.height - 190, 230, 180), GUI.skin.box);
            GUILayout.Label("Hero cosmetics (identity only)");
            if (GUILayout.Button("1  Hat: Cap")) EquipHat(Hat.Cap);
            if (GUILayout.Button("2  Hat: Sweatband")) EquipHat(Hat.Sweatband);
            if (GUILayout.Button("3  Trim: " + (Trim ? "Teal/Yellow" : "Navy/Orange"))) SetTrim(!Trim);
            if (GUILayout.Button("4  Celebration trophy: " + (Trophy ? "on" : "off"))) SetTrophy(!Trophy);
            if (GUILayout.Button("0  Default V4 look")) ResetToDefault();
            GUILayout.EndArea();
        }
    }
}
