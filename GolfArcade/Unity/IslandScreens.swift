import SwiftUI

// The Island screens that carry the play flow: the sport hub, the campaign, and the quick match.
// All of them are driven by the click remote (TennisMenu focus ids) and by touch.

// MARK: - Profile

/// The player's name with a level ring: top of Home.
struct IslandProfilePill: View {
    let player: Player?
    var compact = false
    var body: some View {
        let entry = player.map { Progression.shared.entry(for: $0.id) }
        let level = entry?.level ?? 1
        let fraction = entry.map { min(1, Double($0.xp) / Double(max(1, LevelCurve.needed($0.level)))) } ?? 0
        HStack(spacing: 10) {
            ZStack {
                Circle().stroke(IslandUI.navy.opacity(0.15), lineWidth: 4)
                Circle().trim(from: 0, to: fraction).stroke(IslandUI.lime, style: StrokeStyle(lineWidth: 4, lineCap: .round)).rotationEffect(.degrees(-90))
                Text("\(level)").font(IslandUI.font(compact ? 12 : 15, bold: true))
            }.frame(width: compact ? 30 : 40, height: compact ? 30 : 40)
            Text(player?.name ?? "Player 1").font(IslandUI.font(compact ? 15 : 20, bold: true))
        }
        .foregroundStyle(IslandUI.navy)
        .padding(.leading, 8).padding(.trailing, 16).padding(.vertical, 7)
        .background(IslandUI.paper.opacity(0.94), in: Capsule()).shadow(color: .black.opacity(0.18), radius: 3, y: 2)
        .accessibilityElement(children: .ignore).accessibilityLabel("\(player?.name ?? "Player 1"), level \(level)")
    }
}

// MARK: - Hub

struct IslandHubScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool

    private struct Mode { let title: String; let subtitle: String; let icon: String; let locked: Bool; let done: Bool }

    private func mode(_ id: String) -> Mode {
        let unlocked = menu.hubUnlocked(sport, id)
        let finished = menu.progress.finishedTutorial(sport)
        let reason = "Finish the tutorial first"
        switch id {
        case "tutorial":
            return Mode(title: "Tutorial", subtitle: finished ? "Coach Ray · done" : "Start here · a few minutes with Coach Ray", icon: "graduationcap.fill", locked: false, done: finished)
        case "campaign":
            let c = menu.campaign
            return Mode(title: "Campaign", subtitle: !finished ? reason : c.champion ? "Island Circuit champion — defend your title" : "Next: \(c.next.roundTitle.capitalized) · \(c.next.name)", icon: "trophy.fill", locked: !unlocked, done: false)
        case "exhibition":
            return Mode(title: "Quick Match", subtitle: finished ? "Pick a rival and the length" : reason, icon: "bolt.fill", locked: !unlocked, done: false)
        case "training":
            return Mode(title: "Training", subtitle: finished ? "Free rally with the coach" : reason, icon: "tennis.racket", locked: !unlocked, done: false)
        case "round":
            return Mode(title: "Play Golf", subtitle: "Cliffside round", icon: "figure.golf", locked: false, done: false)
        case "golfCampaign":
            return Mode(title: "Golf Tour", subtitle: "Coming soon", icon: "trophy.fill", locked: true, done: false)
        default:
            return Mode(title: "Driving Range", subtitle: "Coming soon", icon: "scope", locked: true, done: false)
        }
    }

    var body: some View {
        IslandShell(title: sport.title.capitalized, compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(alignment: .top, spacing: 40))
            layout {
                VStack(alignment: .leading, spacing: compact ? 10 : 14) {
                    ForEach(TennisMenu.hubItems(sport), id: \.self) { id in row(id) }
                    if !menu.notice.isEmpty { IslandMenuNotice(menu: menu, compact: compact) }
                    Spacer(minLength: 8)
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 170)
                }.frame(maxWidth: compact ? .infinity : 600, alignment: .leading)
                if !compact { detail.frame(maxWidth: .infinity, maxHeight: .infinity) }
            }
        }
    }

    private func row(_ id: String) -> some View {
        let m = mode(id), focused = menu.isFocused(id), primary = id == "tutorial" && !m.done
        return Button { menu.tap(id) } label: {
            HStack(spacing: 16) {
                Image(systemName: m.icon).font(.system(size: compact ? 19 : 25, weight: .bold)).frame(width: compact ? 40 : 54, height: compact ? 40 : 54)
                    .background(IslandUI.navy.opacity(0.08), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text(m.title).font(IslandUI.font(compact ? 20 : 27, bold: true))
                    Text(m.subtitle).font(IslandUI.font(compact ? 13 : 17)).foregroundStyle(IslandUI.muted).lineLimit(2)
                }
                Spacer(minLength: 6)
                if m.done { Text("✓ Done").font(IslandUI.font(compact ? 12 : 15, bold: true)).padding(.horizontal, 12).padding(.vertical, 5).background(IslandUI.navy.opacity(0.1), in: Capsule()) }
                else if m.locked { Image(systemName: "lock.fill").font(.system(size: compact ? 16 : 20, weight: .bold)).foregroundStyle(IslandUI.muted) }
                else { Image(systemName: "play.fill").font(.system(size: compact ? 14 : 18, weight: .bold)) }
            }
            .foregroundStyle(IslandUI.navy).opacity(m.locked && !focused ? 0.62 : 1)
            .padding(.horizontal, compact ? 14 : 22).frame(minHeight: compact ? 66 : 90)
            .islandFocus(focused, radius: 20, base: primary ? IslandUI.lime.opacity(0.65) : .white)
        }
        .buttonStyle(.plain).accessibilityIdentifier("hub-\(id)")
        .accessibilityLabel("\(m.title). \(m.subtitle)\(m.locked ? ". Locked." : "")")
    }

    /// The focused mode previewed: the next rival for the campaign, the player for the rest.
    @ViewBuilder private var detail: some View {
        if menu.focused == "campaign", menu.hubUnlocked(sport, "campaign") {
            let o = menu.campaign.next
            RivalCard(opponent: o, round: TennisCampaign.draw.firstIndex(of: o) ?? 0)
        } else {
            VStack(spacing: 8) {
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0), sport: sport == .golf ? .golf : nil)
            }
        }
    }
}

