using GolfArcade.Net;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The first thing in the scene. On a desktop this machine is the screen, so the game
    /// starts at once (and listens for a phone to be its club). On a phone the player chooses:
    /// play right here, or be the club for the game on the Mac.
    public sealed class AppLauncher : MonoBehaviour
    {
        public enum Mode { Ask, Play, Controller }

        [Tooltip("The inactive child that holds GolfGame; activated when playing here.")]
        public GameObject Game;
        [Tooltip("Skip the menu (tests, editor).")]
        public Mode Startup = Mode.Ask;

        GameObject menu;

        void Awake()
        {
            var mode = Startup;
            if (NativeSportsSession.Active) mode = Mode.Play;
            if (mode == Mode.Ask && !Application.isMobilePlatform) mode = Mode.Play;
            switch (mode)
            {
                case Mode.Play: Play(); break;
                case Mode.Controller: Controller(); break;
                default: ShowMenu(); break;
            }
        }

        void Play()
        {
            if (Game) Game.SetActive(true);
        }

        void Controller()
        {
            PhoneController.Create();
        }

        void ShowMenu()
        {
            menu = new GameObject("Mode menu");
            UiKit.Canvas(menu);
            var root = menu.transform;
            UiKit.Panel(root, "Backdrop", new Color(0.07f, 0.2f, 0.1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform.offsetMax = Vector2.zero;
            var title = UiKit.Label(root, "Title", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 520), new Vector2(1000, 120));
            title.text = "GOLF ARCADE";
            var sub = UiKit.Label(root, "Sub", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 420), new Vector2(1000, 60));
            sub.text = "Swing the phone like a club"; sub.color = new Color(0.85f, 0.95f, 1f);

            var play = UiKit.Button(root, "PLAY ON THIS PHONE", new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(820, 180), 44, new Color(0.35f, 0.75f, 0.35f, 0.9f));
            var playHint = UiKit.Label(root, "PlayHint", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 50));
            playHint.text = "The hole, the buttons and the swing all on this screen";
            var club = UiKit.Button(root, "USE AS CLUB FOR THE MAC", new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(820, 180), 44, new Color(0.3f, 0.55f, 0.9f, 0.9f));
            var clubHint = UiKit.Label(root, "ClubHint", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(900, 50));
            clubHint.text = "The game on the big screen, this phone in your hands — Wii style";

            play.Pressed = () => { Haptics.Tick(); Destroy(menu); Play(); };
            club.Pressed = () => { Haptics.Tick(); Destroy(menu); Controller(); };
        }
    }
}
