using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Records the last few seconds of every rendered frame -- both players' full skeletons,
    /// their rackets and the ball -- and plays a winner back in slow motion from a low
    /// camera from the umpire's chair. Storage is fixed at build time; recording never allocates.
    public sealed class TennisReplay : MonoBehaviour
    {
        public const int Frames = 200;          // ~3.3s at 60fps
        public const float Slowdown = .45f;     // playback speed
        Transform[] tracked;
        Vector3[] positions;
        Quaternion[] rotations;
        float[] stamps;
        int head, recorded;
        int transformsPerFrame;
        public bool Playing { get; private set; }
        float playTime, startStamp, endStamp;
        Transform ball;
        Action<bool> onPose;
        Camera cam;
        Vector3 cameraRestPosition; Quaternion cameraRestRotation;
        /// The live frame at the moment playback started, restored afterwards so play resumes
        /// exactly where it was rather than from the replay's last frame.
        Vector3[] livePositions; Quaternion[] liveRotations;

        /// `roots` are recorded in full (every child transform), in world space.
        public void Build(IList<Transform> roots, Transform ballTransform, Camera camera, Action<bool> refresh)
        {
            var list = new List<Transform>();
            foreach (var r in roots) list.AddRange(r.GetComponentsInChildren<Transform>(true));
            list.Add(ballTransform);
            tracked = list.ToArray();
            transformsPerFrame = tracked.Length;
            positions = new Vector3[Frames * transformsPerFrame];
            rotations = new Quaternion[Frames * transformsPerFrame];
            stamps = new float[Frames];
            livePositions = new Vector3[transformsPerFrame]; liveRotations = new Quaternion[transformsPerFrame];
            head = 0; recorded = 0;
            ball = ballTransform; cam = camera; onPose = refresh;
        }

        /// Capture the frame as rendered. Called after the players have been posed.
        public void Record(float time)
        {
            if (Playing || tracked == null) return;
            int offset = head * transformsPerFrame;
            for (int i = 0; i < transformsPerFrame; i++)
            {
                var t = tracked[i];
                if (!t) continue;
                positions[offset + i] = t.position; rotations[offset + i] = t.rotation;
            }
            stamps[head] = time;
            head = (head + 1) % Frames; recorded = Mathf.Min(Frames, recorded + 1);
        }

        /// Play back the last `seconds` of recorded play, ending `trim` seconds before now.
        public bool Play(float seconds, float trim = 0)
        {
            if (recorded < 20 || tracked == null) return false;
            int newest = (head - 1 + Frames) % Frames;
            endStamp = stamps[newest] - trim;
            startStamp = Mathf.Max(stamps[(head - recorded + Frames) % Frames], endStamp - seconds);
            playTime = startStamp;
            Playing = true;
            cameraRestPosition = cam.transform.position; cameraRestRotation = cam.transform.rotation;
            for (int i = 0; i < transformsPerFrame; i++)
                if (tracked[i]) { livePositions[i] = tracked[i].position; liveRotations[i] = tracked[i].rotation; }
            var trail = ball ? ball.GetComponent<TrailRenderer>() : null; if (trail) trail.Clear();
            onPose?.Invoke(true);
            return true;
        }

        public void Stop()
        {
            if (!Playing) return;
            Playing = false;
            cam.transform.SetPositionAndRotation(cameraRestPosition, cameraRestRotation);
            for (int i = 0; i < transformsPerFrame; i++)
                if (tracked[i]) tracked[i].SetPositionAndRotation(livePositions[i], liveRotations[i]);
            var trail = ball ? ball.GetComponent<TrailRenderer>() : null; if (trail) trail.Clear();
            onPose?.Invoke(false);
        }

        /// Advance playback. Returns false once finished.
        public bool Tick(float dt)
        {
            if (!Playing) return false;
            playTime += dt * Slowdown;
            if (playTime >= endStamp) { Stop(); return false; }
            // Find the two recorded frames either side of the playback time.
            int newest = (head - 1 + Frames) % Frames;
            int b = newest;
            for (int n = 0; n < recorded; n++)
            {
                int i = (newest - n + Frames) % Frames;
                if (stamps[i] <= playTime) { b = i; break; }
            }
            int a = b, c = (b + 1) % Frames;
            if (c == head) c = b;
            float span = stamps[c] - stamps[a];
            float t = span > 1e-5f ? Mathf.Clamp01((playTime - stamps[a]) / span) : 0;
            int oa = a * transformsPerFrame, oc = c * transformsPerFrame;
            for (int i = 0; i < transformsPerFrame; i++)
            {
                var tr = tracked[i]; if (!tr) continue;
                tr.SetPositionAndRotation(Vector3.Lerp(positions[oa + i], positions[oc + i], t),
                                          Quaternion.Slerp(rotations[oa + i], rotations[oc + i], t));
            }
            onPose?.Invoke(true);
            // The umpire's view: high in the chair at the net, looking down the court and
            // turning to follow the ball, so the whole point reads from above.
            Vector3 focus = ball.position;
            Vector3 eye = TennisUmpire.Seat + new Vector3(.7f, 2.1f, Mathf.Clamp(focus.z * .12f, -1.5f, 1.5f));
            cam.transform.position = Vector3.Lerp(cam.transform.position, eye, 1 - Mathf.Exp(-dt * 4));
            var look = Quaternion.LookRotation(new Vector3(focus.x * .6f, Mathf.Max(.4f, focus.y * .5f), focus.z) - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, 1 - Mathf.Exp(-dt * 6));
            return true;
        }
    }
}
