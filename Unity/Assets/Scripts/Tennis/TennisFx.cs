using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Hit, bounce and supercharge effects. Nothing used to happen on a hit or a bounce, and
    /// a supercharge was only text; these are the small, fast cues that make contact feel
    /// solid. Every system is built once and re-emitted, never instantiated per event.
    public sealed class TennisFx : MonoBehaviour
    {
        ParticleSystem fuzz, flash, dust, charge;
        Material additive, alpha;
        Transform[] skids;
        Material[] skidMaterials;
        float[] skidAge;
        int nextSkid;
        const int SkidCount = 6;
        const float SkidLife = 2.2f;
        /// Camera punch requested by the last strong contact; the game camera reads and
        /// decays it.
        public float Shake { get; private set; }
        static readonly Color Felt = new(.86f, 1f, .28f);

        public void Build()
        {
            var sprite = Shader.Find("Sprites/Default");
            alpha = new Material(sprite) { name = "Tennis fx (alpha)", mainTexture = TennisLook.Falloff };
            additive = new Material(sprite) { name = "Tennis fx (glow)", mainTexture = TennisLook.Falloff };
            fuzz = System("Contact fuzz", alpha, 40, .22f, .5f, .035f, .06f, 2.8f, 6.5f, 3.5f);
            flash = System("Contact flash", additive, 6, .10f, .14f, .25f, .55f, 0, 0, 0);
            dust = System("Bounce dust", alpha, 30, .35f, .7f, .06f, .16f, .5f, 1.6f, -.4f);
            charge = System("Supercharge stream", additive, 120, .18f, .32f, .06f, .12f, .1f, .5f, 0);
            var chargeMain = charge.main; chargeMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var chargeEmission = charge.emission; chargeEmission.rateOverDistance = 18;
            skids = new Transform[SkidCount]; skidMaterials = new Material[SkidCount]; skidAge = new float[SkidCount];
            for (int i = 0; i < SkidCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Bounce mark"; Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.sharedMaterial = skidMaterials[i] = new Material(sprite) { mainTexture = TennisLook.Falloff, color = new Color(1, 1, 1, 0) };
                quad.SetActive(false);
                skids[i] = quad.transform; skidAge[i] = SkidLife;
            }
        }

        ParticleSystem System(string label, Material material, int max, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float speedMin, float speedMax, float gravity)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .05f;
            var colour = ps.colorOverLifetime; colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            colour.color = fade;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .6f, 1, 1.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static void Burst(ParticleSystem ps, Vector3 at, int count, Color color)
        {
            var p = new ParticleSystem.EmitParams { position = at, startColor = color, applyShapeToPosition = true };
            ps.Emit(p, count);
        }

        /// A struck ball: felt fuzz off the strings and a flash whose size says how clean it was.
        public void Contact(Vector3 at, Timing grade, bool supercharged)
        {
            int strength = (int)grade;
            Burst(fuzz, at, 6 + strength * 4, Felt);
            Color glow = supercharged ? new Color(.45f, .95f, 1f, .9f) : Color.Lerp(new Color(1, 1, 1, .45f), new Color(1, .95f, .55f, .9f), strength / 5f);
            Burst(flash, at, supercharged ? 3 : 1 + strength / 2, glow);
            Shake = Mathf.Max(Shake, supercharged ? 1 : grade >= Timing.Excellent ? .55f : grade >= Timing.Great ? .3f : 0);
        }

        /// The ball meets the court: a puff of dust and a mark that fades.
        public void Bounce(Vector3 at, Vector3 velocity, Color court)
        {
            float pace = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 30f);
            Burst(dust, at + Vector3.up * .03f, 4 + (int)(pace * 10), new Color(court.r, court.g, court.b, .55f));
            var skid = skids[nextSkid]; skidAge[nextSkid] = 0;
            skid.gameObject.SetActive(true);
            skid.position = new Vector3(at.x, .014f, at.z);
            Vector3 along = new Vector3(velocity.x, 0, velocity.z);
            skid.rotation = Quaternion.LookRotation(Vector3.down, along.sqrMagnitude > .01f ? along.normalized : Vector3.forward);
            skid.localScale = new Vector3(.13f, .13f + pace * .32f, 1);
            nextSkid = (nextSkid + 1) % SkidCount;
        }

        /// While a supercharged ball flies it leaves a glowing stream.
        public void Stream(Vector3 ball, bool on)
        {
            charge.transform.position = ball;
            var e = charge.emission; e.enabled = on;
            if (on && !charge.isPlaying) charge.Play();
        }

        /// Remove every live particle and mark; used after the shader warm-up.
        public void Clear()
        {
            foreach (var ps in new[] { fuzz, flash, dust, charge }) ps.Clear(true);
            for (int i = 0; i < SkidCount; i++) { skidAge[i] = SkidLife; skids[i].gameObject.SetActive(false); }
            Shake = 0;
        }

        void Update()
        {
            Shake = Mathf.MoveTowards(Shake, 0, Time.deltaTime * 3.5f);
            for (int i = 0; i < SkidCount; i++)
            {
                if (skidAge[i] >= SkidLife) continue;
                skidAge[i] += Time.deltaTime;
                float a = .32f * (1 - skidAge[i] / SkidLife);
                skidMaterials[i].color = new Color(.28f, .3f, .22f, a);
                if (skidAge[i] >= SkidLife) skids[i].gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (additive) Destroy(additive); if (alpha) Destroy(alpha);
            if (skidMaterials != null) foreach (var m in skidMaterials) if (m) Destroy(m);
        }
    }
}
