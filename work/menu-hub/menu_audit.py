#!/usr/bin/env python3
"""NO_DEAD_IDS audit for the menu (PLAN_MenuHub_WalkableWorld phase 6). Reads GolfArcade/Unity/*.swift and reports:
  1. focus ids that rows() can produce but nothing handles (select / adjust / online select / locker / views' taps)
  2. ids handled in select() that no screen produces (dead handlers)
  3. MenuScreen cases nothing ever shows (dead states)
Exit 1 when (1) or (3) is non-empty."""
import re, sys, glob, os
root = sys.argv[1] if len(sys.argv) > 1 else "."
src = {p: open(p).read() for p in glob.glob(os.path.join(root, "GolfArcade/**/*.swift"), recursive=True)}
menu = src[os.path.join(root, "GolfArcade/Unity/TennisMenu.swift")]
def block(text, start, end_marker):
    i = text.index(start); j = text.index(end_marker, i); return text[i:j]
rows = block(menu, "    func rows(_ screen: MenuScreen) -> [[String]] {", "    var focused: String {")
rows += block(menu, "    func lockerRows() -> [[String]] {", "    /// Opening the locker")
rows += block(menu, "    static func settingsRows(_ tab: SettingsTab) -> [String] {", "    // MARK: Layout")
rows += block(menu, "    static func hubItems(_ sport: Sport) -> [String] {", "    static func settingsRows(")
rows += block(menu, "    var postMatchChoices: [String] {", "    private enum PostChoice")
rows = re.sub(r'==\s*"[\w-]+"', '', rows)   # comparisons are not ids
produced = set(re.findall(r'"([a-zA-Z][\w-]*)"', rows))
dynamic_prefixes = sorted(set(re.findall(r'"([a-zA-Z][\w-]*-)\\\(', rows)) | set(re.findall(r'"([a-z]+)\\\(', rows)))
handled_text = block(menu, "    func select() {", "    func back() {") + block(menu, "    func adjust(_ id: String, by step: Int) -> Bool {", "    func setBodySize(")
handled_text += block(menu, "    fileprivate func lockerSelect(_ id: String) {", "    fileprivate func lockerOpenRange")
online = [t for p, t in src.items() if "OnlineLobbyMenu" in t]
handled = set(re.findall(r'case\s+((?:"[\w-]+"\s*,?\s*)+)', handled_text)); flat = set()
for group in handled: flat |= set(re.findall(r'"([\w-]+)"', group))
prefixes = set(re.findall(r'hasPrefix\("([\w-]+)"\)', handled_text))
online_handled = set()
for t in online: online_handled |= set(re.findall(r'"((?:net|online|party|home|lk)[\w-]*)"', t))
views_handled = set()
for p, t in src.items():
    if p.endswith("TennisMenu.swift"): continue
    views_handled |= set(re.findall(r'(?:tap|select|isFocused|focus)\("([\w-]+)"\)', t))
not_ids = {"back"} | {s for s in produced if s[0].isupper()}
def ok(i): return i in flat or i in online_handled or any(i.startswith(p) for p in prefixes) or i == "back"
unhandled = sorted(i for i in produced if not ok(i) and i not in not_ids)
dead_handlers = sorted(i for i in flat if i not in produced and not any(i.startswith(p) for p in dynamic_prefixes)
                       and i not in views_handled and i not in {"start", "back"})
cases = re.findall(r'case ([a-z][\w, ]+)', block(menu, "enum MenuScreen: Hashable {", "enum OnlineLobbyScreen"))
screens = set()
for c in cases:
    for name in c.split(","): name = name.strip().split("(")[0]; screens.add(name)
shown = set()
for p, t in src.items():
    shown |= set(re.findall(r'(?:show|debugShow|openFromPlaza|showOnline)\(\.(\w+)', t))
    shown |= set(re.findall(r'(?:screen|next) == \.(\w+)', t)) - set()
shown_calls = set()
for p, t in src.items(): shown_calls |= set(re.findall(r'(?<!debug)[sS]how\(\.(\w+)', t)) | set(re.findall(r'openFromPlaza\(\.(\w+)', t)) | set(re.findall(r'\? \.(\w+) :|: \.(\w+)\)', t)[0] if False else set())
dead_screens = sorted(s for s in screens if s not in shown_calls and s not in {"online"})
print("1. ids produced by rows() and handled by nothing:"); print("   " + (", ".join(unhandled) or "none"))
print("2. ids handled in select()/adjust() that no rows() produce (candidates, check views):"); print("   " + (", ".join(dead_handlers) or "none"))
print("3. MenuScreen cases nothing shows (outside debugShow):"); print("   " + (", ".join(dead_screens) or "none"))
sys.exit(1 if unhandled or dead_screens else 0)