/// A rival: portrait, round, nickname, blurb, difficulty stars.
struct RivalCard: View {
    let opponent: TennisOpponent
    let round: Int
    var body: some View {
        ZStack(alignment: .topTrailing) {
            VStack(alignment: .leading, spacing: 6) {
                Spacer(minLength: 150)
                Text("ROUND \(round + 1) · \(opponent.nickname.uppercased())").font(IslandUI.font(14, bold: true)).tracking(1.2).foregroundStyle(IslandUI.muted)
                Text(opponent.name).font(IslandUI.font(38, bold: true))
                Text(opponent.blurb).font(IslandUI.font(18)).foregroundStyle(IslandUI.muted).fixedSize(horizontal: false, vertical: true)
                HStack(spacing: 3) { ForEach(0..<3, id: \.self) { Image(systemName: $0 < opponent.stars ? "star.fill" : "star").foregroundStyle(Color(hex: "E0A100")) } }.padding(.top, 6)
            }.padding(28).frame(maxWidth: .infinity, alignment: .leading)
            RivalPortrait(opponent: opponent).frame(height: 400).offset(x: -20, y: -62)
        }
        .foregroundStyle(IslandUI.navy)
        .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .shadow(color: IslandUI.navy.opacity(0.2), radius: 16, y: 8)
        .frame(maxWidth: 500, maxHeight: 560)
        .accessibilityElement(children: .ignore).accessibilityLabel("Round \(round + 1), \(opponent.name), \(opponent.nickname). \(opponent.blurb)")
    }
}

// MARK: - Campaign and Quick Match

struct IslandLadderScreen: View {
    let menu: TennisMenu
    let compact: Bool
    let exhibition: Bool
    private let points: [CGPoint] = [CGPoint(x:0.10,y:0.28),CGPoint(x:0.28,y:0.52),CGPoint(x:0.43,y:0.22),CGPoint(x:0.59,y:0.55),CGPoint(x:0.80,y:0.29),CGPoint(x:0.91,y:0.65),CGPoint(x:0.72,y:0.84),CGPoint(x:0.48,y:0.77),CGPoint(x:0.26,y:0.87),CGPoint(x:0.08,y:0.70)]

    var body: some View {
        if exhibition { quickMatch } else { campaign }
    }

    // MARK: quick match

