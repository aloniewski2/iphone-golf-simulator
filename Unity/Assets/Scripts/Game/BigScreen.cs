using System.Runtime.InteropServices;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The picture on a Mac or TV, the phone in the hand as the club. When the phone is
    /// mirroring (AirPlay Screen Mirroring, or a cable) iOS gives the game a second display:
    /// the course camera moves there, in landscape framing, and the whole phone becomes the
    /// controller sheet (UI.ControllerSheet), like the Wii's TV and remote.
    public sealed class BigScreen : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GolfAirPlay_ShowPicker();
#else
        static void GolfAirPlay_ShowPicker() { }
#endif
        const float PhoneFov = 60f, BigFov = 42f;

        Camera course;
        public bool Wanted { get; private set; }
        public bool Active { get; private set; }
        /// The phone-side layout without a second display (the editor, reviews): the controller
        /// sheet comes in over the course.
        public bool Preview { get; private set; }
        /// Fired when the controller layout should come or go.
        public System.Action<bool> OnChanged;

        public static bool DisplayAvailable => Display.displays.Length > 1;
        /// The course is really on another screen (not a preview).
        public bool Live => Active && !Preview;

        public static BigScreen Create(Transform parent, Camera courseCamera)
        {
            var go = new GameObject("Big screen");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BigScreen>();
            b.course = courseCamera;
            b.Wanted = PlayerPrefs.GetInt("bigscreen", 0) == 1;
            return b;
        }

        /// Opens the system AirPlay picker (sound to the Mac); the picture follows once
        /// Screen Mirroring is on.
        public void OpenAirPlayPicker() => GolfAirPlay_ShowPicker();

        public void SetWanted(bool on)
        {
            Wanted = on;
            PlayerPrefs.SetInt("bigscreen", on ? 1 : 0); PlayerPrefs.Save();
            Apply(Wanted && (DisplayAvailable || Preview));
        }

        public void SetPreview(bool on)
        {
            Preview = on;
            Wanted = on || PlayerPrefs.GetInt("bigscreen", 0) == 1;
            Apply(Wanted && (DisplayAvailable || Preview));
        }

        public string Status => !Wanted ? "Off — the game plays on the phone."
            : Active ? "On — the course is on the big screen; this phone is your club."
            : "Waiting for a screen: Control Center → Screen Mirroring → your Mac, then the course moves there.";

        void Update()
        {
            bool on = Wanted && (DisplayAvailable || Preview);
            if (on != Active) Apply(on);
        }

        void Apply(bool on)
        {
            bool was = Active;
            Active = on;
            course.rect = new Rect(0, 0, 1, 1);
            if (on && DisplayAvailable && !Preview)
            {
                var d = Display.displays[1];
                if (!d.active) d.Activate();
                course.targetDisplay = 1;
                course.fieldOfView = BigFov;
            }
            else
            {
                // off, or a preview: the course stays on the phone (under the sheet, in a preview)
                course.targetDisplay = 0;
                course.fieldOfView = PhoneFov;
            }
            if (was != on) OnChanged?.Invoke(on);
        }
    }
}
