using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Game-feel effects. Every system is built once and re-emitted; nothing is instantiated
    /// per event. Sprites are generated in Higgsfield (impact, ring, sparkle, dust) and live
    /// in Resources/Tennis/Fx; procedural fallbacks keep everything working without them.
    ///
    ///   contact   impact flash stretched along the shot, a shockwave ring, felt fuzz,
    ///             sparkles on clean hits, a big double ring and star shower on a supercharge
    ///   bounce    dust puff, a ripple ring flat on the court, a fading mark
    ///   movement  dust from planted feet at speed, a skid on a dive
    ///   moments   confetti for winners and the match
    ///   timing    a ring that closes on the ball as it reaches the hitting zone
    public sealed class TennisFx : MonoBehaviour
    {
        ParticleSystem impact, shock, sparkle, fuzz, dust, ripple, confetti, charge, footDust;
        Material additiveImpact, additiveRing, additiveSparkle, additiveGlow, alphaDust, alphaFuzz, alphaConfetti;
        Transform[] skids;
        Material[] skidMaterials;
        float[] skidAge;
        int nextSkid;
        const int SkidCount = 6;
        const float SkidLife = 2.2f;
        Transform cue; Material cueMaterial;
        /// Camera punch requested by the last strong contact; the game camera reads and decays it.
        public float Shake { get; private set; }
        /// Full-screen flash strength for supercharges; the HUD reads and decays it.
        public float Flash { get; private set; }
        static readonly Color Felt = new(.86f, 1f, .28f);

        static Texture2D Sprite(string name, Texture2D fallback)
        {
            var t = Resources.Load<Texture2D>("Tennis/Fx/" + name);
            return t ? t : fallback;
        }

        static Material Make(string shader, Texture texture, float intensity = 1.6f)
        {
            var s = Resources.Load<Shader>("Tennis/Shaders/" + shader) ?? Shader.Find("Sprites/Default");
            var m = new Material(s) { mainTexture = texture };
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            return m;
        }

        public void Build()
        {
            var soft = TennisLook.Falloff;
            var square = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Confetti" };
            var white = new Color32[16]; for (int i = 0; i < 16; i++) white[i] = new Color32(255, 255, 255, 255);
            square.SetPixels32(white); square.Apply();
            additiveImpact = Make("TennisFxAdditive", Sprite("impact", soft), 2.2f);
            additiveRing = Make("TennisFxAdditive", Sprite("ring", soft), 1.8f);
            additiveSparkle = Make("TennisFxAdditive", Sprite("sparkle", soft), 2.4f);
            additiveGlow = Make("TennisFxAdditive", soft, 1.8f);
            alphaDust = Make("TennisFxAlpha", Sprite("dust", soft));
            alphaFuzz = Make("TennisFxAlpha", soft);
            alphaConfetti = Make("TennisFxAlpha", square);

            impact = System("Impact flash", additiveImpact, 4, .09f, .13f, .9f, 1.3f, 0, 0, 0, grow: 1.5f);
            shock = System("Shockwave", additiveRing, 6, .22f, .3f, .35f, .35f, 0, 0, 0, grow: 7f);
            sparkle = System("Sparkles", additiveSparkle, 60, .25f, .5f, .08f, .2f, 2f, 5.5f, .6f, grow: .4f, spin: true);
            fuzz = System("Felt fuzz", alphaFuzz, 50, .22f, .5f, .03f, .06f, 2.8f, 6.5f, 3.5f);
            dust = System("Bounce dust", alphaDust, 40, .35f, .7f, .12f, .28f, .4f, 1.4f, -.3f, grow: 1.8f, spin: true);
            footDust = System("Footstep dust", alphaDust, 60, .3f, .55f, .1f, .2f, .3f, .9f, -.2f, grow: 1.9f, spin: true);
            ripple = System("Bounce ripple", additiveRing, 8, .3f, .4f, .2f, .2f, 0, 0, 0, grow: 5f);
            var rippleRenderer = ripple.GetComponent<ParticleSystemRenderer>(); rippleRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            confetti = System("Confetti", alphaConfetti, 240, 1.6f, 2.6f, .05f, .09f, 3f, 7f, .9f, spin: true);
            var confettiMain = confetti.main; confettiMain.startRotation3D = true;
            var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8, 8); rot.y = new ParticleSystem.MinMaxCurve(-8, 8); rot.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var drag = confetti.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.6f;
            charge = System("Supercharge stream", additiveGlow, 160, .18f, .32f, .1f, .2f, .1f, .5f, 0);
            var chargeEmission = charge.emission; chargeEmission.rateOverDistance = 22;

            skids = new Transform[SkidCount]; skidMaterials = new Material[SkidCount]; skidAge = new float[SkidCount];
            for (int i = 0; i < SkidCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Bounce mark"; Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.sharedMaterial = skidMaterials[i] = Make("TennisFxAlpha", soft);
                skidMaterials[i].color = new Color(1, 1, 1, 0);
                quad.SetActive(false);
                skids[i] = quad.transform; skidAge[i] = SkidLife;
            }

            var cueQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cueQuad.name = "Timing cue"; Destroy(cueQuad.GetComponent<Collider>());
            cueQuad.transform.SetParent(transform, false);
            var cr = cueQuad.GetComponent<MeshRenderer>();
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cr.receiveShadows = false;
            cr.sharedMaterial = cueMaterial = Make("TennisFxAdditive", Sprite("ring", soft), 1.4f);
            cue = cueQuad.transform; cueQuad.SetActive(false);
        }

        ParticleSystem System(string label, Material material, int max, float lifeMin, float lifeMax, float sizeMin, float sizeMax,
            float speedMin, float speedMax, float gravity, float grow = 1.4f, bool spin = false)
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
            if (spin) main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .05f;
            var colour = ps.colorOverLifetime; colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .35f), new GradientAlphaKey(0, 1) });
            colour.color = fade;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .55f, 1, grow));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingFudge = -5;
            return ps;
        }

        static void Burst(ParticleSystem ps, Vector3 at, int count, Color color, float size = -1, Vector3 velocity = default)
        {
            var p = new ParticleSystem.EmitParams { position = at, startColor = color, applyShapeToPosition = true };
            if (size > 0) p.startSize = size;
            if (velocity != default) p.velocity = velocity;
            ps.Emit(p, count);
        }

        /// A struck ball. How big and bright it gets says how clean the contact was.
        public void Contact(Vector3 at, Timing grade, bool supercharged)
        {
            int strength = (int)grade;
            float scale = supercharged ? 1.8f : Mathf.Lerp(.6f, 1.25f, strength / 5f);
            Color hot = supercharged ? new Color(.55f, .95f, 1f, 1) : Color.Lerp(new Color(1, .95f, .8f, .8f), new Color(1, .92f, .45f, 1), strength / 5f);
            Burst(impact, at, 1, hot, scale);
            Burst(shock, at, supercharged ? 2 : 1, new Color(hot.r, hot.g, hot.b, .8f), .3f * scale);
            Burst(fuzz, at, 6 + strength * 3, Felt);
            if (grade >= Timing.Great || supercharged) Burst(sparkle, at, supercharged ? 22 : strength * 2, hot);
            Shake = Mathf.Max(Shake, supercharged ? 1 : grade >= Timing.Perfect ? .7f : grade >= Timing.Excellent ? .45f : grade >= Timing.Great ? .25f : 0);
            if (supercharged) Flash = 1;
        }

        /// The opponent's racket on the ball: the same language, a size smaller.
        public void OpponentContact(Vector3 at)
        {
            Burst(impact, at, 1, new Color(1, .95f, .85f, .7f), .7f);
            Burst(shock, at, 1, new Color(1, 1, 1, .5f), .25f);
            Burst(fuzz, at, 6, Felt);
        }

        /// The ball meets the court: a puff of dust, a ripple and a mark that fades.
        public void Bounce(Vector3 at, Vector3 velocity, Color court)
        {
            float pace = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 30f);
            Burst(dust, at + Vector3.up * .05f, 3 + (int)(pace * 6), new Color(1, .97f, .92f, .55f));
            Burst(ripple, at + Vector3.up * .02f, 1, new Color(1, 1, 1, .35f + pace * .3f), .18f);
            var skid = skids[nextSkid]; skidAge[nextSkid] = 0;
            skid.gameObject.SetActive(true);
            skid.position = new Vector3(at.x, .014f, at.z);
            Vector3 along = new Vector3(velocity.x, 0, velocity.z);
            skid.rotation = Quaternion.LookRotation(Vector3.down, along.sqrMagnitude > .01f ? along.normalized : Vector3.forward);
            skid.localScale = new Vector3(.13f, .13f + pace * .32f, 1);
            nextSkid = (nextSkid + 1) % SkidCount;
        }

        /// A foot planted at speed kicks up a little dust; a dive slides a big cloud.
        public void Footstep(Vector3 at, float speed)
        {
            if (speed < 3f) return;
            Burst(footDust, at + Vector3.up * .04f, speed > 6 ? 3 : 2, new Color(.95f, .93f, .88f, Mathf.Lerp(.25f, .55f, (speed - 3) / 5)));
        }

        public void Slide(Vector3 at) => Burst(dust, at + Vector3.up * .05f, 14, new Color(.95f, .93f, .88f, .7f));

        /// Confetti over a winner or the match.
        public void Celebrate(Vector3 at, float amount)
        {
            int n = (int)Mathf.Lerp(40, 200, Mathf.Clamp01(amount));
            for (int i = 0; i < n; i++)
            {
                Color c = Color.HSVToRGB(Random.value, .75f, 1f);
                Vector3 v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 8f), Random.Range(-2.5f, 2.5f));
                Burst(confetti, at + Vector3.up * 1.6f, 1, c, -1, v);
            }
        }

        /// While a supercharged ball flies it leaves a glowing stream.
        public void Stream(Vector3 ball, bool on)
        {
            charge.transform.position = ball;
            var e = charge.emission; e.enabled = on;
            if (on && !charge.isPlaying) charge.Play();
        }

        /// The timing cue: a ring around the ball that closes as it reaches the hitting zone.
        /// `closing` is 1 far out and 0 at the ideal moment to be striking it.
        public void Cue(bool show, Vector3 ball, Camera camera, float closing)
        {
            if (cue.gameObject.activeSelf != show) cue.gameObject.SetActive(show);
            if (!show || !camera) return;
            cue.position = ball;
            cue.rotation = Quaternion.LookRotation(cue.position - camera.transform.position, camera.transform.up);
            float size = Mathf.Lerp(.28f, 1.5f, Mathf.Clamp01(closing));
            cue.localScale = Vector3.one * size;
            bool now = closing < .12f;
            cueMaterial.color = now ? new Color(.4f, 1f, .55f, 1) : new Color(1, 1, 1, Mathf.Lerp(.9f, .25f, closing));
        }

        /// Remove every live particle and mark; used after the shader warm-up.
        public void Clear()
        {
            foreach (var ps in new[] { impact, shock, sparkle, fuzz, dust, ripple, confetti, charge, footDust }) ps.Clear(true);
            for (int i = 0; i < SkidCount; i++) { skidAge[i] = SkidLife; skids[i].gameObject.SetActive(false); }
            cue.gameObject.SetActive(false);
            Shake = 0; Flash = 0;
        }

        void Update()
        {
            Shake = Mathf.MoveTowards(Shake, 0, Time.deltaTime * 3.5f);
            Flash = Mathf.MoveTowards(Flash, 0, Time.deltaTime * 5f);
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
            foreach (var m in new[] { additiveImpact, additiveRing, additiveSparkle, additiveGlow, alphaDust, alphaFuzz, alphaConfetti, cueMaterial })
                if (m) Destroy(m);
            if (skidMaterials != null) foreach (var m in skidMaterials) if (m) Destroy(m);
        }
    }
}