    private var quickMatch: some View {
        IslandShell(title: "Quick Match", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 18)) : AnyLayout(HStackLayout(spacing: 56))
            layout {
                VStack(alignment: .leading, spacing: 18) {
                    IslandSelector(label: "Opponent", value: TennisCampaign.draw[menu.quickOpponent].name, focused: menu.isFocused("quickOpponent"), compact: compact) { _ = menu.adjust("quickOpponent", by: $0) }
                    IslandSelector(label: "Difficulty", value: TennisMenu.trainingLevels[menu.quickDifficulty].name, focused: menu.isFocused("quickDifficulty"), compact: compact) { _ = menu.adjust("quickDifficulty", by: $0) }
                    IslandSelector(label: "Match length", value: ["3 games", "1 set", "Best of 3"][menu.quickLength], focused: menu.isFocused("quickLength"), compact: compact) { _ = menu.adjust("quickLength", by: $0) }
                    IslandAction(title: "Start Match", focused: menu.isFocused("quickStart"), primary: true, compact: compact) { menu.tap("quickStart") }.padding(.top, 20)
                    if !menu.notice.isEmpty { IslandMenuNotice(menu: menu, compact: compact) }
                    Spacer(minLength: 12)
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                }.frame(width: compact ? nil : 460)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0)).frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
    }

    // MARK: campaign

    private var campaign: some View {
        let round = min(menu.selectedRound, TennisCampaign.draw.count - 1)
        let rival = TennisCampaign.draw[round]
        return GeometryReader { geo in
            ZStack {
                Image(uiImage: UIImage(named: "island-campaign.png") ?? UIImage()).resizable().scaledToFill()
                    .frame(width: geo.size.width, height: geo.size.height).clipped().ignoresSafeArea()
                Color.white.opacity(0.12).ignoresSafeArea()
                mapNodes(in: geo.size)
                VStack(spacing: 0) {
                    HStack {
                        Text("Island Circuit").font(IslandUI.font(compact ? 30 : 48, bold: true)).foregroundStyle(IslandUI.navy)
                            .shadow(color: .white.opacity(0.8), radius: 0, y: 1.5)
                        Spacer()
                        if menu.campaign.titles > 0 { Label("× \(menu.campaign.titles)", systemImage: "trophy.fill").font(IslandUI.font(20, bold: true)).foregroundStyle(IslandUI.navy).padding(8).background(IslandUI.paper.opacity(0.9), in: Capsule()) }
                    }.padding(.horizontal, compact ? 22 : 52).padding(.top, compact ? 14 : 32)
                    Spacer()
                    HStack { sheet(rival, round: round, width: compact ? nil : 540); if !compact { Spacer() } }
                        .padding(.horizontal, compact ? 14 : 48).padding(.bottom, compact ? 20 : 54)
                }
                if menu.confirmingRestart { restartConfirm }
                if !compact { VStack { Spacer(); HStack { IslandHintBar(items: IslandHintBar.move); Spacer() }.padding(.leading, 48).padding(.bottom, 20) } }
            }
        }.preferredColorScheme(.light)
    }

    private func mapNodes(in size: CGSize) -> some View {
        let area = compact ? CGRect(x: 14, y: size.height * 0.14, width: size.width - 28, height: size.height * 0.40)
                           : CGRect(x: size.width * 0.44, y: size.height * 0.20, width: size.width * 0.52, height: size.height * 0.64)
        func pt(_ p: CGPoint) -> CGPoint { CGPoint(x: area.minX + area.width * p.x, y: area.minY + area.height * p.y) }
        return ZStack {
            Path { path in
                for (i, p) in points.enumerated() { if i == 0 { path.move(to: pt(p)) } else { path.addLine(to: pt(p)) } }
            }.stroke(.white.opacity(0.9), style: StrokeStyle(lineWidth: 4, lineCap: .round, dash: [2, 11]))
            ForEach(0..<TennisCampaign.draw.count, id: \.self) { i in
                let unlocked = TennisCampaign.shared.unlocked(i), selected = menu.selectedRound == i, focused = menu.isFocused("round\(i)")
                let d: CGFloat = (compact ? 40 : 56) * (selected ? 1.25 : 1)
                Button { menu.tap("round\(i)") } label: {
                    Group {
                        if i < menu.campaign.nextRound && unlocked && !selected { Image(systemName: "checkmark") }
                        else if unlocked { Text("\(i + 1)") } else { Image(systemName: "lock.fill") }
                    }
                    .font(IslandUI.font(selected ? 24 : 19, bold: true)).foregroundStyle(selected ? IslandUI.navy : .white)
                    .frame(width: d, height: d).background(selected ? IslandUI.lime : IslandUI.navy, in: Circle())
                    .overlay(Circle().strokeBorder(.white, lineWidth: 3))
                    .overlay(Circle().strokeBorder(IslandUI.navy, lineWidth: focused ? 4 : 0).padding(-5))
                    .shadow(color: .black.opacity(0.2), radius: 3, y: 2)
                }.buttonStyle(.plain).position(pt(points[i]))
                    .accessibilityLabel("Round \(i + 1), \(TennisCampaign.draw[i].name)\(unlocked ? "" : ", locked")")
            }
        }
    }

    private func sheet(_ rival: TennisOpponent, round: Int, width: CGFloat?) -> some View {
        VStack(alignment: .leading, spacing: compact ? 4 : 8) {
            Text("ROUND \(round + 1) OF \(TennisCampaign.draw.count)").font(IslandUI.font(13, bold: true)).tracking(1.4).foregroundStyle(IslandUI.muted)
            Text(rival.name).font(IslandUI.font(compact ? 28 : 40, bold: true))
            Text("\(rival.nickname) · \(rival.formatTitle)").font(IslandUI.font(compact ? 14 : 18)).foregroundStyle(IslandUI.muted)
            if !menu.notice.isEmpty { Text(menu.notice).font(IslandUI.font(14, bold: true)).foregroundStyle(IslandUI.coral) }
            HStack(spacing: 12) {
                Button { menu.tap("campaignPlay") } label: {
                    HStack { Text("Play Round").font(IslandUI.font(compact ? 20 : 25, bold: true)); Spacer(); Image(systemName: "play.fill") }
                        .foregroundStyle(IslandUI.navy).padding(.horizontal, 22).frame(minHeight: compact ? 54 : 62)
                        .background(IslandUI.lime, in: Capsule())
                        .overlay(Capsule().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused("campaignPlay") ? 4 : 2))
                }.buttonStyle(.plain).accessibilityIdentifier("campaign-play")
                Button { menu.tap("campaignMore") } label: {
                    Image(systemName: "ellipsis").font(.system(size: 20, weight: .bold)).foregroundStyle(IslandUI.navy)
                        .frame(width: compact ? 54 : 62, height: compact ? 54 : 62).background(.white, in: Circle())
                        .overlay(Circle().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused("campaignMore") ? 4 : 0))
                }.buttonStyle(.plain).accessibilityLabel("More: new tournament")
            }.padding(.top, 8)
            Button { menu.tap("back") } label: {
                Text("‹ Back").font(IslandUI.font(compact ? 16 : 19, bold: true)).foregroundStyle(IslandUI.navy)
                    .padding(.vertical, 6).padding(.horizontal, 4)
                    .overlay(alignment: .bottom) { Rectangle().fill(IslandUI.navy).frame(height: menu.isFocused("back") ? 3 : 0) }
            }.buttonStyle(.plain)
        }
        .foregroundStyle(IslandUI.navy).padding(compact ? 18 : 28)
        .frame(width: width).frame(maxWidth: compact ? .infinity : nil, alignment: .leading)
        .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .shadow(color: IslandUI.navy.opacity(0.25), radius: 18, y: 8)
    }

    private var restartConfirm: some View {
        ZStack {
            Color.black.opacity(0.35).ignoresSafeArea()
            VStack(alignment: .leading, spacing: 14) {
                Text("Start a new tournament?").font(IslandUI.font(compact ? 24 : 32, bold: true))
                Text("This erases your Island Circuit progress and story. Your look and level stay.").font(IslandUI.font(compact ? 16 : 20)).foregroundStyle(IslandUI.muted)
                HStack(spacing: 12) {
                    confirmButton("Keep my progress", id: "restartNo", primary: true)
                    confirmButton("Erase and restart", id: "restartYes", primary: false)
                }.padding(.top, 8)
            }
            .padding(compact ? 22 : 34).frame(maxWidth: compact ? .infinity : 620, alignment: .leading)
            .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 28, style: .continuous)).padding(compact ? 18 : 0)
            .foregroundStyle(IslandUI.navy)
        }
    }

    private func confirmButton(_ title: String, id: String, primary: Bool) -> some View {
        Button { menu.tap(id) } label: {
            Text(title).font(IslandUI.font(compact ? 16 : 20, bold: true)).foregroundStyle(primary ? IslandUI.navy : Color(hex: "B3261E"))
                .lineLimit(1).minimumScaleFactor(0.7).frame(maxWidth: .infinity, minHeight: compact ? 50 : 58)
                .background(primary ? IslandUI.lime : .white, in: Capsule())
                .overlay(Capsule().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 4 : primary ? 2 : 1))
        }.buttonStyle(.plain)
    }
}


