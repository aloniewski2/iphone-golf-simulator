using UnityEngine;

namespace GolfArcade.Course
{
    /// Drives the gentle plant sway of the postcard holes (GolfArcade/GolfPlants, RUNTIME.md "v2 2026-10-05 area U"): one global shader vector, _GolfWind,
    /// set every frame from the hole's wind. THE ONLY CODE THAT READS THE CLOCK FOR VISUALS (with GolfPlants.shader): the lava, surf, fall, sky, ground,
    /// rocks and water stay static.
    ///
    ///   _GolfWind = (dirX * amp, 0, dirZ * amp, time)    amp in yards, time in seconds wrapped at LoopSeconds
    ///   direction: Wind.DirectionDegrees is where the wind blows TOWARD, degrees right of +D (down the hole); the course axes are world +Z = down the hole, +X = right,
    ///   so the world direction is (sin d, 0, cos d)  (HoleView.ToWorld: (x, y, d); Hole.HeadingTo = atan2(dx, dd))
    ///   amp = AmpFullYards x (IdleShare + (1 - IdleShare) x min(mph, 20) / 20): a faint idle breeze at 0 mph, monotonic in speed, 1.0 at Wind.MaxMPH
    ///
    /// GolfGame.StartHole calls SetWind(Wind) (the minimal hook); the driver also polls GolfGame.Wind each frame (the multiplayer path assigns the wind elsewhere), so the
    /// two cannot disagree for long. No per-frame allocation. Without a driver (edit mode, tennis) _GolfWind is never set = zero = static plants.
    ///
    /// Tests / stills: FreezeTime (seconds; null = real time) and ForcedWind (a fixed wind; null = the game's) are static overrides; Push() applies them immediately.
    /// Displacement() is the C# mirror of GolfPlantsSwayOS in GolfPlants.shader (the editor check GolfPlantSwayCheck compares the two on the GPU): keep them in step.
    public static class GolfWindSway
    {
        /// Tip displacement at 20 mph for weight 1 (yards, one-sided: the plant leans downwind and breathes back to rest). 0.058 x sqrt(1 + CrossShare^2) = 0.0592 <= the tuft cap 0.06;
        /// the per-kind caps (flower .05, shrub .03, vine .08) are caps on the tip WEIGHT: displacement = AmpFull x weight (linear, the user's formula).
        public const float AmpFullYards = 0.058f;
        public const float IdleShare = 0.25f;
        /// The clock wraps here; every frequency in the shader is a whole number of cycles per loop, so the wrap is invisible.
        public const float LoopSeconds = 600f;
        /// Largest crosswise share of the lean (the shader's `side` term): the displacement magnitude is at most amp x w x sqrt(1 + CrossShare^2).
        public const float CrossShare = 0.2f;

        static readonly int WindId = Shader.PropertyToID("_GolfWind");
        static Wind current = Wind.Calm, hookWind = Wind.Calm;
        static bool haveWind, hookSet;
        static GolfWindSwayDriver driver;

        /// Seconds the shader sees instead of the clock (null = Time.time). Stills / checks only.
        public static float? FreezeTime;
        /// A fixed wind instead of the game's (null = the game's). Stills / checks only.
        public static Wind? ForcedWind;

        /// The wind the plants sway to right now (the forced one, else the hole's).
        public static Wind ActiveWind => ForcedWind ?? current;
        public static bool HasWind => ForcedWind.HasValue || haveWind;
        /// What GolfGame.StartHole handed over (the hook), and whether it has: the stills tool checks it equals GolfGame.Wind after every hole start.
        public static Wind HookWind => hookWind;
        public static bool HookSet => hookSet;

        /// The hole's wind (GolfGame.StartHole). Creates the driver the first time.
        public static void SetWind(Wind wind)
        {
            current = wind; haveWind = true; hookWind = wind; hookSet = true;
            EnsureDriver();
            Push();
        }

        /// Forget the hole's wind (the plants go back to the idle breeze at direction 0 on the next Push; a game that is quitting).
        public static void Clear() { current = Wind.Calm; haveWind = false; Push(); }

        static void EnsureDriver()
        {
            if (driver || !Application.isPlaying) return;
            var go = new GameObject("GolfWindSway") { hideFlags = HideFlags.HideInHierarchy };   // not DontSave: the editor destroys it with the play session instead of leaking it
            Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<GolfWindSwayDriver>();
        }

        /// Amplitude in yards for a wind speed (the shader's `amp`).
        public static float Amplitude(double mph)
        {
            float k = Mathf.Clamp01((float)(double.IsFinite(mph) ? mph : 0) / (float)Wind.MaxMPH);
            return AmpFullYards * (IdleShare + (1f - IdleShare) * k);
        }

