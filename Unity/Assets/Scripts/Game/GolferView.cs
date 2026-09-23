using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using GolfArcade.Course;
using GolfArcade.Shot;

namespace GolfArcade.Game
{
    /// The golfer on screen: Adnan's standard character (Resources/Golfer/golfer_m|f, out of
    /// his sports animation studio via blender/scripts/adnan_golfer_export.py) with a swing
    /// clip per club — Drive, IronSwing, HalfSwing, Chip, Putt — and the club in hand. The phone
    /// drives the clip: the backswing is scrubbed by the detector's load, so the figure winds
    /// up exactly as far as the player does, and on impact the downswing runs through to the
    /// finish at real speed. Without the model a Mii-simple figure of primitives stands in.
    /// Off the course it can also Perform the studio's moves (the intro's wave, the gallery's
    /// idle and cheering), and a spectator is the same figure in a look of its own.
    public sealed class GolferView : MonoBehaviour
    {
        /// Who a figure looks like: the player's comes from GolferStyle; a spectator has its own.
        public struct Look
        {
            public string ModelPath, HairMesh;
            public Color Skin, Hair;
            /// The polo and the trousers, where they differ from the studio's golf kit (teal and
            /// sand); null keeps the kit.
            public Color? Shirt, Trousers;
            public static Look Player => new()
            {
                ModelPath = GolferStyle.ModelPath, HairMesh = GolferStyle.HairMesh,
                Skin = GolferStyle.SkinColor, Hair = GolferStyle.HairColor,
                Shirt = GolferStyle.ShirtColor, Trousers = GolferStyle.TrousersColor,
            };
        }
        Look? own;
        Look CurrentLook => own ?? Look.Player;
        /// A spectator: its own look, never holds a club, and isn't animated while off screen.
        bool spectator;

        const float MetresToYards = 1.0936f;

        /// One clip's landmarks, from golfer_<m|f>_clips.json (frames found in Blender from the
        /// club head's path: farthest from address is the top, its closest return is impact).
#pragma warning disable 649 // filled by JsonUtility
        [System.Serializable]
        public class ClipInfo { public string name, club; public int frames, fps, top, impact; }
        [System.Serializable]
        class ClipSet { public string gender; public ClipInfo[] clips; }
#pragma warning restore 649

        // rigged model
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable clip;
        AnimationClip clipAsset;
        // crossfades: the pose being left stays behind, frozen, on the mixer's second input and
        // fades out under the new one, so no change of clip or pose ever pops
        AnimationMixerPlayable mixer;
        AnimationClipPlayable ghost;
        float fade, fadeSeconds;
        readonly Dictionary<string, AnimationClip> clips = new();
        readonly Dictionary<string, ClipInfo> landmarks = new();
        readonly Dictionary<string, GameObject> clubMeshes = new();
        string clipName;
        float TopTime, ImpactTime, EndTime;
        bool hasModel;
        float time;            // clip time being shown
        float loadTarget;      // where the backswing should be, from the phone
        float loadVelocity;    // SmoothDamp's, following it
        /// 0 posing with the load, 2 the downswing, 5 the follow-through, 3 holding the finish, 4 performing a move
        int phase;
        // The swing's tempo, per clip (SetClub). The studio's clips come down from the top in
        // 0.6 s at an even pace; a real downswing takes about a third of a second and speeds up all the way to
        // the ball, and the follow-through leaves the ball at that speed and eases into the
        // finish. So the downswing is played over Downswing seconds with clip time running as
        // u^Accel, and the follow-through as 1-(1-v)^EaseOut over however long matches the club's
        // speed at impact.
        float Downswing = 0.38f, Accel = 1.4f, EaseOut = 2.6f;
        float swingU, swingRate, throughV, throughSeconds, finishTime;

        // primitive fallback
        Transform pivot, club, body;
        float shownLoad;
        float swingThrough = -1;
        const float rest = 20f;

