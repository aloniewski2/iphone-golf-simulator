#!/usr/bin/env python3
"""CONTROLLER_LOCK / NO_GC_INVITES audit (friends + controller lock, 2026-10-11). Static checks over GolfArcade/**/*.swift that the
issues Adnan reported cannot come back:
  1. No Game Center invite / accept UI anywhere in the app (GKMatchmakerViewController is never built; no invite sheet paths).
  2. The phone's root (SportsHome) never shows the old TV remote or the old phone menu (the phone menu only when Unity is missing,
     i.e. the simulator), and outside a match it is always the Plaza controller.
  3. The lobby never opens a screen of its own: OnlineLobbyMenu.sync and the match callbacks call no showOnline(...).
  4. Every Plaza bench routes to its own sport and mode: no bench opens the old online / local / play screens.
  5. The Friends list has no "Invite from Game Center".
Exit 1 on any finding."""
import glob, os, re, sys
root = sys.argv[1] if len(sys.argv) > 1 else "."
src = {os.path.relpath(p, root): open(p).read() for p in glob.glob(os.path.join(root, "GolfArcade/**/*.swift"), recursive=True)}
def block(path, start, end):
    t = src[path]; i = t.index(start); j = t.index(end, i + len(start)); return t[i:j]
findings = []
# 1
for p, t in src.items():
    code = re.sub(r'//.*', '', t)
    for pat in [r'GKMatchmakerViewController\s*\(', r'\binvitationController\s*\(', r'\bacceptInvitation\s*\(', r'\binviteFromPlaza\b',
                r'\bpendingInvite\b', r'\bprivateRoom\s*\(', r'\bopenPrivateRoom\b', r'\bjoinPrivateRoom\b', r'GKMatchRequest\(\).*recipients']:
        for m in re.finditer(pat, code): findings.append(f"1. {p}: Game Center invite path `{m.group(0)}`")
# 2
home = block("GolfArcade/Unity/SportsHome.swift", "        Group {", "        .sheet(")
if "TennisRemote()" in home: findings.append("2. SportsHome shows TennisRemote()")
for line in home.splitlines():
    if "TennisPhoneMenu()" in line and "runtimeMissing" not in line: findings.append("2. SportsHome shows TennisPhoneMenu() outside the no-Unity case: " + line.strip())
if not re.search(r'else if !session\.active \{ if hub\.runtimeMissing \{ TennisPhoneMenu\(\) \} else \{ HubControllerView\(\) \} \}', home):
    findings.append("2. SportsHome: outside a match the root is not always HubControllerView")
# 3
menu = "GolfArcade/Unity/TennisMenu.swift"
for name, start, end in [("sync", "    func sync(_ menu: TennisMenu, force: Bool = false) {", "\n    }\n"),
                         ("installCallbacks", "    func installCallbacks(_ menu: TennisMenu) {", "\n    }\n}")]:
    b = block(menu, start, end)
    if "showOnline(" in b: findings.append(f"3. OnlineLobbyMenu.{name} opens a lobby screen (showOnline)")
    if "showMenu()" in b: findings.append(f"3. OnlineLobbyMenu.{name} puts the old TV menu over the game (showMenu)")
hub = src["GolfArcade/Unity/HubSession.swift"]
if "showOnline(" in hub: findings.append("3. HubSession opens a lobby screen")
# 4
routes = block("GolfArcade/Unity/HubSession.swift", "    static func route(_ id: String) -> Route? {", "        default: nil")
for line in routes.splitlines():
    m = re.match(r'\s*case "(bay-[\w-]+)"', line)
    if not m: continue
    bay = m.group(1)
    for bad in [".onlineChoice", ".localChoice", ".party", ".gameSelect", ".multiplayer", ".hub(", "onlineQuick", "partyOnline"]:
        if bad in line: findings.append(f"4. bench {bay} routes to an old screen ({bad})")
    sport = "golf" if "-golf-" in bay else "tennis"
    other = "tennis" if sport == "golf" else "golf"
    if f"(.{other})" in line or f"sport: .{other}" in line: findings.append(f"4. bench {bay} launches {other}")
# 5
if "Invite from Game Center" in src["GolfArcade/Social/ClubHubView.swift"]: findings.append("5. the Friends list still offers Invite from Game Center")
print("\n".join(findings) if findings else "controller_audit: no findings (CONTROLLER_LOCK, NO_GC_INVITES, BENCH_ROUTES static checks pass)")
sys.exit(1 if findings else 0)
