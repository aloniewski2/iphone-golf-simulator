using System;
using System.Collections.Generic;
using System.Text;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Net.Online;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The screens behind the home screen's PROFILE button and its 2 PLAYERS and ONLINE modes, in
    /// the arcade cards' look, over the golfer on the tee:
    ///
    /// - PROFILE: the name, the golfer (edited on the golfer select screen), the record, signing
    ///   in online, and the other golfers on this phone.
    /// - 2 PLAYERS: two to four profiles taking turns on this phone, each with their own golfer.
    /// - ONLINE: quick match, a room with a code, or joining one; the lobby; the leaderboard.
    ///
    /// The game wires what leaves it: a round to play, a golfer to edit, and back to the menu.
    public sealed class Lobby : MonoBehaviour
    {
        public enum Page { Profile, Local, Online, Tournament }
        const int MaxLocalPlayers = 4;

        /// The player colours for rounds with others: yellow, sky, coral, green.
        public static readonly Color[] PlayerColors = { UiKit.ArcadeYellow, UiKit.Hex("5CD6FF"), UiKit.Hex("FF8A5C"), UiKit.Hex("7DE07D") };
        public static Color PlayerColor(int i) => PlayerColors[((i % PlayerColors.Length) + PlayerColors.Length) % PlayerColors.Length];

        Action<GameSetup> play;
        Action<PlayerProfile, Action> editGolfer;
        Action close;
        Func<int> holes;
        RectTransform content;
        Page page;
        OnlineSession session;
        Text leaderboardText;
        readonly List<string> localIds = new();
        PlayerProfile shown;
        bool signingIn;
        MatchFormat format = MatchFormat.StrokePlay;

        public Page Showing => page;

        /// `holes`: the home screen's choice (0 the round, else one hole's number).
        public static Lobby Create(Page page, Func<int> holes, Action<GameSetup> play, Action<PlayerProfile, Action> editGolfer, Action close)
        {
            var go = new GameObject("Lobby");
            var lobby = go.AddComponent<Lobby>();
            lobby.play = play; lobby.editGolfer = editGolfer; lobby.close = close; lobby.holes = holes;
            lobby.Build();
            lobby.Open(page);
            return lobby;
        }

        void Build()
        {
            var canvas = UiKit.Canvas(gameObject);
            canvas.sortingOrder = 10;
            var area = new GameObject("Safe area").AddComponent<RectTransform>();
            area.SetParent(transform, false);
            var safe = Screen.safeArea;
            area.anchorMin = Screen.width > 0 ? new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height) : Vector2.zero;
            area.anchorMax = Screen.width > 0 ? new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height) : Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;
            content = area;
        }

        public void Open(Page p)
        {
            page = p;
            switch (p)
            {
                case Page.Profile: ShowProfile(shown ?? ProfileStore.Active); break;
                case Page.Local: ShowLocal(); break;
                case Page.Tournament: ShowTournament(); break;
                default: ShowOnline(); break;
            }
        }

        void OnDestroy() => Listen(null);

        // ----- PROFILE -----

        void ShowProfile(PlayerProfile profile)
        {
            Listen(null);
            page = Page.Profile;
            shown = profile;
            var book = ProfileStore.Book;
            bool active = profile.Id == book.ActiveId;
            Frame("PROFILE", "face", () => close());

            var card = Card(0, 560, 980, 420);
            Caption(card, active ? "PLAYS ON THIS PHONE" : "ANOTHER GOLFER ON THIS PHONE", 150);
            NameField(card, profile, 0, 50, 880);
            Caption(card, LookLine(profile), -80, UiKit.ArcadeYellow);
            Pill(card, "EDIT GOLFER", "swing", 0, -150, 520, 96, UiKit.ArcadeYellow, UiKit.ArcadeInk, () => editGolfer(profile, () => ShowProfile(profile)));

            var s = profile.Stats;
            Tiles(-40, new[]
            {
                ("flag", "Rounds", s.RoundsPlayed.ToString()),
                ("trophy", "Best round", s.HasBest ? Scorecard.FormatToPar(s.BestToPar) : "—"),
                ("target", "Avg a hole", s.HolesPlayed > 0 ? s.AverageToParPerHole.ToString("+0.0;-0.0;0.0") : "—"),
                ("star", "Birdies", (s.Birdies + s.Eagles).ToString()),
                ("ball", "Holes in one", s.HolesInOne.ToString()),
                ("swing", "Won · tied · lost", s.MatchesPlayed > 0 ? $"{s.MatchesWon}·{s.MatchesTied}·{s.MatchesLost}" : "—"),
            });

            if (BackendConfig.IsConfigured)
            {
                if (profile.IsSignedIn) Caption(content, "ONLINE ACCOUNT  ✓", -440, UiKit.ArcadeYellow);
                else Pill(content, "SIGN IN ONLINE", "play", 0, -440, 900, 104, UiKit.Hex("DCEEFD"), UiKit.ArcadeBlue, async () =>
                {
                    bool ok = await BackendClient.SignUp(profile);
                    if (!this) return;
                    ShowProfile(profile);
                    if (!ok) Toast(BackendClient.LastError);
                });
            }

            float w = 290;
            Pill(content, active ? "NEXT GOLFER" : "PLAY AS", "menu", -305, -580, w, 104, UiKit.ArcadeBlue, Color.white, () =>
            {
                if (!active) { book.SetActive(profile.Id); SaveAsDevice(profile); ShowProfile(profile); return; }
                int i = book.Profiles.IndexOf(profile);
                ShowProfile(book.Profiles[(i + 1) % book.Profiles.Count]);
            }, 30);
            if (book.CanAdd) Pill(content, "NEW", "face", 0, -580, w, 104, UiKit.ArcadeBlue, Color.white, () => { var p = book.Add(); ProfileStore.Save(); ShowProfile(p); }, 30);
            if (book.Profiles.Count > 1) Pill(content, "DELETE", "menu", 305, -580, w, 104, UiKit.Hex("E8352F"), Color.white, () =>
            {
                book.Remove(profile.Id); ProfileStore.Save();
                SaveAsDevice(book.Active);
                ShowProfile(book.Active);
            }, 30);
            Pill(content, "DONE", "play", 0, -740, 900, 130, UiKit.ArcadeYellow, UiKit.ArcadeInk, () => close(), 48);
        }

        static string LookLine(PlayerProfile p) =>
            $"{(p.Body == 1 ? "FEMALE" : "MALE")} GOLFER  ·  {GolferStyle.KitNames[Clamp(p.Kit)].ToUpperInvariant()} KIT" +
            (p.Shirt > 0 ? $" & {GolferStyle.ShirtNames[Clamp(p.Shirt)].ToUpperInvariant()}" : "");

        static int Clamp(int i) => Mathf.Clamp(i, 0, PlayerProfile.Colours - 1);

        /// The active profile's golfer is also the device's own look (GolferStyle's saved one).
        static void SaveAsDevice(PlayerProfile p) => GolferStyle.Save(p.Body, p.Kit, p.Shirt);

        // ----- 2 PLAYERS -----

        void ShowLocal()
        {
            Listen(null);
            page = Page.Local;
            var book = ProfileStore.Book;
            localIds.RemoveAll(id => book.Find(id) == null);
            if (localIds.Count == 0 || localIds[0] != book.ActiveId) { localIds.Remove(book.ActiveId); localIds.Insert(0, book.ActiveId); }
            while (localIds.Count < 2) if (!AddLocalPlayer()) break;
            Frame("2 PLAYERS", "swing", () => close());
            Caption(content, "ONE PHONE  ·  PLAY THE HOLE OUT, THEN PASS IT ON", 820);

            for (int i = 0; i < localIds.Count; i++)
            {
                var profile = book.Find(localIds[i]);
                float y = 640 - i * 250;
                var row = Card(0, y, 980, 220);
                var disc = UiKit.Panel(row, "Colour", PlayerColor(i), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(110, 110));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0, 0.5f);
                var number = UiKit.Chunky(disc.transform, "P", 50, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
                number.text = $"P{i + 1}";
                NameField(row, profile, 80, 44, 640, 42, 88);
                var who = profile;
                Pill(row, LookLine(profile), "swing", 80, -56, 640, 76, UiKit.Hex("DCEEFD"), UiKit.ArcadeBlue, () => editGolfer(who, ShowLocal), 22);
            }
            float below = 640 - localIds.Count * 250 + 40;
            if (localIds.Count < MaxLocalPlayers && book.CanAdd) Pill(content, "+ PLAYER", "face", localIds.Count > 2 ? -225 : 0, below, 430, 96, UiKit.ArcadeBlue, Color.white, () => { AddLocalPlayer(); ShowLocal(); }, 32);
            if (localIds.Count > 2) Pill(content, "– PLAYER", "menu", localIds.Count < MaxLocalPlayers ? 225 : 0, below, 430, 96, UiKit.ArcadeBlue, Color.white, () => { localIds.RemoveAt(localIds.Count - 1); ShowLocal(); }, 32);

            FormatPicker(-520);
            Pill(content, "TEE OFF", "play", 0, -740, 900, 140, UiKit.ArcadeYellow, UiKit.ArcadeInk, () =>
            {
                var players = new List<PlayerProfile>();
                foreach (var id in localIds) players.Add(book.Find(id));
                ProfileStore.Save();
                play(GameSetup.LocalVersus(players, holes(), format));
            }, 52);
        }

        /// How the round is won: four halves of one pill, the chosen one lit, and what it means.
        void FormatPicker(float y)
        {
            string[] words = { "STROKE", "MATCH", "CLOSEST", "LONG DRIVE" };
            string[] meaning =
            {
                "FEWEST STROKES OVER THE ROUND WINS",
                "WIN HOLES, NOT STROKES: THE MOST HOLES WINS",
                "ONE TEE SHOT EACH ON THE PAR 3s: NEAREST THE PIN",
                "ONE DRIVE EACH ON THE PAR 4s AND 5s: LONGEST ON THE FAIRWAY",
            };
            var bar = UiKit.Pill(content, "Format", UiKit.ArcadeBlueDeep, Center, new Vector2(0, y), new Vector2(980, 96), out var barFill, 5f);
            _ = bar;
            for (int i = 0; i < words.Length; i++)
            {
                bool on = (int)format == i;
                var half = UiKit.Panel(barFill.transform, words[i], on ? UiKit.ArcadeYellow : new Color(1, 1, 1, 0), Center, Center, new Vector2((i - 1.5f) * 236, 0), new Vector2(228, 74));
                half.sprite = UiKit.Circle; half.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var w = UiKit.Label(half.transform, "Word", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                w.text = words[i]; w.color = on ? UiKit.ArcadeInk : Color.white; w.raycastTarget = false;
                Icons.Fit(w, 18, 28);
                var hold = half.gameObject.AddComponent<HoldButton>();
                hold.Fill = half; hold.RestColor = half.color;
                var chosen = (MatchFormat)i;
                hold.Pressed = () => { Haptics.Tick(); format = chosen; ShowLocal(); };
            }
            Caption(content, meaning[(int)format], y - 80, UiKit.ArcadeYellow);
        }

        bool AddLocalPlayer()
        {
            var book = ProfileStore.Book;
            foreach (var p in book.Profiles)
                if (!localIds.Contains(p.Id)) { localIds.Add(p.Id); return true; }
            var added = book.Add();
            if (added == null) return false;
            localIds.Add(added.Id);
            ProfileStore.Save();
            return true;
        }

        // ----- THE OPEN -----

        /// The four-round tournament: the clubhouse leaderboard, and the next round to play.
        void ShowTournament()
        {
            Listen(null);
            page = Page.Tournament;
            var open = ChampionshipStore.Current;
            var me = ProfileStore.Active;
            Frame(open != null ? open.Title.ToUpperInvariant() : "THE OPEN", "trophy", () => close());

            if (open == null)
            {
                Caption(content, "FOUR ROUNDS OF CLIFFSIDE AGAINST ELEVEN TOUR PROS", 700, UiKit.ArcadeYellow);
                Caption(content, "PLAY A ROUND WHENEVER YOU LIKE  ·  THE LEADERBOARD WAITS FOR YOU", 640);
                Pill(content, "TEE OFF IN THE OPEN", "play", 0, -740, 900, 140, UiKit.ArcadeYellow, UiKit.ArcadeInk, () => NewOpen(me), 46);
                return;
            }

            Caption(content, open.IsOver ? "FINAL RESULTS" : $"ROUND {open.Round + 1} OF {Championship.Rounds}  ·  {open.Headline().ToUpperInvariant()}", 820, UiKit.ArcadeYellow);
            LeaderboardCard(open, me.Id, 180);
            if (!open.IsOver)
                Pill(content, $"PLAY ROUND {open.Round + 1}", "play", 0, -600, 900, 140, UiKit.ArcadeYellow, UiKit.ArcadeInk, () =>
                    play(GameSetup.Tournament(open, Players(open))), 48);
            Pill(content, open.IsOver ? "NEW OPEN" : "START OVER", "flag", 0, -760, 900, 110,
                open.IsOver ? UiKit.ArcadeYellow : UiKit.Hex("DCEEFD"), open.IsOver ? UiKit.ArcadeInk : UiKit.ArcadeBlue, () => NewOpen(me), 40);
        }

        void NewOpen(PlayerProfile me)
        {
            var cliffside = Course.Course.Cliffside();
            var pars = new int[cliffside.Holes.Length];
            for (int i = 0; i < pars.Length; i++) pars[i] = cliffside.Holes[i].Par;
            var open = Championship.Start(new[] { (me.Name, me.Id) }, "cliffside", cliffside.Name, pars, Environment.TickCount);
            ChampionshipStore.Current = open;
            play(GameSetup.Tournament(open, Players(open)));
        }

        /// The Open's players who are profiles on this phone.
        static List<PlayerProfile> Players(Championship open)
        {
            var list = new List<PlayerProfile>();
            foreach (var e in open.Field)
                if (e.IsPlayer && ProfileStore.Book.Find(e.ProfileId) is PlayerProfile p) list.Add(p);
            if (list.Count == 0) list.Add(ProfileStore.Active);
            return list;
        }

        /// POS · NAME · R1–R4 · TOTAL for the whole field, the phone's players on yellow.
        void LeaderboardCard(Championship open, string meId, float centreY)
        {
            var board = open.Leaderboard();
            const float rowH = 62;
            float h = rowH * (board.Count + 1) + 40;
            var card = Card(0, centreY, 1000, h);
            float top = h / 2 - 20 - rowH / 2;
            float[] xs = { -420, -130, 150, 230, 310, 390 };
            Text Cell(string text, float x, float y, float w, Color c, TextAnchor a = TextAnchor.MiddleCenter)
            {
                var l = UiKit.Label(card, "Cell", 30, a, Center, Center, new Vector2(x, y), new Vector2(w, rowH), UiKit.Display, false);
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.text = text; l.color = c; l.raycastTarget = false;
                return l;
            }
            Cell("POS", xs[0], top, 90, UiKit.ArcadeYellow);
            Cell("PLAYER", xs[1], top, 420, UiKit.ArcadeYellow, TextAnchor.MiddleLeft);
            for (int r = 0; r < Championship.Rounds; r++) Cell($"R{r + 1}", xs[2] + r * 80 - 80, top, 80, UiKit.ArcadeYellow);
            Cell("TOT", xs[5] + 20, top, 110, UiKit.ArcadeYellow);
            for (int i = 0; i < board.Count; i++)
            {
                var e = board[i];
                float y = top - rowH * (i + 1);
                bool mine = e.IsPlayer;
                if (mine)
                {
                    var band = UiKit.Panel(card, "You", UiKit.ArcadeYellow, Center, Center, new Vector2(0, y), new Vector2(960, rowH - 6));
                    band.rectTransform.pivot = new Vector2(0.5f, 0.5f); band.raycastTarget = false;
                }
                var ink = mine ? UiKit.ArcadeInk : Color.white;
                Cell(open.Position(e), xs[0], y, 90, ink);
                var name = Cell(e.Name.ToUpperInvariant(), xs[1], y, 420, ink, TextAnchor.MiddleLeft);
                Icons.Fit(name, 18, 30);
                for (int r = 0; r < Championship.Rounds; r++)
                    Cell(e.RoundToPar[r] == Championship.NotPlayed ? "–" : Scorecard.FormatToPar(e.RoundToPar[r]), xs[2] + r * 80 - 80, y, 80, ink);
                Cell(e.RoundsPlayed == 0 ? "–" : Scorecard.FormatToPar(e.Total), xs[5] + 20, y, 110, ink);
            }
            _ = meId;
        }

        // ----- ONLINE -----

        void ShowOnline()
        {
            page = Page.Online;
            session = OnlineSession.Ensure();
            Listen(session);
            var me = ProfileStore.Active;
            var room = session.Room;

            string status = !BackendConfig.IsConfigured ? "SET THE GAME SERVER TO PLAY ONLINE"
                : !me.IsSignedIn ? "SIGNING IN…"
                : session.State == OnlineSession.Status.Online ? $"ONLINE AS {me.Name.ToUpperInvariant()}"
                : session.StatusText.ToUpperInvariant();
            Frame("ONLINE", "flag", () =>
            {
                var s = session;
                Listen(null);
                if (!room.Started) s.Disconnect();
                close();
            });
            Caption(content, status, 820, UiKit.ArcadeYellow);

            if (BackendConfig.IsConfigured && !me.IsSignedIn) { if (!signingIn) SignInThenConnect(me); }
            else if (BackendConfig.IsConfigured && session.State == OnlineSession.Status.Offline) session.Connect(me.ServerToken);
            bool online = session.State == OnlineSession.Status.Online;

            if (!room.InRoom)
            {
                var server = TextField(content, "https://your-server", 0, 700, 900, 90, 30, 200);
                server.text = BackendConfig.ServerUrl;
                server.keyboardType = TouchScreenKeyboardType.URL;
                server.onEndEdit.AddListener(url =>
                {
                    if (url.Trim().TrimEnd('/') == BackendConfig.ServerUrl) return;
                    session.Disconnect();
                    BackendConfig.ServerUrl = url;
                    foreach (var p in ProfileStore.Book.Profiles) { p.ServerId = ""; p.ServerToken = ""; }
                    ProfileStore.Save();
                    ShowOnline();
                });
                string holesName = holes() == 0 ? "THE FULL ROUND" : $"HOLE {holes()}";
                if (online)
                {
                    string course = GameSetup.CourseIdFor(holes());
                    Pill(content, "QUICK MATCH", "play", 0, 540, 900, 130, UiKit.ArcadeYellow, UiKit.ArcadeInk, () => session.Send(OnlineMessage.Quick(course)), 48);
                    Pill(content, "CREATE A ROOM", "flag", 0, 390, 900, 110, UiKit.ArcadeBlue, Color.white, () => session.Send(OnlineMessage.Create(course)), 40);
                    var code = TextField(content, "CODE", -225, 250, 430, 110, 52, 4);
                    code.characterValidation = InputField.CharacterValidation.Alphanumeric;
                    Pill(content, "JOIN", "play", 235, 250, 410, 110, UiKit.ArcadeBlue, Color.white, () =>
                    {
                        if (code.text.Trim().Length == 4) session.Send(OnlineMessage.Join(code.text.Trim().ToUpperInvariant()));
                    }, 40);
                    Caption(content, $"A NEW ROOM PLAYS {holesName}  ·  CHANGE IT WITH COURSE", 150);
                }
                var board = Card(0, -300, 980, 520);
                leaderboardText = UiKit.Label(board, "Leaderboard", 34, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                leaderboardText.rectTransform.offsetMin = new Vector2(30, 20); leaderboardText.rectTransform.offsetMax = new Vector2(-30, -26);
                leaderboardText.color = Color.white; leaderboardText.raycastTarget = false;
                if (BackendConfig.IsConfigured) LoadLeaderboard();
            }
            else
            {
                var codeCard = Card(0, 620, 700, 190);
                var codeText = UiKit.Chunky(codeCard, "Code", 96, UiKit.ArcadeYellow, UiKit.ArcadeInk, 5f);
                codeText.text = room.Code;
                Caption(content, room.IsPublic ? "QUICK MATCH  ·  WAITING FOR GOLFERS" : "TELL YOUR FRIENDS THE CODE", 480);
                for (int i = 0; i < room.Players.Count; i++)
                {
                    var p = room.Players[i];
                    var row = Card(0, 360 - i * 130, 900, 112);
                    var dot = UiKit.Panel(row, "Colour", PlayerColor(i), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(60, 60));
                    dot.sprite = UiKit.Circle; dot.type = Image.Type.Simple; dot.raycastTarget = false; dot.rectTransform.pivot = new Vector2(0, 0.5f);
                    string tags = (p.id == room.HostId ? "  ·  HOST" : "") + (p.id == room.MyId ? "  ·  YOU" : "") + (p.connected ? "" : "  ·  AWAY");
                    var line = UiKit.Label(row, "Name", 40, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(110, 0), Vector2.zero, UiKit.Display, false);
                    line.rectTransform.offsetMin = new Vector2(110, 0);
                    line.text = p.name.ToUpperInvariant() + tags; line.color = Color.white; line.raycastTarget = false;
                }
                if (room.CanStart) Pill(content, "TEE OFF", "play", 0, -580, 900, 140, UiKit.ArcadeYellow, UiKit.ArcadeInk, () => session.Send(OnlineMessage.Start()), 52);
                else Caption(content, room.IsHost ? "WAITING FOR ANOTHER GOLFER…" : "THE HOST TEES OFF WHEN EVERYONE IS HERE", -580);
                Pill(content, "LEAVE ROOM", "menu", 0, -740, 900, 110, UiKit.Hex("DCEEFD"), UiKit.ArcadeBlue, () =>
                {
                    session.Send(OnlineMessage.Leave()); room.Reset(); ShowOnline();
                }, 40);
                if (room.LastError.Length > 0) Toast(room.LastError);
            }
        }

        async void SignInThenConnect(PlayerProfile me)
        {
            signingIn = true;
            bool ok = await BackendClient.SignUp(me);
            signingIn = false;
            if (!this || session == null) return;
            if (ok) session.Connect(me.ServerToken);
            if (page == Page.Online) ShowOnline();
            if (!ok) Toast(BackendClient.LastError);
        }

        async void LoadLeaderboard()
        {
            var target = leaderboardText;
            string course = GameSetup.CourseIdFor(holes());
            var board = await BackendClient.FetchLeaderboard(course, 8);
            if (!this || !target) return;
            var b = new StringBuilder(holes() == 0 ? "TOP ROUNDS\n\n" : $"TOP SCORES  ·  HOLE {holes()}\n\n");
            if (board == null) b.Append("THE LEADERBOARD IS OFFLINE");
            else if (board.entries.Length == 0) b.Append("NO SCORES YET — BE THE FIRST");
            for (int i = 0; board != null && i < board.entries.Length; i++)
                b.Append($"{i + 1}.  {board.entries[i].name.ToUpperInvariant()}   {Scorecard.FormatToPar(board.entries[i].bestToPar)}\n");
            target.text = b.ToString();
        }

        /// Follow the session while ONLINE is up: changes redraw it, and the host's TEE OFF sends
        /// everyone to the first tee.
        void Listen(OnlineSession s)
        {
            if (session != null)
            {
                session.Room.Changed -= OnRoomChanged;
                session.Room.RoundStarted -= OnRoundStarted;
                session.StateChanged -= OnRoomChanged;
            }
            session = s;
            if (s == null) return;
            s.Room.Changed += OnRoomChanged;
            s.Room.RoundStarted += OnRoundStarted;
            s.StateChanged += OnRoomChanged;
        }

        void OnRoomChanged() { if (this && session != null && page == Page.Online) ShowOnline(); }

        void OnRoundStarted(int seed, string course)
        {
            if (!this || session == null) return;
            var setup = GameSetup.Online(session.Room, ProfileStore.Active);
            Listen(null);
            play(setup);
        }

        // ----- Building blocks, in the arcade cards' look -----

        static readonly Vector2 Center = new(0.5f, 0.5f);

        /// A fresh page: the course dimmed behind, the title on a navy pill with its icon, and
        /// a round back button.
        void Frame(string title, string icon, Action back)
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            var sheet = UiKit.Panel(content, "Sheet", new Color(0.02f, 0.06f, 0.18f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;

            var head = UiKit.Pill(content, "Title", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 1), new Vector2(40, -100), new Vector2(780, 128), out var headFill, 5f);
            var disc = UiKit.Panel(headFill.transform, "Disc", UiKit.ArcadeYellow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(86, 86));
            disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Icons.Place(disc.transform, icon, UiKit.ArcadeInk, new Vector2(0.5f, 0.5f), Vector2.zero, 56);
            var t = UiKit.Chunky(headFill.transform, "Word", 60, Color.white, UiKit.ArcadeInk, 4f);
            t.text = title; t.rectTransform.offsetMin = new Vector2(80, 0);
            Icons.Fit(t, 36, 60);
            _ = head;

            var b = UiKit.Pill(content, "Back", UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(96, -100), new Vector2(112, 112), out var backFill, 5f);
            foreach (var img in b.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            var arrow = Icons.Place(backFill.transform, "play", Color.white, new Vector2(0.5f, 0.5f), new Vector2(-3, 0), 44);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            Hold(b, backFill, UiKit.ArcadeBlue, back);
        }

        /// A blue card (rounded box), centred at (x, y) from the middle of the screen.
        RectTransform Card(float x, float y, float w, float h)
        {
            var card = UiKit.Pill(content, "Card", UiKit.ArcadeBlue, Center, new Vector2(x, y), new Vector2(w, h), out var fill, 5f, false);
            foreach (var img in card.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            return fill.rectTransform;
        }

        /// A pill button with an icon disc and a word; `action` on the press.
        HoldButton Pill(Transform parent, string text, string icon, float x, float y, float w, float h, Color color, Color ink, Action action, int size = 40)
        {
            var b = UiKit.Pill(parent, text, color, Center, new Vector2(x, y), new Vector2(w, h), out var fill, 5f);
            float d = Mathf.Min(h - 30, 78);
            var disc = UiKit.Panel(fill.transform, "Disc", ink, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(d / 2 + 22, 0), new Vector2(d, d));
            disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Icons.Place(disc.transform, icon, color.a < 0.5f ? Color.white : color, new Vector2(0.5f, 0.5f), new Vector2(icon == "play" ? 3 : 0, 0), d * 0.55f);
            var l = UiKit.Label(fill.transform, "Word", size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            l.rectTransform.offsetMin = new Vector2(d + 30, 0); l.rectTransform.offsetMax = new Vector2(-24, 0);
            l.text = text; l.color = ink; l.raycastTarget = false;
            Icons.Fit(l, Mathf.Min(20, size), size);
            return Hold(b, fill, color, action);
        }

        HoldButton Hold(RectTransform button, Image fill, Color rest, Action action)
        {
            var hit = button.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var hold = button.gameObject.AddComponent<HoldButton>();
            hold.Fill = fill; hold.RestColor = rest;
            hold.Pressed = () => { Haptics.Tick(); action(); };
            return hold;
        }

        Text Caption(Transform parent, string text, float y, Color? color = null)
        {
            var l = UiKit.Label(parent, "Caption", 28, TextAnchor.MiddleCenter, Center, Center, new Vector2(0, y), new Vector2(960, 50), UiKit.Display, false);
            l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            l.text = text; l.color = color ?? Color.white; l.raycastTarget = false;
            Icons.Fit(l, 18, 28);
            return l;
        }

        /// Six white stat tiles, three to a row, like the round card's.
        void Tiles(float top, (string icon, string title, string value)[] tiles)
        {
            const float W = 980, gap = 18, tileH = 150;
            float hw = (W - 2 * gap) / 3f;
            for (int i = 0; i < tiles.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var tile = UiKit.Panel(content, tiles[i].title, Color.white, Center, Center,
                    new Vector2(-W / 2 + hw / 2 + col * (hw + gap), top - row * (tileH + gap) - tileH / 2), new Vector2(hw, tileH));
                tile.sprite = UiKit.RoundedLarge; tile.raycastTarget = false; tile.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var disc = UiKit.Panel(tile.transform, "Disc", UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -46), new Vector2(60, 60));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Icons.Place(disc.transform, tiles[i].icon, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 38);
                var ht = UiKit.Label(tile.transform, "Title", 22, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -46), new Vector2(-92, 56), UiKit.Display, false);
                ht.rectTransform.pivot = new Vector2(0, 0.5f); ht.rectTransform.sizeDelta = new Vector2(-92, 56);
                ht.text = tiles[i].title.ToUpperInvariant(); ht.color = UiKit.ArcadeBlue; ht.raycastTarget = false;
                Icons.Fit(ht, 16, 22);
                var hv = UiKit.Label(tile.transform, "Value", 42, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 42), new Vector2(0, 60), UiKit.Display, false);
                hv.text = tiles[i].value; hv.color = UiKit.ArcadeInk; hv.raycastTarget = false;
            }
        }

        /// A white one-line box for names, codes and the server; the phone's keyboard on a tap.
        InputField TextField(Transform parent, string placeholder, float x, float y, float w, float h, int size, int limit)
        {
            var box = UiKit.Panel(parent, "Field", Color.white, Center, Center, new Vector2(x, y), new Vector2(w, h));
            box.sprite = UiKit.RoundedLarge; box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hint = UiKit.Label(box.transform, "Placeholder", size, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            hint.rectTransform.offsetMin = new Vector2(30, 0); hint.rectTransform.offsetMax = new Vector2(-30, 0);
            hint.text = placeholder; hint.color = new Color(0.07f, 0.16f, 0.42f, 0.35f);
            var text = UiKit.Label(box.transform, "Text", size, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            text.rectTransform.offsetMin = new Vector2(30, 0); text.rectTransform.offsetMax = new Vector2(-30, 0);
            text.color = UiKit.ArcadeInk; text.supportRichText = false;
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text; field.placeholder = hint;
            field.characterLimit = limit; field.lineType = InputField.LineType.SingleLine;
            field.targetGraphic = box;
            return field;
        }

        void NameField(Transform parent, PlayerProfile profile, float x, float y, float w, int size = 52, float h = 110)
        {
            var field = TextField(parent, "NAME", x, y, w, h, size, PlayerProfile.MaxNameLength);
            field.text = profile.Name;
            field.onEndEdit.AddListener(value =>
            {
                profile.Rename(value);
                field.text = profile.Name;
                ProfileStore.Save();
                _ = BackendClient.PushProfile(profile);
            });
        }

        /// A short message near the foot (errors from the server).
        void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Caption(content, message.ToUpperInvariant(), -880, UiKit.Hex("FFB3A6"));
        }
    }
}
