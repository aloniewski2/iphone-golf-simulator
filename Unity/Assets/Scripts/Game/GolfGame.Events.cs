using System;
using GolfArcade.Course;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The round's events on the course (Course/RoundEvents.cs): the twists dealt before the first
    /// tee shot and revealed over the opening, each hole's weather (seen and heard: WeatherView)
    /// turning between shots, the wind gusting, and the balloons, carts and gulls moving about the
    /// holes (MoverView) that the ball can meet. The match settings turn the twists, and the
    /// weather and the things moving about, on or off for a round (PrepareNativeAddress,
    /// ConfigureNetwork); a multiplayer round plays the host's (NetworkGolfRound).
    public sealed partial class GolfGame
    {
        RoundEvents events = RoundEvents.None;
        /// The match settings for rounds started here: twists dealt, weather and movers on.
        bool twistsWanted = true, worldWanted = true;
        Weather weather;
        /// The hole's own wind, before any gust: gusts swing round it.
        Wind holeWind;
        /// Swings taken on this hole, by anyone: the events' count of the hole's shots.
        int holeSwings, eventsRolledFor = -1, weatherTurns;
        int firstHoleIndex = -1;
        MoverView moverView;
        WeatherView weatherView;
        EventSounds eventSounds;
        /// The movers' clock when the next ball leaves the club, the host's in a multiplayer round.
        double? networkMoverPhase;

        /// The round's events (for the HUD and the tests).
        public RoundEvents Events => events;
        public Weather CurrentWeather => weather;
        public WeatherView WeatherShown => weatherView;
        public MoverView MoversShown => moverView;

        /// For reviews and tests: this weather on every hole started from now on (null: the round's own).
        public static Weather? ForcedWeather;
        /// Lightning now, in a storm (for reviews and tests).
        public void StrikeLightning() => weatherView?.Strike();

        /// Twists, and weather with things moving about, for the rounds started from now on.
        public void SetRoundEvents(bool twists, bool world) { twistsWanted = twists; worldWanted = world; }

        /// Clear skies after rain or a storm: a rainbow in the sky, and a rainbow ring to fly through.
        void SkiesCleared(Weather was)
        {
            if (!was.IsWet()) return;
            weatherView?.Rainbow(hole.Pin);
            LayRainbowRing();
        }

        void EnsureEventViews()
        {
            if (!eventSounds) eventSounds = EventSounds.Create(transform);
            if (!weatherView) weatherView = WeatherView.Create(transform, eventSounds);
        }

        /// A new round: its events, dealt from its seed.
        void DealEvents(int seed)
        {
            events = ForcedTwists != null
                ? RoundEvents.FromCode(seed, (worldWanted ? "w" : "s") + (challengesWanted ? "c" : "") + ":" + string.Join(",", Array.ConvertAll(ForcedTwists, t => ((int)t).ToString())))
                : RoundEvents.Deal(seed, twistsWanted, worldWanted, challengesWanted);
            firstHoleIndex = -1;
            ResetChallenges();
        }

        /// The hole about to be built: today's weather, and the rules it and the twists make
        /// (they shape the cup it is built with). The last hole's weather comes off the light first.
        void PrepareHoleEvents(int index, Weather? given = null)
        {
            weatherView?.Restore();
            weather = given ?? ForcedWeather ?? events.WeatherFor(index);
            hole.Rules = events.RulesFor(weather);
            hole.Movers = null;
            holeSwings = 0; eventsRolledFor = 0; weatherTurns = 0;
            if (firstHoleIndex < 0) firstHoleIndex = index;
        }

        /// The hole built and its wind set: the gusts' base, what moves about it, its sky.
        void StartHoleEvents(int index, double? moverClock = null)
        {
            holeWind = Wind;
            hole.Movers = events.MoversFor(index, hole, weather, hole.Ground);
            moverView = hole.Movers != null ? MoverView.Build(holeView.transform, hole.Movers) : null;
            if (hole.Movers != null && moverClock.HasValue) hole.Movers.SetClock(moverClock.Value);
            // (with the weather off it is plain golf's sky, light and sound: nothing is laid over them)
            if (events.World || events.Has(Twist.Hurricane)) { EnsureEventViews(); weatherView.Begin(rig.Camera, hole, weather, Wind, instant: true); }
            else weatherView?.Restore();
            GolfWindSway.SetWind(Wind);
            hud.SetConditions(ConditionFaces());
            StartHoleChallenges(index);
        }

        /// The weather in words before the wind on the hole's title card.
        string WeatherWords(string wind) => weather == Weather.Clear || !events.World ? wind : $"{weather.Title(Weathers.SkyOf(hole))}   ·   {wind}";

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;

        static EventFace FaceOf(Twist t) => new(t.Title(), t.Line(), t.Icon(), Hex(t.Colour()));
        EventFace WeatherFace(Weather w) { var sky = Weathers.SkyOf(hole); return new(w.Title(sky), w.Line(sky), w.Icon(sky), Hex(w.Colour(sky))); }

        /// The chips in the HUD's corner: the weather (when there is any to speak of) and the twists.
        EventFace[] ConditionFaces()
        {
            var list = new System.Collections.Generic.List<EventFace>();
            if (events.World && weather != Weather.Clear) list.Add(WeatherFace(weather));
            foreach (var t in events.Dealt) list.Add(FaceOf(t));
            return list.ToArray();
        }

        /// The twists dealt, over the opening of the round's first hole.
        void RevealTwists()
        {
            if (events.Dealt.Length == 0 || holeIndex != firstHoleIndex || hole == null) return;
            EnsureEventViews();
            var reveal = hud.TwistReveal;
            reveal.Ratchet = () => eventSounds.Ratchet();
            reveal.Stamp = () => { eventSounds.Stamp(); Haptics.Tick(); };
            reveal.Show(Array.ConvertAll(events.Dealt, FaceOf), Array.ConvertAll(Twists.All, FaceOf));
        }

        /// Before a stroke (not the hole's first): the weather may turn and the wind may gust,
        /// drawn from the round's seed for this many swings into the hole. A multiplayer round's
        /// come from the host instead (NetworkWeather).
        void RollShotEvents()
        {
            if (networkConfigured || hole == null || holeSwings == 0 || holeSwings == eventsRolledFor || !events.Any) return;
            eventsRolledFor = holeSwings;
            string word = null, detail = null; Color colour = Color.white; string icon = null;
            if (weatherTurns < 2 && events.TurnBefore(holeIndex, holeSwings, weather) is Weather turned)
            {
                weatherTurns++;
                var was = weather;
                ChangeWeather(turned);
                if (turned == Weather.Clear) SkiesCleared(was);
                (word, detail, colour, icon) = WeatherTurnWords(turned, was);
                holeWind = events.WindFor(holeIndex, holeWind, turned);
                SetWind(holeWind);
            }
            if (events.GustBefore(holeIndex, holeSwings, holeWind, weather) is Wind gust)
            {
                SetWind(gust);
                double line = ballAt.HeadingTo(hole.LieAt(ballAt).IsPuttingSurface() ? hole.Pin : hole.RecommendedTarget(ballAt));
                if (word == null) { word = "Gust!"; detail = $"Wind now {Wind.Describe(line)}"; colour = Hex("3FA9F5"); icon = "wind"; }
                else detail += $"  ·  {Wind.Describe(line)}";
                eventSounds?.Gust();
            }
            if (word != null) hud.ShowEvent(word, detail, colour, icon, 2.8f);
        }

        /// The weather turning now: the rules for the shots to come, the sky easing over, the chips.
        void ChangeWeather(Weather w)
        {
            weather = w;
            hole.Rules = events.RulesFor(w);
            weatherView?.Change(w);
            hud.SetConditions(ConditionFaces());
        }

        (string, string, Color, string) WeatherTurnWords(Weather w, Weather was)
        {
            var sky = Weathers.SkyOf(hole);
            string word = w switch
            {
                Weather.Clear => was == Weather.Fog ? (sky == Sky.Ash ? "Smoke clears" : "Fog lifts") : "Skies clear",
                Weather.Gusty => "Wind's up",
                Weather.Fog => sky == Sky.Ash ? "Smoke rolls in" : "Fog rolls in",
                Weather.Rain => sky == Sky.Snow ? "Snow's falling" : sky == Sky.Ash ? "Ash is falling" : "Rain rolls in",
                _ => sky == Sky.Snow ? "Blizzard!" : sky == Sky.Ash ? "Eruption!" : "Storm!",
            };
            string detail = w != Weather.Clear ? w.Line(sky)
                : was == Weather.Fog ? "The green's in sight again"
                : was.IsWet() && rainbowLaid ? "A rainbow ring to fly through!"
                : "Dry turf again: the ball runs";
            return (word, detail, Hex(w.Colour(sky)), w.Icon(sky));
        }

        /// The movers' clock as the ball leaves the club (`toBall` seconds from now), so the shot
        /// meets them where the picture will show them; the host's in a multiplayer round.
        void PrimeMovers(float toBall)
        {
            holeSwings++;
            if (hole.Movers == null) { networkMoverPhase = null; return; }
            hole.Movers.PhaseAtLaunch = networkMoverPhase ?? hole.Movers.Clock + toBall;
            networkMoverPhase = null;
        }

        /// The ball met something moving about the hole: the mover reacts, the effect, the sound, a word.
        void MoverKnock(CourseShot.Knock k, Vector3 at)
        {
            var movers = hole.Movers;
            if (movers != null)
            {
                // which one: the nearest of its kind when it happened
                int best = -1; double near = double.MaxValue;
                for (int i = 0; i < movers.All.Length; i++)
                {
                    if (CourseMovers.KnockKind(movers.All[i].Kind) != k.Kind) continue;
                    var p = movers.All[i].At(movers.PhaseAtLaunch + k.Time);
                    double d = (p.x - k.X) * (p.x - k.X) + (p.d - k.D) * (p.d - k.D);
                    if (d < near) { near = d; best = i; }
                }
                moverView?.React(best);
            }
            switch (k.Kind)
            {
                case ObstacleKind.Balloon:
                    effects.Burst(at, new Color(1f, 1f, 1f, 0.8f), new Color(1f, 0.85f, 0.9f, 0.6f), 26, 3.2f, 0.5f);
                    eventSounds?.Boing(); Haptics.Impact(0.6);
                    hud.ShowCallout("Boing!", Hex("FF6FA8"));
                    break;
                case ObstacleKind.Twister:
                    effects.Burst(at, new Color(0.55f, 0.5f, 0.42f, 0.9f), new Color(0.4f, 0.55f, 0.3f, 0.8f), 46, 6f, 0.4f, 1.6f, 1.2f);
                    eventSounds?.Gust(); Haptics.Impact(0.8);
                    hud.ShowCallout("Twister!", Hex("8FA3B8"));
                    break;
                case ObstacleKind.Gull:
                    effects.Burst(at, Color.white, new Color(0.78f, 0.82f, 0.86f), 34, 2.4f, 0.32f, 1.4f, 0.4f);
                    eventSounds?.Squawk(); Haptics.Tick();
                    hud.ShowCallout("Birdie!", Hex("FFE066"));
                    break;
                default:
                    effects.Burst(at, new Color(0.75f, 0.68f, 0.55f), new Color(0.9f, 0.86f, 0.78f), 14, 2f, 0.4f);
                    sounds.PlayKnock(false); eventSounds?.Horn(); Haptics.Impact(0.5);
                    hud.ShowCallout("Beep beep!", Hex("2FA36B"));
                    break;
            }
        }

        /// Out of the round: the light as the course has it, nothing falling, no twists in the corner.
        void EndRoundEvents()
        {
            weatherView?.Restore();
            hud.HideTwists();
            hud.SetConditions(null);
            hud.SetBounty(null, 0, 0, null);
            hud.SetCoins(null);
            hud.Map.Rings.Clear(); hud.Map.Changed();
        }

        // ----- A multiplayer round: the host's events -----

        /// The host's events for the round and this hole, before StartHole builds it.
        void NetworkHoleEvents(Multiplayer.NetworkGolfState state)
        {
            if (events.Seed != state.eventSeed || events.Code != (state.events ?? "")) { events = RoundEvents.FromCode(state.eventSeed, state.events); firstHoleIndex = -1; }
            pendingNetworkWeather = (Weather)Mathf.Clamp(state.weather, 0, (int)Weather.Fog);
        }
        Weather? pendingNetworkWeather;

        /// Every frame of a multiplayer round: the host's weather and wind, announced when they turn.
        void NetworkWeather(Multiplayer.NetworkGolfState state)
        {
            var w = (Weather)Mathf.Clamp(state.weather, 0, (int)Weather.Fog);
            string word = null, detail = null; Color colour = Color.white; string icon = null;
            if (w != weather) { var was = weather; ChangeWeather(w); (word, detail, colour, icon) = WeatherTurnWords(w, was); if (w == Weather.Clear && was.IsWet()) weatherView?.Rainbow(hole.Pin); }
            var now = new Wind(state.windSpeed, state.windDirection);
            if (Math.Abs(now.SpeedMPH - Wind.SpeedMPH) > 0.01 || Math.Abs(now.DirectionDegrees - Wind.DirectionDegrees) > 0.01)
            {
                bool gust = state.hole == networkHole && Wind.SpeedMPH + now.SpeedMPH > 0;
                SetWind(now);
                weatherView?.SetWind(now);
                if (gust && word == null) { word = "Gust!"; detail = $"Wind now {now.Describe(heading)}"; colour = Hex("3FA9F5"); icon = "wind"; eventSounds?.Gust(); }
            }
            if (word != null && state.phase == "aim") hud.ShowEvent(word, detail, colour, icon, 2.8f);
        }
    }
}
