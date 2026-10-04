using UnityEngine;

namespace GolfArcade.Tennis
{
    public static class TennisShoulderCamera
    {
        public static void Frame(Vector3 player, bool leftHanded, Vector3 ball, bool overhead, float aspect,
            out Vector3 position, out Vector3 look, out float fov)
        {
            float shoulder = leftHanded ? -1 : 1;
            bool portrait = aspect < 1;
            position = player + new Vector3(shoulder * .8f, portrait ? 2.4f : 2.2f, portrait ? -4.8f : -4f);
            float lift = overhead ? Mathf.Clamp((ball.y - 2.5f) * .18f, 0, .65f) : 0;
            look = new Vector3(player.x * .78f + shoulder * .85f, .95f + lift, player.z + 12);
            fov = portrait ? 66 : 62;
        }
    }
}