// MARK: - Settings

/// Settings: a row per setting with the control it deserves — a switch, a segmented choice, a button for an action, a stepper —
/// and full-width labels (no more "Training & exhibi…"). On the TV a card explains the focused row.
struct IslandSettingsScreen: View {
    let menu: TennisMenu
    let compact: Bool

    private enum Control { case toggle(Bool), choice([String], Int), action(String, danger: Bool), stepper(String), link }
    private struct Item { let title: String; let detail: String; let control: Control }

    private func item(_ id: String) -> Item {
        let s = SportsSession.shared, p = menu.player
        switch id {
        case "level":
            let i = TennisMenu.trainingLevels.firstIndex { abs($0.difficulty - s.tennisDifficulty) < 0.01 } ?? 1
            return Item(title: "Training coach", detail: "How hard the practice coach hits back. Standard is a proper rally.", control: .choice(TennisMenu.trainingLevels.map(\.name), i))
        case "coaching": return Item(title: "Coaching tips", detail: "Short hints on the phone and TV during matches.", control: .toggle(s.coachingTips))
        case "resetTips": return Item(title: "Show every coaching tip again", detail: "The hints you have already seen will come back.", control: .action("Reset", danger: false))
        case "replayOnboarding": return Item(title: "Replay onboarding", detail: "Account, look, screen setup and a tutorial again. Your progress stays.", control: .action("Replay", danger: false))
        case "resetProgress": return Item(title: "Erase tutorials & campaign", detail: "Starts the tutorial and the Island Circuit from the beginning. Your look and level stay. Asks twice.", control: .action(menu.confirmingReset ? "Press again" : "Erase…", danger: true))
        case "controls": return Item(title: "Controls", detail: "Swing the phone like a racket, or play with touch buttons.", control: .choice(["Swing", "Touch"], s.touch ? 1 : 0))
        case "range": return Item(title: "Step to cross court", detail: "How far you walk for a full court width.", control: .stepper("\(Int(s.travel * 100)) cm"))
        case "hand": return Item(title: "Plays", detail: "Which hand holds the racket.", control: .choice(["Right", "Left"], p?.handedness == .left ? 1 : 0))
        case "relock": return Item(title: "Court direction", detail: "Point at the TV again at the start of your next match.", control: .action("Set again", danger: false))
        case "timing": return Item(title: "Swing timing check", detail: "Re-measure your TV's picture delay at the start of your next match.", control: .action("Run next match", danger: false))
        case "howto": return Item(title: "How to play", detail: "A few short pages: connecting a TV, swinging, serving.", control: .link)
        case "fps": return Item(title: "120 fps on the phone", detail: "Smoother on ProMotion phones, uses more battery.", control: .toggle(s.highFrameRate))
        case "overscan": return Item(title: "Screen edge margin", detail: "Pulls the picture in from the edges if your TV crops it.", control: .stepper(s.overscan == 0 ? "None" : "\(Int(s.overscan * 100))%"))
        case "sound": return Item(title: "Sound", detail: "Menu and match sound.", control: .toggle(s.sound))
        case "haptics": return Item(title: "Haptics", detail: "Taps and buzzes on the phone.", control: .toggle(s.haptics))
        case "bigText": return Item(title: "Larger text on the remote", detail: "Bigger words on the phone while it is your TV remote.", control: .toggle(s.bigText))
        case "reduceMotion": return Item(title: "Reduce motion", detail: "Still pictures instead of moving backdrops.", control: .toggle(s.reduceMotion))
        case "classic": return Item(title: "Classic options menu", detail: "The plain list of every option. For testing.", control: .action("Open", danger: false))
        default: return Item(title: "Frame-time benchmark", detail: "Launch the app with -benchTennis to run it.", control: .action("Info", danger: false))
        }
    }

