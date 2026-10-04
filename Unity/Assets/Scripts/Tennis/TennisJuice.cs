using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Plan 2 / 2B spectacle. Everything fires from a CONFIRMED event — the ball leaving real strings,
    /// the game calling a whiff, a point being awarded — never from a button press.
    ///   * Named camera signatures that CUT during the hit-stop: PERFECT (low Dutch), SMASH (tight low
    ///     side + ball track), WHIFF (comedy crash-zoom, blue fail vignette), plus a perfect-serve toss
    ///     cam from behind the knees. Speed lines / vignette / title card on top, then snap back.
    ///   * After every point a reaction cam: winner, then loser, while their auto emotes play.
    ///   * The ULTIMATE cinematic: freeze, void, orbit around the hitter, release with a hyper trail.
    ///   * The rival cam inset and a gentle FOV breath in rallies, so the far-side player reads.
    /// Slow motion / freezes never override the app's pause and always hand timeScale back at 1.
    public sealed class TennisJuice : MonoBehaviour
    {
        public enum Beat { None, Perfect, Smash, Whiff }
        enum Shot { None, Cut, BallTrack, Toss, Reaction, Ultimate }

        public Beat Current { get; private set; }
        public int Perfects { get; private set; }
        public int Smashes { get; private set; }
        public int Whiffs { get; private set; }
        public int Reactions { get; private set; }
        public int Ultimates { get; private set; }
        public static bool BeatsEnabled = true;
        public string ShotName => shot.ToString();
        public bool UltimateActive => shot == Shot.Ultimate;
        public Transform ChargeSubject => shot == Shot.Ultimate ? subject : null;

        /// A plain perfect only gets the camera once in a while (its FX, hit-stop and sting fire every
        /// time), so a rally of clean hits stays readable; supercharged perfects, smashes, whiffs and
        /// ultimates always get theirs.
        public const float PerfectBeatCooldown = 8f;
        float lastPerfectBeat = -99;

        Shot shot; float shotT, shotLen; Transform subject, subject2, ball; Vector3 ballDir; float roll, fovFrom, fovTo;
        float slowFor, slowScale = 1, slowFrom; bool ownsTimeScale; bool frozen;
        Color ultimateColor = new Color(1,.55f,.15f);
        readonly List<Canvas> cinematicHidden = new List<Canvas>();
        AudioSource ultimateAudio;
        float punch;   // impact FOV punch, decays

        // ---- overlay
        Canvas canvas; RawImage vignette, lines, flash, rivalView; Image rivalFrame, pMeter, rMeter, pMeterBack, rMeterBack; Text title, sub, pLabel, rLabel, rivalLabel;
        Camera rivalCam; RenderTexture rivalRT; float rivalShow, titleT = 99, titleLen = 1;
        Color vignetteColor = Color.black; float vignetteA, linesA, flashA;

        static int Rank(Beat b) => b == Beat.Smash ? 3 : b == Beat.Perfect ? 2 : b == Beat.Whiff ? 1 : 0;

        /// Spectacle clock: real time on device, exactly one frame per captured frame in capture runs
        /// (so a 2.3 s cinematic is 2.3 s of video whatever the render speed).
        static float Udt => Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;
        float clock;
        void Awake() { BuildOverlay(); }
        /// Draw the overlay through the gameplay camera (so it is part of every rendered frame).
        public void AttachCamera(Camera cam)
        {
            if (!canvas || !cam) return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = cam.nearClipPlane + .05f;
        }

        // =============================================================== triggers (all post-contact)
        public void Trigger(Beat b, Transform who, bool force = false)
        {
            if (b == Beat.Perfect) Perfects++; else if (b == Beat.Smash) Smashes++; else if (b == Beat.Whiff) Whiffs++;
            if (shot == Shot.Ultimate || shot == Shot.Reaction) return;
            if (b == Beat.Perfect) {
                lastPerfectBeat = clock;
                Title(force ? "SUPERCHARGED!" : "PERFECT!", "", .8f); Punch(1);
                return; // All perfect contacts keep the gameplay angle, including the first and forced beats.
            }
            if (!BeatsEnabled || (Current != Beat.None && Rank(b) < Rank(Current) && shotT < shotLen * .6f)) return;
            if (b == Beat.Perfect) lastPerfectBeat = clock;
            Current = b; subject = who;
            switch (b)
            {
                case Beat.Perfect:
                    Begin(Shot.Cut, .4f); roll = 12; fovFrom = 44; fovTo = 36; Slow(.14f, .4f);
                    Overlay(Color.black, .55f, .9f, .5f); Title(force ? "SUPERCHARGED!" : "PERFECT!", "", .8f);
                    break;
                case Beat.Smash:
                    Current = Beat.None; flashA = .1f; Title("SMASH!", "", .9f); Punch(1);
                    break;
                case Beat.Whiff:
                    // No cinematic: the whiff emote plays from the regular play camera (title only).
                    Current = Beat.None; Title("WHIFF!", "", .9f);
                    break;
            }
        }
        bool trackAfter;

        /// A good toss on the player's serve: low behind the knees, watching the ball go up.
        public void BeginTossCam(Transform server, Transform tossedBall)
        {
            if (shot == Shot.Ultimate || shot == Shot.Reaction) return;
            subject = server; ball = tossedBall; Begin(Shot.Toss, 3f); roll = 0;
        }
        public void EndTossCam() { if (shot == Shot.Toss) End(); }
        /// Perfect serve: contact close-up, then a brief ball track.
        public void PerfectServe(Transform server, Transform flyingBall)
        {
            // Plan 2C: the ball is live and must be readable for its whole flight — no contact cut, no ball
            // track; the play camera owns it. The moment still pops: title, flash, FOV punch (plus FX/sting).
            Perfects++; lastPerfectBeat = clock;
            if (shot == Shot.Toss) End();
            flashA = .12f; Title("PERFECT SERVE!", "", .8f); Punch(1);
        }
        public void SetBall(Transform b) => ball = b;
        /// A short called beat (NET!...) on the play camera.
        public void Call(string text) { if (shot != Shot.Ultimate) Title(text, "", .8f); }

        /// Announce the point in the current third-person view; no reaction-camera cut.
        public void PointReaction(Transform winner, Transform loser, string headline)
        {
            Reactions++;
            if (shot == Shot.Ultimate) return;
            End(); Current = Beat.None;
            Title(headline, "", 1.2f);
        }

        /// ULTIMATE: the hit is already real (strings met the ball); the world freezes on it, the camera
        /// orbits the hitter in a void, then play resumes with the hyper ball.
        /// ULTIMATE charge: the cinematic runs BEFORE the ball is hit. A full-meter hitter a beat from
        /// contact: the world freezes, the camera orbits them in the void, then cuts back to the normal
        /// play camera and the real contact happens there. Once the ball is hit the camera is regular.
        public void UltimateCharge(Transform hitter, string who, string ability = "SUPERNOVA")
        {
            if (!TennisAbilities.UltimatesEnabled) return;
            foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(c != canvas && c.enabled) { cinematicHidden.Add(c); c.enabled=false; }
            ultimateColor = ability == "Rescue Lob" ? new Color(.25f,.95f,.8f) : ability == "Curveball" ? new Color(.55f,.4f,1) : new Color(1,.55f,.15f);
            PlayUltimateCue(ability);
            Ultimates++; subject = hitter; Current = Beat.None; trackAfter = false;
            Begin(Shot.Ultimate, UltimateFreeze); roll = 0;
            frozen = true; SetScale(0);
            Overlay(ultimateColor * .12f, .92f, .85f, .12f); Title(ability.ToUpperInvariant(), who + " · ULTIMATE", UltimateFreeze);
        }
        /// The ultimate contact itself (play camera): title, flash and punch — no camera change.
        public void UltimateHit(string who, string ability = "SUPERNOVA") { if (!TennisAbilities.UltimatesEnabled) return; if (shot == Shot.Cut || shot == Shot.BallTrack) End(); flashA = .22f; Punch(1f); Title(ability.ToUpperInvariant() + "!", who, .7f); /* light flash: the live ball must stay readable */ }

        void PlayUltimateCue(string ability)
        {
            if(!ultimateAudio) { ultimateAudio=gameObject.AddComponent<AudioSource>(); ultimateAudio.spatialBlend=0; }
            const int rate=22050; int n=(int)(rate*UltimateFreeze);
            var data=new float[n]; float baseHz=ability=="Skybreaker" ? 180 : ability=="Rescue Lob" ? 260 : 220;
            for(int i=0;i<n;i++) {
                float t=(float)i/rate, u=t/UltimateFreeze;
                float env=Mathf.Min(1,t/.025f)*Mathf.Clamp01((UltimateFreeze-t)/.12f);
                float phase=2*Mathf.PI*baseHz*(t+.3f*t*t);
                data[i]=env*(Mathf.Sin(phase)*.13f+Mathf.Sin(phase*1.5f)*.07f)*( .6f+.4f*u);
            }
            var cue=AudioClip.Create("Ultimate " + ability,n,1,rate,false); cue.SetData(data,0);
            ultimateAudio.PlayOneShot(cue); Destroy(cue,UltimateFreeze+1);
        }

        public void Punch(float amount) { punch = 0; }
        public const float UltimateFreeze = 2.2f;
        /// Plan 2C law: a live ball the player must play is always on the play camera. Any hit cut or
        /// toss cam still running is ended (the rival just struck, the server is swinging...).
        public void EndLiveShot() { if (shot == Shot.Cut || shot == Shot.BallTrack || shot == Shot.Toss) { End(); if (!frozen) Release(); } }

        // =============================================================== update
        void Begin(Shot s, float len) { shot = s; shotT = 0; shotLen = len; }
        void RestoreCinematicHUD() { foreach(var c in cinematicHidden) if(c) c.enabled=true; cinematicHidden.Clear(); }
        void End() { RestoreCinematicHUD(); shot = Shot.None; Current = Beat.None; trackAfter = false; }
        void Slow(float scale, float seconds) { slowScale = scale; slowFor = seconds; slowFrom = clock; }
        void SetScale(float s) { if (Time.timeScale == 0 && !ownsTimeScale) return; Time.timeScale = s; ownsTimeScale = true; }
        void Release() { if (ownsTimeScale) { Time.timeScale = 1; ownsTimeScale = false; } frozen = false; }
        void OnDisable() { RestoreCinematicHUD(); Release(); }

        void Update()
        {
            float udt = Udt; clock += udt;
            bool appPaused = Time.timeScale == 0 && !ownsTimeScale;
            if (!appPaused)
            {
                if (shot != Shot.None) shotT += udt;
                if (shot == Shot.Ultimate)
                {
                    // Plan 2C: the moment the ball goes live again the play camera takes over (no release
                    // POV); a short 0.5x -> 1x ramp gives the receiver a beat to read the hyper ball.
                    if (shotT >= UltimateFreeze) { frozen = false; End(); Release(); }
                }
                else if (clock - slowFrom < slowFor) SetScale(Mathf.Lerp(slowScale, 1, Mathf.SmoothStep(0, 1, (clock - slowFrom) / slowFor)));
                else if (ownsTimeScale) Release();
                if (shot != Shot.None && shotT >= shotLen)
                {
                    if (shot == Shot.Cut && trackAfter && ball) { Begin(Shot.BallTrack, .4f); trackAfter = false; }
                    else { End(); if (!frozen) Release(); }
                }
            }
            punch = Mathf.MoveTowards(punch, 0, udt * 5.5f);   // settles in ~0.2 s
            UpdateOverlay(udt);
        }

        // =============================================================== camera
        /// Plan 2C / Score80 C law, enforced here: set by the game every frame while a rally ball is live.
        public bool BallLive { get; set; }
        /// Frames a cinematic was refused because the ball was live (should stay 0: nothing asks for one).
        public int LiveCutsRefused { get; private set; }
        /// True when a cinematic owns the camera this frame; `cam` is then fully placed here.
        public bool OverrideCamera(Camera cam)
        {
            if (shot == Shot.None || !subject) return false;
            // Spectacle only while the ball is frozen or dead: a live, playable ball is always on the play camera.
            // (The ultimate charge freezes the world, so it may run; the toss cam runs before the serve is struck.)
            if (BallLive && (shot == Shot.Cut || shot == Shot.BallTrack || shot == Shot.Reaction || (shot == Shot.Ultimate && !frozen)))
            { LiveCutsRefused++; End(); if (!frozen) Release(); return false; }
            // A shot that has run its time hands over THIS frame (the ball is live from its first frame).
            if (shot != Shot.Reaction && shotLen > 0 && shotT + Udt * .5f >= shotLen && !(shot == Shot.Cut && trackAfter)) return false;
            Vector3 p = subject.position, fwd = subject.forward, right = subject.right;
            Vector3 pos, look; float fov;
            float u = shotLen > 0 ? Mathf.Clamp01(shotT / shotLen) : 1;
            switch (shot)
            {
                case Shot.Cut:
                    if (Current == Beat.Whiff)
                    {
                        // comedy crash-zoom into the awkward pose and face
                        pos = p + fwd * 2.3f + right * .5f + Vector3.up * 1.15f; look = p + Vector3.up * 1.0f;
                        fov = Mathf.Lerp(fovFrom, fovTo, Mathf.SmoothStep(0, 1, Mathf.Clamp01(shotT / .22f)));
                    }
                    else if (Current == Beat.Smash)
                    {
                        pos = p + right * 1.5f + fwd * .6f + Vector3.up * .55f; look = p + Vector3.up * 1.5f + fwd * .6f;
                        fov = Mathf.Lerp(fovFrom, fovTo, u);
                    }
                    else
                    {
                        // low front-side angle looking up at the hitter: face, racket and the ball leaving
                        pos = p + fwd * 2.5f + right * 1.7f + Vector3.up * .45f; look = p + Vector3.up * 1.15f + right * .25f;
                        fov = Mathf.Lerp(fovFrom, fovTo, u);
                    }
                    Place(cam, pos, look, fov, roll * (1 - u * .3f));
                    return true;
                case Shot.BallTrack:
                    if (!ball) return false;
                    var d = ballDir.sqrMagnitude > .01f ? ballDir : fwd;
                    pos = ball.position - d.normalized * 2.6f + Vector3.up * .9f; look = ball.position + d.normalized * 1.5f;
                    Place(cam, pos, look, 42, 0); return true;
                case Shot.Toss:
                    if (!ball) return false;
                    pos = p - fwd * 1.4f - right * .55f + Vector3.up * .42f; look = Vector3.Lerp(p + Vector3.up * 1.2f, ball.position, .7f);
                    Place(cam, pos, look, 48, 0); return true;
                case Shot.Reaction:
                {
                    var who = shotT < 1.15f || !subject2 ? subject : subject2;
                    var wp = who.position; var wf = who.forward; var wr = who.right;
                    float k = shotT < 1.15f ? shotT / 1.15f : (shotT - 1.15f) / 1.15f;
                    pos = wp + wf * Mathf.Lerp(3.0f, 2.5f, k) + wr * .9f + Vector3.up * 1.15f; look = wp + Vector3.up * .85f;
                    Place(cam, pos, look, 38, 0); return true;
                }
                case Shot.Ultimate:
                {
                    // Three deliberate shots: face reveal, racket charge, full-body power pose.
                    if (shotT < .55f) {
                        float k = Mathf.SmoothStep(0,1,shotT/.55f);
                        pos=p+fwd*Mathf.Lerp(1.8f,1.45f,k)+right*.4f+Vector3.up*1.5f;
                        look=p+Vector3.up*1.4f; fov=34;
                    } else if(shotT < 1.35f) {
                        float k=(shotT-.55f)/.8f;
                        pos=p+fwd*2.3f+right*Mathf.Lerp(1.8f,.8f,k)+Vector3.up*.8f;
                        look=p+Vector3.up*1.05f; fov=43;
                    } else {
                        float k=Mathf.SmoothStep(0,1,(shotT-1.35f)/.85f);
                        pos=p+fwd*Mathf.Lerp(3.5f,3,k)-right*1.5f+Vector3.up*.65f;
                        look=p+Vector3.up*1.05f; fov=44;
                    }
                    Place(cam,pos,look,fov,0);
                    return true;
                }
            }
            return false;
        }
        static void Place(Camera cam, Vector3 pos, Vector3 look, float fov, float rollDeg)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up) * Quaternion.Euler(0, 0, rollDeg);
            cam.fieldOfView = fov;
        }
        public void NoteBallVelocity(Vector3 v) { if (v.sqrMagnitude > 1) ballDir = v; }

        /// Rally FOV breath: a gentle pull as the ball approaches the player, a punch on big contact,
        /// and a mild bias toward the far player while the ball is on their side (identity reads).
        public void Apply(ref Vector3 position, ref Vector3 look, ref float fov, float approach, float rivalBias, Vector3 rivalChest)
        {
            // Score80 C: the contact pulse is tiny (<= 1.8 deg) and only for Perfect-class contact
            // Gameplay framing owns a constant FOV; contacts never move or zoom the camera.
            // Rival focus stays in the shoulder composition.
        }
        public void Apply(ref Vector3 position, ref Vector3 look, ref float fov) => Apply(ref position, ref look, ref fov, 0, 0, Vector3.zero);

        // =============================================================== overlay
        void BuildOverlay()
        {
            var go = new GameObject("Spectacle overlay"); go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            RawImage Full(string n, Texture t) { var r = new GameObject(n).AddComponent<RawImage>(); r.transform.SetParent(go.transform, false); r.texture = t; r.raycastTarget = false; var rt = r.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; r.color = new Color(1, 1, 1, 0); return r; }
            vignette = Full("Vignette", VignetteTex()); lines = Full("Speed lines", LinesTex()); flash = Full("Flash", Texture2D.whiteTexture);
            var font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text Label(string n, int size, Vector2 anchor, Vector2 off, Vector2 box)
            {
                var t = new GameObject(n).AddComponent<Text>(); t.transform.SetParent(go.transform, false); t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.color = new Color(1, 1, 1, 0);
                var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = box; rt.anchoredPosition = off;
                foreach (var dd in new[] { new Vector2(3, -3), new Vector2(-3, 3) }) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.05f, .08f, .25f, .9f); o.effectDistance = dd; }
                return t;
            }
            title = Label("Moment title", 80, new Vector2(.5f, .5f), new Vector2(0, 262), new Vector2(1200, 110));
            sub = Label("Moment subtitle", 32, new Vector2(.5f, .5f), new Vector2(0, 200), new Vector2(1200, 56));
            // ultimate meters (equal for both)
            Image Bar(string n, Vector2 anchor, Vector2 off, Color c, out Image back)
            {
                back = new GameObject(n + " back").AddComponent<Image>(); back.transform.SetParent(go.transform, false); back.color = new Color(0, 0, 0, .45f); back.raycastTarget = false;
                var br = back.rectTransform; br.anchorMin = br.anchorMax = anchor; br.pivot = anchor; br.sizeDelta = new Vector2(220, 16); br.anchoredPosition = off;
                var f = new GameObject(n).AddComponent<Image>(); f.transform.SetParent(back.transform, false); f.color = c; f.raycastTarget = false;
                var fr = f.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, .5f); fr.offsetMin = new Vector2(2, 2); fr.offsetMax = new Vector2(2, -2);
                return f;
            }
            pMeter = Bar("You ultimate", new Vector2(0, 0), new Vector2(24, 24), new Color(1, .75f, .2f), out pMeterBack);
            rMeter = Bar("Rival ultimate", new Vector2(1, 1), new Vector2(-24, -64), new Color(.35f, .9f, 1f), out rMeterBack);
            pLabel = Label("You ult label", 20, new Vector2(0, 0), new Vector2(134, 54), new Vector2(240, 26)); pLabel.color = Color.white;
            rLabel = Label("Rival ult label", 20, new Vector2(1, 1), new Vector2(-134, -94), new Vector2(240, 26)); rLabel.color = Color.white;
            pMeterBack.gameObject.SetActive(false); rMeterBack.gameObject.SetActive(false);
            pLabel.gameObject.SetActive(false); rLabel.gameObject.SetActive(false);
            // rival cam inset
            rivalRT = new RenderTexture(320, 320, 16) { name = "Rival cam", antiAliasing = 2 };
            rivalFrame = new GameObject("Rival cam frame").AddComponent<Image>(); rivalFrame.transform.SetParent(go.transform, false); rivalFrame.color = new Color(1, 1, 1, 0); rivalFrame.raycastTarget = false;
            var fr2 = rivalFrame.rectTransform; fr2.anchorMin = fr2.anchorMax = new Vector2(1, 1); fr2.pivot = new Vector2(1, 1); fr2.sizeDelta = new Vector2(212, 212); fr2.anchoredPosition = new Vector2(-18, -96);
            rivalView = new GameObject("Rival cam").AddComponent<RawImage>(); rivalView.transform.SetParent(rivalFrame.transform, false); rivalView.texture = rivalRT; rivalView.raycastTarget = false; rivalView.color = new Color(1, 1, 1, 0);
            var rv = rivalView.rectTransform; rv.anchorMin = Vector2.zero; rv.anchorMax = Vector2.one; rv.offsetMin = new Vector2(5, 5); rv.offsetMax = new Vector2(-5, -5);
            rivalLabel = Label("Rival cam label", 18, new Vector2(1, 1), new Vector2(-124, -300), new Vector2(212, 24));
        }

        void Overlay(Color v, float vA, float linesAlpha, float flashAlpha) { vignetteColor = v; vignetteA = vA; linesA = linesAlpha; flashA = flashAlpha; }
        void Title(string t, string s, float len) {
            if (GetComponent<TennisGame>()?.ScoreOnlyText ?? true) { title.text = sub.text = ""; titleLen = 0; return; }
            title.text = t; sub.text = s; titleT = 0; titleLen = len;
        }

        public void SetMeters(float player, float rival, string ability = "ULTIMATE", bool armed = false)
        {
            if (!pMeter) return;
            pMeter.rectTransform.sizeDelta = new Vector2(216 * Mathf.Clamp01(player), -4); rMeter.rectTransform.sizeDelta = new Vector2(216 * Mathf.Clamp01(rival), -4);
            bool pr = player >= 1, rr = rival >= 1; float blink = .6f + .4f * Mathf.Sin(clock * 8);
            pLabel.text = ability.ToUpperInvariant() + (armed ? " · ARMED" : pr ? " · READY" : ""); rLabel.text = rr ? "RIVAL ULTIMATE READY!" : "RIVAL ULTIMATE";
            pLabel.color = new Color(1, pr ? .85f : 1, pr ? .3f : 1, pr ? blink : .8f); rLabel.color = new Color(rr ? .5f : 1, 1, 1, rr ? blink : .8f);
        }

        /// The far player, up close: shown while the ball is on their side (they prepare, swing, react).
        public void UpdateRivalCam(bool show, Transform rival, string name)
        {
            if (!rival) return;
            show &= !(GetComponent<TennisGame>()?.ScoreOnlyText ?? true);
            if (!rivalCam)
            {
                rivalCam = new GameObject("Rival cam camera").AddComponent<Camera>(); rivalCam.transform.SetParent(transform, false);
                rivalCam.targetTexture = rivalRT; rivalCam.fieldOfView = 34; rivalCam.nearClipPlane = .1f; rivalCam.farClipPlane = 60; rivalCam.enabled = false;
            }
            rivalShow = Mathf.MoveTowards(rivalShow, show && shot != Shot.Ultimate && shot != Shot.Reaction ? 1 : 0, Udt * 5);
            rivalLabel.text = "RIVAL CAM · " + name;
            if (rivalShow > .01f)
            {
                var p = rival.position; var f = rival.forward;
                rivalCam.transform.position = p + f * 3.0f + rival.right * .7f + Vector3.up * 1.15f; rivalCam.transform.LookAt(p + Vector3.up * .8f);
                rivalCam.Render();
            }
        }

        void UpdateOverlay(float udt)
        {
            if (GetComponent<TennisGame>()?.ScoreOnlyText ?? true) { title.text = sub.text = rivalLabel.text = ""; titleLen = 0; rivalShow = 0; }
            bool active = shot == Shot.Cut || shot == Shot.Ultimate || shot == Shot.BallTrack;
            float tv = active ? vignetteA : 0, tl = active ? linesA : 0;
            var vc = vignette.color; vc = Color.Lerp(vc, new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, tv), 1 - Mathf.Exp(-udt * 18)); vignette.color = vc;
            var rayColor = shot == Shot.Ultimate ? ultimateColor : Color.white;
            lines.color = new Color(rayColor.r, rayColor.g, rayColor.b, Mathf.Lerp(lines.color.a, tl * .8f, 1 - Mathf.Exp(-udt * 18)));
            lines.rectTransform.localRotation = Quaternion.Euler(0, 0, clock * 90);
            flashA = Mathf.MoveTowards(flashA, 0, udt * 4f); flash.color = new Color(1, 1, 1, flashA * .7f);
            titleT += udt; float ta = titleT < titleLen ? Mathf.Min(1, titleT / .08f) * Mathf.Clamp01((titleLen - titleT) / .2f) : 0;
            float sc = 1 + .25f * Mathf.Clamp01(1 - titleT / .15f);
            title.color = new Color(1, .93f, .45f, ta); sub.color = new Color(1, 1, 1, ta); title.rectTransform.localScale = Vector3.one * sc;
            rivalFrame.color = new Color(1, 1, 1, .9f * rivalShow); rivalView.color = new Color(1, 1, 1, rivalShow); rivalLabel.color = new Color(1, 1, 1, rivalShow);
            bool meters = TennisAbilities.UltimatesEnabled && shot != Shot.Reaction && shot != Shot.Ultimate;
            pMeterBack.gameObject.SetActive(meters); rMeterBack.gameObject.SetActive(meters); pLabel.gameObject.SetActive(meters); rLabel.gameObject.SetActive(meters); float ma = meters ? 1 : 0;
            pMeterBack.color = new Color(0, 0, 0, .45f * ma); rMeterBack.color = new Color(0, 0, 0, .45f * ma);
        }

        static Texture2D VignetteTex()
        {
            const int N = 128; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + .5f) / N * 2 - 1, dy = (y + .5f) / N * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((r - .45f) / .75f); t.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            t.Apply(); return t;
        }
        static Texture2D LinesTex()
        {
            const int N = 256; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(7); var rays = new float[72]; for (int i = 0; i < rays.Length; i++) rays[i] = (float)rng.NextDouble();
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + .5f) / N * 2 - 1, dy = (y + .5f) / N * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = (Mathf.Atan2(dy, dx) / (2 * Mathf.PI) + .5f) * rays.Length; int i = Mathf.FloorToInt(ang) % rays.Length; float f = ang - Mathf.Floor(ang);
                float ray = rays[i] > .55f ? Mathf.Clamp01(1 - Mathf.Abs(f - .5f) * 2 / (.15f + rays[i] * .25f)) : 0;
                float a = ray * Mathf.Clamp01((r - .5f) / .4f); t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply(); return t;
        }
    }
}
