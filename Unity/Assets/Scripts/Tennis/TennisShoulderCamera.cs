using UnityEngine;

namespace GolfArcade.Tennis
{
    public static class TennisShoulderCamera
    {
        public static void Frame(Vector3 player, bool leftHanded, Vector3 ball, bool overhead, float aspect,
            out Vector3 position, out Vector3 look, out float fov)
        {
            // Restore the elevated court view used before the close shoulder camera.
            float lead = Mathf.Clamp(player.x, -5f, 5f);
            position = new Vector3(lead * .45f, 7.0f, player.z - 7.8f);
            float lift = overhead ? Mathf.Clamp((ball.y - 2.5f) * .18f, 0, .65f) : 0;
            look = new Vector3(lead * .26f, .95f + lift, player.z + 7.2f);
            fov = aspect < 1 ? 49 : 46;
        }
    }
}
