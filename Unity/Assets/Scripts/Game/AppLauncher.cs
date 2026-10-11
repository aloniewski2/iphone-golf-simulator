using UnityEngine;

namespace GolfArcade.Game
{
    /// The first thing in the scene: it starts the game (the inactive child that holds GolfGame).
    public sealed class AppLauncher : MonoBehaviour
    {
        [Tooltip("The inactive child that holds GolfGame; activated at start.")]
        public GameObject Game;

        void Awake()
        {
            if (Game) Game.SetActive(true);
        }
    }
}