        /// Blender material name → flat game colour, matching the character sheet.
        static readonly Dictionary<string, Color> Palette = new()
        {
            ["MAT_SKIN"] = Rgb(226, 160, 110), ["MAT_SHIRT"] = Rgb(38, 84, 176), ["MAT_TROUSERS"] = Rgb(214, 196, 160),
            ["MAT_BELT"] = Rgb(34, 40, 62), ["MAT_EYE"] = Rgb(28, 24, 22), ["MAT_LOGO"] = Rgb(38, 84, 176),
            ["MAT_CAP"] = Rgb(245, 245, 245), ["MAT_GLOVE"] = Rgb(240, 240, 240), ["MAT_SHOE"] = Rgb(240, 240, 240),
            ["MAT_SHOE_SOLE"] = Rgb(40, 40, 44), ["MAT_SHAFT"] = Rgb(190, 192, 198), ["MAT_CLUBHEAD"] = Rgb(40, 42, 48),
            ["MAT_GRIP"] = Rgb(30, 30, 34), ["MAT_HAIR"] = Rgb(70, 48, 30), ["MAT_LIPS"] = Rgb(196, 84, 90), ["MAT_SHIRT_DARK"] = Rgb(28, 62, 136),
        };
        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        /// The kit's cloth, which carries the knit of Adnan's tee and joggers.
        static readonly HashSet<string> Cloth = new()
        {
            "V4 teal", "V4 sand", "V4 navy", "V4 joined hip panel", "V4 golf belt", "MAT_SHIRT", "MAT_TROUSERS", "MAT_SHIRT_DARK",
        };

        static bool IsShirtTrim(string part) =>
            part.StartsWith("Collar") || part.StartsWith("Sleeve piping") || part.StartsWith("Armhole binding")
            || part.StartsWith("V4 shirt button") || part.EndsWith("Golf 1 top");

        static readonly Dictionary<(Color, bool, Texture), Material> clayMaterials = new();
        static Texture2D matCap, knit;

        /// A GolferClay material (Shaders/GolferClay.shader): the soft matte look of Adnan's
        /// studio renders, from a Higgsfield MatCap of his lighting; cloth adds the knit. Falls
        /// back to the course's flat lit material if the shader isn't in the build.
        static Material Clay(Color color, bool cloth, Texture map = null)
        {
            if (clayMaterials.TryGetValue((color, cloth, map), out var m) && m) return m;
            var shader = Shader.Find("GolfArcade/GolferClay");
            if (!shader) return HoleView.Mat(color);
            if (!matCap) matCap = Resources.Load<Texture2D>("Golfer/Look/clay_matcap");
            if (!knit) knit = Resources.Load<Texture2D>("Golfer/Look/knit_detail");
            m = new Material(shader) { color = color };
            if (matCap) m.SetTexture("_MatCap", matCap);
            if (knit) m.SetTexture("_Knit", knit);
            m.SetFloat("_Fabric", cloth ? 0.6f : 0f);
            if (map) { m.SetTexture("_MainTex", map); m.SetFloat("_MatCapStrength", 0.6f); }   // (the map carries its own shading)
            clayMaterials[(color, cloth, map)] = m;
            return m;
        }

        /// The clubs' finishes (blender/scripts/golf_clubs.py FINISHES): colour, metallic, smoothness.
        static readonly Dictionary<string, (Color color, float metal, float smooth)> ClubFinishes = new()
        {
            ["CLUB chrome"] = (new Color(0.86f, 0.87f, 0.89f), 1f, 0.88f),
            ["CLUB satin"] = (new Color(0.72f, 0.73f, 0.75f), 1f, 0.62f),
            ["CLUB gunmetal"] = (new Color(0.30f, 0.31f, 0.33f), 1f, 0.68f),
            ["CLUB carbon"] = (new Color(0.05f, 0.052f, 0.058f), 0f, 0.70f),
            ["CLUB black"] = (new Color(0.035f, 0.035f, 0.04f), 0f, 0.45f),
            ["CLUB grip"] = (new Color(0.05f, 0.05f, 0.055f), 0f, 0.15f),
            ["CLUB accent"] = (new Color(0.05f, 0.62f, 0.62f), 0f, 0.55f),
            ["CLUB white"] = (new Color(0.92f, 0.92f, 0.90f), 0f, 0.5f),
            ["CLUB groove"] = (new Color(0.10f, 0.10f, 0.11f), 1f, 0.5f),
        };
        static readonly Dictionary<string, Material> clubMaterials = new();

