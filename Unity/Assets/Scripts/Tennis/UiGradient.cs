using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Vertical two-colour gradient for any UI graphic (sliced panels and text alike), by
    /// tinting vertices by their height. Colours multiply with the graphic's own colour.
    public sealed class UiGradient : BaseMeshEffect
    {
        public Color Top = Color.white, Bottom = Color.white;

        public static UiGradient On(Graphic graphic, Color top, Color bottom)
        {
            var g = graphic.gameObject.GetComponent<UiGradient>() ?? graphic.gameObject.AddComponent<UiGradient>();
            g.Top = top; g.Bottom = bottom; graphic.SetVerticesDirty();
            return g;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var v = new UIVertex();
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                lo = Mathf.Min(lo, v.position.y); hi = Mathf.Max(hi, v.position.y);
            }
            float span = Mathf.Max(1e-3f, hi - lo);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.color *= Color.Lerp(Bottom, Top, (v.position.y - lo) / span);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