    var body: some View {
        IslandShell(title: "Settings", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(alignment: .leading, spacing: 14)) : AnyLayout(HStackLayout(alignment: .top, spacing: 36))
            layout {
                VStack(alignment: .leading, spacing: compact ? 14 : 20) {
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 10) {
                            ForEach(SettingsTab.visible, id: \.self) { tab in
                                let id = "tab-\(tab.rawValue)", on = menu.settingsTab == tab
                                Button { menu.tap(id) } label: {
                                    Text(tab.title).font(IslandUI.font(compact ? 16 : 18, bold: true)).foregroundStyle(IslandUI.navy)
                                        .padding(.horizontal, compact ? 18 : 14).padding(.vertical, compact ? 10 : 12)
                                        .background(on ? IslandUI.lime : .white, in: Capsule())
                                        .overlay(Capsule().strokeBorder(IslandUI.navy, lineWidth: menu.isFocused(id) ? 3.5 : on ? 1.5 : 0))
                                }.buttonStyle(.plain).accessibilityAddTraits(on ? .isSelected : [])
                            }
                        }.padding(.vertical, 4).padding(.horizontal, 3)
                    }
                    ScrollView(showsIndicators: false) {
                        VStack(spacing: compact ? 10 : 12) {
                            ForEach(TennisMenu.settingsRows(menu.settingsTab), id: \.self) { id in row(id) }
                        }.padding(.horizontal, 3).padding(.vertical, 4)
                    }
                    IslandMenuNotice(menu: menu, compact: compact)
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 170)
                }.frame(maxWidth: compact ? .infinity : 720, alignment: .leading)
                if !compact { explainer.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top) }
            }
        }
    }

    private func row(_ id: String) -> some View {
        let it = item(id), focused = menu.isFocused(id)
        let stacked: Bool = { if compact, case .choice = it.control { return true } else { return false } }()
        return (stacked ? AnyLayout(VStackLayout(alignment: .leading, spacing: 10)) : AnyLayout(HStackLayout(spacing: 12))) {
            Text(it.title).font(IslandUI.font(compact ? 17 : 22, bold: true)).fixedSize(horizontal: false, vertical: true)
            if !stacked { Spacer(minLength: 8) }
            control(id, it.control)
        }
        .foregroundStyle(IslandUI.navy).padding(.horizontal, compact ? 14 : 20).padding(.vertical, compact ? 12 : 16)
        .frame(minHeight: compact ? 58 : 72)
        .islandFocus(focused, radius: 18)
        .contentShape(Rectangle())
        .onTapGesture {
            switch it.control { case .choice, .stepper: _ = menu.focus(id); default: menu.tap(id) }
        }
        .accessibilityElement(children: .combine).accessibilityLabel(it.title)
        .accessibilityHint(it.detail)
        .accessibilityAdjustableAction { dir in _ = menu.adjust(id, by: dir == .increment ? 1 : -1) }
    }

    @ViewBuilder private func control(_ id: String, _ c: Control) -> some View {
        switch c {
        case .toggle(let on): IslandSwitch(on: on, compact: compact)
        case .choice(let names, let selected):
            IslandSegments(titles: names, selected: selected, compact: compact) { i in
                _ = menu.focus(id); var guardCount = 0
                while guardCount < names.count, let cur = currentChoice(id, names), cur != i { _ = menu.adjust(id, by: 1); guardCount += 1 }
            }.frame(maxWidth: compact ? .infinity : 280)
        case .action(let label, let danger):
            Button { menu.tap(id) } label: {
                Text(label).font(IslandUI.font(compact ? 15 : 19, bold: true)).foregroundStyle(danger ? Color(hex: "B3261E") : IslandUI.navy)
                    .padding(.horizontal, 18).padding(.vertical, compact ? 9 : 11).background(IslandUI.navy.opacity(0.08), in: Capsule())
            }.buttonStyle(.plain)
        case .stepper(let value):
            HStack(spacing: 4) {
                Button { _ = menu.adjust(id, by: -1) } label: { Image(systemName: "minus").frame(width: 40, height: 40) }
                Text(value).font(IslandUI.font(compact ? 15 : 19, bold: true)).frame(minWidth: compact ? 64 : 84)
                Button { _ = menu.adjust(id, by: 1) } label: { Image(systemName: "plus").frame(width: 40, height: 40) }
            }.buttonStyle(.plain).font(.system(size: 16, weight: .bold))
        case .link: Image(systemName: "chevron.right").font(.system(size: 18, weight: .bold))
        }
    }

    private func currentChoice(_ id: String, _ names: [String]) -> Int? {
        if case .choice(_, let i) = item(id).control { return i }
        return nil
    }

    /// TV: what the focused row does.
    private var explainer: some View {
        let id = menu.focused
        let it = item(TennisMenu.settingsRows(menu.settingsTab).contains(id) ? id : (TennisMenu.settingsRows(menu.settingsTab).first ?? "level"))
        return VStack(alignment: .leading, spacing: 10) {
            Text(it.title.uppercased()).font(IslandUI.font(14, bold: true)).tracking(1.4).foregroundStyle(IslandUI.muted)
            Text(it.detail).font(IslandUI.font(22)).foregroundStyle(IslandUI.navy).fixedSize(horizontal: false, vertical: true)
        }
        .padding(28).frame(maxWidth: 420, alignment: .leading)
        .background(IslandUI.paper, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .shadow(color: IslandUI.navy.opacity(0.15), radius: 12, y: 6)
    }
}

