using UnityEngine;
using UnityEngine.Video;

namespace GolfArcade.Hub
{
    /// A bay's big screen (PLAN_MenuHub_WalkableWorld §3): a few seconds of our own gameplay for that mode, looping through the
    /// glitch shader like a stylised memory. It plays only while you are in its room, calms down as the match loads behind the
    /// plaza (you are seated in this bay), and flashes when its clip changes. Clips: Resources/Hub/Previews (captured from the game).
    public sealed class HubBayScreen : MonoBehaviour
    {
        public string bayId, place, clipName;
        VideoPlayer player; RenderTexture target; Material material; float flash = 1;
        public bool Playing => player && player.isPlaying;
        public Material Material => material;

        public static string ClipFor(string bayId) => bayId switch
        {
            "bay-tennis-exhibition" => "tennis_resort",
            "bay-tennis-campaign" => "tennis_volcano",
            "bay-tennis-training" => "tennis_skyscraper",
            "bay-tennis-online" => "tennis_resort",
            "bay-golf-round" => "golf_cliffside",
            "bay-golf-online" => "golf_wildisles",
            "bay-golf-pass" => "golf_magma",
            _ => null
        };

        /// Puts the player on a screen renderer. Without the clip (or the shader) the screen keeps its plain glow.
        public static HubBayScreen Attach(Renderer screen, HubLayout.Spot bay)
        {
            var source = Resources.Load<Material>("Hub/HubGlitch");
            string name = ClipFor(bay.id);
            var clip = name != null ? Resources.Load<VideoClip>("Hub/Previews/" + name) : null;
            if (!source || !source.shader || !source.shader.isSupported) return null;
            var s = screen.gameObject.AddComponent<HubBayScreen>();
            s.bayId = bay.id; s.place = bay.place; s.clipName = name;
            s.material = new Material(source) { name = "Bay screen " + bay.id };
            screen.sharedMaterial = s.material;
            s.target = new RenderTexture(640, 360, 0, RenderTextureFormat.ARGB32) { name = "Bay preview " + bay.id };
            s.material.SetTexture("_MainTex", s.target);
            if (clip)
            {
                var p = s.player = screen.gameObject.AddComponent<VideoPlayer>();
                p.playOnAwake = false; p.isLooping = true; p.skipOnDrop = true; p.clip = clip;
                p.renderMode = VideoRenderMode.RenderTexture; p.targetTexture = s.target;
                p.audioOutputMode = VideoAudioOutputMode.None; p.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                p.time = (bay.id.GetHashCode() & 0xff) / 255f * clip.length;   // the bays in a room do not loop in step
            }
            return s;
        }

        /// A new clip (the venue picked on the phone): the screen bursts, then plays it.
        public void Show(string clip)
        {
            if (clip == clipName || !player) return;
            var c = Resources.Load<VideoClip>("Hub/Previews/" + clip); if (!c) return;
            clipName = clip; player.clip = c; flash = 1; if (Here) player.Play();
        }

        bool Here { get { var w = HubWorld.Instance; return w && w.gameObject.activeInHierarchy && w.Place == place; } }

        void Update()
        {
            var w = HubWorld.Instance; bool here = Here;
            if (player)
            {
                if (here && !player.isPlaying) player.Play();
                else if (!here && player.isPlaying) player.Pause();
            }
            float glitch = .5f;
            if (w && w.Station != null && w.Station.id == bayId && w.BayProgress >= 0) glitch = Mathf.Lerp(.5f, .05f, w.BayProgress);
            flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 2.2f);
            material.SetFloat("_Glitch", glitch); material.SetFloat("_Flash", flash);
        }

        void OnDestroy() { if (target) { target.Release(); Destroy(target); } if (material) Destroy(material); }
    }
}
