using System;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// A hole's signature shot, as animated in Blender: the ball's path and the camera that
    /// follows it, every frame, out of Resources/Course/hole_NN_shot.json (written by
    /// blender/scripts/hole12_animate_finish.py). The game plays it as the second half of the
    /// hole's intro, after the aerial — the designed shot over the water, seen from the tee,
    /// chased through the air, and watched in from behind the cup.
    ///
    /// The file is in the Blender scene's metres. The mapping onto the course is solved from
    /// reference empties the file names and the placed model carries (tee, cup, up, a landing
    /// point): four points fix the affine map exactly, whatever the importer did with axes.
    public sealed class SignatureShot
    {
        [Serializable]
        class Data
        {
            public int fps, frames, start, rest;
            public float ballRadius, sensorWidth, lens;
            public int[] landings;
            public string[] refNames;
            public float[] refPoints, ball, cam;
        }

        readonly Data data;
        Matrix4x4 toWorld;          // scene metres → world yards, affine
        Vector3 up;                 // the scene's up, in the world, per metre

        /// Seconds the shot runs, from its start frame.
        public float Duration => (data.frames - data.start) / (float)data.fps;
        /// Horizontal field of view the shot was framed with, degrees.
        public float HorizontalFov => 2f * Mathf.Atan(data.sensorWidth / (2f * data.lens)) * Mathf.Rad2Deg;
        /// Seconds into the shot at which the ball meets the ground (the landing and each bounce).
        public float[] Landings { get; private set; }

        SignatureShot(Data data) { this.data = data; }

        /// The hole's shot bound to its placed model, or null when the hole has none.
        public static SignatureShot Load(int holeNumber, HoleView view)
        {
            var text = Resources.Load<TextAsset>($"Course/hole_{holeNumber:00}_shot");
            if (!text || !view) return null;
            var data = JsonUtility.FromJson<Data>(text.text);
            if (data == null || data.ball == null || data.ball.Length < data.frames * 3 || data.refNames == null || data.refNames.Length < 4) return null;
            var shot = new SignatureShot(data);
            return shot.Bind(view) ? shot : null;
        }

        /// Solve world = A·(p − p0) + w0 from the four reference points.
        bool Bind(HoleView view)
        {
            var scene = new Vector3[4]; var world = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                scene[i] = new Vector3(data.refPoints[i * 3], data.refPoints[i * 3 + 1], data.refPoints[i * 3 + 2]);
                var node = view.ModelNode(data.refNames[i]);
                if (!node) { Debug.LogWarning($"Signature shot: the course model has no {data.refNames[i]}"); return false; }
                world[i] = node.position;
            }
            var P = Matrix4x4.identity; var W = Matrix4x4.identity;
            for (int i = 1; i < 4; i++)
            {
                P.SetColumn(i - 1, scene[i] - scene[0]);
                W.SetColumn(i - 1, world[i] - world[0]);
            }
            if (Mathf.Abs(P.determinant) < 1e-6f) { Debug.LogWarning("Signature shot: reference points are coplanar"); return false; }
            var A = W * P.inverse;                       // linear part, 3×3 in the top-left
            toWorld = A;
            toWorld.SetColumn(3, new Vector4(0, 0, 0, 1));
            var shift = world[0] - toWorld.MultiplyPoint3x4(scene[0]);
            toWorld.SetColumn(3, new Vector4(shift.x, shift.y, shift.z, 1));
            up = toWorld.MultiplyVector(Vector3.forward);   // Blender +Z is up; MultiplyVector ignores the shift
            Landings = Array.ConvertAll(data.landings ?? Array.Empty<int>(), f => (f - data.start) / (float)data.fps);
            return true;
        }

        public Vector3 ToWorld(Vector3 sceneMetres) => toWorld.MultiplyPoint3x4(sceneMetres);

        float Frame(float seconds) => Mathf.Clamp(data.start - 1 + seconds * data.fps, 0, data.frames - 1);

        Vector3 Sample(float[] flat, int stride, int offset, float frame)
        {
            int i = Mathf.FloorToInt(frame); float f = frame - i;
            int j = Mathf.Min(i + 1, data.frames - 1);
            Vector3 At(int k) => new(flat[k * stride + offset], flat[k * stride + offset + 1], flat[k * stride + offset + 2]);
            return Vector3.LerpUnclamped(At(i), At(j), f);
        }

        /// Where the ball's underside touches, `seconds` into the shot — the file's ball is a
        /// big presentation ball, so its radius comes off and the game's own goes back on.
        public Vector3 BallAt(float seconds, float gameBallRadius)
        {
            var centre = Sample(data.ball, 3, 0, Frame(seconds));
            return ToWorld(centre - Vector3.forward * data.ballRadius) + up.normalized * gameBallRadius;
        }

        public void CameraAt(float seconds, out Vector3 position, out Vector3 lookAt)
        {
            float frame = Frame(seconds);
            position = ToWorld(Sample(data.cam, 6, 0, frame));
            lookAt = ToWorld(Sample(data.cam, 6, 3, frame));
        }
    }
}