        /// Real metal for the clubs: Standard, so chrome and satin catch the sky.
        static Material ClubMaterial(string name)
        {
            if (clubMaterials.TryGetValue(name, out var m) && m) return m;
            var f = ClubFinishes.TryGetValue(name, out var known) ? known : (color: Color.grey, metal: 0f, smooth: 0.3f);
            var shader = Shader.Find("Standard");
            if (!shader) return HoleView.Mat(f.color);
            m = new Material(shader) { color = f.color };
            m.SetFloat("_Metallic", f.metal);
            m.SetFloat("_Glossiness", f.smooth);
            clubMaterials[name] = m;
            return m;
        }

        GameObject modelGo;

        // ----- The face: Adnan's expression atlas (Resources/Golfer/Look/FaceAtlas, 4 × 2 cells in
        // this order, as his TennisActor uses it) on the Higgsfield golfer's face decal.

        public enum Expression { Neutral, Blink, Happy, Focus, Effort, Sad, Surprised, Cheer }
        Material faceMaterial;
        Expression face = (Expression)(-1);
        float nextBlink = 2f, blinkFor;
        static Texture2D faceAtlas;

        Material FaceMaterial()
        {
            if (!faceAtlas) faceAtlas = Resources.Load<Texture2D>("Golfer/Look/FaceAtlas");
            var shader = Shader.Find("Unlit/Transparent");
            var m = new Material(shader ? shader : Shader.Find("Standard")) { mainTexture = faceAtlas, mainTextureScale = new Vector2(0.25f, 0.5f) };
            face = (Expression)(-1);
            return m;
        }

        void ShowFace(Expression e)
        {
            if (!faceMaterial || e == face) return;
            face = e;
            int cell = (int)e;
            faceMaterial.mainTextureOffset = new Vector2((cell % 4) * 0.25f, cell < 4 ? 0.5f : 0f);
        }

        /// What the face says: effort through the downswing, focus as the backswing loads, a big
        /// smile for a cheer or a fist pump, happy while waving; a blink every few seconds.
        void UpdateFace(float dt)
        {
            if (!faceMaterial) return;
            Expression want = phase switch
            {
                2 => Expression.Effort,
                0 when loadTarget > 0.3f => Expression.Focus,
                4 when clipName is "Cheer" or "FistPump" => Expression.Cheer,
                4 when clipName == "Wave" => Expression.Happy,
                _ => Expression.Neutral,
            };
            nextBlink -= dt;
            if (nextBlink <= 0) { blinkFor = 0.13f; nextBlink = Random.Range(2.2f, 4.5f); }
            if (blinkFor > 0) { blinkFor -= dt; if (want is Expression.Neutral or Expression.Focus) want = Expression.Blink; }
            ShowFace(want);
        }

        public static GolferView Create(Transform parent)
        {
            var go = new GameObject("Golfer");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GolferView>();
            v.ApplyStyle();
            return v;
        }

        public static GolferView CreateSpectator(Transform parent, Look look)
        {
            var go = new GameObject("Spectator");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GolferView>();
            v.own = look; v.spectator = true;
            v.ApplyStyle();
            return v;
        }

        /// (Re)build the figure from GolferStyle: body model and skin tone. Comes up at address.
        public void ApplyStyle()
        {
            if (graph.IsValid()) graph.Destroy();
            if (modelGo) Destroy(modelGo);
            faceMaterial = null;
            if (body) Destroy(body.gameObject);
            hasModel = false; body = null; modelGo = null;
            phase = 0; loadTarget = 0; time = 0; swingThrough = -1; shownLoad = 0;
            var look = CurrentLook;
            var model = Resources.Load<GameObject>(look.ModelPath);
            if (model && !BuildModel(model, look)) Debug.LogWarning($"{look.ModelPath} has no swing clips; using the primitive golfer");
            if (!hasModel) BuildFigure();
            SetClub(shownClub, shownShort);
        }

