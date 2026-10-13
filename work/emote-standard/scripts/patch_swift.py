"""EMOTE STANDARD: the iPhone app's side. The emotion set joins the emote catalog (locker, phone remote, plaza / golf / tennis buttons all read it)
and the TV locker's focus grid grows one row per three emotes.  python3 patch_swift.py <repo> [--check]
Asserted exact replacements; originals kept once in work/emote-standard/rollback/code/."""
import os
import shutil
import sys

W = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
repo = sys.argv[1]
check = '--check' in sys.argv
RB = W + '/rollback/code'
os.makedirs(RB, exist_ok=True)
EDITS = {
    'GolfArcade/Domain/Player.swift': [
        ('    static let ids = ["scuba", "thrust", "spike", "wave", "bringIt", "pushups"]\n'
         '    static let names = ["Scuba", "Thrust", "Spike", "Wave", "Bring it", "Pushups"]',
         '    static let ids = ["scuba", "thrust", "spike", "wave", "bringIt", "pushups", "jumpJoy", "fistPump", "stomp", "slump", "shock", "victory", "defeat"]\n'
         '    static let names = ["Scuba", "Thrust", "Spike", "Wave", "Bring it", "Pushups", "Jump for Joy", "Fist Pump", "Stomp", "Slump", "Shock", "Victory", "Defeat"]'),
    ],
    'GolfArcade/Unity/TennisMenu.swift': [
        ('            return [tabs, (0..<3).map { "lk-emote-slot-\\($0)" },\n'
         '                    Array(EmoteCatalog.ids.prefix(3)).map { "lk-emote-\\($0)" },\n'
         '                    Array(EmoteCatalog.ids.suffix(3)).map { "lk-emote-\\($0)" }, actions]',
         '            // one focus row per three emotes, in the locker grid\'s own order (three columns)\n'
         '            let emoteRows = stride(from: 0, to: EmoteCatalog.ids.count, by: 3).map { start in\n'
         '                EmoteCatalog.ids[start..<min(start + 3, EmoteCatalog.ids.count)].map { "lk-emote-\\($0)" } }\n'
         '            return [tabs, (0..<3).map { "lk-emote-slot-\\($0)" }] + emoteRows + [actions]'),
    ],
}
ok = True
for rel, edits in EDITS.items():
    p = os.path.join(repo, rel)
    s = open(p).read()
    keep = os.path.join(RB, rel.replace('/', '__'))
    if not os.path.exists(keep) and not check:
        shutil.copy(p, keep)
    for a, b in edits:
        if b in s:
            continue
        if s.count(a) != 1:
            print('MISSING/AMBIGUOUS in', rel, ':', a[:80].replace('\n', ' | '))
            ok = False
            continue
        s = s.replace(a, b)
    if not check and ok:
        open(p, 'w').write(s)
print('SWIFT PATCH', 'OK' if ok else 'FAILED', '(check only)' if check else '')
sys.exit(0 if ok else 1)
