using UnityEngine;
namespace GolfArcade.Tennis
{
    // The imported source stays shared; this instance owns its generated material.
    internal sealed class TennisHorizonMaterialOwner : MonoBehaviour
    {
        public Material Material;
        void OnDestroy() { if (Material) Destroy(Material); }
    }
}