        // ----- Rigged model -----

        bool BuildModel(GameObject prefab, Look look)
        {
            string path = look.ModelPath;
            clips.Clear(); landmarks.Clear(); clubMeshes.Clear();
            foreach (var c in Resources.LoadAll<AnimationClip>(path)) clips[c.name] = c;
            if (clips.Count == 0) return false;
            var json = Resources.Load<TextAsset>(path + "_clips");
            if (json)
                foreach (var info in JsonUtility.FromJson<ClipSet>(json.text).clips) landmarks[info.name] = info;

            var model = Instantiate(prefab, transform);
            modelGo = model;
            model.name = "Golfer model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * MetresToYards; // the FBX is in metres, the course in yards
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    string name = mats[i].name.Replace(" (Instance)", "");
                    if (name.StartsWith("CLUB ")) { mats[i] = ClubMaterial(name); continue; }
                    // The Higgsfield golfer (blender/scripts/fit_golf_characters.py): its colour map
                    // beside the FBX, and a face painted from Adnan's expression atlas.
                    if (name.StartsWith("V4 Higgs body")) { mats[i] = Clay(Color.white, false, Resources.Load<Texture2D>($"{System.IO.Path.GetDirectoryName(path)}/higgs_{(path.EndsWith("_f") ? "f" : "m")}_color")); continue; }
                    if (name.StartsWith("V4 face decal"))
                    {
                        mats[i] = faceMaterial = FaceMaterial();
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // a thin shell on the head
                        continue;
                    }
                    Color color = mats[i].color;
                    if (name == "MAT_SKIN") color = look.Skin;
                    else if (name == "MAT_HAIR") color = look.Hair;
                    else if (name == "V4 teal" && look.Shirt is Color shirt) color = shirt;
                    else if (name == "V4 sand" && look.Trousers is Color legs) color = legs;
                    // Adnan's plain tee and joggers have none of the kit's ivory trim (collar,
                    // hem, piping, the stripe down the leg) or navy buttons; the shoes stay white.
                    else if ((name == "V4 ivory" || name == "V4 navy") && look.Shirt is Color tee && IsShirtTrim(r.name)) color = tee;
                    else if (name == "V4 ivory" && look.Trousers is Color joggers && r.name.Contains("bottom leg")) color = joggers;
                    else if (Palette.TryGetValue(name, out var known)) color = known;
                    mats[i] = Clay(color, Cloth.Contains(name));
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = !spectator; if (!body0) body0 = smr; }
                if (spectator) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("CLUB_")) clubMeshes[t.name.Substring(5)] = t.gameObject;
                if (t.name.StartsWith("HAIR_")) t.gameObject.SetActive(t.name == look.HairMesh);
            }
            if (spectator) foreach (var club in clubMeshes.Values) club.SetActive(false);
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("Golfer swing");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Swing", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            fade = 0;
            graph.Play();
            hasModel = true;
            clipName = null;
            return true;
        }

        GolfClub shownClub = GolfClub.Driver;
        bool shownShort;
        SkinnedMeshRenderer body0;

        // ----- Moves off the course -----

        /// The studio's shared moves were made facing a quarter turn from its golf clips; this
        /// turns them back so every clip faces where the golfer stands.
        static float ClipYaw(string move) => move is "Idle" or "Wave" or "Cheer" ? 90f : 0f;
        float performTime, performLength;

        /// Plays a move off the course — "Wave", "Cheer", "FistPump", "Idle" — at real speed,
        /// looping, empty-handed, from `startAt` seconds in. Settle() (or SetClub) brings the
        /// swing back. False when the model has no such clip.
        public bool Perform(string move, float startAt = 0f)
        {
            if (!hasModel || !clips.TryGetValue(move, out var c)) return false;
            var yaw = Quaternion.Euler(0, ClipYaw(move), 0);
            // (the shared moves face a quarter turn round, so from a swing pose there's no fading)
            Play(c, modelGo.transform.localRotation == yaw ? 0.25f : 0f);
            clipName = move;
            foreach (var kv in clubMeshes) kv.Value.SetActive(false);
            modelGo.transform.localRotation = yaw;
            performLength = Mathf.Max(0.1f, c.length);
            performTime = Mathf.Repeat(startAt, performLength);
            phase = 4;
            clip.SetTime(performTime);
            graph.Evaluate();
            return true;
        }

        /// The move playing now, or null while it's the swing.
        public string Performing => phase == 4 ? clipName : null;

        /// The clip and the club in hand for `club`: the driver's swing, the iron's, a half
        /// swing or a chip for the wedge depending on how far there is to go, the putt.
        public void SetClub(GolfClub club, bool shortShot)
        {
            shownClub = club; shownShort = shortShot;
            if (!hasModel) return;
            string wanted = club switch
            {
                GolfClub.Driver => "Drive",
                GolfClub.Iron => "IronSwing",
                GolfClub.Wedge => shortShot ? "Chip" : "HalfSwing",
                _ => "Putt",
            };
            if (!clips.ContainsKey(wanted)) wanted = clips.ContainsKey("Drive") ? "Drive" : new List<string>(clips.Keys)[0];
            if (wanted == clipName) return;
            clipName = wanted;
            var c = clips[wanted];
            if (landmarks.TryGetValue(wanted, out var info) && info.fps > 0)
            {
                TopTime = (float)info.top / info.fps; ImpactTime = (float)info.impact / info.fps; EndTime = (float)(info.frames - 1) / info.fps;
            }
            else { TopTime = c.length * 0.55f; ImpactTime = c.length * 0.75f; EndTime = c.length; }
            string clubMesh = info != null ? info.club.ToUpperInvariant() : club.ToString().ToUpperInvariant();
            foreach (var kv in clubMeshes) kv.Value.SetActive(!spectator && kv.Key == clubMesh);
            (Downswing, Accel, EaseOut) = wanted switch
            {
                "Putt" => (0.42f, 1.1f, 1.6f),     // a pendulum: hardly any acceleration
                "Chip" => (0.42f, 1.25f, 2.0f),
                "HalfSwing" => (0.34f, 1.35f, 2.4f),
                _ => (0.38f, 1.4f, 2.6f),           // about twice the studio pace at the ball, and still readable at 60 fps
            };
            Play(c, modelGo.transform.localRotation == Quaternion.identity ? 0.2f : 0f);
            modelGo.transform.localRotation = Quaternion.identity;
            if (phase == 4) { phase = 0; time = 0; }
            Show(Mathf.Min(time, TopTime));
        }

        void Show(float t)
        {
            time = Mathf.Clamp(t, 0, EndTime);
            clip.SetTime(time);
            graph.Evaluate();
        }

        /// Puts `c` on the mixer; with a crossfade, the pose on screen now stays behind, frozen,
        /// and fades out over that many seconds.
        void Play(AnimationClip c, float crossfade)
        {
            if (ghost.IsValid()) { graph.Disconnect(mixer, 1); ghost.Destroy(); }
            if (clip.IsValid())
            {
                graph.Disconnect(mixer, 0);
                if (crossfade > 0) { ghost = clip; graph.Connect(ghost, 0, mixer, 1); fade = 1; fadeSeconds = crossfade; }
                else clip.Destroy();
            }
            if (!ghost.IsValid()) fade = 0;   // nothing on screen yet to fade from
            clip = AnimationClipPlayable.Create(graph, c);
            clip.SetApplyFootIK(false);
            clip.SetApplyPlayableIK(false);
            graph.Connect(clip, 0, mixer, 0);
            clipAsset = c;
            Weigh();
        }

        /// Leaves the pose on screen for another point in the same clip, smoothly.
        void Crossfade(float seconds) { if (clipAsset) { float t = time; Play(clipAsset, seconds); clip.SetTime(t); } }

        void Weigh()
        {
            mixer.SetInputWeight(0, 1 - fade);
            mixer.SetInputWeight(1, ghost.IsValid() ? fade : 0);
        }

        void TickFade(float dt)
        {
            if (fade <= 0) return;
            fade = Mathf.Max(0, fade - dt / fadeSeconds);
            if (fade == 0 && ghost.IsValid()) { graph.Disconnect(mixer, 1); ghost.Destroy(); }
            Weigh();
        }

        void ShowThrough()
        {
            if (throughV >= 1) { phase = 3; Show(finishTime); return; }
            Show(ImpactTime + (finishTime - ImpactTime) * (1 - Mathf.Pow(1 - throughV, EaseOut)));
        }

        float DownswingTime(float u) => TopTime + (ImpactTime - TopTime) * Mathf.Pow(Mathf.Clamp01(u), Accel);

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        // ----- Shared API -----

        /// Stand beside the ball, facing across the aim line (a right-hander stands to the
        /// ball's left as seen from behind).
        public void Stand(Vector3 ball, Vector3 aimDirection)
        {
            var side = Vector3.Cross(Vector3.up, aimDirection).normalized; // right of the line
            transform.position = ball - side * 0.75f;
            transform.rotation = Quaternion.LookRotation(side, Vector3.up);
        }

        public void SetVisible(bool on) => gameObject.SetActive(on);

        /// The backswing follows the phone: 0 is address, 1 the top.
        public void ShowLoad(float load)
        {
            shownLoad = load;
            if (phase == 3 && hasModel) { Crossfade(0.3f); phase = 0; loadVelocity = 0; Show(0); }
            if (phase == 3) phase = 0;
            if (phase == 0) loadTarget = Mathf.Clamp01(load);
        }

        /// Swing through from wherever the backswing got to. Returns the seconds until the club
        /// reaches the ball, so the ball can leave exactly then.
        public float Strike()
        {
            swingThrough = 0;
            if (!hasModel) return 0.12f;
            // How far back it got. A full swing comes down from the top; a shorter one joins the
            // downswing where the club is about as far back, a little slower (a shorter swing
            // is a gentler one), and its follow-through stops short of the full finish.
            float f = Mathf.Clamp01(time / Mathf.Max(0.01f, TopTime));
            bool full = f >= 0.9f;
            swingU = full ? 0 : Mathf.Min(0.95f, Mathf.Pow(1 - f, 1 / Accel));   // (even a tap swings through a little)
            float seconds = Downswing * (full ? 1 : Mathf.Lerp(0.7f, 1f, f));
            swingRate = (1 - swingU) / seconds;
            // the club's clip-speed at impact, which the follow-through picks up
            float impactSpeed = (ImpactTime - TopTime) * Accel * swingRate;
            finishTime = full ? EndTime : ImpactTime + (EndTime - ImpactTime) * Mathf.Max(0.55f, f);
            throughSeconds = Mathf.Clamp((finishTime - ImpactTime) * EaseOut / Mathf.Max(0.01f, impactSpeed), 0.35f, 1.6f);
            Crossfade(full ? 0.07f : 0.12f);   // from the backswing pose into the downswing's
            phase = 2;
            Show(DownswingTime(swingU));
            return seconds;
        }

        public void Settle()
        {
            if (phase == 4) { clipName = null; SetClub(shownClub, shownShort); }
            swingThrough = -1; shownLoad = 0;
            // back to address from wherever it is (the finish, a half-made backswing) without a pop
            if (hasModel && phase != 4 && time > 0.001f) Crossfade(0.3f);
            phase = 0; loadTarget = 0; loadVelocity = 0;
            if (hasModel) Show(0);
        }

        void Update()
        {
            if (hasModel) UpdateModel(); else UpdateFigure();
        }

        void UpdateModel()
        {
            float dt = Time.deltaTime;
            TickFade(dt);
            UpdateFace(dt);
            switch (phase)
            {
                case 0:
                    // follow the phone's load with a critically damped spring: live, but it eases
                    // in and out of every move instead of travelling at a constant rate
                    Show(Mathf.SmoothDamp(time, loadTarget * TopTime, ref loadVelocity, 0.07f, TopTime * 10f, dt));
                    break;
                case 2:
                    swingU += swingRate * dt;
                    if (swingU < 1) { Show(DownswingTime(swingU)); break; }
                    // through the ball: the time left over this frame goes into the follow-through
                    throughV = swingRate > 0 ? (swingU - 1) / swingRate / throughSeconds : 0;
                    phase = 5;
                    ShowThrough();
                    break;
                case 5:
                    throughV += dt / throughSeconds;
                    ShowThrough();
                    break;
                case 3:
                    if (fade > 0) graph.Evaluate();
                    break;
                case 4:
                    performTime = Mathf.Repeat(performTime + dt, performLength);
                    // a spectator nobody can see isn't worth posing
                    if (spectator && body0 && !body0.isVisible) break;
                    clip.SetTime(performTime);
                    graph.Evaluate();
                    break;
            }
        }

        // ----- Primitive fallback -----

        void BuildFigure()
        {
            var skin = GolferStyle.SkinColor;
            var shirt = new Color(0.2f, 0.45f, 0.85f);
            var trousers = new Color(0.25f, 0.25f, 0.3f);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var legs = HoleView.Primitive(PrimitiveType.Capsule, "Legs", trousers, body);
            legs.transform.localPosition = new Vector3(0, 0.45f, 0);
            legs.transform.localScale = new Vector3(0.35f, 0.45f, 0.3f);
            var torso = HoleView.Primitive(PrimitiveType.Capsule, "Torso", shirt, body);
            torso.transform.localPosition = new Vector3(0, 1.05f, 0);
            torso.transform.localScale = new Vector3(0.45f, 0.35f, 0.32f);
            var head = HoleView.Primitive(PrimitiveType.Sphere, "Head", skin, body);
            head.transform.localPosition = new Vector3(0, 1.62f, 0);
            head.transform.localScale = Vector3.one * 0.34f;
            var cap = HoleView.Primitive(PrimitiveType.Cylinder, "Cap", Color.white, body);
            cap.transform.localPosition = new Vector3(0, 1.78f, 0.02f);
            cap.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f);

            pivot = new GameObject("Shoulders").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(0, 1.3f, 0);

            var arms = HoleView.Primitive(PrimitiveType.Capsule, "Arms", skin, pivot);
            arms.transform.localPosition = new Vector3(0, -0.35f, 0.15f);
            arms.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);

            club = new GameObject("Club").transform;
            club.SetParent(pivot, false);
            var shaft = HoleView.Primitive(PrimitiveType.Cylinder, "Shaft", new Color(0.75f, 0.75f, 0.78f), club);
            shaft.transform.localPosition = new Vector3(0, -0.85f, 0.2f);
            shaft.transform.localScale = new Vector3(0.03f, 0.55f, 0.03f);
            var headGo = HoleView.Primitive(PrimitiveType.Cube, "Clubhead", new Color(0.3f, 0.3f, 0.32f), club);
            headGo.transform.localPosition = new Vector3(0.06f, -1.38f, 0.2f);
            headGo.transform.localScale = new Vector3(0.16f, 0.07f, 0.09f);
        }

        void UpdateFigure()
        {
            float angle;
            if (swingThrough >= 0)
            {
                swingThrough += Time.deltaTime;
                float t = Mathf.Clamp01(swingThrough / 0.35f);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                angle = Mathf.Lerp(-(rest + shownLoad * 150f), 170f, eased);
                if (t >= 1 && swingThrough > 1.2f) { swingThrough = -1; shownLoad = 0; }
            }
            else
            {
                angle = -(rest + shownLoad * 150f);
            }
            pivot.localRotation = Quaternion.AngleAxis(-angle + rest, Vector3.forward) * Quaternion.AngleAxis(25f, Vector3.right);
            float turn = swingThrough >= 0 ? Mathf.Lerp(-shownLoad * 30f, 40f, Mathf.Clamp01(swingThrough / 0.35f)) : -shownLoad * 30f;
            body.localRotation = Quaternion.AngleAxis(turn, Vector3.up);
        }
    }
}
