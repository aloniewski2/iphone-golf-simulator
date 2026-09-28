using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Plan 3A: a living face on the locked Hero V4 without touching its meshes. The painted face has
    /// no face bones or blendshapes, so:
    ///   * the two eye spheres are re-pivoted (baked at bind, parented to the head at their centres) so
    ///     they can AIM — at the ball in play, at the camera in reaction shots;
    ///   * each eye gets a skin-coloured upper LID (sphere cap + dark lash line) that rotates over the eye:
    ///     blinks, effort squint on swings / the ultimate charge, happy squint on a won point, droop on a
    ///     lost one, wide on a whiff; the lid also rolls so inner corners drop (grit) or outer ones (sad);
    ///   * the head tracks its target with lag, so the face reads before the body turns.
    /// Everything is cosmetic; bodies, racket, contact and cameras are untouched.
    [DefaultExecutionOrder(1100)]   // after HeroTennisDriver has posed the body
    public sealed class HeroFace : MonoBehaviour
    {
        public enum Mood { Neutral, Effort, Happy, Sad, Shock }
        public Mood mood;
        public Transform lookTarget; public Vector3 lookPoint; public float lookWeight = 1; public float headWeight = .5f;
        public float Close { get; private set; }      // 0 open .. 1 shut (upper lid)

        Transform head, neck; readonly Transform[] eyes = new Transform[2], lids = new Transform[2];
        readonly Quaternion[] eyeRest = new Quaternion[2], lidRest = new Quaternion[2];
        Vector3 fwdLocal, upLocal, rightLocal; float radius;
        float nextBlink = 2, blinkT = -1, close, roll; Quaternion headLag = Quaternion.identity; bool built;
        const float OpenAngle = 108, ClosedAngle = -4;   // lid rotation about the eye's right axis (deg)

        public static HeroFace Build(ModularHeroLook look, Color skin)
        {
            var anim = look.animator; var headBone = anim.GetBoneTransform(HumanBodyBones.Head); if (!headBone) return null;
            var f = look.gameObject.AddComponent<HeroFace>(); f.head = headBone; f.neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            // head-space axes at bind (the hero faces +z)
            f.fwdLocal = headBone.InverseTransformDirection(look.transform.forward).normalized;
            f.upLocal = headBone.InverseTransformDirection(look.transform.up).normalized;
            f.rightLocal = headBone.InverseTransformDirection(look.transform.right).normalized;
            var skinMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hero lid skin" };
            skinMat.SetColor("_BaseColor", skin); skinMat.SetFloat("_Smoothness", .25f);
            var lashMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hero lid lash" };
            lashMat.SetColor("_BaseColor", new Color(.16f, .09f, .06f)); lashMat.SetFloat("_Smoothness", .1f);
            int n = 0;
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.name.StartsWith("Body_EyeSphere") || n >= 2) continue;
                bool left = r.name.EndsWith("_L");
                var baked = new Mesh(); r.BakeMesh(baked, false);   // skinned output ignores the renderer's scale
                var v = baked.vertices; var world = new Vector3[v.Length]; Vector3 c = Vector3.zero;
                for (int i = 0; i < v.Length; i++) { world[i] = r.transform.position + r.transform.rotation * v[i]; c += world[i]; }
                // the eye is a flattened lens dome: measure it on the head axes
                var hr = headBone.rotation; Vector3 ax = hr * f.rightLocal, ay = hr * f.upLocal, az = hr * f.fwdLocal;
                Vector3 mn = Vector3.one * 1e9f, mxv = -Vector3.one * 1e9f;
                foreach (var w in world) { var q = new Vector3(Vector3.Dot(w, ax), Vector3.Dot(w, ay), Vector3.Dot(w, az)); mn = Vector3.Min(mn, q); mxv = Vector3.Max(mxv, q); }
                var mid = (mn + mxv) * .5f; var half = (mxv - mn) * .5f;
                c = ax * mid.x + ay * mid.y + az * mid.z;
                float rad = Mathf.Max(half.x, half.y);
                var ext = new Vector3(half.x * 1.1f, half.y * 1.1f, half.z * 1.35f + .004f);
                var pivot = c - az * (rad * 1.8f);   // rotate the lens about a point behind it, like an eyeball
                // pivoted eye: world-scale child of the head at the sphere centre, same orientation as the head
                var socket = new GameObject("Eye socket " + (left ? "L" : "R")).transform;
                socket.SetParent(headBone, false); socket.position = pivot; socket.rotation = headBone.rotation;
                socket.localScale = new Vector3(1 / headBone.lossyScale.x, 1 / headBone.lossyScale.y, 1 / headBone.lossyScale.z);
                var eye = new GameObject("Eye " + (left ? "L" : "R")).transform; eye.SetParent(socket, false);
                var ev = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) ev[i] = Quaternion.Inverse(eye.rotation) * (world[i] - pivot);
                baked.vertices = ev;
                var nr = baked.normals; var toEye = Quaternion.Inverse(eye.rotation) * r.transform.rotation;
                for (int i = 0; i < nr.Length; i++) nr[i] = toEye * nr[i];
                baked.normals = nr;
                if (baked.tangents.Length == nr.Length) { var tg = baked.tangents; for (int i = 0; i < tg.Length; i++) { var t3 = toEye * new Vector3(tg[i].x, tg[i].y, tg[i].z); tg[i] = new Vector4(t3.x, t3.y, t3.z, tg[i].w); } baked.tangents = tg; }
                baked.RecalculateBounds();
                eye.gameObject.AddComponent<MeshFilter>().sharedMesh = baked;
                var mr = eye.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = r.sharedMaterials; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
                // lid: sphere cap (polar 0..105 deg around head-up) slightly larger than the eye, lash band at the edge
                var lid = new GameObject("Lid " + (left ? "L" : "R")).transform;
                lid.SetParent(socket, false); lid.position = c;
                lid.gameObject.AddComponent<MeshFilter>().sharedMesh = Cap(ext, f.rightLocal, f.upLocal, f.fwdLocal);
                var lr = lid.gameObject.AddComponent<MeshRenderer>(); lr.sharedMaterials = new[] { skinMat, lashMat };
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                int k = left ? 0 : 1; f.eyes[k] = eye; f.lids[k] = lid; f.eyeRest[k] = eye.localRotation; f.lidRest[k] = lid.localRotation; f.radius = rad; n++;
            }
            f.built = n == 2; if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1") Debug.Log($"[HeroFace] eyes={n} radius={f.radius:F4}"); return f;
        }

        /// Upper-lid cap in eye space: a spherical cap around `up`, 0..105 deg from the pole, open toward `fwd`
        /// at the rim. Submesh 1 is the last ring band (the lash line).
        static Mesh Cap(Vector3 ext, Vector3 right, Vector3 up, Vector3 fwd)
        {
            const int rings = 10, seg = 28; float r = 1;
            var vs = new System.Collections.Generic.List<Vector3>(); var skin = new System.Collections.Generic.List<int>(); var lash = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float polar = Mathf.Deg2Rad * (i == rings ? 108 : 105f * i / (rings - 1));
                float rr = i == rings ? r * 1.012f : r;
                for (int j = 0; j < seg; j++)
                {
                    float az = 2 * Mathf.PI * j / seg;
                    float cy = Mathf.Cos(polar) * rr, cf = Mathf.Cos(az) * Mathf.Sin(polar) * rr, cr = Mathf.Sin(az) * Mathf.Sin(polar) * rr;
                    vs.Add(up * (cy * ext.y) + fwd * (cf * ext.z) + right * (cr * ext.x));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * seg + j, b = i * seg + (j + 1) % seg, c = (i + 1) * seg + j, d = (i + 1) * seg + (j + 1) % seg;
                    var list = i >= rings - 1 ? lash : skin;
                    list.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "Hero lid" }; m.SetVertices(vs); m.subMeshCount = 2; m.SetTriangles(skin, 0); m.SetTriangles(lash, 1);
            m.RecalculateNormals();
            // outward-facing (the lid must be visible from outside the eye)
            var nn = m.normals; if (Vector3.Dot(nn[seg * 3], vs[seg * 3]) < 0)
            { skin.Reverse(); lash.Reverse(); m.SetTriangles(skin, 0); m.SetTriangles(lash, 1); m.RecalculateNormals(); }
            m.RecalculateBounds(); return m;
        }

        /// Least-squares sphere through the eye's vertices (the eye mesh is only the visible part of a
        /// sphere, so its vertex average is not the centre). Falls back to the average.
        static void FitSphere(Vector3[] p, ref Vector3 c, out float r)
        {
            // |x|^2 = 2 c.x + k  ->  linear least squares in (cx, cy, cz, k)
            var A = new double[4, 4]; var b = new double[4];
            foreach (var q in p)
            {
                double[] row = { 2 * q.x, 2 * q.y, 2 * q.z, 1 }; double y = q.sqrMagnitude;
                for (int i = 0; i < 4; i++) { b[i] += row[i] * y; for (int j = 0; j < 4; j++) A[i, j] += row[i] * row[j]; }
            }
            var x = Solve4(A, b);
            if (x != null)
            {
                var cc = new Vector3((float)x[0], (float)x[1], (float)x[2]); double rr = x[3] + cc.sqrMagnitude;
                if (rr > 0 && Vector3.Distance(cc, c) < .2f) { c = cc; r = (float)System.Math.Sqrt(rr); return; }
            }
            r = 0; foreach (var q in p) r = Mathf.Max(r, Vector3.Distance(q, c));
        }
        static double[] Solve4(double[,] A, double[] b)
        {
            int n = 4; var M = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) M[i, j] = A[i, j]; M[i, n] = b[i]; }
            for (int col = 0; col < n; col++)
            {
                int piv = col; for (int r = col + 1; r < n; r++) if (System.Math.Abs(M[r, col]) > System.Math.Abs(M[piv, col])) piv = r;
                if (System.Math.Abs(M[piv, col]) < 1e-12) return null;
                for (int j = 0; j <= n; j++) { var t = M[col, j]; M[col, j] = M[piv, j]; M[piv, j] = t; }
                for (int r = 0; r < n; r++) { if (r == col) continue; double f = M[r, col] / M[col, col]; for (int j = col; j <= n; j++) M[r, j] -= f * M[col, j]; }
            }
            var x = new double[n]; for (int i = 0; i < n; i++) x[i] = M[i, n] / M[i, i]; return x;
        }

        readonly Renderer[] faceRenderers = new Renderer[4];
        /// Eyes are never visible from behind: hide them (and the lids) when the camera is behind the face
        /// plane, so they can't show through the hair / hat-band gap at any head angle.
        void OnEnable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += PerCamera; }
        void OnDisable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= PerCamera; }
        void PerCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (!built || !head) return;
            if (faceRenderers[0] == null) for (int k = 0; k < 2; k++) { faceRenderers[k] = eyes[k].GetComponent<Renderer>(); faceRenderers[k + 2] = lids[k].GetComponent<Renderer>(); }
            var toCam = (cam.transform.position - head.position).normalized;
            bool front = Vector3.Dot(toCam, head.TransformDirection(fwdLocal)) > -.05f;
            foreach (var r in faceRenderers) if (r) r.enabled = front;
        }
        void OnBackCheck() { }

        void LateUpdate()
        {
            if (!built) return;
            OnBackCheck();
            float dt = Time.timeScale > 0 ? Time.deltaTime : (Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime);
            // ---- head lag toward the target (applied on top of the clip)
            Vector3 target = lookTarget ? lookTarget.position : lookPoint;
            bool hasTarget = lookTarget || lookPoint != Vector3.zero;
            if (hasTarget && neck && headWeight > 0)
            {
                var want = Quaternion.FromToRotation(head.TransformDirection(fwdLocal), (target - head.position).normalized);
                want = Quaternion.Slerp(Quaternion.identity, want, headWeight);
                want.ToAngleAxis(out float ang, out var ax); if (ang > 180) ang -= 360;
                want = Quaternion.AngleAxis(Mathf.Clamp(ang, -40, 40), ax);
                headLag = Quaternion.Slerp(headLag, want, 1 - Mathf.Exp(-dt * 7));
                neck.rotation = Quaternion.Slerp(Quaternion.identity, headLag, .45f) * neck.rotation;
                head.rotation = Quaternion.Slerp(Quaternion.identity, headLag, .55f) * head.rotation;
            }
            // ---- eyes aim (after the head moved)
            for (int k = 0; k < 2; k++)
            {
                var e = eyes[k]; e.localRotation = eyeRest[k];
                if (!hasTarget || lookWeight <= 0) continue;
                var fwd = e.TransformDirection(fwdLocal); var to = (target - e.position).normalized;
                var q = Quaternion.FromToRotation(fwd, to); q.ToAngleAxis(out float a2, out var ax2); if (a2 > 180) a2 -= 360;
                e.rotation = Quaternion.AngleAxis(Mathf.Clamp(a2, -7, 7) * lookWeight, ax2) * e.rotation;
            }
            // ---- lids: mood + blink
            float baseClose = mood == Mood.Effort ? .64f : mood == Mood.Happy ? .6f : mood == Mood.Sad ? .45f : mood == Mood.Shock ? -.1f : 0f;
            float baseRoll = mood == Mood.Effort ? 14 : mood == Mood.Sad ? -16 : 0;
            nextBlink -= dt;
            if (nextBlink <= 0 && blinkT < 0) { blinkT = 0; nextBlink = Random.Range(2.2f, 4.8f); }
            float blink = 0;
            if (blinkT >= 0) { blinkT += dt; blink = Mathf.Sin(Mathf.Clamp01(blinkT / .16f) * Mathf.PI); if (blinkT > .16f) blinkT = -1; }
            close = Mathf.MoveTowards(close, baseClose, dt * 4); roll = Mathf.MoveTowards(roll, baseRoll, dt * 90);
            Close = Mathf.Max(close, blink);
            for (int k = 0; k < 2; k++)
            {
                float ang = Mathf.Lerp(OpenAngle, ClosedAngle, Mathf.Clamp01(Close)) + (Close < 0 ? -Close * 20 : 0);
                float side = k == 0 ? 1 : -1;   // left eye inner corner is toward -right
                lids[k].localRotation = lidRest[k] * Quaternion.AngleAxis(-ang, rightLocal) * Quaternion.AngleAxis(roll * side, fwdLocal);
            }
        }
    }
}
