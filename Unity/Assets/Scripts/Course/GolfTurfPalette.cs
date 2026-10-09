using UnityEngine;

namespace GolfArcade.Course
{
    /// Managed turf hierarchy, shared by the surface and physical blade materials.
    /// Colours are art-directed sRGB albedos, not measurements from lit photographs.
    public static class GolfTurfPalette
    {
        public readonly struct Field
        {
            public readonly Color Low, High;
            public Field(Color low, Color high) { Low=low; High=high; }
        }
        public static bool Managed(GolfCourseLook.Surface role) => role is GolfCourseLook.Surface.Fairway
            or GolfCourseLook.Surface.Green or GolfCourseLook.Surface.Tee or GolfCourseLook.Surface.Fringe or GolfCourseLook.Surface.Rough;

        public static string Setting(Hole hole) => hole.Number==18 ? "dry" : hole.Number==17 ? "cold"
            : hole.Number==19 ? "tropical" : hole.Number==16 || hole.Theme=="magma" ? "volcanic"
            : hole.Number is 7 or 8 or 9 or 10 or 12 or 15 ? "coastal" : "temperate";

        public static Field For(Hole hole, GolfCourseLook.Surface role)
        {
            Color low, high;
            switch(role)
            {
                case GolfCourseLook.Surface.Green: low=new(.40f,.60f,.19f); high=new(.61f,.77f,.30f); break;
                case GolfCourseLook.Surface.Tee: low=new(.30f,.54f,.12f); high=new(.55f,.73f,.23f); break;
                case GolfCourseLook.Surface.Fairway: low=new(.25f,.51f,.11f); high=new(.51f,.71f,.23f); break;
                case GolfCourseLook.Surface.Fringe: low=new(.17f,.40f,.14f); high=new(.32f,.57f,.20f); break;
                default: low=new(.16f,.35f,.12f); high=new(.37f,.54f,.20f); break;
            }
            string setting=Setting(hole);
            if(setting=="tropical")
            {
                low*=new Color(.88f,1.03f,1.12f); high*=new Color(.90f,1.04f,1.12f);
            }
            else if(setting=="cold")
            {
                low*=new Color(.96f,.96f,1.14f); high*=new Color(.98f,.97f,1.12f);
            }
            else if(setting=="temperate")
            {
                low*=new Color(.97f,1.02f,1.04f); high*=new Color(.97f,1.02f,1.04f);
            }
            else if(setting=="dry")
            {
                // Retain the existing physics regions; dry native ground is not
                // repainted as a lush coastal rough. Sand/Desert slots stay intact.
                if(role==GolfCourseLook.Surface.Rough) { low=new(.330f,.330f,.170f); high=new(.570f,.515f,.300f); }
                else { low*=new Color(1.12f,.98f,.91f); high*=new Color(1.10f,.98f,.92f); }
            }
            else if(setting=="volcanic")
            {
                low*=new Color(.97f,1.02f,.95f); high*=new Color(.98f,1.02f,.95f);
            }
            // HDR daylight needs less albedo energy than the former clipped LDR look.
            low*=new Color(.94f,.90f,1.12f);high*=new Color(.94f,.90f,1.12f);
            return new Field(low,high);
        }

        public static void ApplyGround(Material material, Hole hole, GolfCourseLook.Surface role, bool closeAtlas=false)
        {
            if(!Managed(role))return;
            var field=For(hole,role);
            material.SetColor("_BaseColor",Color.white);
            material.SetColor("_LowColor",field.Low); material.SetColor("_HighColor",field.High);
            material.SetFloat("_PaletteMode",1); material.SetFloat("_TurfManaged",1);
            material.SetFloat("_DetailContrast",closeAtlas?4.5f:role==GolfCourseLook.Surface.Rough?.8f:.30f);
            material.SetFloat("_Smoothness",role==GolfCourseLook.Surface.Green?.07f:.08f);
            material.SetFloat("_BumpScale",closeAtlas?.65f:role==GolfCourseLook.Surface.Green?.12f:role==GolfCourseLook.Surface.Tee?.18f:.25f);
            // StripeWidth is one complete light/dark pair, so10m gives5m bands.
            material.SetFloat("_StripeWidth",(role==GolfCourseLook.Surface.Fairway?10f:role==GolfCourseLook.Surface.Green?6f:4f)/.9144f);
            material.SetFloat("_Bands",role==GolfCourseLook.Surface.Fairway?.22f:role==GolfCourseLook.Surface.Tee?.16f:role==GolfCourseLook.Surface.Green?.10f:0);
            if(role==GolfCourseLook.Surface.Green)
            {
                // The same authored folded-blade atlas at .32m produces fine
                // 2–3mm lamina. Only the texture response changes: no green
                // blade geometry, palette midpoint, or putting physics.
                var colour=Resources.Load<Texture2D>("Course/Resort/CoastalTurf_C");
                var normal=Resources.Load<Texture2D>("Course/Resort/CoastalTurf_N");
                if(colour)
                {
                    material.SetTexture("_BaseMap",colour); material.SetTexture("_BumpMap",normal);
                    material.SetFloat("_NormalEnabled",normal?1:0); material.SetFloat("_BumpScale",.18f);
                    material.SetFloat("_TileYards",.32f/.9144f);
                    material.SetFloat("_PaletteDetail",1); material.SetFloat("_TurfMidpoint",.0907707f);
                    material.SetFloat("_DetailContrast",6.5f); material.SetFloat("_SheenFromAlpha",0);
                }
            }
        }
        public static void ApplyBlades(Material material,Hole hole)
        {
            foreach(var role in new[]{GolfCourseLook.Surface.Tee,GolfCourseLook.Surface.Fairway,GolfCourseLook.Surface.Fringe,GolfCourseLook.Surface.Rough})
            {
                var field=For(hole,role);
                string prefix=role==GolfCourseLook.Surface.Tee?"":role.ToString();
                material.SetColor("_"+prefix+"RootColor",field.Low);
                material.SetColor("_"+prefix+"TipColor",field.High);
            }
            material.SetFloat("_StripeWidth",10f/.9144f); material.SetFloat("_Bands",.22f);
        }
    }
}
