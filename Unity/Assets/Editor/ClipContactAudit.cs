using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Finds each stroke clip's real contact frame: the moment the racket head is moving
    /// fastest through the hitting zone. Every stroke used to be assumed to strike the ball
    /// exactly halfway through its clip, whatever the clip actually animates.
    ///
    ///   Unity -batchmode -executeMethod GolfArcade.EditorTools.ClipContactAudit.Run -quit
    ///
    /// Writes Assets/Resources/Tennis/StrokeContacts.json, which TennisActor loads.
    public static class ClipContactAudit
    {
        const string Output = "Assets/Resources/Tennis/StrokeContacts.json";
        static readonly Dictionary<string, float> VideoContact = new Dictionary<string, float> { { "Forehand", .55f }, { "Backhand", .55f } };
        static readonly string[] Strokes =
        {
            "Forehand", "Backhand", "RunningForehand", "RunningBackhand", "LowPickup", "DiveForehand", "DiveBackhand",
            "Serve", "Smash", "VolleyForehand", "VolleyBackhand", "Lob", "ForehandTopspin", "Slice", "MissedSwing",
        };

        [MenuItem("Golf Arcade/Tennis/Measure stroke contact frames")]
        public static void Run()
        {
            const string path = "Assets/Resources/StandardCharacters/standard_male_tennis.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            var rig = Object.Instantiate(prefab);
            var sweet = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "TennisSweetSpot");
            var entries = new List<string>();
            var report = new StringBuilder();
            foreach (var clip in clips)
            {
                string logical = clip.name.EndsWith("_RH") || clip.name.EndsWith("_LH") ? clip.name.Substring(0, clip.name.Length - 3) : clip.name;
                if (!Strokes.Contains(logical)) continue;
                const int samples = 240;
                // Overheads strike the ball at full stretch -- the racket's highest point --
                // which comes just after its speed peaks on the way up. Groundstrokes strike
                // at peak racket speed.
                bool overhead = logical == "Serve" || logical == "Smash";
                // A volley is a block, not a swing: it meets the ball at the end of a short
                // punch, where the racket is furthest out in front. Its fastest moment is the
                // recovery afterwards.
                bool volley = logical.StartsWith("Volley");
                float furthest = float.MinValue, furthestT = .5f;
                float highest = float.MinValue, highestT = .5f;
                Vector3 previous = Vector3.zero; float best = 0, bestT = .5f;
                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    clip.SampleAnimation(rig, t * clip.length);
                    Vector3 p = rig.transform.InverseTransformPoint(sweet.position);
                    if (overhead && t >= .28f && t <= .7f && p.y > highest) { highest = p.y; highestT = t; }
                    if (volley && t >= .28f && t <= .7f && -p.z > furthest) { furthest = -p.z; furthestT = t; }   // the model faces -Z
                    if (i > 0 && t >= .28f && t <= .78f)
                    {
                        float speed = (p - previous).magnitude * samples / clip.length;
                        if (speed > best) { best = speed; bestT = t; }
                    }
                    previous = p;
                }
                if (overhead) { report.AppendLine($"OVERHEAD {clip.name} fastest={bestT:0.000} highest={highestT:0.000} at {highest:0.00}m"); bestT = highestT; }
                // Neither speed nor reach finds it (the ready stance already holds the racket
                // head further out than the punch does), so volleys use their authored contact:
                // frame 30 of 60 (blender/scripts/author_tennis_motion.py).
                if (volley) { report.AppendLine($"VOLLEY {clip.name} fastest={bestT:0.000} furthest={furthestT:0.000} authored=0.500"); bestT = .5f; }
                // The groundstrokes taken from the gameplay video are laid out so the video's
                // contact frame lands on frame 33 of 60 (blender/scripts/retarget_video_pose.py
                // --timeline); the reconstructed racket can sweep fastest elsewhere.
                if (VideoContact.TryGetValue(logical, out float laid)) { report.AppendLine($"VIDEO {clip.name} fastest={bestT:0.000} laid out={laid:0.000}"); bestT = laid; }
                entries.Add($"{{\"clip\":\"{clip.name}\",\"contact\":{bestT:0.000}}}");
                report.AppendLine($"CONTACT {clip.name} t={bestT:0.000} peak={best:0.0}m/s");
            }
            // Backhand torso: how much further the chest turns than in the forehand, at the
            // points the runtime plays. Positive is the same direction the runtime coil used.
            var chest = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Chest");
            // Shoulder-line yaw: 0 = square to the net; positive = the right shoulder has come
            // forward (a turn to the LEFT, seen from above), which is a right-hander's
            // backhand preparation. A forehand preparation goes negative.
            var bones = rig.GetComponentsInChildren<Transform>(true);
            var left = bones.First(t => t.name == "UpperArm.L"); var right = bones.First(t => t.name == "UpperArm.R");
            float Shoulders(string name, float phase)
            {
                var clip = clips.FirstOrDefault(c => c.name == name); if (!clip) return float.NaN;
                clip.SampleAnimation(rig, phase * clip.length);
                Vector3 line = rig.transform.InverseTransformDirection(right.position - left.position); line.y = 0;
                return Mathf.Atan2(line.z, line.x) * Mathf.Rad2Deg;
            }
            foreach (var stroke in new[] { "Forehand_RH", "Backhand_RH", "Forehand_LH", "Backhand_LH", "Ready_RH" })
            {
                var line = new StringBuilder($"SHOULDERS {stroke}:");
                foreach (float phase in new[] { .10f, .20f, .30f, .40f, .50f, .60f, .70f, .85f })
                    line.Append($" {phase:0.00}={Shoulders(stroke, phase):0}");
                report.AppendLine(line.ToString());
            }
            // Run stride: during stance a foot moves backward under the body at the clip's own
            // ground speed. Stride = that speed x the cycle length, which is what the
            // gameplay cycle rate must use for the feet not to slide.
            foreach (var runName in new[] { "RunLeft_RH", "RunRight_RH" })
            {
                var run = clips.FirstOrDefault(c => c.name == runName); if (!run) continue;
                foreach (var footName in new[] { "Foot.L", "Foot.R" })
                {
                    var foot = bones.First(t => t.name == footName);
                    const int n = 240; var xs = new float[n + 1]; var ys = new float[n + 1];
                    for (int i = 0; i <= n; i++)
                    {
                        run.SampleAnimation(rig, i / (float)n * run.length);
                        // As the game plays it: the Root bone's lateral travel is removed.
                        var rootBone = bones.First(t => t.name == "Root");
                        Vector3 p = rig.transform.InverseTransformPoint(foot.position) - new Vector3(rootBone.localPosition.x, 0, 0);
                        xs[i] = p.x; ys[i] = p.y;
                    }
                    var trace = new StringBuilder($"FOOTPATH {runName} {footName}:");
                    for (int i = 0; i <= n; i += 8) trace.Append($" ({xs[i]:0.00},{ys[i]:0.00})");
                    report.AppendLine(trace.ToString());
                    float low = ys.Min(); float speedSum = 0; int stanceSamples = 0;
                    for (int i = 1; i <= n; i++)
                        if (ys[i] < low + .01f && ys[i - 1] < low + .01f) { speedSum += (xs[i] - xs[i - 1]) * n / run.length; stanceSamples++; }
                    float ground = stanceSamples > 0 ? speedSum / stanceSamples : 0;
                    report.AppendLine($"STRIDE {runName} {footName} stance={stanceSamples * 100 / n}% footSpeedInStance={ground:0.00}m/s cycle={run.length:0.000}s travelPerCycle={-ground * run.length:0.00}m");
                }
            }
            foreach (var runName in new[] { "RunLeft_RH", "RunRight_RH", "Retreat_RH", "Ready_RH" })
            {
                var run = clips.FirstOrDefault(c => c.name == runName); if (!run) continue;
                var hipsBone = bones.First(t => t.name == "Hips"); var rootBone = bones.First(t => t.name == "Root");
                float lean = 0, shoulder = 0; Vector3 rootStart = Vector3.zero, rootEnd = Vector3.zero;
                for (int i = 0; i <= 20; i++)
                {
                    run.SampleAnimation(rig, i / 20f * run.length);
                    if (i == 0) rootStart = rootBone.localPosition; if (i == 20) rootEnd = rootBone.localPosition;
                    Vector3 line = rig.transform.InverseTransformDirection(right.position - left.position); line.y = 0;
                    shoulder += Mathf.Atan2(line.z, line.x) * Mathf.Rad2Deg / 21;
                    Vector3 up = rig.transform.InverseTransformDirection(bones.First(t => t.name == "Head").position - hipsBone.position);
                    lean += Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg / 21;
                }
                report.AppendLine($"TORSO {runName} shoulderYaw={shoulder:0} sideLean={lean:0} (positive = leaning toward +x) rootTravel={(rootEnd - rootStart).x:0.00}m");
            }
            Object.DestroyImmediate(rig);
            File.WriteAllText(Output, "{\"clips\":[\n" + string.Join(",\n", entries) + "\n]}\n");
            AssetDatabase.ImportAsset(Output);
            Debug.Log(report.ToString());
        }
    }
}
