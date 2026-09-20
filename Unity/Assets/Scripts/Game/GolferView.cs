using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.Game
{
    /// A Mii-simple golfer: body, head, arms and a club that mirrors the phone. The club draws
    /// back with the backswing load and whips through on impact, so what the player does with
    /// their hands is what they see on the screen.
    public sealed class GolferView : MonoBehaviour
    {
        Transform pivot;      // shoulders: the club rotates about here
        Transform club;
        Transform body;
        float shownLoad;
        float swingThrough = -1; // seconds since impact; negative = not swinging through
        float rest = 20f;        // degrees the club hangs forward at address

        public static GolferView Create(Transform parent)
        {
            var go = new GameObject("Golfer");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GolferView>();
            v.BuildFigure();
            return v;
        }

        void BuildFigure()
        {
            var skin = new Color(0.95f, 0.8f, 0.65f);
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

        /// Stand beside the ball, facing across the aim line (a right-hander stands to the
        /// ball's left as seen from behind).
        public void Stand(Vector3 ball, Vector3 aimDirection)
        {
            var side = Vector3.Cross(Vector3.up, aimDirection).normalized; // right of the line
            transform.position = ball - side * 0.75f;
            transform.rotation = Quaternion.LookRotation(side, Vector3.up);
        }

        public void SetVisible(bool on) => gameObject.SetActive(on);

        public void ShowLoad(float load) => shownLoad = load;

        public void Strike() => swingThrough = 0;

        public void Settle() { swingThrough = -1; shownLoad = 0; }

        void Update()
        {
            float angle;
            if (swingThrough >= 0)
            {
                swingThrough += Time.deltaTime;
                // From the top, through the ball, up to a finish, in a third of a second.
                float t = Mathf.Clamp01(swingThrough / 0.35f);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                angle = Mathf.Lerp(-(rest + shownLoad * 150f), 170f, eased);
                if (t >= 1 && swingThrough > 1.2f) { swingThrough = -1; shownLoad = 0; }
            }
            else
            {
                angle = -(rest + shownLoad * 150f);
            }
            // The club swings in the plane facing the golfer, i.e. about the golfer's forward axis.
            pivot.localRotation = Quaternion.AngleAxis(-angle + rest, Vector3.forward) * Quaternion.AngleAxis(25f, Vector3.right);
            float turn = swingThrough >= 0 ? Mathf.Lerp(-shownLoad * 30f, 40f, Mathf.Clamp01(swingThrough / 0.35f)) : -shownLoad * 30f;
            body.localRotation = Quaternion.AngleAxis(turn, Vector3.up);
        }
    }
}