// MARK: - Party / online lobby

struct IslandPartyScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title:"Play with friends",compact:compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing:16)) : AnyLayout(HStackLayout(spacing:48))
            layout {
                VStack(alignment:.leading,spacing:12) {
                    Text("Meet on the island").islandType(compact ? 23 : 32,bold:true)
                    Text("Invite friends online or find a lobby nearby. Bring your own look.").islandType(compact ? 15 : 20).foregroundStyle(IslandUI.muted).padding(.bottom,10)
                    IslandLobbyRow(title:"Solo",icon:"person.fill",id:"partySolo",menu:menu,compact:compact)
                    IslandLobbyRow(title:"Play Online",subtitle:"Quick match or invite friends",icon:"globe",id:"partyOnline",menu:menu,compact:compact)
                    IslandLobbyRow(title:"Nearby",subtitle:"On the same Wi-Fi",icon:"wifi",id:"partyNearby",menu:menu,compact:compact)
                    IslandLobbyRow(title:"Back",icon:"arrow.left",id:"back",menu:menu,compact:compact)
                }.frame(maxWidth:compact ? .infinity : 500)
                if !compact { CharacterModelPreview(player:menu.player ?? Player(name:"Player 1",colorIndex:0),cameraDistance:3.8,idleSport:.tennis).frame(maxWidth:.infinity) }
            }.foregroundStyle(IslandUI.navy)
        }
    }
}

