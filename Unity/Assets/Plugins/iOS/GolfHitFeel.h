// What a racket or club strike feels like in the hand, written as Core Haptics events. Plain C++ with
// no Apple headers, so the shapes can be built and checked on a Mac (work/hit-feel/) as well as played
// by GolfHaptics.mm on the phone.
//
// A hit is up to five layers, each its own player so each can carry its own intensity curve:
//   contact  the crack (a transient) and the body behind it, which dies away: always there.
//            Clean contact is a bright, sharp crack; a scrappy one is duller. Heavier strokes carry more body.
//   sting    the frame rattling in the hand after an off-centre or mistimed ball, like the real thing.
//   reward   what the grade earned, kept after the crack so it never muddies it: a ring for EXCELLENT,
//            a ring and a sparkle for PERFECT, a second crack and a surge for a SUPERCHARGED ball.
//   shock    a smash's heavy aftershock.
//   pips     one small tap for each clean ball banked toward a supercharge ("two of three").
#pragma once
#include <cmath>
#include <vector>

namespace GolfFeel {

enum Style { Drive = 0, Touch = 1, Heavy = 2, Serve = 3, Scramble = 4, GolfFull = 5, GolfPutt = 6 };
/// The game's grade (TennisRules.Timing order).
enum Grade { Miss = 0, Ok = 1, Good = 2, Great = 3, Excellent = 4, Perfect = 5 };
enum Flag { Supercharged = 1 };
/// Moments that are not a strike. The TV-side ones are played late by the TV's delay, so the hand feels
/// them when the eyes see them.
enum Cue { Swing = 0, Whiff = 1, Bounce = 2, PointWon = 3, PointLost = 4, GameWon = 5, MatchWon = 6, Top = 7, Roar = 8 };
/// The phone's own speaker: a short knock that, unlike the TV's sound, arrives at once.
enum Sound { NoSound = -1, Tok = 0, Ting = 1, Clunk = 2, Click = 3, Tock = 4, SoundCount = 5 };

struct Hit
{
    int style, grade, pips, flags;
    float power;    // 0..1 how hard the swing was
    float center;   // 0..1, 1 = the sweet spot, 0 = the frame
    float mistime;  // 0..1, 0 = on time
    float heat;     // 0..1, how long the rally has run
};

struct Event { bool continuous; float time, duration, intensity, sharpness; };
struct Point { float time, value; };
/// One player's worth of events, and an optional intensity-control curve (a 0..1 multiplier).
struct Layer { const char *name; std::vector<Event> events; std::vector<Point> curve; };
typedef std::vector<Layer> Score;

/// Core Haptics refuses a parameter curve with more control points than this.
const int MaxCurvePoints = 16;

inline float C01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
inline float Mix(float a, float b, float t) { return a + (b - a) * C01(t); }

inline void Scale(Score &s, float strength)
{
    float k = strength < .2f ? .2f : (strength > 1 ? 1 : strength);
    for (Layer &l : s) for (Event &e : l.events) e.intensity = C01(e.intensity * k);
}

/// How much mass the stroke carries into the hand.
inline float Weight(int style, float power)
{
    switch (style)
    {
        case Touch: return Mix(.25f, .55f, power);
        case Heavy: return 1;
        case Serve: return Mix(.6f, 1, power);
        case Scramble: return .45f;
        case GolfFull: return Mix(.55f, 1, power);
        case GolfPutt: return Mix(.15f, .5f, power);
        default: return Mix(.45f, 1, power);
    }
}

/// How pure the contact was, 0..1: where it met the strings and when, pulled toward what the grade says
/// so the hand always agrees with the call on the TV.
inline float Clean(const Hit &h)
{
    static const float byGrade[6] = { .1f, .3f, .55f, .75f, .9f, 1 };
    int g = h.grade < 0 ? 0 : (h.grade > 5 ? 5 : h.grade);
    float measured = C01(h.center) * (1 - .7f * C01(h.mistime));
    return Mix(measured, byGrade[g], .5f);
}

/// The frame rattle, 0 for a clean ball up to 1 for one off the frame.
inline float Sting(const Hit &h) { return h.style == GolfPutt ? 0 : C01((1 - Clean(h)) * 1.25f - .3f); }

inline Score BuildHit(const Hit &h, float strength)
{
    Score s;
    float p = C01(h.power), heat = C01(h.heat);
    float weight = Weight(h.style, p), clean = Clean(h), sting = Sting(h);
    bool supercharged = (h.flags & Supercharged) != 0;
    bool putt = h.style == GolfPutt;

    // 1. Contact. The crack is the hit; the body under it gives the weight and fades within a few dozen ms.
    Layer contact = { "contact", {}, {} };
    float crackI = putt ? Mix(.35f, .75f, p) : Mix(.6f, 1, weight) * (h.grade >= Excellent ? 1 : Mix(.85f, 1, clean));
    float crackS = putt ? .7f : Mix(.3f, 1, clean);
    float bodyLen = h.style == Heavy ? .09f : putt ? .02f : Mix(.03f, .07f, weight);
    float bodyI = Mix(.3f, .8f, weight) * Mix(1, 1.15f, heat);
    contact.events.push_back({ false, 0, 0, C01(crackI), crackS });
    contact.events.push_back({ true, .004f, bodyLen, C01(bodyI), Mix(.12f, .32f, clean) });
    contact.curve = { { 0, 1 }, { .004f, 1 }, { .004f + bodyLen, .1f } };
    s.push_back(contact);

    // 2. Sting: a dull rattle (about 40 Hz of tremolo on a low, buzzy texture) that dies away.
    if (sting > .05f)
    {
        Layer l = { "sting", {}, {} };
        float len = Mix(.07f, .16f, sting), start = .012f;
        l.events.push_back({ true, start, len, Mix(.35f, .85f, sting), Mix(.15f, .03f, sting) });
        for (float t = 0; t + .0125f < len && (int)l.curve.size() <= MaxCurvePoints - 3; t += .025f)
        {
            float env = 1 - t / len;
            l.curve.push_back({ start + t, env });
            l.curve.push_back({ start + t + .0125f, env * .35f });
        }
        l.curve.push_back({ start + len, 0 });
        s.push_back(l);
    }

    // 3. Reward, after the crack.
    if (supercharged)
    {
        Layer l = { "surge", {}, {} };
        l.events.push_back({ false, .07f, 0, 1, .9f });           // the second crack
        l.events.push_back({ true, .1f, .22f, .95f, .55f });      // the ball leaving charged
        l.curve = { { 0, 1 }, { .1f, .25f }, { .2f, 1 }, { .32f, 0 } };
        s.push_back(l);
    }
    else if (h.grade >= Perfect)
    {
        Layer l = { "ring", {}, {} };
        l.events.push_back({ true, .03f, .1f, Mix(.45f, .6f, heat), 1 });   // the strings ringing bright
        l.events.push_back({ false, .14f, 0, Mix(.55f, .75f, heat), 1 });   // and the sparkle on top
        l.curve = { { .03f, 1 }, { .13f, .1f }, { .135f, 1 } };
        s.push_back(l);
    }
    else if (h.grade == Excellent)
    {
        Layer l = { "ring", {}, {} };
        l.events.push_back({ true, .03f, .06f, Mix(.32f, .45f, heat), .95f });
        l.curve = { { .03f, 1 }, { .09f, .1f } };
        s.push_back(l);
    }

    // 4. A smash lands like a hammer: a low aftershock behind the crack.
    if (h.style == Heavy)
        s.push_back({ "shock", { { false, .08f, 0, .7f, .2f } }, {} });

    // 5. Streak pips, spaced wide enough to count.
    if (h.pips > 0 && !supercharged)
    {
        Layer l = { "pips", {}, {} };
        int n = h.pips > 4 ? 4 : h.pips;
        for (int i = 0; i < n; i++) l.events.push_back({ false, .22f + .07f * i, 0, .4f, .7f });
        s.push_back(l);
    }

    Scale(s, strength);
    return s;
}

inline Score BuildCue(int kind, float intensity, float strength)
{
    float k = C01(intensity);
    Score s;
    switch (kind)
    {
        case Swing:   // the phone felt the swing: a soft swish, nothing like a contact
            s.push_back({ "swish", { { true, 0, .05f, .3f * k, .15f } }, { { 0, .3f }, { .02f, 1 }, { .05f, 0 } } });
            break;
        case Whiff:   // a swing through air
            s.push_back({ "air", { { true, 0, .16f, .4f * k, .1f } }, { { 0, 0 }, { .06f, 1 }, { .16f, 0 } } });
            break;
        case Bounce:  // the ball landing on your side: a light knock to swing off
            s.push_back({ "bounce", { { false, 0, 0, .32f * k, .45f } }, {} });
            break;
        case PointWon:
            s.push_back({ "won", { { false, 0, 0, .45f * k, .45f }, { false, .09f, 0, .6f * k, .65f }, { false, .18f, 0, .85f * k, .9f } }, {} });
            break;
        case PointLost:
            s.push_back({ "lost", { { false, 0, 0, .45f * k, .3f }, { false, .14f, 0, .28f * k, .12f } }, {} });
            break;
        case GameWon:
        case MatchWon:
        {
            bool match = kind == MatchWon;
            Layer taps = { "won", {}, {} };
            int n = match ? 6 : 3;
            for (int i = 0; i < n; i++)
                taps.events.push_back({ false, .09f * i, 0, Mix(.45f, .95f, (float)i / (n - 1)) * k, Mix(.45f, .95f, (float)i / (n - 1)) });
            s.push_back(taps);
            float at = .09f * n, len = match ? 1.f : .5f;
            s.push_back({ "crowd", { { true, at, len, .55f * k, .3f } }, { { at, 1 }, { at + len, 0 } } });
            break;
        }
        case Top:     // the top of a golf backswing: one crisp click as the club turns for home
            s.push_back({ "top", { { false, 0, 0, Mix(.55f, 1, k), 1 } }, {} });
            break;
        default:      // Roar: the gallery going up for a holed ball, a rolling run of thumps
        {
            Layer run = { "roar", {}, {} };
            for (int i = 0; i < 9; i++) run.events.push_back({ false, .085f * i, 0, (.9f - i * .07f) * Mix(.5f, 1, k), .35f + .05f * (i % 3) });
            run.events.push_back({ true, 0, .8f, .25f * k, .2f });
            s.push_back(run);
            break;
        }
    }
    Scale(s, strength);
    return s;
}

/// Which knock the phone's own speaker gives for a hit.
inline int SoundFor(const Hit &h)
{
    if (h.style == GolfPutt) return Tock;
    if ((h.flags & Supercharged) || h.grade >= Excellent) return Ting;
    if (Sting(h) > .5f) return Clunk;
    return h.style == GolfFull ? Click : Tok;
}

/// The speaker knock as mono samples at `rate`: a few damped partials (all above 600 Hz, which is where
/// a phone speaker starts to speak) and a click of noise at the front. Short and dry, and pitched unlike
/// the TV's hit sound so the two never read as one sound played twice.
inline std::vector<float> RenderSound(int sound, int rate = 44100)
{
    struct Partial { float hz, amp, tau; };
    std::vector<Partial> partials;
    float length = .05f, noise = .25f, noiseLen = .0015f, peak = .7f, drive = 0;
    switch (sound)
    {
        case Ting: partials = { { 1500, .8f, .012f }, { 3600, .45f, .05f }, { 5400, .2f, .03f } }; length = .14f; noise = .2f; noiseLen = .001f; break;
        case Clunk: partials = { { 700, 1, .018f }, { 1050, .6f, .014f } }; length = .07f; noise = .35f; noiseLen = .004f; drive = 3; break;
        case Click: partials = { { 2600, .8f, .012f }, { 4100, .5f, .008f }, { 6500, .3f, .005f } }; noise = .3f; noiseLen = .001f; break;
        case Tock: partials = { { 900, .6f, .01f }, { 1800, .2f, .005f } }; length = .04f; noise = .1f; noiseLen = .001f; peak = .45f; break;
        default: partials = { { 1250, 1, .009f }, { 2900, .35f, .004f } }; length = .045f; break;
    }
    int n = (int)(length * rate);
    std::vector<float> x(n);
    unsigned seed = 0x9e3779b9u + (unsigned)sound;
    float top = 0;
    for (int i = 0; i < n; i++)
    {
        float t = (float)i / rate, v = 0;
        for (const Partial &p : partials) v += p.amp * std::exp(-t / p.tau) * std::sin(6.2831853f * p.hz * t);
        seed = seed * 1664525u + 1013904223u;
        if (t < noiseLen) v += noise * (((seed >> 8) & 0xffff) / 32768.f - 1) * (1 - t / noiseLen);
        if (drive > 0) v = std::tanh(drive * v) / std::tanh(drive);
        v *= t < .0002f ? t / .0002f : 1;                       // a 0.2 ms onset, so the start is a knock and not a pop
        v *= i > n - rate / 1000 ? (float)(n - i) / (rate / 1000) : 1;   // and no click at the end
        x[i] = v;
        top = std::fabs(v) > top ? std::fabs(v) : top;
    }
    if (top > 0) for (float &v : x) v *= peak / top;
    return x;
}

/// The feel test on the phone's Settings: every hit and moment, in an order that teaches the ladder.
struct Demo { const char *name; bool cue; int kind; Hit hit; };
inline const std::vector<Demo> &Demos()
{
    static const std::vector<Demo> list = {
        { "Perfect forehand", false, 0, { Drive, Perfect, 0, 0, .8f, 1, 0, .3f } },
        { "Excellent forehand", false, 0, { Drive, Excellent, 0, 0, .8f, .9f, .1f, .3f } },
        { "Great drive", false, 0, { Drive, Great, 0, 0, .7f, .85f, .2f, .3f } },
        { "Good, a bit late", false, 0, { Drive, Good, 0, 0, .7f, .7f, .4f, .3f } },
        { "Off centre", false, 0, { Drive, Ok, 0, 0, .7f, .3f, .3f, .3f } },
        { "Off the frame", false, 0, { Drive, Ok, 0, 0, .7f, .05f, .7f, .3f } },
        { "Volley (touch shot)", false, 0, { Touch, Great, 0, 0, .4f, .85f, .2f, .3f } },
        { "Smash", false, 0, { Heavy, Great, 0, 0, 1, .85f, .2f, .3f } },
        { "Perfect serve", false, 0, { Serve, Perfect, 0, 0, .95f, 1, 0, 0 } },
        { "Streak: two of three", false, 0, { Drive, Great, 2, 0, .7f, .85f, .2f, .3f } },
        { "SUPERCHARGED", false, 0, { Drive, Perfect, 0, Supercharged, .8f, 1, 0, .5f } },
        { "Swing, no ball", true, Whiff, {} },
        { "Ball lands on your side", true, Bounce, {} },
        { "Point won", true, PointWon, {} },
        { "Point lost", true, PointLost, {} },
        { "Game won", true, GameWon, {} },
        { "Match won", true, MatchWon, {} },
        { "Golf: pure strike", false, 0, { GolfFull, Perfect, 0, 0, .9f, 1, 0, 0 } },
        { "Golf: solid", false, 0, { GolfFull, Great, 0, 0, .8f, .85f, .2f, 0 } },
        { "Golf: thin", false, 0, { GolfFull, Ok, 0, 0, .8f, .25f, .4f, 0 } },
        { "Golf: putt", false, 0, { GolfPutt, Great, 0, 0, .4f, .9f, .1f, 0 } },
        { "Holed! (gallery)", true, Roar, {} },
    };
    return list;
}

}
