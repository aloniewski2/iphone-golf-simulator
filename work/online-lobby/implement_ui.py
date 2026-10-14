from pathlib import Path
root=Path('.')
def edit(path,a,b):
 p=root/path;s=p.read_text();assert a in s,(path,a[:60]);p.write_text(s.replace(a,b))
edit('GolfArcade/Unity/TennisMenuViews.swift','case .party: ClubPartyScreen(menu: menu, compact: compact)','case .party: IslandPartyScreen(menu: menu, compact: compact)\n            case .online(let route): IslandOnlineScreen(menu: menu, route: route, compact: compact)')
edit('GolfArcade/Unity/TennisPlayViews.swift','case .party: "PLAY WITH FRIENDS";','case .online(let route): route.rawValue.uppercased()\n        case .party: "PLAY WITH FRIENDS";')
edit('GolfArcade/Unity/TennisMenu.swift','if a.id == service.localID { return true };','if a.id == b.id { return false }; if a.id == service.localID { return true };')
edit('GolfArcade/Unity/TennisMenu.swift','case .lobby: return [["net-ready","net-emotes","net-clothes"],["net-settings"],["net-invite","net-find"],["net-start"],["net-leave"]]','case .lobby: return [["net-ready","net-emotes","net-clothes"],["net-settings"],["net-invite","net-find"]] + (service.isOwner ? [["net-start"]] : []) + [["net-leave"]]')
edit('GolfArcade/Unity/TennisMenu.swift','case .results: return [["net-rematch"],["net-return"]]','case .results: return (service.isOwner ? [["net-rematch"]] : []) + [["net-return"]]')
edit('GolfArcade/Unity/TennisMenu.swift','func goHome() {\n        switch screen','func goHome() {\n        if screen.isOnline { showOnline(.leave); return }\n        switch screen')
edit('GolfArcade/Unity/LockerStudio.swift','let compact: Bool\n    @State private var editingName','let compact: Bool\n    var lobbyPanel = false\n    @State private var editingName')
edit('GolfArcade/Unity/LockerStudio.swift','var body: some View {\n        GeometryReader { geo in','var body: some View {\n        Group {\n        if lobbyPanel { panel } else {\n        GeometryReader { geo in')
edit('GolfArcade/Unity/LockerStudio.swift','        .preferredColorScheme(.light)\n        .onAppear','        } }\n        .preferredColorScheme(.light)\n        .onAppear')
edit('GolfArcade/Unity/LockerStudio.swift','                tabs\n                Group','                tabs\n                if lobbyPanel && menu.lockerTab == .gear { sportToggle }\n                Group')