struct IslandOnlineScreen: View {
    let menu: TennisMenu
    let route: OnlineLobbyScreen
    let compact: Bool
    var room: ClubRoom = .locker
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    private var online: OnlineLobbyMenu { menu.online }
    private var service: MultiplayerService { online.service }
    private var lobby: MultiplayerLobby? { service.lobby }
    private var local: MultiplayerParticipant? { lobby?.participants.first { $0.id == service.localID } }
    private var title: String {
        switch route {
        case .entry: "Play Online"; case .nearby: "Nearby"; case .searching: "Finding friends"
        case .emotes: "Emotes"; case .clothes: "Change Clothes"; case .settings: "Match Settings"
        case .match: "Playing together"
        case .loading: "Loading the match"; case .results: "Match complete"; case .leave: "Leave the party?"
        case .lobby: "Your party"
        }
    }
    var body: some View {
        Group {
            if [.entry,.nearby,.searching].contains(route) && lobby == nil { entry }
            else { sharedRoom }
        }.preferredColorScheme(.light)
        .sheet(item:Binding(get:{online.sheet},set:{online.sheet=$0})) { sheet in OnlineGameCenterSheet(controller:sheet.controller).ignoresSafeArea() }
    }
    private var entry: some View {
        IslandShell(title:title,compact:compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing:12)) : AnyLayout(HStackLayout(spacing:48))
            layout {
                VStack(alignment:.leading,spacing:compact ? 10 : 14) {
                    if route == .entry {
                        if !service.authenticated {
                            IslandNotice(text:"Sign in to play with friends",compact:compact)
                            action("net-signin","Sign in to Game Center",icon:"person.crop.circle")
                        }
                        action("net-tennis","Quick Match Tennis",icon:"tennis.racket")
                        action("net-golf","Quick Match Golf",icon:"figure.golf")
                        action("net-invite","Invite Friends",icon:"person.badge.plus")
                        action("back","Back",icon:"arrow.left")
                    } else if route == .nearby {
                        Text("Choose a friend's lobby on the same Wi-Fi.").islandType(compact ? 15 : 19).foregroundStyle(IslandUI.muted)
                        if online.nearbyItems.isEmpty { IslandNotice(text:"Looking for nearby lobbies…",compact:compact) }
                        ForEach(online.nearbyItems) { found in
                            action("net-join-\(found.id)",found.name,subtitle:"\(found.sport.capitalized) · \(found.players)/4 players",icon:"person.2.fill")
                        }
                        if service.discoveredLobbies.count > 4 {
                            HStack { action("net-page-prev","Previous",icon:"chevron.left"); action("net-page-next","Next",icon:"chevron.right") }
                        }
                        action("net-host","Host a Lobby",icon:"plus")
                        action("back","Back",icon:"arrow.left")
                    } else {
                        Text(online.searchSport.rawValue.capitalized).islandType(compact ? 25 : 34,bold:true)
                        TimelineView(.periodic(from:.now,by:1)) { context in
                            Text("Searching · \(max(0,Int(context.date.timeIntervalSince(online.searchStarted)))) s").islandType(compact ? 17 : 22).monospacedDigit()
                        }
                        Text("Your hero is warming up while we find a match.").islandType(compact ? 14 : 18).foregroundStyle(IslandUI.muted)
                        action("net-cancel","Cancel",icon:"xmark")
                    }
                    if !menu.notice.isEmpty { IslandMenuNotice(menu:menu,compact:compact) }
                }.frame(maxWidth:compact ? .infinity : 500,alignment:.leading)
                if route == .searching || !compact {
                    CharacterModelPreview(player:menu.player ?? Player(name:"Player 1",colorIndex:0),cameraDistance:3.4,idleSport:.tennis).frame(maxWidth:.infinity,minHeight:compact ? 230 : 400)
                }
            }.foregroundStyle(IslandUI.navy)
        }
    }
    private var sharedRoom: some View {
        GeometryReader { geo in
            ZStack {
                IslandBackdrop(room:room)
                LinearGradient(colors:[IslandUI.paper.opacity(0.5),.clear],startPoint:.top,endPoint:.center).ignoresSafeArea()
                VStack(spacing:compact ? 8 : 14) {
                    HStack(alignment:.firstTextBaseline) {
                        Text(title).islandType(compact ? 28 : 38,bold:true)
                        Spacer()
                        Text(lobby?.sport.rawValue.capitalized ?? "Tennis").islandType(compact ? 13 : 18,bold:true).padding(.horizontal,14).padding(.vertical,7).background(IslandUI.paper,in:Capsule())
                    }.foregroundStyle(IslandUI.navy).padding(.horizontal,compact ? 18 : 48).padding(.top,compact ? 12 : 22)
                    let layout = compact ? AnyLayout(VStackLayout(spacing:10)) : AnyLayout(HStackLayout(alignment:.top,spacing:24))
                    layout {
                        VStack(spacing:8) {
                            stage.frame(maxWidth:.infinity,maxHeight:.infinity)
                            HStack(spacing:compact ? 5 : 10) {
                                ForEach(online.participants,id:\.id) { p in
                                    IslandLobbyPlayerCard(participant:p,host:p.id == lobby?.ownerID,local:p.id == service.localID,loading:route == .loading,compact:compact)
                                }
                            }.padding(.horizontal,compact ? 12 : 0)
                        }.frame(maxWidth:.infinity,minHeight:compact ? 210 : nil,maxHeight:.infinity)
                        panel.frame(width:compact ? nil : (route == .clothes ? 560 : 440),height:compact ? panelHeight(geo.size.height) : nil)
                    }.padding(.horizontal,compact ? 0 : 48)
                    if !compact { IslandHintBar(items:IslandHintBar.move + [("A · Emotes","Emote"),("A · Ready","Ready")]).padding(.bottom,20) }
                }
                if !menu.notice.isEmpty { VStack { Spacer(); IslandMenuNotice(menu:menu,compact:compact).padding(.bottom,compact ? 16 : 72) } }
            }
        }
    }
    private var stage: some View {
        LobbyHeroStage(participants:online.participants,sport:lobby?.sport ?? .tennis,localID:service.localID,
                       editingPlayer:route == .clothes ? menu.player : nil,emotes:service.emotes,networkTime:service.networkTime,
                       winnerID:route == .results ? online.winnerID : nil,introduce:lobby?.phase == .lobby,animate:!reduceMotion && !SportsSession.shared.reduceMotion)
            .id("online-shared-stage")
    }
    private func panelHeight(_ height: CGFloat) -> CGFloat {
        switch route {
        case .clothes: min(480,height * 0.56)
        case .settings: min(445,height * 0.53)
        case .emotes: 355
        case .lobby: min(438,height * 0.51)
        default: min(330,height * 0.38)
        }
    }
    @ViewBuilder private var panel: some View {
        if route == .clothes { IslandLockerScreen(menu:menu,compact:compact,lobbyPanel:true) }
        else {
            VStack(alignment:.leading,spacing:compact ? 9 : 12) {
                switch route {
                case .lobby:
                    action("net-ready",local?.ready == true ? "Ready" : "Ready?",icon:"checkmark",ready:local?.ready ?? false)
                    HStack { action("net-emotes","Emotes",icon:"face.smiling"); action("net-clothes","Clothes",icon:"tshirt") }
                    action("net-settings","Match Settings",icon:"slider.horizontal.3")
                    HStack { action("net-invite","Invite More",icon:"person.badge.plus"); action("net-find","Find More",icon:"magnifyingglass") }
                    if service.isOwner { action("net-start","Start Match",subtitle:lobby?.startReason ?? "Invite another player to play",icon:"play.fill",enabled:lobby?.canStart == true) }
                    else { Text("\(lobby?.startReason ?? "Waiting for the host") · Host starts the match").islandType(compact ? 12 : 15).foregroundStyle(IslandUI.muted) }
                    action("net-leave","Leave",icon:"arrow.left")
                case .emotes: emotePicker
                case .settings: settings
                case .loading:
                    let total = lobby?.participants.filter(\.connected).count ?? 0, loaded = lobby?.participants.filter { $0.connected && $0.loaded }.count ?? 0
                    Text("Loading \(loaded)/\(total)").islandType(compact ? 26 : 34,bold:true)
                    Text("Everyone travels together. The match starts when seated players have loaded.").islandType(compact ? 15 : 19).foregroundStyle(IslandUI.muted)
                    action("net-leave","Leave",icon:"arrow.left")
                case .results:
                    Text(online.winnerID.flatMap { id in lobby?.participants.first { $0.id == id }?.name }.map { "\($0) wins!" } ?? "Thanks for playing").islandType(compact ? 25 : 32,bold:true)
                    if service.isOwner { action("net-rematch","Rematch",subtitle:"Return together, then ready up",icon:"arrow.clockwise") }
                    action("net-return","Back to Lobby",icon:"person.2")
                    if service.localSeat < 0 { action("net-queue",lobby?.queue.contains(service.localID) == true ? "Queued for Next" : "Queue for Next",icon:"person.crop.circle.badge.plus") }
                    action("net-leave","Leave",icon:"arrow.left")
                case .leave:
                    Text("Your friends will see that you've left.").islandType(compact ? 18 : 24).foregroundStyle(IslandUI.muted)
                    HStack { action("net-stay","Stay",icon:"person.2.fill"); action("net-confirm-leave","Leave",icon:"arrow.left") }
                default: EmptyView()
                }
            }.foregroundStyle(IslandUI.navy).padding(compact ? 16 : 22).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.top)
                .background(IslandUI.paper.opacity(0.96),in:RoundedRectangle(cornerRadius:compact ? 32 : 26,style:.continuous))
        }
    }
    private var emotePicker: some View {
        TimelineView(.periodic(from:.now,by:0.1)) { _ in
            VStack(alignment:.leading,spacing:8) {
                ForEach(0..<2) { row in
                    Text(row == 0 ? "Taunts" : "Intros").islandType(compact ? 13 : 18,bold:true)
                    HStack(spacing:compact ? 9 : 12) {
                        ForEach(row*3..<row*3+3,id:\.self) { i in
                            IslandEmoteTile(id:MultiplayerEmote.ids[i],title:MultiplayerEmote.names[i],image:LobbyEmoteThumbs.image(MultiplayerEmote.ids[i],player:menu.player ?? local?.lobbyPlayer ?? Player(name:"Player",colorIndex:0)),menu:menu,cooldown:service.emoteCooldown,scale:compact ? 0.9 : 1)
                        }
                    }
                }
                action("back","Back",icon:"arrow.left")
            }
        }
    }
    @ViewBuilder private var settings: some View {
        if let lobby {
            VStack(alignment:.leading,spacing:10) {
                if !service.isOwner { IslandNotice(text:"Only the host changes settings",compact:compact) }
                if online.settingsSeats {
                    ForEach(online.participants,id:\.id) { p in
                        if service.isOwner { action("net-seat-\(p.id)",p.name,subtitle:p.seat < 0 ? "Watching · Select to take a seat" : "P\(p.seat+1) · Select to watch",icon:"person.fill") }
                        else { Text("\(p.name) · \(p.seat < 0 ? "Watching" : "P\(p.seat+1)")").islandType(compact ? 15 : 19) }
                    }
                    action("net-settings-match","Match Options",icon:"slider.horizontal.3")
                } else {
                    choices("Sport",values:["tennis","golf"],selected:lobby.sport.rawValue,prefix:"net-sport-")
                    choices(lobby.sport == .tennis ? "Court" : "Course",values:lobby.sport == .tennis ? ["resort","skyscraper","volcano"] : ["postcards"],selected:lobby.venue,prefix:"net-venue-")
                    if lobby.sport == .tennis {
                        choices("Sets",values:["1","2","3"],selected:String(lobby.sets),prefix:"net-sets-")
                        choices("Games",values:["1","3","6"],selected:String(lobby.games),prefix:"net-games-")
                    }
                    action("net-settings-seats","Player Seats",icon:"person.2.fill")
                }
                action("back","Back",icon:"arrow.left")
            }
        }
    }
    private func choices(_ label: String,values:[String],selected:String,prefix:String) -> some View {
        VStack(alignment:.leading,spacing:5) {
            Text(label).islandType(compact ? 12 : 15,bold:true)
            IslandSegments(titles:values.map(\.capitalized),selected:values.firstIndex(of:selected) ?? 0,compact:compact,focused:values.firstIndex { menu.isFocused(prefix+$0) }) { i in menu.tap(prefix+values[i]) }.disabled(!service.isOwner)
        }.id(values.first { menu.isFocused(prefix+$0) }.map { prefix+$0 } ?? prefix+values[0])
    }
    private func action(_ id: String,_ title: String,subtitle: String = "",icon: String = "chevron.right",enabled: Bool = true,ready: Bool? = nil) -> some View {
        IslandLobbyRow(title:title,subtitle:subtitle,icon:icon,id:id,menu:menu,compact:compact,enabled:enabled,ready:ready)
    }
}

