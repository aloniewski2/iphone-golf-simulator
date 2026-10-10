using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// The Plaza's map as plain data (PLAN_MenuHub_WalkableWorld §2): the outdoor plaza, five rooms placed off-stage, the doors that
    /// pair them, and the stations and bays inside. HubWorld builds the greybox from this; tests read it without a scene.
    ///
    /// Everything is in metres, y up. The plaza sits at the origin and the camera looks north (+z), so "up" on the phone stick walks
    /// toward the PLAY hall. Each interior is a box far below the plaza (y = -1000) and 200 m from its neighbours, so no camera ever
    /// draws two places at once; a door is a 0.4 s walk-through with a fade, never a scene load.
    public static class HubLayout
    {
        public enum Kind { Door, Station, Bay }

        public sealed class Place
        {
            public string id, title;
            public Vector3 origin;          // floor centre
            public Vector2 size;            // interior width (x) and depth (z); the plaza is a disc of radius size.x / 2
            public bool outdoor;
            public Color floor, wall, accent;
        }

        /// A walk-through doorway. `position` is on the floor in the middle of the threshold line; `inward` points from the walkable
        /// side into the doorway. Walking across the threshold moving along `inward` takes you to `to`.
        public sealed class Door
        {
            public string id, place, to, label;
            public Vector3 position, inward;
            public float width = 2.2f;
            public bool locked;             // the "coming soon" arch: shows a label, never transfers
            public Vector3 Arrival => position - inward * 1.7f;
        }

        /// Something you stand at and press A: a locker rack, a settings desk, a queue bay. `id` is what the phone receives.
        public sealed class Spot
        {
            public string id, place, label, detail;
            public Kind kind;
            public Vector3 position, facing;  // where the hero stands, and the way they face while using it
            public float radius = 1.6f;
            public int seats = 1;
        }

        public const float InteriorDepth = -1000f;
        public const string Plaza = "plaza", Locker = "locker", Clubhouse = "clubhouse", PlayHall = "play", TennisRoom = "tennis", GolfRoom = "golf";
        public static readonly Vector3 Spawn = new Vector3(0, 0, -7.5f);
        public const float PlazaRadius = 13.6f;
        /// The emote stage (and the lavender ring around it).
        public static readonly Vector3 Stage = new Vector3(0, 0, 1f);

        static readonly Color Sand = new Color(.93f, .86f, .80f), Navy = new Color(.25f, .33f, .62f), Lime = new Color(.70f, .84f, .26f),
            Cream = new Color(.95f, .92f, .86f), Clay = new Color(.86f, .45f, .30f), Court = new Color(.30f, .46f, .76f), Turf = new Color(.38f, .62f, .30f);

        public static readonly Place[] Places =
        {
            new Place { id = Plaza, title = "The Plaza", origin = Vector3.zero, size = new Vector2(PlazaRadius * 2, PlazaRadius * 2), outdoor = true, floor = Sand, wall = Cream, accent = Navy },
            new Place { id = Locker, title = "Locker Room", origin = new Vector3(-400, InteriorDepth, 0), size = new Vector2(10, 12), floor = new Color(.80f, .74f, .68f), wall = Cream, accent = Navy },
            new Place { id = Clubhouse, title = "Clubhouse", origin = new Vector3(-200, InteriorDepth, 0), size = new Vector2(10, 12), floor = new Color(.62f, .47f, .36f), wall = Cream, accent = new Color(.84f, .66f, .26f) },
            new Place { id = PlayHall, title = "PLAY Hall", origin = new Vector3(0, InteriorDepth, 0), size = new Vector2(12, 14), floor = new Color(.86f, .84f, .82f), wall = Cream, accent = Navy },
            new Place { id = TennisRoom, title = "Tennis", origin = new Vector3(200, InteriorDepth, 0), size = new Vector2(14, 16), floor = Court, wall = Cream, accent = Lime },
            new Place { id = GolfRoom, title = "Golf", origin = new Vector3(400, InteriorDepth, 0), size = new Vector2(14, 16), floor = Turf, wall = Cream, accent = new Color(.96f, .96f, .92f) },
        };

        /// The three plaza buildings, as drum centres and radii; each door sits on its drum facing `face` (the side pavilions turn
        /// toward the arriving player, like concept 03).
        public static readonly (string id, Vector3 centre, float radius, float height, string label, Vector3 face)[] Pavilions =
        {
            ("locker", new Vector3(-13.6f, 0, 3f), 4.6f, 4.2f, "LOCKER", new Vector3(0, 0, -4f)),
            ("clubhouse", new Vector3(13.6f, 0, 3f), 4.6f, 4.2f, "CLUBHOUSE", new Vector3(0, 0, -4f)),
            ("play", new Vector3(0, 0, 17.2f), 5.6f, 9f, "PLAY", Vector3.zero),
        };
        public static Vector3 Facing(Vector3 centre, Vector3 face) { var d = face - centre; d.y = 0; return d.normalized; }

        static Place P(string id) => Array.Find(Places, p => p.id == id);
        public static Place PlaceOf(string id) => P(id);

        /// A door on a plaza pavilion: on the drum surface, facing the plaza centre.
        static Door PavilionDoor(string pavilion, string to, string label)
        {
            var pv = Array.Find(Pavilions, x => x.id == pavilion);
            var toCentre = Facing(pv.centre, pv.face);
            return new Door { id = "door-plaza-" + pavilion, place = Plaza, to = "door-" + to + "-exit", label = label,
                              position = pv.centre + toCentre * (pv.radius + .35f), inward = -toCentre, width = 2.6f };
        }
        /// The way out of a room: in the middle of its south wall.
        static Door ExitDoor(string place, string to, string label)
        {
            var p = P(place);
            return new Door { id = "door-" + place + "-exit", place = place, to = to, label = label,
                              position = p.origin + new Vector3(0, 0, -p.size.y / 2 + .35f), inward = Vector3.back, width = 2.4f };
        }
        /// An archway in the PLAY hall's north wall.
        static Door HallArch(float x, string to, string label, bool locked = false)
        {
            var p = P(PlayHall);
            return new Door { id = "door-play-" + (locked ? "soon" : to), place = PlayHall, to = locked ? null : "door-" + to + "-exit", label = label,
                              position = p.origin + new Vector3(x, 0, p.size.y / 2 - .35f), inward = Vector3.forward, width = 2.6f, locked = locked };
        }

        public static readonly Door[] Doors =
        {
            PavilionDoor("locker", Locker, "Locker"),
            PavilionDoor("clubhouse", Clubhouse, "Clubhouse"),
            PavilionDoor("play", PlayHall, "Play"),
            ExitDoor(Locker, "door-plaza-locker", "Plaza"),
            ExitDoor(Clubhouse, "door-plaza-clubhouse", "Plaza"),
            ExitDoor(PlayHall, "door-plaza-play", "Plaza"),
            HallArch(-3.6f, TennisRoom, "Tennis"),
            HallArch(3.6f, GolfRoom, "Golf"),
            HallArch(0, null, "Coming soon", true),
            ExitDoor(TennisRoom, "door-play-tennis", "Play Hall"),
            ExitDoor(GolfRoom, "door-play-golf", "Play Hall"),
        };
        public static Door DoorById(string id) => Array.Find(Doors, d => d.id == id);

        /// Where you come out when you walk through `door`: in front of its partner, facing into the partner's place.
        public static bool Destination(Door door, out Door partner)
        {
            partner = door == null || door.locked || string.IsNullOrEmpty(door.to) ? null : DoorById(door.to);
            return partner != null;
        }

        static Spot S(string place, string id, Kind kind, string label, string detail, float x, float z, Vector3 facing, int seats = 1)
        {
            var p = P(place);
            return new Spot { id = id, place = place, kind = kind, label = label, detail = detail, position = p.origin + new Vector3(x, 0, z), facing = facing, seats = seats };
        }

        /// Stations (Locker, Clubhouse) and bays (sport rooms). Ids are what the phone panels key on.
        public static readonly Spot[] Spots =
        {
            // Locker: a rack per slot along the walls, the look station in front of the mirror (north wall)
            S(Locker, "rack-shirt", Kind.Station, "Shirt", "Shirt colour", -3.6f, 2.5f, Vector3.left),
            S(Locker, "rack-shorts", Kind.Station, "Shorts", "Shorts colour", -3.6f, -.5f, Vector3.left),
            S(Locker, "rack-shoes", Kind.Station, "Shoes", "Shoe colour", -3.6f, -3.5f, Vector3.left),
            S(Locker, "rack-racket", Kind.Station, "Racket", "Racket colour", 3.6f, 2.5f, Vector3.right),
            S(Locker, "rack-club", Kind.Station, "Clubs", "Golf gear", 3.6f, -.5f, Vector3.right),
            S(Locker, "look-mirror", Kind.Station, "Mirror", "Skin, body and hand", 0, 3.6f, Vector3.forward),
            S(Locker, "emote-mirror", Kind.Station, "Emotes", "Rehearse and equip emotes", 3.6f, -3.5f, Vector3.right),
            // Clubhouse: settings desk, how-to screen, trophy shelf, feedback mailbox, invite board
            S(Clubhouse, "desk-settings", Kind.Station, "Settings", "Gameplay, controls, display, audio", -3.4f, 2.8f, Vector3.left),
            S(Clubhouse, "screen-howto", Kind.Station, "How to play", "Tennis and golf basics", 0, 4.0f, Vector3.forward),
            S(Clubhouse, "shelf-trophies", Kind.Station, "Trophies", "Campaign progress and records", 3.4f, 2.8f, Vector3.right),
            S(Clubhouse, "mailbox-feedback", Kind.Station, "Feedback", "Send the team a note", 3.4f, -2.2f, Vector3.right),
            S(Clubhouse, "board-invite", Kind.Station, "Invite", "Invite friends to your plaza", -3.4f, -2.2f, Vector3.left),
            // Tennis room: four queue bays along the north wall and the sides
            S(TennisRoom, "bay-tennis-exhibition", Kind.Bay, "Exhibition", "Quick match on any court", -4.6f, 4.6f, Vector3.forward),
            S(TennisRoom, "bay-tennis-campaign", Kind.Bay, "Campaign", "The Island Circuit", 4.6f, 4.6f, Vector3.forward),
            S(TennisRoom, "bay-tennis-training", Kind.Bay, "Training", "Rally with the coach", -4.6f, -1.2f, Vector3.left),
            S(TennisRoom, "bay-tennis-online", Kind.Bay, "Online", "Play friends online", 4.6f, -1.2f, Vector3.right, 4),
            // Golf room: three bays
            S(GolfRoom, "bay-golf-round", Kind.Bay, "Round", "Play a course", -4.6f, 4.6f, Vector3.forward),
            S(GolfRoom, "bay-golf-online", Kind.Bay, "Online", "Play friends online", 4.6f, 4.6f, Vector3.forward, 4),
            S(GolfRoom, "bay-golf-pass", Kind.Bay, "Pass the phone", "Local party golf", 0, -1.2f, Vector3.forward, 4),
        };
        public static Spot SpotById(string id) => Array.Find(Spots, s => s.id == id);

        /// Quick-travel targets for the phone's ☰ menu: a place id arrives just inside its entrance; a spot id stands at the spot.
        public static bool QuickTravel(string target, out Vector3 position, out Vector3 facing, out string place)
        {
            var spot = SpotById(target);
            if (spot != null) { position = spot.position - spot.facing * .2f; facing = spot.facing; place = spot.place; return true; }
            if (target == Plaza) { position = Spawn; facing = Vector3.forward; place = Plaza; return true; }
            var exit = Array.Find(Doors, d => d.place == target && d.id.EndsWith("-exit"));
            if (exit != null) { position = exit.Arrival; facing = -exit.inward; place = target; return true; }
            position = default; facing = Vector3.forward; place = null; return false;
        }

        /// Which place contains a world point (the plaza if none of the rooms do).
        public static string PlaceAt(Vector3 p)
        {
            foreach (var place in Places)
            {
                if (place.outdoor) continue;
                var d = p - place.origin;
                if (Mathf.Abs(d.y) < 50 && Mathf.Abs(d.x) <= place.size.x / 2 + 3 && Mathf.Abs(d.z) <= place.size.y / 2 + 3) return place.id;
            }
            return Plaza;
        }

        /// Straight-line walking distances from the spawn to every plaza door (the "every door within 8 s" design rule).
        public static IEnumerable<(Door door, float metres)> SpawnDistances()
        {
            foreach (var d in Doors) if (d.place == Plaza) yield return (d, Vector3.Distance(Spawn, d.position));
        }
    }
}
