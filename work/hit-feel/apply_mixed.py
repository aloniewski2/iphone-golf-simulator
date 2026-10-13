#!/usr/bin/env python3
"""Applies the hit-feel + responsiveness edits to files that other work also changes, by exact anchors, so only these
hunks move between trees (the clean gameplay worktree and the shared build-10/12 tree).
usage: apply_mixed.py <repo root>    Idempotent: an edit whose result is already there is skipped. Exit 1 on a missing anchor.
"""
import sys, os

EDITS = {}
ALTERNATIVES = {}   # path -> [(old, new)]: exactly one of them must apply (the same edit, worded differently per tree)
def edit(path, old, new): EDITS.setdefault(path, []).append((old, new))

# ---- Haptics (hit feel) ----------------------------------------------------------------------------------------------
T = 'Unity/Assets/Scripts/Tennis/TennisGame.cs'
edit(T, '''        /// The phone has confirmed a real swing: one light tick in the hand, straight away. The TV runs 100-250 ms behind the
        /// game, so this is the only feedback that arrives on time. It follows the phone's confirmation, never a raw onset, so
        /// a step or a flinch the detector writes off does not tick (Plan 2: no juice on input). Motion play only; touch play
        /// already feels its own finger.
        void SwingConfirmedTick() { if (NativeControlled && !NativeSportsSession.Touch) Haptics.Tick(); }''',
'''        /// The phone has confirmed a real swing: a soft swish in the hand, straight away. The TV runs 100-250 ms behind the
        /// game, so this is the only feedback that arrives on time. It follows the phone's confirmation, never a raw onset, so
        /// a step or a flinch the detector writes off does not tick (Plan 2: no juice on input). Not when a hit is already
        /// waiting on this confirmation: the strike follows at once and should land alone, not as a tick and a thock.
        /// Motion play only; touch play already feels its own finger.
        void SwingConfirmedTick() { if (NativeControlled && !NativeSportsSession.Touch && !pending.Active) Haptics.SwingFelt(); }''')
edit(T, '            if (NativeControlled) Haptics.Strike(pendingServePower, verdict.Perfect);',
        '            if (NativeControlled) Haptics.Hit(TennisHitFeel.Serve(verdict));')
edit(T, '            if (NativeControlled) { if (toPlayer) Haptics.Success(); else Haptics.Warning(); }',
'''            // Felt as the TV shows it (the TV runs `Lag` behind): a rising run for a point, more for a game, a roll for the match.
            if (NativeControlled)
                Haptics.Play(!toPlayer ? Haptics.Cue.PointLost : Match.Complete ? Haptics.Cue.MatchWon : gameWon ? Haptics.Cue.GameWon : Haptics.Cue.PointWon, 1, Lag);''')
edit(T, '''                    Landed?.Invoke(byPlayer, landedIn, BallPosition, serveInFlight);
                }''',
'''                    Landed?.Invoke(byPlayer, landedIn, BallPosition, serveInFlight);
                    // The rival's ball landing in on your side: a light knock in the hand as the TV shows it, the beat to swing off.
                    if (NativeControlled && !byPlayer && landedIn && overDeck) Haptics.Play(Haptics.Cue.Bounce, 1, Lag);
                }''')
edit(T, '            if (NativeControlled) Haptics.Strike(hit.Quality, LastWasSupercharged);',
        '            if (NativeControlled) Haptics.Hit(TennisHitFeel.Rally(LastGrade, faceOffset, hitLateness, Player.Kind, Player.Power, Streak, LastWasSupercharged, RallyShots));')
edit(T, '''            if (sounds) sounds.Whiff();
            if (juice) juice.Trigger(TennisJuice.Beat.Whiff, Player.transform);
        }''',
'''            if (sounds) sounds.Whiff();
            if (juice) juice.Trigger(TennisJuice.Beat.Whiff, Player.transform);
            if (NativeControlled) Haptics.Play(Haptics.Cue.Whiff);
        }''')

edit('Unity/Assets/Scripts/Game/GolfGame.cs', 'if (club == GolfClub.Putter) Haptics.Impact(LastShot.Power); else',
     'if (club == GolfClub.Putter) Haptics.Putt(LastShot.Power); else')

N = 'Unity/Assets/Scripts/Game/NativeSportsSession.cs'
edit(N, 'Haptics.Enabled=m.haptics;', 'Haptics.Enabled=m.haptics; Haptics.Warm();')
edit(N, 'case "haptics": Haptics.Enabled=m.value>0; Haptics.Release(); break;',
        'case "haptics": Haptics.Enabled=m.value>0; Haptics.Release(); Haptics.Warm(); break;')
