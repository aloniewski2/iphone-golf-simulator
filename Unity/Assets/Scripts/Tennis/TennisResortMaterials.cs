using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    // One resort-only material calibration. No light, geometry, collider or
    // renderer setting is changed; the other venue responses stay at zero.
    public static class TennisResortMaterials
    {
        public static bool Active => TennisVenue.IsResort && Environment.GetEnvironmentVariable("TENNIS_RESORT_MATERIALS_OFF") != "1";
        static void Colour(Material m,Color c,float polish) { m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",polish); }
        public static void Surface(Material m,string role)
        {
            if(!m)return;
            if(m.HasProperty("_ResortResponse"))m.SetFloat("_ResortResponse",Active?1:0);
            if(m.HasProperty("_ArchitecturalRelief"))
            {
                bool architecture=role.StartsWith("TropicalV3_010") || role.StartsWith("TropicalV3_016") || role.StartsWith("TropicalV3_019")
                    || role=="Resort cut stone" || role=="Crafted resort limestone" || role=="Crafted resort ivory ceramic" || role=="Crafted resort sail canvas";
                if(role.StartsWith("Clubhouse role ") && int.TryParse(role.Substring(15),out int reliefCell))
                    architecture=reliefCell==3 || reliefCell==11 || reliefCell==12 || reliefCell==15;
                m.SetFloat("_ArchitecturalRelief",Active && architecture && Environment.GetEnvironmentVariable("TENNIS_RESORT_RELIEF_OFF")!="1"?1:0);
            }
            if(!Active)return;
            if(role.StartsWith("TropicalV3_002") && !TennisVenue.CourtColorOverride.HasValue)
            { Colour(m,new Color(.036f,.47f,.51f),.22f);m.SetFloat("_Variation",.045f); }
            else if(role.StartsWith("TropicalV3_011") && !TennisVenue.CourtColorOverride.HasValue)
            { Colour(m,new Color(.045f,.34f,.315f),.19f);m.SetFloat("_Variation",.04f); }
            else if(role.StartsWith("TropicalV3_010"))Colour(m,new Color(.74f,.75f,.71f),.24f);
            else if(role=="Resort cut stone" || role=="Crafted resort limestone")Colour(m,new Color(.78f,.79f,.735f),.20f);
            else if(role=="Crafted resort ivory ceramic")Colour(m,new Color(.80f,.82f,.765f),.38f);
            else if(role=="Crafted resort sail canvas")Colour(m,new Color(.79f,.83f,.79f),.10f);
            else if(role=="Resort timber" || role=="Crafted resort oiled teak")
            { Colour(m,new Color(.43f,.28f,.135f),.29f);m.SetFloat("_TimberGrain",.10f); }
            else if(role.StartsWith("Clubhouse role ") && int.TryParse(role.Substring(15),out int cell))
            {
                // The source atlas keeps its authored height gradients. Multipliers
                // separate plaster, roof, timber and glass instead of flattening them.
                switch(cell)
                {
                    case 3: Colour(m,new Color(.88f,.93f,.985f),.16f);break;
                    case 4: Colour(m,new Color(.80f,.78f,.76f),.29f);break;
                    case 5: Colour(m,new Color(.78f,.95f,.92f),.34f);break;
                    case 6: Colour(m,new Color(.91f,.87f,.77f),.31f);m.SetFloat("_TimberGrain",.10f);break;
                    case 7: Colour(m,new Color(.65f,.77f,.87f),.78f);m.SetFloat("_ReflectionWeight",.62f);break;
                    case 11: Colour(m,new Color(.91f,.95f,.94f),.22f);break;
                    case 12: case 15: Colour(m,new Color(.88f,.94f,.96f),.27f);break;
                }
            }
        }
        public static void Botanical(Material m)
        {
            if(!m)return;
            m.SetFloat("_ResortResponse",Active?1:0);m.SetFloat("_Gloss",Active?.24f:.18f);
        }
        public static Color BotanicalColour(Color original,int role)
        {
            if(!Active)return original;
            switch(role)
            {
                case 1:return new Color(.05f,.225f,.075f);
                case 2:return new Color(.145f,.37f,.105f);
                case 3:return new Color(.36f,.51f,.155f);
                default:return original;
            }
        }
        public static void Sea(Material sea)
        {
            if(!Active||!sea)return;
            sea.SetColor("_Shallow",new Color(.018f,.63f,.61f));
            sea.SetColor("_Deep",new Color(.008f,.30f,.51f));
            sea.SetColor("_Sky",new Color(.08f,.54f,.76f));
            sea.SetFloat("_Sparkle",1.4f);sea.SetFloat("_WaveScale",.28f);
            sea.SetFloat("_FragmentFog",Environment.GetEnvironmentVariable("TENNIS_WATER_VERTEX_FOG")=="1"?0:1);
            sea.SetFloat("_FilterWaves",Environment.GetEnvironmentVariable("TENNIS_WATER_UNFILTERED")=="1"?0:1);
            sea.SetTexture("_SkyMap",Resources.Load<Texture2D>("Course/Resort/SkyCoastalSmall"));
            sea.SetFloat("_SkyHeading",-45);sea.SetFloat("_ReflectionWeight",.18f);sea.SetFloat("_SunSheen",1.6f);
            if(int.TryParse(Environment.GetEnvironmentVariable("TENNIS_WATER_DEBUG"),out int debug))sea.SetFloat("_WaterDebug",debug);
        }
    }
}