        /// World direction (x, z) the wind blows toward for Wind.DirectionDegrees.
        public static Vector2 Direction(double directionDegrees)
        {
            float r = (float)(directionDegrees * System.Math.PI / 180.0);
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        /// The value of _GolfWind for a wind and a clock reading.
        public static Vector4 GlobalValue(Wind wind, float seconds)
        {
            var d = Direction(wind.DirectionDegrees);
            float a = Amplitude(wind.SpeedMPH);
            float t = seconds % LoopSeconds; if (t < 0) t += LoopSeconds;
            return new Vector4(d.x * a, 0f, d.y * a, t);
        }

        /// Set _GolfWind now (the driver does it every LateUpdate; tools call it after changing FreezeTime / ForcedWind, before cam.Render()).
        public static void Push()
        {
            Shader.SetGlobalVector(WindId, GlobalValue(ActiveWind, FreezeTime ?? Time.time));
        }

        /// The driver's per-frame read of the game's own wind (GolfGame.Wind has a private setter; the multiplayer path assigns it in GolfGame.Network, not through StartHole).
        internal static void Follow(Wind wind) { if (!ForcedWind.HasValue) { current = wind; haveWind = true; } }

        // ------------------------------------------------------------------ the C# mirror of GolfPlantsSwayOS (world space)

        /// Hash without sine (Dave Hoskins), the shader's GolfPlantsHash. p is the pivot rounded to 1 cm.
        public static float Hash(float px, float py)
        {
            float p3x = Frac(px * 0.1031f), p3y = Frac(py * 0.1031f), p3z = Frac(px * 0.1031f);
            float d = p3x * (p3y + 33.33f) + p3y * (p3z + 33.33f) + p3z * (p3x + 33.33f);   // dot(p3, p3.yzx + 33.33)
            p3x += d; p3y += d; p3z += d;
            return Frac((p3x + p3y) * p3z);
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// World-space displacement (yards, x / z; y is 0) of a vertex with sway weight `weight` (R), phase `phase` (G), whose object's pivot is at world (originX, originZ),
        /// for a wind of `windXZ` = _GolfWind.xz (direction x amplitude) at clock `seconds` (the value of _GolfWind.w). Exactly the shader's arithmetic.
        public static Vector2 Displacement(float weight, float phase, float originX, float originZ, Vector2 windXZ, float seconds)
        {
            float amp = windXZ.magnitude;
            float w = amp > 1e-5f ? Mathf.Clamp01(weight) : 0f;
            Vector2 dir = windXZ / Mathf.Max(amp, 1e-5f);
            float cx = Mathf.Round(originX * 100f), cz = Mathf.Round(originZ * 100f);
            float h = Hash(cx, cz), h2 = Hash(cx + 31.7f, cz + 31.7f), h3 = Hash(cx + 71.3f, cz + 71.3f);
            float u = seconds * (1f / LoopSeconds);
            float travel = (originX * dir.x + originZ * dir.y) * 0.025f;
            const float TwoPi = 6.2831853f;
            float s1 = Mathf.Sin(TwoPi * ((270f + 6f * Mathf.Floor(h2 * 4.999f)) * u + Frac(h + phase)));
            float s2 = Mathf.Sin(TwoPi * ((498f + 6f * Mathf.Floor(h3 * 4.999f)) * u + Frac(h2 + phase * 0.5f)));
            float sc = Mathf.Sin(TwoPi * (360f * u + h3));
            float gust = 0.8f + 0.2f * Mathf.Sin(TwoPi * (30f * u - travel));
            float lean = 0.5f + 0.5f * gust * (0.62f * s1 + 0.38f * s2);
            float side = CrossShare * gust * sc;
            float k = amp * w;
            return new Vector2((dir.x * lean - dir.y * side) * k, (dir.y * lean + dir.x * side) * k);
        }

        /// Upper bound of the displacement magnitude for a weight and wind speed: amp x w x sqrt(1 + CrossShare^2).
        public static float MaxDisplacement(float weight, double mph) => Amplitude(mph) * weight * Mathf.Sqrt(1f + CrossShare * CrossShare);
    }

    /// Hidden DontDestroyOnLoad driver: pushes _GolfWind every frame. Created by GolfWindSway.SetWind (golf only; the tennis scenes never create it).
    sealed class GolfWindSwayDriver : MonoBehaviour
    {
        GolfArcade.Game.GolfGame game;
        float nextFind;

        void LateUpdate()
        {
            if (!game && Time.unscaledTime >= nextFind)          // once every 2 s while there is no game (the find is not free)
            {
                game = FindFirstObjectByType<GolfArcade.Game.GolfGame>();
                nextFind = Time.unscaledTime + 2f;
            }
            if (game) GolfWindSway.Follow(game.Wind);
            GolfWindSway.Push();
        }
    }
}
