using UnityEngine;

namespace GolfArcade.Tennis
{
    public enum TennisVenueKind { Resort, Skyscraper, Volcano }

    /// Which court the match is played on. Every venue uses the resort arena's own court kit
    /// (sapphire acrylic court, lines, net and posts, umpire chair, lanterns, benches) at the same
    /// coordinates, so play, contact and the cameras are identical; what changes is what the court
    /// sits on, the light, the court colour and the opening drone shot.
    ///
    ///   Skyscraper  the deck on top of a tower a hundred times taller than anything around it. The
    ///               edge is open -- no glass, no netting -- so a ball hit wide falls into the clouds.
    ///   Volcano     a slab of obsidian hovering over the crater's lava lake; a ball that leaves it
    ///               falls into the magma.
    public static class TennisVenue
    {
        /// What the menu picked (native message "venue"). HERO_VENUE=skyscraper|volcano forces one for tests.
        public static TennisVenueKind Selected = TennisVenueKind.Resort;
        /// A colour picked in the locker replaces the venue's own court colour (null = the venue's).
        public static Color? CourtColorOverride;

        public static TennisVenueKind Current
        {
            get
            {
                var forced = System.Environment.GetEnvironmentVariable("HERO_VENUE");
                return string.IsNullOrEmpty(forced) ? Selected : Parse(forced, Selected);
            }
        }

        public static bool IsResort => Current == TennisVenueKind.Resort;

        public static TennisVenueKind Parse(string key, TennisVenueKind fallback = TennisVenueKind.Resort)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case "resort": case "tropical": return TennisVenueKind.Resort;
                case "skyscraper": case "sky": case "rooftop": return TennisVenueKind.Skyscraper;
                case "volcano": case "magma": case "crater": return TennisVenueKind.Volcano;
                default: return fallback;
            }
        }

        // ---- names on the broadcast cards
        public static string Title => Current switch { TennisVenueKind.Skyscraper => "SKY OPEN", TennisVenueKind.Volcano => "MAGMA OPEN", _ => "TROPICAL OPEN" };
        public static string CourtName => Current switch { TennisVenueKind.Skyscraper => "ROOFTOP COURT", TennisVenueKind.Volcano => "CRATER COURT", _ => "CENTRE COURT" };
        public static string Session => Current switch { TennisVenueKind.Skyscraper => "CLOUD SESSION", TennisVenueKind.Volcano => "ERUPTION SESSION", _ => "SUNSET SESSION" };

        // ---- the surface
        /// The deck the court stands on, half extents in metres (the resort's run-off apron is 10 x 19.5).
        public const float DeckHalfX = 12.6f, DeckHalfZ = 21.6f;

        /// Is there floor under this point? Everywhere at the resort; only on the deck elsewhere.
        /// Off the deck a ball is not bounced: it is called and keeps falling.
        public static bool OverDeck(Vector3 p)
        {
            if (IsResort) return true;
            return Mathf.Abs(p.x) <= DeckHalfX && Mathf.Abs(p.z) <= DeckHalfZ;
        }

        /// The court's own colour (URP base colour, as the resort's sapphire is set).
        public static Color CourtColor => CourtColorOverride ?? Current switch
        {
            TennisVenueKind.Skyscraper => new Color(.03f, .40f, .36f),      // deep teal
            TennisVenueKind.Volcano => new Color(.055f, .055f, .065f),      // obsidian black
            _ => new Color(.015f, .19f, .62f),                              // resort sapphire
        };

        /// The run-off apron around the court: a shade off the court so the lines still frame it.
        public static Color RunoffColor
        {
            get
            {
                if (CourtColorOverride.HasValue) return CourtColorOverride.Value * .88f + new Color(0, 0, 0, 1) * .12f;
                return Current switch
                {
                    TennisVenueKind.Skyscraper => new Color(.025f, .34f, .31f),
                    TennisVenueKind.Volcano => new Color(.04f, .04f, .048f),
                    _ => new Color(.02f, .34f, .34f),
                };
            }
        }

        public static Color LineColor => Current == TennisVenueKind.Volcano ? new Color(.97f, .96f, .92f) : new Color(.95f, .96f, .93f);

        /// How far the camera can see: the resort is a small island; the sky and the crater are not.
        public static float FarClip => Current switch { TennisVenueKind.Skyscraper => 6000, TennisVenueKind.Volcano => 3500, _ => 600 };

        // ---- the drone shot
        /// The opening flyover, 0 to 1: where the camera is, what it looks at and its field of view.
        /// Every path ends where the resort's does, banking in over the stands to the player.
        public static void Drone(float k, out Vector3 pos, out Vector3 look, out float fov)
        {
            Vector3 end = new Vector3(-14, 7.5f, -22);
            switch (Current)
            {
                case TennisVenueKind.Skyscraper:
                    // From high above the cloud sea, tower and city laid out below, then a long descent
                    // behind the deck and in over its near edge. Never below deck height, so nothing
                    // is flown through.
                    pos = Bezier(new Vector3(170, 300, -330), new Vector3(120, 190, -240), new Vector3(-70, 70, -100), end, k);
                    look = Vector3.Lerp(new Vector3(0, -120, 0), new Vector3(0, .5f, 0), Mathf.SmoothStep(0, 1, k));
                    fov = Mathf.Lerp(54, 50, k);
                    break;
                case TennisVenueKind.Volcano:
                    // Starts high and close, looking almost straight down at the slab dead centre in the
                    // lava (only lava and the inner wall in frame, never the land outside), then sinks
                    // and swings low enough to show the floating rock it sits on before settling on the court.
                    pos = Bezier(new Vector3(-45, 95, -62), new Vector3(-55, 70, -78), new Vector3(-48, 18, -70), end, k);
                    look = Vector3.Lerp(new Vector3(0, -32, 0), new Vector3(0, .5f, 0), Mathf.SmoothStep(0, 1, k));
                    fov = Mathf.Lerp(52, 50, k);
                    break;
                default:
                    pos = Bezier(new Vector3(40, 95, 210), new Vector3(-150, 70, 90), new Vector3(-70, 24, -40), end, k);
                    look = Vector3.Lerp(new Vector3(0, -10, -20), new Vector3(0, .5f, 0), k);
                    fov = Mathf.Lerp(46, 50, k);
                    break;
            }
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float k)
        {
            float u = 1 - k;
            return u * u * u * a + 3 * u * u * k * b + 3 * u * k * k * c + k * k * k * d;
        }
    }
}