edit('Unity/Assets/Scripts/Game/NativeSportsSession.Hub.cs', 'Haptics.Enabled = m.haptics;', 'Haptics.Enabled = m.haptics; Haptics.Warm();')

S = 'GolfArcade/Unity/SportsSession.swift'
edit(S, '''    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
''', '''    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
    /// How hard hits land in the hand, 0.55 soft .. 1 full (RacketFeel.strengths).
    var hapticStrength = (UserDefaults.standard.object(forKey:"arcade.hapticStrength") as? Double) ?? 1 {
        didSet { UserDefaults.standard.set(hapticStrength,forKey:"arcade.hapticStrength"); RacketFeel.apply(self) }
    }
    /// A short knock from the phone's own speaker on each hit (system sounds skip the AirPlay route, so it is on time).
    var racketSound = UserDefaults.standard.bool(forKey:"arcade.racketSound") {
        didSet { UserDefaults.standard.set(racketSound,forKey:"arcade.racketSound"); RacketFeel.apply(self) }
    }
''')
edit(S, '''        catch { failStartup(error.localizedDescription); return }
        SportsRuntime.shared().clearTennisResult()
''', '''        catch { failStartup(error.localizedDescription); return }
        RacketFeel.apply(self)
        SportsRuntime.shared().clearTennisResult()
''')
# The plaza's runtime load reads `controllerReplica: phone` on build-10/12 and `: false` at 2fe849f0.
for replica in ('phone', 'false'):
    ALTERNATIVES.setdefault('GolfArcade/Unity/HubSession.swift', []).append(
        (f'            do {{ try SportsRuntime.shared().load(in: window, controllerReplica: {replica}) }}\n',
         f'            do {{ try SportsRuntime.shared().load(in: window, controllerReplica: {replica}); RacketFeel.apply() }}\n'))

M = 'GolfArcade/Unity/TennisMenu.swift'
edit(M, '''    private(set) var notice = ""
''', '''    private(set) var notice = ""
    /// Settings → Audio → Feel test: which entry was played last, and its name.
    private(set) var feelTestLabel = ""
    private var feelTestShown = 0
''')
edit(M, '        case .audio: ["sound", "haptics", "partyVoice", "partyRecording"]',
        '        case .audio: ["sound", "haptics", "hapticStrength", "racketSound", "feelTest", "partyVoice", "partyRecording"]')
edit(M, '''        case "haptics": s.haptics.toggle()
''', '''        case "haptics": s.haptics.toggle()
        // A strength or sound change plays a hit straight away, so the player feels (and hears) what they chose.
        case "hapticStrength":
            s.hapticStrength = RacketFeel.strengths[cycle(RacketFeel.strengthIndex(s.hapticStrength), RacketFeel.strengths.count)].level
            _ = RacketFeel.playDemo(2)
        case "racketSound": s.racketSound.toggle(); _ = RacketFeel.playDemo(2)
        case "feelTest":
            let i = feelTestLabel.isEmpty ? 0 : feelTestShown + step
            feelTestShown = i
            feelTestLabel = RacketFeel.playDemo(i) ?? "Open the plaza or a match first: the feel test plays through the game."
''')

# ---- Responsiveness (added below) -----------------------------------------------------------------------------------
RESPONSIVE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'apply_responsive.py')
if os.path.exists(RESPONSIVE):
    exec(open(RESPONSIVE).read())


def main(root):
    failed = 0
    for path, edits in EDITS.items():
        p = os.path.join(root, path)
        s = open(p).read()
        for old, new in edits:
            if new in s:
                print(f'skip (already there)  {path}: {new.strip().splitlines()[0][:70]}')
            elif s.count(old) == 1:
                s = s.replace(old, new); print(f'applied              {path}: {new.strip().splitlines()[0][:70]}')
            else:
                failed += 1; print(f'MISSING ANCHOR x{s.count(old)} {path}: {old.strip().splitlines()[0][:70]}')
        open(p, 'w').write(s)
    for path, options in ALTERNATIVES.items():
        p = os.path.join(root, path)
        s = open(p).read()
        done = [n for o, n in options if n in s]
        found = [(o, n) for o, n in options if s.count(o) == 1]
        if done: print(f'skip (already there)  {path}: {done[0].strip()[:70]}')
        elif found:
            s = s.replace(*found[0]); open(p, 'w').write(s); print(f'applied              {path}: {found[0][1].strip()[:70]}')
        else: failed += 1; print(f'MISSING ANCHOR       {path}: none of {len(options)} variants')
    print('FAILED' if failed else 'OK')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