struct OnlineGameCenterSheet: UIViewControllerRepresentable {
    let controller: UIViewController
    func makeUIViewController(context:Context) -> UIViewController { controller }
    func updateUIViewController(_ controller:UIViewController,context:Context) { }
}

/// Small native notices over a network match; the same card works on the controller and Unity preview.
struct MultiplayerMatchOverlay: View {
    var menu = TennisMenu.shared
    var compact = true
    private var service: MultiplayerService { menu.online.service }
    var body: some View {
        if let lobby = service.lobby, [.playing,.interrupted].contains(lobby.phase) {
            VStack(alignment:.leading,spacing:8) {
                HStack {
                    if service.localSeat < 0 { IslandNotice(text:"Watching",compact:compact) }
                    Spacer()
                    IslandLobbyRow(title:"Leave",icon:"arrow.left",id:"net-leave",menu:menu,compact:compact).frame(width:compact ? 150 : 190)
                }
                ForEach(lobby.participants.filter { $0.id != service.localID && (!$0.connected || $0.paused == true) },id:\.id) { p in
                    IslandNotice(text:!p.connected ? "\(p.name) reconnecting · Match paused" : "\(p.name) paused",compact:compact)
                }
                if let error = service.lastError { IslandNotice(text:error,compact:compact) }
            }.padding(compact ? 12 : 24)
        }
    }
}
