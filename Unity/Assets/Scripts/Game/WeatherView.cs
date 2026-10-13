using GolfArcade.Course;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Game
{
    /// The hole's weather, seen and heard, laid over the hole's own light rather than replacing it:
    /// a deck of cloud fades in over its sky (GolfArcade/StormSky), the sun dims and cools, the
    /// ambient greys, the haze closes in; rain streaks past the camera and splashes on the ground
    /// (snow drifts over Frostbite Fjord, ash and embers in the Magma Open's crater); a storm throws
    /// lightning — a bolt to the ground somewhere ahead, the clouds lit from inside, the thunder a
    /// moment later (an eruption's flash is orange, and rumbles). A change of weather mid-hole eases
    /// in over a few seconds. Everything it changed goes back (Restore) before the next hole is lit.
    public sealed class WeatherView : MonoBehaviour
    {
        EventSounds sounds;
        Camera cam;
        Light sun;
        Course.Sky sky;
        Weather weather;
        Wind wind;

        // what the hole had before the weather, put back by Restore
        bool captured;
        float baseSun, baseShadow; Color baseSunColor;
        AmbientMode baseMode; Color baseAmbient, baseSkyAmbient, baseEquator, baseGround;
        Color baseFog; float baseFogStart, baseFogEnd;

        // now, and where it is heading: cloud cover, how dark, how much falls, how thick the fog
        float cover, dim, precip, coverTo, dimTo, precipTo, mist, mistTo;
        float nextHorn = float.MaxValue;
        // the rainbow the clearing sky leaves: how much of it shows, and toward where
        float rainbow, rainbowTo; Vector3 rainbowToward;
        Transform rainbowArc; Material rainbowMaterial;
        ParticleSystem wisps;
        float flash; Color flashColor = Color.white;
        /// The light is the weather's now (not the hole's own): clear skies leave it alone.
        bool touched;
        float nextStrike = float.MaxValue, boltUntil;
        int pendingThunder; float thunderAt, thunderDistance;

        ParticleSystem rain, snow, ash, embers, splashes;
        Transform dome; Material domeMaterial;
        LineRenderer bolt, branch;

        public Weather Weather => weather;
        public bool Raining => captured && precip > 0.05f;
        /// Lightning strikes so far (for the tests).
        public int Strikes { get; private set; }

        public static WeatherView Create(Transform parent, EventSounds sounds)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeatherView>();
            w.sounds = sounds;
            w.Build();
            return w;
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("Golf/StormSky");
            if (shader)
            {
                domeMaterial = new Material(shader) { name = "Storm sky" };
                var d = HoleView.Primitive(PrimitiveType.Sphere, "Cloud dome", Color.white, transform);
                var r = d.GetComponent<Renderer>();
                r.sharedMaterial = domeMaterial; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                d.layer = BallLook.OverlayLayer;
                dome = d.transform;
                dome.gameObject.SetActive(false);
            }
            rain = Particles("Rain", 2600, ParticleSystemRenderMode.Stretch, Streak());
            var rr = rain.GetComponent<ParticleSystemRenderer>(); rr.lengthScale = 1.2f; rr.velocityScale = 0.05f;
            snow = Particles("Snow", 3200, ParticleSystemRenderMode.Billboard, ShotEffects.Soft());
            var noise = snow.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.35f; noise.scrollSpeed = 0.3f;
            ash = Particles("Ash", 1400, ParticleSystemRenderMode.Billboard, ShotEffects.Soft());
            var ashNoise = ash.noise; ashNoise.enabled = true; ashNoise.strength = 0.6f; ashNoise.frequency = 0.3f;
            embers = Particles("Embers", 200, ParticleSystemRenderMode.Billboard, ShotEffects.Soft());
            var emberNoise = embers.noise; emberNoise.enabled = true; emberNoise.strength = 1.2f; emberNoise.frequency = 0.5f;
            splashes = Particles("Splashes", 300, ParticleSystemRenderMode.Billboard, ShotEffects.Soft());
            wisps = Particles("Fog wisps", 120, ParticleSystemRenderMode.Billboard, ShotEffects.Soft());
            var wispFade = wisps.colorOverLifetime; wispFade.enabled = true;
            var wg = new Gradient(); wg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            wispFade.color = wg;
            var grow = splashes.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1.4f));
            var fade = splashes.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.7f, 0), new GradientAlphaKey(0, 1) });
            fade.color = g;
            bolt = Bolt("Lightning", 1.6f);
            branch = Bolt("Lightning branch", 0.7f);
        }

        ParticleSystem Particles(string name, int max, ParticleSystemRenderMode mode, Texture2D texture)
        {
            var go = new GameObject(name) { layer = BallLook.OverlayLayer };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.gravityModifier = 0;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = new Material(ShotEffects.ParticleMaterial()) { mainTexture = texture, name = name };
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        LineRenderer Bolt(string name, float width)
        {
            var go = new GameObject(name) { layer = BallLook.OverlayLayer };
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 0;
            line.material = HoleView.UnlitMat(new Color(0.93f, 0.92f, 1f));
            line.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(1, width * 0.35f));
            line.numCapVertices = 2; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        static Texture2D streak;
        /// A raindrop's streak: thin, brightest in the middle, fading at both ends.
        static Texture2D Streak()
        {
            if (streak) return streak;
            const int w = 8, h = 64;
            streak = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Rain streak" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = 1 - Mathf.Abs((x + 0.5f) / w - 0.5f) * 2, along = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(across * across * along)));
                }
            streak.SetPixels32(px); streak.Apply();
            return streak;
        }

        // ----- The game's side -----

        /// A new hole: its light as built (captured, so it can be put back) and its weather, at
        /// once (`instant`) or easing in.
        public void Begin(Camera camera, Hole hole, Weather w, Wind holeWind, bool instant = true)
        {
            Restore();
            cam = camera;
            var go = GameObject.Find("Sun"); sun = go ? go.GetComponent<Light>() : RenderSettings.sun;
            if (sun) { baseSun = sun.intensity; baseSunColor = sun.color; baseShadow = sun.shadowStrength; }
            baseMode = RenderSettings.ambientMode; baseAmbient = RenderSettings.ambientLight;
            baseSkyAmbient = RenderSettings.ambientSkyColor; baseEquator = RenderSettings.ambientEquatorColor; baseGround = RenderSettings.ambientGroundColor;
            baseFog = RenderSettings.fogColor; baseFogStart = RenderSettings.fogStartDistance; baseFogEnd = RenderSettings.fogEndDistance;
            captured = true;
            sky = Weathers.SkyOf(hole);
            wind = holeWind;
            cover = dim = precip = mist = rainbow = rainbowTo = 0;
            Change(w);
            if (instant) { cover = coverTo; dim = dimTo; precip = precipTo; mist = mistTo; }
            Apply();
        }

        /// The weather turns: it eases there over a few seconds.
        public void Change(Weather w)
        {
            weather = w;
            if (sounds) sounds.Prepare(w, sky);
            (coverTo, dimTo, precipTo) = w switch
            {
                Weather.Gusty => (0.32f, 0.08f, 0f),
                Weather.Rain => (0.88f, 0.42f, 0.62f),
                Weather.Storm => (1f, 0.66f, 1f),
                Weather.Fog => (0.8f, 0.28f, 0f),
                _ => (0f, 0f, 0f),
            };
            mistTo = w == Weather.Fog ? 1f : 0f;
            // a foghorn now and then from across the water
            nextHorn = w == Weather.Fog && sky == Course.Sky.Rain ? Time.time + Random.Range(3f, 7f) : float.MaxValue;
            if (w != Weather.Clear) rainbowTo = 0;
            if (sky == Course.Sky.Snow) dimTo *= 0.6f;   // snow light is bright
            nextStrike = w == Weather.Storm && sky != Course.Sky.Snow ? Time.time + Random.Range(2.5f, 6f) : float.MaxValue;
        }

        public void SetWind(Wind w) => wind = w;

        /// The sky has cleared after rain: a rainbow fades in over the hole, beyond `toward` (the pin).
        public void Rainbow(CoursePoint toward)
        {
            if (!captured || sky != Course.Sky.Rain) return;
            rainbowToward = HoleView.ToWorld(toward);
            rainbowTo = 1;
            if (!rainbowArc) BuildRainbow();
        }

        /// The rainbow's arc: seven bands across a wide half ring, soft at its edges, in vertex colour.
        void BuildRainbow()
        {
            Color[] bands = { new(0.93f, 0.25f, 0.27f), new(1f, 0.55f, 0.2f), new(1f, 0.86f, 0.3f), new(0.38f, 0.82f, 0.4f), new(0.27f, 0.6f, 0.95f), new(0.42f, 0.35f, 0.85f), new(0.6f, 0.38f, 0.85f) };
            const int around = 64, across = 14;
            var verts = new System.Collections.Generic.List<Vector3>(); var colours = new System.Collections.Generic.List<Color>(); var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = Mathf.Lerp(0.04f, Mathf.PI - 0.04f, i / (float)around);
                for (int j = 0; j <= across; j++)
                {
                    float u = j / (float)across;                 // outside (red) in
                    float r = 1f - 0.1f * u;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0));
                    var c = bands[Mathf.Min(bands.Length - 1, (int)(u * bands.Length))];
                    // the band edges and the arc's feet fade away
                    float edge = Mathf.Sin(u * Mathf.PI), feet = Mathf.Clamp01(Mathf.Sin(a) * 2.2f);
                    c.a = edge * feet; colours.Add(c); uvs.Add(new Vector2(0.5f, 0.5f));
                }
            }
            for (int i = 0; i < around; i++)
                for (int j = 0; j < across; j++)
                {
                    int a0 = i * (across + 1) + j, a1 = a0 + 1, b0 = a0 + across + 1, b1 = b0 + 1;
                    tris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
                }
            var mesh = new Mesh { name = "Rainbow" };
            mesh.SetVertices(verts); mesh.SetColors(colours); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            var go = new GameObject("Rainbow") { layer = BallLook.OverlayLayer };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            rainbowMaterial = new Material(ShotEffects.ParticleMaterial()) { name = "Rainbow", mainTexture = Texture2D.whiteTexture };
            mr.sharedMaterial = rainbowMaterial; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            rainbowArc = go.transform;
            go.SetActive(false);
        }

        /// Everything the weather changed, put back as the hole had it; nothing falling, nothing heard.
        public void Restore()
        {
            if (captured && touched) PutBack();
            captured = false; touched = false;
            cover = dim = precip = coverTo = dimTo = precipTo = flash = mist = mistTo = rainbow = rainbowTo = 0;
            nextStrike = nextHorn = float.MaxValue; pendingThunder = 0;
            foreach (var ps in new[] { rain, snow, ash, embers, splashes, wisps }) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (rainbowArc) rainbowArc.gameObject.SetActive(false);
            if (dome) dome.gameObject.SetActive(false);
            if (bolt) bolt.enabled = false;
            if (branch) branch.enabled = false;
            if (sounds) sounds.SetAmbience(0, 0);
        }

        void PutBack()
        {
            if (sun) { sun.intensity = baseSun; sun.color = baseSunColor; sun.shadowStrength = baseShadow; }
            RenderSettings.ambientMode = baseMode; RenderSettings.ambientLight = baseAmbient;
            RenderSettings.ambientSkyColor = baseSkyAmbient; RenderSettings.ambientEquatorColor = baseEquator; RenderSettings.ambientGroundColor = baseGround;
            RenderSettings.fogColor = baseFog; RenderSettings.fogStartDistance = baseFogStart; RenderSettings.fogEndDistance = baseFogEnd;
        }

        void OnDisable() => Restore();

        void LateUpdate()
        {
            if (!captured) return;
            float dt = Time.deltaTime;
            cover = Mathf.MoveTowards(cover, coverTo, dt / 4f);
            dim = Mathf.MoveTowards(dim, dimTo, dt / 4f);
            precip = Mathf.MoveTowards(precip, precipTo, dt / 3f);
            mist = Mathf.MoveTowards(mist, mistTo, dt / 5f);
            rainbow = Mathf.MoveTowards(rainbow, rainbowTo, dt / 3.5f);
            if (Time.time >= nextStrike) { Strike(); nextStrike = Time.time + Random.Range(5f, 12f); }
            if (Time.time >= nextHorn) { if (sounds) sounds.Foghorn(); nextHorn = Time.time + Random.Range(16f, 26f); }
            PoseRainbow();
            flash = Mathf.MoveTowards(flash, 0, dt * 4.5f);
            if (bolt.enabled && Time.time >= boltUntil) bolt.enabled = branch.enabled = false;
            else if (bolt.enabled) bolt.enabled = branch.enabled = Mathf.Repeat((boltUntil - Time.time) * 22f, 1f) > 0.3f;
            if (pendingThunder > 0 && Time.time >= thunderAt)
            {
                pendingThunder = 0;
                if (sky == Course.Sky.Ash) sounds.Rumble(); else sounds.Thunder(thunderDistance);
                if (thunderDistance < 0.35f) Haptics.Tick();
            }
            Apply();
        }

        /// Lightning now (a storm's own, every few seconds; the tests ask for one).
        public void Strike()
        {
            if (!captured || !cam) return;
            Strikes++;
            if (sky == Course.Sky.Ash)
            {
                // the volcano: an orange flash from below the crater's rim and the ground's rumble
                flash = 1; flashColor = new Color(1f, 0.55f, 0.25f);
                thunderDistance = 0.3f; thunderAt = Time.time + 0.15f; pendingThunder = 1;
                var burst = cam.transform.position + cam.transform.forward * 30f;
                for (int i = 0; i < 40; i++)
                    embers.Emit(new ParticleSystem.EmitParams { position = burst + Random.insideUnitSphere * 25f, velocity = Vector3.up * Random.Range(1f, 4f), startColor = new Color(1f, Random.Range(0.4f, 0.7f), 0.15f, 0.95f), startSize = Random.Range(0.12f, 0.3f), startLifetime = Random.Range(2f, 4f) }, 1);
                return;
            }
            flash = 1; flashColor = new Color(0.86f, 0.88f, 1f);
            // somewhere ahead, off to one side: seen, not in the player's way
            var forward = cam.transform.forward; forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            float side = Random.value < 0.5f ? -1 : 1;
            var dir = Quaternion.Euler(0, side * Random.Range(8f, 30f), 0) * forward;
            float distance = Random.Range(140f, 320f);
            var ground = cam.transform.position + dir * distance;
            ground.y = (float)HoleView.GroundHeight(HoleView.ToCourse(ground));
            var top = ground + Vector3.up * Random.Range(140f, 190f) + Random.insideUnitSphere * 20f;
            Jag(bolt, top, ground, 16, 9f);
            var fork = Vector3.Lerp(top, ground, Random.Range(0.3f, 0.55f));
            Jag(branch, fork, fork + (dir * Random.Range(-1f, 1f) + Random.insideUnitSphere * 0.5f).normalized * 40f + Vector3.down * 45f, 8, 6f);
            bolt.enabled = branch.enabled = true;
            boltUntil = Time.time + 0.32f;
            thunderDistance = Mathf.InverseLerp(140f, 320f, distance);
            thunderAt = Time.time + 0.25f + distance * 0.9144f / 343f; pendingThunder = 1;
        }

        static void Jag(LineRenderer line, Vector3 from, Vector3 to, int points, float wander)
        {
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float u = i / (points - 1f);
                var p = Vector3.Lerp(from, to, u);
                if (i > 0 && i < points - 1) p += new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)) * wander * (1 - Mathf.Abs(u - 0.5f));
                line.SetPosition(i, p);
            }
        }

        static readonly Color RainFog = new(0.56f, 0.61f, 0.67f), StormFog = new(0.4f, 0.44f, 0.51f), SnowFog = new(0.82f, 0.86f, 0.92f), AshFog = new(0.4f, 0.33f, 0.31f);
        static readonly Color SeaMist = new(0.8f, 0.83f, 0.85f), FrostMist = new(0.86f, 0.89f, 0.93f), Smoke = new(0.47f, 0.42f, 0.4f);

        /// The rainbow over the hole beyond the pin, turned to face the camera, standing on the horizon.
        void PoseRainbow()
        {
            if (!rainbowArc) return;
            bool show = rainbow > 0.01f && cam;
            if (rainbowArc.gameObject.activeSelf != show) rainbowArc.gameObject.SetActive(show);
            if (!show) return;
            var from = cam.transform.position;
            var toward = rainbowToward - from; toward.y = 0;
            if (toward.sqrMagnitude < 1f) { toward = cam.transform.forward; toward.y = 0; }
            toward.Normalize();
            float distance = Mathf.Min(cam.farClipPlane * 0.6f, 520f), radius = distance * 0.62f;
            var foot = from + toward * distance; foot.y = from.y - radius * 0.3f;
            rainbowArc.SetPositionAndRotation(foot, Quaternion.LookRotation(toward, Vector3.up));
            rainbowArc.localScale = Vector3.one * radius;
            rainbowMaterial.color = new Color(1, 1, 1, 0.42f * rainbow);
        }

        void Apply()
        {
            bool still = cover <= 0.001f && dim <= 0.001f && precip <= 0.001f && flash <= 0.001f && mist <= 0.001f;
            if (still)
            {
                // clear skies: the hole's own light, untouched (put back once if the weather had it)
                if (touched) { PutBack(); touched = false; }
                if (dome && dome.gameObject.activeSelf) dome.gameObject.SetActive(false);
                Fall();
                if (sounds) sounds.SetAmbience(0, Mathf.Clamp01(((float)wind.SpeedMPH - 6f) / 18f) * (weather.IsGusty() ? 1f : 0.65f));
                return;
            }
            touched = true;
            float storm = Mathf.Clamp01((dim - 0.42f) / 0.24f);
            var fog = sky switch
            {
                Course.Sky.Snow => SnowFog,
                Course.Sky.Ash => AshFog,
                _ => Color.Lerp(RainFog, StormFog, storm),
            };
            // fog: the haze closes right in, pale (smoke in the crater)
            fog = Color.Lerp(fog, sky switch { Course.Sky.Snow => FrostMist, Course.Sky.Ash => Smoke, _ => SeaMist }, mist);
            float flashLight = flash * flash;
            if (sun)
            {
                sun.intensity = baseSun * (1 - 0.72f * dim) + baseSun * 0.9f * flashLight;
                // under a deck of cloud the shadows go soft
                sun.shadowStrength = baseShadow * (1 - 0.7f * Mathf.Max(cover, mist * 0.95f));
                sun.color = Color.Lerp(Color.Lerp(baseSunColor, new Color(0.8f, 0.85f, 0.95f), dim * 0.7f), flashColor, flashLight * 0.6f);
            }
            float shade = 1 - 0.45f * dim + 0.5f * flashLight;
            Color Grey(Color c) { var g = Color.Lerp(c, new Color(c.grayscale * 0.92f, c.grayscale * 0.96f, c.grayscale * 1.06f), dim * 0.6f) * shade; g.a = c.a; return g; }
            RenderSettings.ambientLight = Grey(baseAmbient);
            RenderSettings.ambientSkyColor = Grey(baseSkyAmbient);
            RenderSettings.ambientEquatorColor = Grey(baseEquator);
            RenderSettings.ambientGroundColor = Grey(baseGround);
            var haze = Color.Lerp(baseFog, fog, Mathf.Max(cover, mist));
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Lerp(baseFogStart, baseFogStart * 0.3f, precip), 4f, mist);
            RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(baseFogEnd, Mathf.Max(baseFogEnd * 0.5f, 360f), precip), 175f, mist);

            if (dome && cam)
            {
                bool show = cover > 0.01f || flash > 0.01f || mist > 0.01f;
                if (dome.gameObject.activeSelf != show) dome.gameObject.SetActive(show);
                if (show)
                {
                    dome.position = cam.transform.position;
                    dome.localScale = Vector3.one * cam.farClipPlane * 1.7f;   // (a sphere of radius 0.85 of the far clip)
                    var (dark, light) = sky switch
                    {
                        Course.Sky.Snow => (new Color(0.62f, 0.66f, 0.72f), new Color(0.88f, 0.9f, 0.94f)),
                        Course.Sky.Ash => (new Color(0.22f, 0.18f, 0.18f), new Color(0.46f, 0.38f, 0.35f)),
                        _ => (Color.Lerp(new Color(0.42f, 0.46f, 0.53f), new Color(0.24f, 0.27f, 0.33f), storm), Color.Lerp(new Color(0.7f, 0.73f, 0.78f), new Color(0.48f, 0.52f, 0.58f), storm)),
                    };
                    // in fog the sky is the fog
                    dark = Color.Lerp(dark, haze * 0.94f, mist); light = Color.Lerp(light, haze * 1.04f, mist);
                    domeMaterial.SetColor("_Dark", dark); domeMaterial.SetColor("_Light", light);
                    domeMaterial.SetColor("_Horizon", haze);
                    domeMaterial.SetColor("_FlashColor", flashColor);
                    domeMaterial.SetFloat("_Coverage", Mathf.Max(cover, mist));
                    domeMaterial.SetFloat("_Flash", flash);
                    var drift = new Vector3((float)System.Math.Sin(wind.DirectionDegrees * Mathf.Deg2Rad), 0, (float)System.Math.Cos(wind.DirectionDegrees * Mathf.Deg2Rad)) * (2 + (float)wind.SpeedMPH * 0.6f);
                    domeMaterial.SetVector("_Drift", new Vector4(drift.x, drift.z, 0, 0));
                }
            }
            Fall();
            if (sounds)
            {
                float rainSound = sky == Course.Sky.Rain ? precip : sky == Course.Sky.Ash ? precip * 0.35f : 0;
                float windSound = Mathf.Clamp01(((float)wind.SpeedMPH - 6f) / 18f) * (weather.IsGusty() ? 1f : 0.65f) + (sky == Course.Sky.Snow ? precip * 0.4f : 0);
                sounds.SetAmbience(rainSound, windSound);
            }
        }

        /// What falls, round the camera wherever it goes: the systems ride with it, their drops
        /// left in the world as it moves.
        void Fall()
        {
            if (!cam) return;
            var at = cam.transform.position;
            var drift = new Vector3((float)System.Math.Sin(wind.DirectionDegrees * Mathf.Deg2Rad), 0, (float)System.Math.Cos(wind.DirectionDegrees * Mathf.Deg2Rad)) * (float)wind.SpeedMPH * 0.447f / 0.9144f;
            Emit(rain, sky == Course.Sky.Rain ? precip : 0, at, 1500f, ps =>
            {
                var m = ps.main; m.startLifetime = 1.4f; m.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.034f);
                m.startColor = new Color(0.78f, 0.84f, 0.95f, Mathf.Lerp(0.35f, 0.55f, dim));
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = drift.x * 0.35f; v.y = -24f; v.z = drift.z * 0.35f;
                var s = ps.shape; s.scale = new Vector3(46, 1, 46); s.position = Vector3.zero;
                ps.transform.position = at + Vector3.up * 16f + cam.transform.forward * 10f;
            });
            Emit(snow, sky == Course.Sky.Snow ? precip : 0, at, 1100f, ps =>
            {
                var m = ps.main; m.startLifetime = 8f; m.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f); m.startColor = new Color(1, 1, 1, 0.92f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = drift.x * 0.6f; v.y = -2.2f; v.z = drift.z * 0.6f;
                var s = ps.shape; s.scale = new Vector3(48, 1, 48);
                ps.transform.position = at + Vector3.up * 13f + cam.transform.forward * 9f - drift * 2f;
            });
            Emit(ash, sky == Course.Sky.Ash ? precip : 0, at, 450f, ps =>
            {
                var m = ps.main; m.startLifetime = 9f; m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); m.startColor = new Color(0.32f, 0.3f, 0.3f, 0.85f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = drift.x * 0.5f; v.y = -1.6f; v.z = drift.z * 0.5f;
                var s = ps.shape; s.scale = new Vector3(56, 1, 56);
                ps.transform.position = at + Vector3.up * 14f + cam.transform.forward * 10f - drift * 2f;
            });
            Emit(embers, sky == Course.Sky.Ash ? precip : 0, at, 26f, ps =>
            {
                var m = ps.main; m.startLifetime = 4f; m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f); m.startColor = new Color(1f, 0.55f, 0.2f, 0.95f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = drift.x * 0.3f; v.y = 1.4f; v.z = drift.z * 0.3f;
                var s = ps.shape; s.scale = new Vector3(40, 1, 40);
                ps.transform.position = at + cam.transform.forward * 12f + Vector3.down * 3f;
            });
            // banks of mist drifting low across the ground ahead
            Emit(wisps, mist, at, 7f, ps =>
            {
                var m = ps.main; m.startLifetime = 11f; m.startSize = new ParticleSystem.MinMaxCurve(9f, 18f);
                var c = sky switch { Course.Sky.Ash => Smoke, Course.Sky.Snow => FrostMist, _ => SeaMist }; c.a = 0.16f; m.startColor = c;
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = drift.x * 0.25f + 0.6f; v.y = 0; v.z = drift.z * 0.25f;
                var s = ps.shape; s.scale = new Vector3(70, 3, 70);
                var f = cam.transform.forward; f.y = 0; f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
                var p = at + f * 34f; p.y = (float)HoleView.GroundHeight(HoleView.ToCourse(p)) + 2.5f;
                ps.transform.position = p;
            });
            // rain splashing on the ground just ahead of the camera
            if (sky == Course.Sky.Rain && precip > 0.1f && Time.timeScale > 0)
            {
                int n = Mathf.RoundToInt(precip * 45f * Time.deltaTime + Random.value);
                var f = cam.transform.forward; f.y = 0; f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
                for (int i = 0; i < n; i++)
                {
                    var p = at + f * Random.Range(1.5f, 18f) + Vector3.Cross(Vector3.up, f) * Random.Range(-9f, 9f);
                    p.y = (float)HoleView.GroundHeight(HoleView.ToCourse(p)) + 0.03f;
                    splashes.Emit(new ParticleSystem.EmitParams { position = p, startColor = new Color(0.86f, 0.9f, 1f, 0.6f), startSize = Random.Range(0.08f, 0.16f), startLifetime = Random.Range(0.18f, 0.3f) }, 1);
                }
            }
        }

        static void Emit(ParticleSystem ps, float level, Vector3 at, float rate, System.Action<ParticleSystem> shape)
        {
            var emission = ps.emission;
            if (level <= 0.01f)
            {
                emission.rateOverTime = 0;
                if (ps.isPlaying && ps.particleCount == 0) ps.Stop();
                return;
            }
            shape(ps);
            emission.rateOverTime = rate * level;
            if (!ps.isPlaying) ps.Play();
        }
    }
}
