import SwiftUI

// Every menu screen on the Island Sports Club system (ClubDesign.swift). Each area has its own
// painted scene; all share the header, cards, focus, entrances and hint bar. TV layouts are for
// the 1280×720 canvas (fitted to 16:10 Macs); `compact` is the phone held upright.

extension TennisMenu {
    var player: Player? {
        let s = SportsSession.shared
        return s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : nil
    }
}

// MARK: - Title

struct ClubTitleScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var drop = false
    @State private var breathe = false
    var body: some View {
        ZStack {
            ClubBackdrop(scene: "home", tint: 0.2)
            LinearGradient(colors: [Club.lagoonDeep.opacity(0.2), Club.lagoonDeep.opacity(0.85)], startPoint: .top, endPoint: .bottom).ignoresSafeArea()
            VStack(spacing: compact ? 14 : 18) {
                Spacer()
                ClubCrest(size: compact ? 150 : 210)
                    .scaleEffect(drop ? 1 : 2.2).opacity(drop ? 1 : 0).rotationEffect(.degrees(drop ? 0 : -18))
                VStack(spacing: -6) {
                    Text("ISLAND").font(Club.caps(compact ? 16 : 22)).tracking(compact ? 10 : 16).foregroundStyle(Club.sun)
                    Text("SPORTS CLUB").font(Club.display(compact ? 56 : 104)).foregroundStyle(.white)
                        .shadow(color: .black.opacity(0.4), radius: 0, x: 0, y: 5)
                }
                .opacity(drop ? 1 : 0).offset(y: drop ? 0 : 30)
                Spacer()
                Text(compact ? "Tap to start" : "Press  Ⓐ  to start")
                    .font(Club.title(compact ? 22 : 30)).foregroundStyle(Club.ink)
                    .padding(.horizontal, 28).padding(.vertical, 12)
                    .background(Capsule().fill(Club.sun))
                    .scaleEffect(breathe ? 1.05 : 0.97)
                if TennisCampaign.shared.titles > 0 {
                    Label("\(TennisCampaign.shared.titles)× Tropical Open champion", systemImage: "crown.fill")
                        .font(Club.ui(15, 700)).foregroundStyle(Club.sun)
                }
                Spacer().frame(height: compact ? 50 : 40)
            }
        }
        .contentShape(Rectangle())
        .onTapGesture { menu.tap("start") }
        .onAppear {
            withAnimation(.spring(response: 0.7, dampingFraction: 0.6).delay(0.2)) { drop = true }
            if !Club.still { withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) { breathe = true } }
        }
    }
}

// MARK: - Home

struct ClubHomeScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "home", breadcrumb: ["Clubhouse"], compact: compact, tint: 0.35) {
            if compact {
                ScrollView {
                    VStack(spacing: 14) {
                        topActions
                        stage.frame(height: 300)
                        greeting
                        playButton.frame(height: 108)
                        continueCard.frame(height: 110)
                        HStack(spacing: 12) { lookCard; friendsCard }.frame(height: 190)
                    }.padding(.vertical, 6)
                }
            } else {
                VStack(spacing: 16) {
                    HStack { Spacer(); topActions }
                    HStack(alignment: .bottom, spacing: 30) {
                        stage.frame(width: 360)
                        VStack(alignment: .leading, spacing: 16) {
                            greeting
                            HStack(spacing: 18) {
                                playButton.frame(width: 470).clubEntrance(0)
                                continueCard.clubEntrance(1)
                            }.frame(height: 170)
                            HStack(spacing: 18) {
                                lookCard.clubEntrance(2); friendsCard.clubEntrance(3)
                            }.frame(height: 180)
                        }
                    }
                    .frame(maxHeight: .infinity, alignment: .bottom)
                }
            }
        }
    }

    private var topActions: some View {
        HStack(spacing: 10) {
            ClubPill(title: "How to play", icon: "questionmark.circle.fill", focused: menu.isFocused("howto"), compact: compact) { menu.tap("howto") }
            ClubPill(title: "Settings", icon: "gearshape.fill", focused: menu.isFocused("settings"), compact: compact) { menu.tap("settings") }
        }
    }

    private var greeting: some View {
        let hour = Calendar.current.component(.hour, from: Date())
        let hello = hour < 12 ? "Good morning" : hour < 18 ? "Welcome back" : "Good evening"
        return VStack(alignment: .leading, spacing: 0) {
            Text("\(hello), \(menu.player?.name ?? "friend")").font(Club.caps(compact ? 13 : 16)).tracking(1.5).foregroundStyle(Club.sun)
            Text("What are we playing today?").font(Club.display(compact ? 36 : 54)).foregroundStyle(.white)
                .shadow(color: .black.opacity(0.35), radius: 0, x: 0, y: 4)
            if let p = menu.player { LevelStrip(player: p.id, compact: compact).padding(.top, 4) }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    /// Your player standing on the clubhouse floor, on a lit disc.
    private var stage: some View {
        ZStack(alignment: .bottom) {
            Ellipse().fill(RadialGradient(colors: [Club.sun.opacity(0.55), .clear], center: .center, startRadius: 0, endRadius: 150))
                .frame(width: 300, height: 70).offset(y: 10)
            Ellipse().strokeBorder(.white.opacity(0.5), lineWidth: 2).frame(width: 200, height: 40).offset(y: -2)
            if let p = menu.player {
                CharacterModelPreview(player: p, cameraDistance: 3.9).padding(.bottom, 12)
            } else {
                HeroArt(name: "menu-hero-player-male").padding(.bottom, 12)
            }
        }
    }

    private var playButton: some View {
        Button { menu.tap("play") } label: {
            HStack(spacing: compact ? 12 : 20) {
                ClubArt(name: "play", fallback: "play.circle.fill").frame(width: compact ? 70 : 110, height: compact ? 70 : 110)
                    .shadow(color: .black.opacity(0.25), radius: 6, y: 4)
                VStack(alignment: .leading, spacing: 2) {
                    Text("PLAY").font(Club.display(compact ? 54 : 84)).foregroundStyle(Club.ink)
                    Text("Pick a sport").font(Club.ui(compact ? 14 : 18, 600)).foregroundStyle(Club.ink.opacity(0.7))
                }
                Spacer(minLength: 0)
            }
            .padding(.horizontal, compact ? 16 : 24)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(ZStack {
                RoundedRectangle(cornerRadius: 30, style: .continuous).fill(Club.sunDeep).offset(y: 7)
                RoundedRectangle(cornerRadius: 30, style: .continuous).fill(Club.sun)
                RoundedRectangle(cornerRadius: 30, style: .continuous).fill(LinearGradient(colors: [.white.opacity(0.35), .clear], startPoint: .top, endPoint: .center))
            })
        }
        .buttonStyle(ClubPress())
        .clubFocus(menu.isFocused("play"), corner: 30)
        .accessibilityIdentifier("home-play")
    }

    private var continueCard: some View {
        let label = menu.continueLabel
        let tutorial = !menu.progress.finishedTutorial(.tennis)
        return ClubCard(art: tutorial ? "lesson" : TennisCampaign.shared.champion ? "crown" : "trophy",
                        title: label.title, subtitle: label.subtitle, badge: tutorial ? .new : .next, tint: Club.coral,
                        focused: menu.isFocused("continue"), compact: compact, horizontal: true) { menu.tap("continue") }
            .accessibilityIdentifier("home-continue")
    }
    private var lookCard: some View {
        ClubCard(art: "kit", title: "Your look", subtitle: "Style your player", tint: Club.coral,
                 focused: menu.isFocused("character"), compact: compact, horizontal: !compact) { menu.tap("character") }
    }
    private var friendsCard: some View {
        ClubCard(art: "friends", title: "With friends", subtitle: "Couch & online party play", badge: .soon, tint: Club.violet,
                 focused: menu.isFocused("homePlay"), compact: compact, horizontal: !compact) { menu.tap("homePlay") }
    }
}

// MARK: - Game select

struct ClubGameSelectScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "pavilion", breadcrumb: ["Clubhouse", "Pick a sport"], compact: compact, tint: 0.3) {
            VStack(spacing: 16) {
                if compact {
                    ScrollView { VStack(spacing: 12) { ForEach(Array(Sport.allCases.enumerated()), id: \.offset) { i, s in arch(s).frame(height: 150).clubEntrance(i) } } }
                } else {
                    HStack(spacing: 16) { ForEach(Array(Sport.allCases.enumerated()), id: \.offset) { i, s in arch(s).clubEntrance(i) } }
                        .frame(height: 440)
                }
                HStack {
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                    Spacer()
                }
            }
        }
    }

    /// One tall arch per sport: b-roll inside the arch, its object and name below.
    private func arch(_ sport: Sport) -> some View {
        let id = "sport-\(sport.rawValue)"
        let focused = menu.isFocused(id)
        let tint = Club.color(sport)
        let new = sport.playable && !menu.progress.finishedTutorial(sport)
        let shape = UnevenRoundedRectangle(topLeadingRadius: compact ? 30 : 110, bottomLeadingRadius: 22, bottomTrailingRadius: 22, topTrailingRadius: compact ? 30 : 110, style: .continuous)
        return Button { menu.tap(id) } label: {
            ZStack(alignment: .bottom) {
                SceneImage(name: Club.scene(for: sport))
                    .saturation(sport.playable ? 1 : 0.15)
                LinearGradient(colors: [.clear, tint.opacity(0.35), Club.lagoonDeep.opacity(0.95)], startPoint: .top, endPoint: .bottom)
                VStack(spacing: compact ? 4 : 8) {
                    ClubArt(name: sport.playable ? sport.rawValue : sport.rawValue, fallback: sport.icon)
                        .frame(width: compact ? 70 : 118, height: compact ? 70 : 118)
                        .shadow(color: .black.opacity(0.4), radius: 8, y: 5)
                        .scaleEffect(focused ? 1.12 : 1).offset(y: focused ? -6 : 0)
                    Text(sport.title.capitalized).font(Club.display(compact ? 30 : 38)).foregroundStyle(.white).lineLimit(1).minimumScaleFactor(0.6)
                    if !sport.playable { ClubBadge(kind: .soon) } else if new { ClubBadge(kind: .new) }
                    else { Text(sport.tagline).font(Club.ui(compact ? 12 : 13, 500)).foregroundStyle(.white.opacity(0.85)).multilineTextAlignment(.center).lineLimit(2) }
                }
                .padding(.horizontal, 10).padding(.bottom, compact ? 12 : 18)
            }
            .clipShape(shape)
            .overlay(shape.strokeBorder(tint, lineWidth: 4))
        }
        .buttonStyle(ClubPress())
        .clubFocus(focused, corner: compact ? 30 : 60)
        .accessibilityLabel(sport.title.capitalized)
    }
}

// MARK: - Sport hub

struct ClubHubScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool
    var body: some View {
        let done = menu.progress.finishedTutorial(sport)
        ClubScreen(scene: sport == .golf ? "cliff" : "court", breadcrumb: ["Pick a sport", sport.title.capitalized], compact: compact, tint: 0.4) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 14)) : AnyLayout(HStackLayout(alignment: .top, spacing: 26))
            VStack(alignment: .leading, spacing: 14) {
                layout {
                    hero(done: done).frame(width: compact ? nil : 430, height: compact ? 210 : 470)
                    let items = TennisMenu.hubItems(sport)
                    let grid = VStack(spacing: 14) {
                        ForEach(0..<(items.count + 1) / 2, id: \.self) { r in
                            HStack(spacing: 14) {
                                ForEach(items[(r * 2)..<min(items.count, r * 2 + 2)], id: \.self) { id in
                                    card(id).clubEntrance(items.firstIndex(of: id) ?? 0)
                                }
                            }
                        }
                    }
                    grid.frame(height: compact ? 380 : 470)
                }
                HStack(spacing: 12) {
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                    if done { ClubButton(title: "Replay tutorial", icon: "arrow.counterclockwise", focused: menu.isFocused("replayTutorial"), style: .quiet, size: 20) { menu.tap("replayTutorial") } }
                    if !menu.notice.isEmpty { Text(menu.notice).font(Club.ui(16, 700)).foregroundStyle(Club.sun) }
                    Spacer()
                }
            }
        }
    }

    private func hero(done: Bool) -> some View {
        ZStack(alignment: .bottomLeading) {
            SceneImage(name: Club.scene(for: sport))
            LinearGradient(colors: [.clear, Club.lagoonDeep.opacity(0.9)], startPoint: .center, endPoint: .bottom)
            VStack(alignment: .leading, spacing: 6) {
                ClubArt(name: sport.rawValue, fallback: sport.icon).frame(width: compact ? 60 : 96, height: compact ? 60 : 96)
                Text(sport.title.capitalized).font(Club.display(compact ? 44 : 66)).foregroundStyle(.white)
                Text(sport.tagline).font(Club.ui(compact ? 14 : 18, 600)).foregroundStyle(.white.opacity(0.9))
                if !done {
                    Label("New here? The tutorial unlocks everything else.", systemImage: "sparkles")
                        .font(Club.ui(compact ? 13 : 15, 700)).foregroundStyle(Club.sun)
                }
            }.padding(compact ? 16 : 24)
        }
        .clipShape(RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).strokeBorder(Club.color(sport), lineWidth: 4))
        .shadow(color: .black.opacity(0.3), radius: 16, y: 8)
    }

    private func card(_ id: String) -> some View {
        let c = TennisCampaign.shared
        let done = menu.progress.finishedTutorial(sport)
        let unlocked = menu.hubUnlocked(sport, id)
        let info: (art: String, title: String, subtitle: String, badge: ClubBadge.Kind?) = switch id {
        case "tutorial": (sport == .tennis ? "lesson" : "whistle", sport == .tennis ? "Tutorial" : "Golf lesson",
                          sport == .tennis ? "Learn with Coach Ray · 5 min" : "Stance, swing, aim", done ? .done : .new)
        case "campaign": ("trophy", "Adventure", c.champion ? "Champion! Defend your title" : "Round \(c.nextRound + 1)/10 · vs \(c.next.name)", c.champion ? nil : .next)
        case "exhibition": ("quick", "Quick match", "Any rival you've reached", nil)
        case "training": ("training", "Training court", "Free rally, no score", nil)
        case "round": ("golf", "Play Cliffside", "A round on the ocean links", nil)
        case "golfCampaign": ("trophy", "Golf tour", "Coming soon", .soon)
        default: ("training", "Driving range", "Coming soon", .soon)
        }
        return ClubCard(art: info.art, title: info.title, subtitle: info.subtitle, badge: unlocked ? info.badge : (id.hasPrefix("golf") ? .soon : .locked),
                        tint: Club.color(sport), locked: !unlocked, highlight: id == "tutorial" && !done,
                        focused: menu.isFocused(id), refusals: menu.isFocused(id) ? menu.refusals : 0, compact: compact) { menu.tap(id) }
    }
}

// MARK: - Coming soon / party / quick play

struct ClubLockedScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "pavilion", breadcrumb: ["Pick a sport", sport.title.capitalized], compact: compact, tint: 0.2) {
            ZStack(alignment: .bottomLeading) {
                SceneImage(name: Club.scene(for: sport)).saturation(0.3)
                LinearGradient(colors: [.clear, Club.lagoonDeep.opacity(0.9)], startPoint: .center, endPoint: .bottom)
                HStack(alignment: .bottom, spacing: 24) {
                    VStack(alignment: .leading, spacing: 10) {
                        ClubBadge(kind: .soon)
                        Text(sport.title.capitalized).font(Club.display(compact ? 60 : 110)).foregroundStyle(.white)
                        Text("\(sport.tagline). We're building it now — tennis and golf are ready today.")
                            .font(Club.ui(compact ? 15 : 20, 600)).foregroundStyle(.white.opacity(0.9)).frame(maxWidth: 620, alignment: .leading)
                        ClubButton(title: "Back to sports", icon: "arrow.left", focused: menu.isFocused("back"), size: 22) { menu.tap("back") }
                    }
                    Spacer()
                    if !compact { ClubArt(name: sport.rawValue, fallback: sport.icon).frame(width: 230, height: 230).rotationEffect(.degrees(-10)) }
                }.padding(compact ? 18 : 36)
            }
            .clipShape(RoundedRectangle(cornerRadius: 30, style: .continuous))
        }
    }
}

struct ClubPartyScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "home", breadcrumb: ["Clubhouse", "With friends"], compact: compact) {
            HStack(spacing: 30) {
                VStack(alignment: .leading, spacing: 16) {
                    ClubBadge(kind: .soon)
                    Text("Play with friends").font(Club.display(compact ? 44 : 76)).foregroundStyle(.white)
                    Text("Couch parties and online invites are on the way. Until then, the island's rivals are ready for you.")
                        .font(Club.ui(compact ? 15 : 21, 500)).foregroundStyle(.white.opacity(0.85)).frame(maxWidth: 560, alignment: .leading)
                    HStack(spacing: 14) {
                        ClubButton(title: "Play solo", focused: menu.isFocused("partySolo"), size: 22) { menu.tap("partySolo") }
                        ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 22) { menu.tap("back") }
                    }
                }
                if !compact { ClubArt(name: "friends").frame(width: 300, height: 300).clubEntrance(1) }
            }
            .frame(maxHeight: .infinity)
        }
    }
}

struct ClubQuickPlayScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "pavilion", breadcrumb: ["Clubhouse", "Quick play"], compact: compact) {
            VStack(alignment: .leading, spacing: 18) {
                Text("Jump straight in").font(Club.display(compact ? 40 : 60)).foregroundStyle(.white)
                HStack(spacing: 18) {
                    ClubCard(art: "tennis", title: "Tennis", subtitle: "A relaxed match", tint: Club.sky,
                             focused: menu.isFocused("quickTennis"), compact: compact) { menu.tap("quickTennis") }.clubEntrance(0)
                    ClubCard(art: "golf", title: "Golf", subtitle: "A round at Cliffside", tint: Club.green,
                             focused: menu.isFocused("quickGolf"), compact: compact) { menu.tap("quickGolf") }.clubEntrance(1)
                }.frame(height: compact ? 220 : 320)
                ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
            }
        }
    }
}

// MARK: - Adventure ladder and quick match

struct ClubLadderScreen: View {
    let menu: TennisMenu
    let compact: Bool
    let exhibition: Bool
    var body: some View {
        let c = TennisCampaign.shared
        let prefix = exhibition ? "rival" : "round"
        let focusedIndex = Int(menu.focused.dropFirst(5)) ?? c.nextRound
        let shown = TennisCampaign.draw[menu.focused.hasPrefix(prefix) ? focusedIndex : c.nextRound]
        let shownIndex = TennisCampaign.draw.firstIndex(of: shown) ?? 0
        ClubScreen(scene: "trophy", breadcrumb: ["Tennis", exhibition ? "Quick match" : "Adventure"], compact: compact, tint: 0.5) {
            VStack(alignment: .leading, spacing: compact ? 10 : 12) {
                if compact {
                    ScrollView {
                        VStack(spacing: 10) {
                            ForEach(Array(TennisCampaign.draw.enumerated()), id: \.offset) { i, o in plinth(o, i, prefix).frame(height: 110) }
                        }
                    }
                } else {
                    ForEach(0..<2, id: \.self) { half in
                        HStack(spacing: 14) {
                            ForEach(half * 5..<half * 5 + 5, id: \.self) { i in plinth(TennisCampaign.draw[i], i, prefix).clubEntrance(i) }
                        }.frame(height: 196)
                    }
                    detail(shown, shownIndex)
                }
                HStack(spacing: 12) {
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                    if !exhibition { ClubButton(title: "New tournament", icon: "arrow.counterclockwise", focused: menu.isFocused("restart"), style: .quiet, size: 20) { menu.tap("restart") } }
                    if compact && !menu.notice.isEmpty { Text(menu.notice).font(Club.ui(13, 700)).foregroundStyle(Club.sun) }
                    Spacer()
                }
            }
        }
    }

    /// A rival on their plinth: portrait (a silhouette until reached), round, and name.
    private func plinth(_ o: TennisOpponent, _ i: Int, _ prefix: String) -> some View {
        let c = TennisCampaign.shared
        let id = "\(prefix)\(i)"
        let locked = !c.unlocked(i), beaten = c.beaten(i), next = i == c.nextRound && !c.champion
        let focused = menu.isFocused(id)
        return Button { menu.tap(id) } label: {
            ZStack(alignment: .bottom) {
                RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .fill(LinearGradient(colors: [o.boss ? Club.coral.opacity(0.5) : Club.violet.opacity(0.35), Club.lagoonDeep.opacity(0.85)], startPoint: .top, endPoint: .bottom))
                HeroArt(name: o.art, silhouette: locked)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: compact ? .trailing : .top)
                    .padding(.top, compact ? 4 : 8).padding(.bottom, compact ? 0 : 58).padding(.trailing, compact ? 8 : 0)
                VStack(alignment: compact ? .leading : .center, spacing: 2) {
                    Text(o.round).font(Club.caps(compact ? 10 : 10)).tracking(1).foregroundStyle(Club.sun).lineLimit(1)
                    Text(locked ? "???" : o.name.components(separatedBy: " ")[0]).font(Club.title(compact ? 22 : 22)).foregroundStyle(.white)
                    Stars(count: o.stars, of: 5, size: 10)
                }
                .padding(8).frame(maxWidth: .infinity, alignment: compact ? .leading : .center)
                .background(LinearGradient(colors: [.clear, Club.lagoonDeep.opacity(0.95)], startPoint: .top, endPoint: .bottom))
            }
            .overlay(alignment: .topLeading) {
                Group {
                    if beaten { ClubArt(name: "crown", fallback: "crown.fill").frame(width: 34, height: 34) }
                    else if locked { ClubArt(name: "lock", fallback: "lock.fill").frame(width: 28, height: 28) }
                    else if next { ClubBadge(kind: o.boss ? .boss : .next) }
                }.padding(8)
            }
            .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).strokeBorder(o.boss ? Club.coral : .white.opacity(0.25), lineWidth: o.boss ? 3 : 1.5))
        }
        .buttonStyle(ClubPress())
        .clubFocus(focused, corner: 20, refusals: focused ? menu.refusals : 0)
    }

    private func detail(_ o: TennisOpponent, _ i: Int) -> some View {
        let locked = !TennisCampaign.shared.unlocked(i)
        return HStack(spacing: 16) {
            VStack(alignment: .leading, spacing: 3) {
                Text(locked ? "Locked" : "\(o.name) · \(o.nickname)").font(Club.title(22)).foregroundStyle(o.boss ? Club.coral : Club.sun)
                Text(!menu.notice.isEmpty ? menu.notice : locked ? "Win the round before to meet this rival." : o.blurb)
                    .font(Club.ui(16, 500)).foregroundStyle(.white).lineLimit(2)
            }
            Spacer()
            if !locked {
                Text(exhibition ? "One set" : o.formatTitle).font(Club.caps(13)).foregroundStyle(.white.opacity(0.75))
                Text(exhibition ? "Ⓐ Play" : TennisCampaign.shared.beaten(i) ? "Ⓐ Replay" : "Ⓐ Play match").font(Club.title(22)).foregroundStyle(Club.sun)
            }
        }
        .padding(.horizontal, 20).padding(.vertical, 12)
        .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(Club.lagoonDeep.opacity(0.82)))
    }
}

// MARK: - Training

struct ClubTrainingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ClubScreen(scene: "court", breadcrumb: ["Tennis", "Training court"], compact: compact) {
            VStack(alignment: .leading, spacing: 18) {
                Text("Free rally with the club coach").font(Club.display(compact ? 34 : 54)).foregroundStyle(.white)
                Text("No score, no pressure — work on timing, aim and the sweet spot. Press ◀ ▶ to pick a coach.")
                    .font(Club.ui(compact ? 15 : 19, 500)).foregroundStyle(.white.opacity(0.85))
                HStack(spacing: 16) {
                    ForEach(Array(TennisMenu.trainingLevels.enumerated()), id: \.offset) { i, level in
                        let selected = menu.trainingLevel == i
                        VStack(spacing: 6) {
                            ClubArt(name: i == 0 ? "training" : i == 1 ? "tennis" : "quick").frame(width: compact ? 60 : 96, height: compact ? 60 : 96)
                            Text(level.name).font(Club.title(compact ? 20 : 28)).foregroundStyle(selected ? Club.ink : .white)
                            Text(["Slow, friendly feeds", "A proper rally", "Makes you run"][i]).font(Club.ui(compact ? 12 : 15)).foregroundStyle(selected ? Club.ink.opacity(0.7) : .white.opacity(0.75))
                        }
                        .frame(maxWidth: .infinity).padding(.vertical, compact ? 12 : 20)
                        .background(RoundedRectangle(cornerRadius: 22, style: .continuous).fill(selected ? Club.sun : Color.black.opacity(0.3)))
                        .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(.white.opacity(menu.isFocused("level") && selected ? 1 : 0.15), lineWidth: menu.isFocused("level") && selected ? 4 : 1))
                        .scaleEffect(selected ? 1.04 : 1).animation(Club.pop, value: selected)
                        .onTapGesture { while menu.trainingLevel != i { menu.tap("level") } }
                    }
                }
                HStack(spacing: 14) {
                    ClubButton(title: "Start training", icon: "play.fill", focused: menu.isFocused("start"), size: 24) { menu.tap("start") }
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                }
            }
        }
    }
}

// MARK: - Your look

struct ClubCharacterScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var name = ""
    var body: some View {
        let s = SportsSession.shared
        let p = menu.player ?? Player(name: "Player 1", colorIndex: 0)
        let rows: [(String, String, String, Color?)] = [
            ("body", "Player", p.standardFemale ? "Girl" : "Boy", nil),
            ("haircut", "Hairstyle", HeroV4.haircuts[p.haircut], nil),
            ("skin", "Skin tone", "\(Int((p.skinT * 100).rounded()))%", Color(hex: p.skinHex)),
            ("hair", "Headwear", HeroV4.headwear[p.hairStyle], nil),
            ("hairColor", "Hair colour", p.hairDyed ? "Dyed" : LockerColor.hairPresets.min { abs($0.1 - p.hairT) < abs($1.1 - p.hairT) }!.0, Color(hex: p.hairHex)),
            ("hand", "Plays", p.handedness == .left ? "Left-handed" : "Right-handed", nil),
            ("shirt", "Shirt", p.outfitName("shirt"), p.outfitColor("shirt")),
            ("shorts", "Shorts & trim", p.outfitName("shorts"), p.outfitColor("shorts")),
            ("accent", "Shoes", p.outfitName("accent"), p.outfitColor("accent")),
            ("racket", "Racket", p.outfitName("racket"), p.outfitColor("racket")),
        ]
        ClubScreen(scene: "locker", breadcrumb: ["Clubhouse", "Your look"], compact: compact, tint: 0.35) {
            if compact { LockerStudio(menu: menu) } else {
            HStack(alignment: .top, spacing: compact ? 10 : 28) {
                VStack(spacing: compact ? 6 : 8) {
                    if compact {
                        HStack {
                            Text("Name").font(Club.ui(14, 600)).foregroundStyle(.white)
                            TextField("Your name", text: $name).font(Club.title(16)).foregroundStyle(Club.sun).multilineTextAlignment(.trailing)
                                .onChange(of: name) { _, v in rename(v) }
                        }.padding(10).background(RoundedRectangle(cornerRadius: 14).fill(.black.opacity(0.3)))
                    }
                    ScrollViewReader { proxy in
                        ScrollView {
                            VStack(spacing: compact ? 6 : 8) {
                                ForEach(rows, id: \.0) { row in
                                    if row.0 == "build" {
                                        CharacterSizeControl(value: Binding(get: { menu.player?.bodySize ?? 0.5 }, set: { menu.setBodySize($0) }), focused: menu.isFocused("build"), compact: compact).id("build")
                                    } else if compact {
                                        VStack(alignment: .leading, spacing: 2) {
                                            HStack {
                                                Text(row.1).font(Club.ui(13, 600)).lineLimit(1)
                                                Spacer()
                                                if let swatch=row.3 { Circle().fill(swatch).frame(width:14,height:14) }
                                            }
                                            HStack(spacing: 0) {
                                                Button { _ = menu.adjust(row.0, by:-1) } label: { Image(systemName:"chevron.left").frame(width:36,height:44) }.accessibilityLabel("Previous \(row.1)")
                                                Text(row.2).font(Club.title(15)).lineLimit(1).minimumScaleFactor(0.75).frame(maxWidth:.infinity)
                                                Button { _ = menu.adjust(row.0, by:1) } label: { Image(systemName:"chevron.right").frame(width:36,height:44) }.accessibilityLabel("Next \(row.1)")
                                            }.buttonStyle(.plain)
                                        }
                                        .foregroundStyle(.white).padding(.horizontal,10).padding(.top,8)
                                        .background(RoundedRectangle(cornerRadius:14).fill(.black.opacity(0.35)))
                                        .id(row.0)
                                    } else {
                                        ClubRow(label: row.1, value: row.2, swatch: row.3, focused: menu.isFocused(row.0), compact: false,
                                                step: { _ = menu.adjust(row.0, by: $0) }) { menu.tap(row.0) }.id(row.0)
                                    }
                                }
                            }.padding(6)
                        }
                        .onChange(of: menu.focused) { _, id in withAnimation { proxy.scrollTo(id, anchor: .center) } }
                    }
                    (compact ? AnyLayout(VStackLayout(spacing: 6)) : AnyLayout(HStackLayout(spacing: 10))) {
                        ClubButton(title: compact ? "Shuffle" : "Surprise me", icon: "dice.fill", focused: menu.isFocused("randomize"), style: .secondary, size: compact ? 15 : 19) { menu.tap("randomize") }
                        ClubButton(title: compact ? "Reset colours" : "Kit colours", icon: "arrow.uturn.backward", focused: menu.isFocused("reset"), style: .quiet, size: compact ? 15 : 19) { menu.tap("reset") }
                        ClubButton(title: "Done", icon: "checkmark", focused: menu.isFocused("back"), size: compact ? 15 : 19) { menu.tap("back") }
                    }
                }
                // The mirror: your player, live.
                ZStack(alignment: .bottom) {
                    RoundedRectangle(cornerRadius: 30, style: .continuous).fill(LinearGradient(colors: [.white.opacity(0.22), .white.opacity(0.05)], startPoint: .top, endPoint: .bottom))
                    RoundedRectangle(cornerRadius: 30, style: .continuous).strokeBorder(Club.sun, lineWidth: 5)
                    CharacterModelPreview(player: p, cameraDistance: 3.6).padding(.bottom, 40)
                    Text(p.name).font(Club.title(compact ? 16 : 24)).foregroundStyle(Club.ink)
                        .padding(.horizontal, 18).padding(.vertical, 6).background(Capsule().fill(Club.sun)).padding(.bottom, 12)
                }
                .frame(width: compact ? 130 : 400)
                .frame(maxHeight: compact ? 360 : .infinity)
            }
            .onAppear { name = p.name; _ = s }
            }
        }
    }
    private func rename(_ text: String) {
        let s = SportsSession.shared
        let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty, s.players.indices.contains(s.playerIndex) else { return }
        s.players[s.playerIndex].name = String(t.prefix(14)); s.savePlayers()
    }
}

// MARK: - Settings

struct ClubSettingsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        let s = SportsSession.shared
        ClubScreen(scene: "office", breadcrumb: ["Clubhouse", "Settings"], compact: compact, tint: 0.5) {
            VStack(alignment: .leading, spacing: 12) {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(SettingsTab.allCases, id: \.self) { tab in
                            ClubPill(title: tab.title, selected: menu.settingsTab == tab, focused: menu.isFocused("tab-\(tab.rawValue)"), compact: compact) { menu.tap("tab-\(tab.rawValue)") }
                        }
                    }.padding(.vertical, 6).padding(.horizontal, 4)
                }
                HStack(alignment: .top, spacing: 24) {
                    VStack(spacing: 8) {
                        ForEach(TennisMenu.settingsRows(menu.settingsTab), id: \.self) { id in
                            let (label, value) = SettingsScreen.describe(id, s, menu.player)
                            ClubRow(label: label, value: value, focused: menu.isFocused(id), compact: compact,
                                    step: { _ = menu.adjust(id, by: $0) }) { menu.tap(id) }
                        }
                        if !menu.notice.isEmpty {
                            Text(menu.notice).font(Club.ui(compact ? 14 : 17, 700)).foregroundStyle(Club.sun).frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                    if !compact {
                        ClubArt(name: menu.settingsTab == .display ? "tennis" : menu.settingsTab == .controls ? "training" : "dial")
                            .frame(width: 220, height: 220).id(menu.settingsTab).transition(.scale.combined(with: .opacity))
                    }
                }
                Spacer(minLength: 0)
                ClubButton(title: "Done", icon: "checkmark", focused: menu.isFocused("back"), size: 20) { menu.tap("back") }
            }
            .animation(Club.spring, value: menu.settingsTab)
        }
    }
}

// MARK: - How to play and the golf lesson

struct ClubGuideScreen: View {
    let menu: TennisMenu
    let compact: Bool
    let golf: Bool
    var body: some View {
        let cards = golf ? GolfLesson.cards : HowTo.pages
        let index = min(golf ? menu.lessonCard : menu.howToPage, cards.count - 1)
        let card = cards[index]
        let last = index == cards.count - 1
        let nextID = golf ? "nextCard" : "nextPage"
        ClubScreen(scene: golf ? "cliff" : "office", breadcrumb: golf ? ["Golf", "Lesson"] : ["Clubhouse", "How to play"], compact: compact, tint: 0.55) {
            VStack(alignment: .leading, spacing: 18) {
                HStack(alignment: .center, spacing: compact ? 14 : 30) {
                    ZStack {
                        Circle().fill(Club.sun.opacity(0.25)).blur(radius: 16)
                        HowToArt(art: card.art)
                    }.frame(width: compact ? 110 : 250, height: compact ? 110 : 250)
                    VStack(alignment: .leading, spacing: compact ? 8 : 14) {
                        Text("Step \(index + 1) of \(cards.count)").font(Club.caps(compact ? 12 : 14)).tracking(1.5).foregroundStyle(Club.sun)
                        Text(card.title).font(Club.display(compact ? 32 : 58)).foregroundStyle(.white)
                        ForEach(Array(card.steps.enumerated()), id: \.offset) { i, step in
                            HStack(alignment: .top, spacing: 12) {
                                Text("\(i + 1)").font(Club.title(compact ? 14 : 18)).foregroundStyle(Club.ink)
                                    .frame(width: compact ? 24 : 32, height: compact ? 24 : 32).background(Circle().fill(Club.sun))
                                Text(step).font(Club.ui(compact ? 14 : 20, 500)).foregroundStyle(.white).fixedSize(horizontal: false, vertical: true)
                            }
                        }
                    }
                }
                .padding(compact ? 16 : 30)
                .background(RoundedRectangle(cornerRadius: 30, style: .continuous).fill(Club.lagoonDeep.opacity(0.78)))
                .id(index).transition(.asymmetric(insertion: .move(edge: .trailing).combined(with: .opacity), removal: .opacity))
                HStack(spacing: 8) {
                    ForEach(cards.indices, id: \.self) { i in
                        Capsule().fill(i == index ? Club.sun : .white.opacity(0.35)).frame(width: i == index ? 34 : 12, height: 10)
                    }
                }.frame(maxWidth: .infinity)
                Spacer(minLength: 0)
                HStack(spacing: 12) {
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
                    Spacer()
                    ClubButton(title: "Previous", icon: "arrow.left", focused: menu.isFocused("prev"), style: .quiet, size: 20) { menu.tap("prev") }
                    if !(last && !golf) {
                        ClubButton(title: last ? "Hit a practice shot" : "Next", icon: last ? "play.fill" : "arrow.right",
                                   focused: menu.isFocused(nextID), size: 22) { menu.tap(nextID) }
                    }
                }
            }
            .animation(Club.spring, value: index)
        }
    }
}

// MARK: - Connect a screen

struct ClubConnectScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var pulse = false
    var body: some View {
        ClubScreen(scene: "home", breadcrumb: ["Connect a screen"], compact: compact, showHints: false, tint: 0.6) {
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    Text("Mirror to a TV or a Mac").font(Club.display(compact ? 32 : 50)).foregroundStyle(.white)
                    Text("The game plays on the big screen. Your phone becomes the racket.").font(Club.ui(15, 500)).foregroundStyle(.white.opacity(0.85))
                    guide(HowTo.connectTV); guide(HowTo.connectMac)
                    HStack(spacing: 12) {
                        Circle().fill(Club.sun).frame(width: 12, height: 12).scaleEffect(pulse ? 1.4 : 0.8).opacity(pulse ? 0.5 : 1)
                        Text("Waiting for a screen… the match starts as soon as one connects.").font(Club.ui(13, 600)).foregroundStyle(Club.sun)
                    }
                    HStack(spacing: 12) {
                        AirPlayButton().frame(width: 48, height: 48).background(Circle().fill(.white.opacity(0.18)))
                        ClubButton(title: "Play on this phone", icon: "iphone", focused: menu.isFocused("phone"), style: .secondary, size: 18) { menu.tap("phone") }
                    }
                    ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 18) { menu.tap("back") }
                }
            }
        }
        .onAppear { if !Club.still { withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) { pulse = true } } }
    }
    private func guide(_ card: HowToCard) -> some View {
        HStack(alignment: .top, spacing: 12) {
            HowToArt(art: card.art).frame(width: 70, height: 70)
            VStack(alignment: .leading, spacing: 4) {
                Text(card.title).font(Club.title(18)).foregroundStyle(Club.sun)
                ForEach(Array(card.steps.enumerated()), id: \.offset) { i, s in
                    Text("\(i + 1). \(s)").font(Club.ui(13, 500)).foregroundStyle(.white).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .padding(12).background(RoundedRectangle(cornerRadius: 20, style: .continuous).fill(Club.lagoonDeep.opacity(0.8)))
    }
}

// MARK: - Story and results

struct ClubStoryScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var shown = 0
    @State private var typing: Task<Void, Never>?
    var body: some View {
        let line = menu.storyLine ?? StoryLine(speaker: "ray", text: "")
        let female = menu.player?.standardFemale ?? false
        let rival = TennisCampaign.draw.first { $0.key == line.speaker }
        let accent: Color = line.speaker == "you" ? Club.sun : rival?.boss == true ? Club.coral : rival != nil ? Club.violet : Club.sky
        let scene = line.speaker == "announcer" ? "stands" : rival != nil ? "trophy" : "office"
        ZStack(alignment: .bottom) {
            ClubBackdrop(scene: scene, tint: 0.35)
            HeroArt(name: TennisStory.art(for: line.speaker, female: female), silhouette: line.speaker == "stranger")
                .frame(maxHeight: compact ? 360 : 560)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: line.speaker == "you" ? .bottomTrailing : .bottomLeading)
                .padding(.horizontal, compact ? 0 : 70).padding(.bottom, compact ? 190 : 130)
                .id(line.speaker).transition(.move(edge: line.speaker == "you" ? .trailing : .leading).combined(with: .opacity))
            VStack(alignment: .leading, spacing: 10) {
                Text(TennisStory.name(for: line.speaker)).font(Club.title(compact ? 18 : 26)).foregroundStyle(Club.ink)
                    .padding(.horizontal, 18).padding(.vertical, 6).background(Capsule().fill(accent))
                    .offset(y: compact ? -24 : -30).padding(.bottom, compact ? -24 : -30)
                Text(String(line.text.prefix(shown))).font(Club.ui(compact ? 18 : 27, 600)).foregroundStyle(.white)
                    .frame(maxWidth: .infinity, minHeight: compact ? 110 : 120, alignment: .topLeading)
                HStack {
                    Text("\(menu.storyIndex + 1) / \(menu.story.count)").font(Club.caps(compact ? 12 : 15)).foregroundStyle(.white.opacity(0.5))
                    Spacer()
                    ClubButton(title: "Skip", icon: "forward.end.fill", focused: menu.isFocused("skip"), style: .quiet, size: compact ? 15 : 18) { menu.tap("skip") }
                    ClubButton(title: shown < line.text.count ? "…" : "Next", icon: "chevron.right", focused: menu.isFocused("next"), size: compact ? 15 : 18) { advance(line) }
                }
            }
            .padding(.horizontal, compact ? 20 : 34).padding(.top, compact ? 26 : 30).padding(.bottom, compact ? 16 : 20)
            .background(RoundedRectangle(cornerRadius: 28, style: .continuous).fill(Club.lagoonDeep.opacity(0.94)))
            .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).strokeBorder(accent, lineWidth: 3))
            .padding(.horizontal, compact ? 12 : 60).padding(.bottom, compact ? 20 : 34)
        }
        .contentShape(Rectangle())
        .onTapGesture { advance(line) }
        .onAppear { if menu.storyInstant { shown = line.text.count } else { type(line) } }
        .onChange(of: menu.storyIndex) { _, _ in type(menu.storyLine ?? line) }
        .animation(Club.spring, value: line.speaker)
    }
    private func advance(_ line: StoryLine) {
        if shown < line.text.count { typing?.cancel(); shown = line.text.count } else { menu.advanceStory() }
    }
    private func type(_ line: StoryLine) {
        typing?.cancel(); shown = 0
        typing = Task { @MainActor in
            while shown < line.text.count, !Task.isCancelled { try? await Task.sleep(for: .milliseconds(24)); shown += 1 }
        }
    }
}

struct ClubResultsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var pop = false
    var body: some View {
        let c = TennisCampaign.shared
        let result = menu.result
        let round = result?.round ?? 0
        let o = TennisCampaign.draw[round]
        let won = result?.won ?? false
        let crowned = won && round == TennisCampaign.draw.count - 1
        ZStack {
            ClubBackdrop(scene: "stands", tint: won ? 0.25 : 0.6)
            if crowned { Confetti() }
            HStack(spacing: 40) {
                Group {
                    if crowned { ClubArt(name: "trophy").frame(width: compact ? 180 : 360, height: compact ? 180 : 360) }
                    else { HeroArt(name: o.art).frame(maxWidth: compact ? 140 : 340, maxHeight: compact ? 220 : 470).saturation(won ? 0.4 : 1) }
                }
                .scaleEffect(pop ? 1 : 0.5).opacity(pop ? 1 : 0)
                VStack(alignment: .leading, spacing: compact ? 10 : 16) {
                    Text(crowned ? "Champion!" : won ? "Victory!" : "So close").font(Club.display(compact ? 60 : 110))
                        .foregroundStyle(won ? Club.sun : .white).shadow(color: .black.opacity(0.4), radius: 0, x: 0, y: 6)
                        .scaleEffect(pop ? 1 : 1.6).opacity(pop ? 1 : 0)
                    Text(won ? "You beat \(o.name) \(result?.score ?? "")" : "\(o.name) wins \(result.map { String($0.score.reversed()) } ?? "")")
                        .font(Club.title(compact ? 20 : 32)).foregroundStyle(.white)
                    Text(crowned ? "Tropical Open champion. Viktor has finally fallen — and Old Ray gets his final back."
                         : won ? "Coach Ray: \"Next up: \(c.next.name), \(c.next.nickname). \(c.next.formatTitle).\""
                         : "Coach Ray: \"Shake it off. \(o.blurb)\"")
                        .font(Club.ui(compact ? 15 : 20, 500)).foregroundStyle(.white.opacity(0.9)).frame(maxWidth: 560, alignment: .leading)
                    HStack(spacing: 12) {
                        ForEach(menu.rows(.results).flatMap { $0 }, id: \.self) { id in
                            ClubButton(title: id == "continue" ? "Next match" : id == "retry" ? "Rematch" : id == "restart" ? "New tournament" : "Adventure",
                                       icon: id == "menu" ? "list.bullet" : "play.fill", focused: menu.isFocused(id),
                                       style: id == "menu" ? .quiet : .primary, size: compact ? 18 : 24) { menu.tap(id) }
                        }
                    }.padding(.top, 8)
                }
            }
            .padding(compact ? 20 : 60)
        }
        .onAppear { withAnimation(.spring(response: 0.5, dampingFraction: 0.55).delay(0.2)) { pop = true } }
    }
}

/// The same continuous value is sent to both sports; remote arrows adjust by five percent.
struct CharacterSizeControl: View {
    @Binding var value: Double
    var focused = false
    var compact = false
    var body: some View {
        VStack(spacing: 4) {
            HStack {
                Text("Body size").font(Club.ui(compact ? 14 : 20, 600))
                Spacer()
                Text("\(Int(value * 100))%").monospacedDigit().font(Club.ui(compact ? 13 : 18, 600))
            }
            Slider(value: $value, in: 0...1)
                .tint(Club.sun)
                .accessibilityLabel("Body size")
                .accessibilityValue("\(Int(value * 100)) percent, from skinny to big")
                .accessibilityIdentifier("characterBodySize")
            HStack {
                Text("Skinny"); Spacer(); Text("Big")
            }.font(Club.ui(compact ? 11 : 15, 500)).foregroundStyle(.white.opacity(0.8))
        }
        .foregroundStyle(.white).padding(compact ? 10 : 16)
        .background(RoundedRectangle(cornerRadius: 14).fill(.black.opacity(0.3)))
        .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(focused ? Club.sun : .clear, lineWidth: 3))
    }
}

// MARK: - Simple Island screen family
struct IslandTitleScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        GeometryReader { g in
            ZStack {
                IslandBackdrop(intro: true)
                IslandWordmark(size: compact ? 44 : 78)
                    .position(x: g.size.width * (compact ? 0.5 : 0.45), y: g.size.height * 0.22)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0))
                    .frame(width: g.size.width * (compact ? 0.95 : 0.42), height: g.size.height * 0.72)
                    .position(x: g.size.width * (compact ? 0.5 : 0.76), y: g.size.height * 0.60)
                IslandAction(title: "Play", focused: menu.isFocused("start"), primary: true, tint: .white, compact: compact, identifier: "intro-play") { menu.tap("start") }
                    .frame(width: compact ? 200 : 240)
                    .position(x: g.size.width * (compact ? 0.5 : 0.45), y: g.size.height * 0.89)
            }
        }.preferredColorScheme(.light)
    }
}

struct IslandHomeScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        ZStack {
            IslandBackdrop()
            if compact { IslandUI.paper.opacity(0.45).ignoresSafeArea() }
            let layout = compact ? AnyLayout(VStackLayout(spacing: 8)) : AnyLayout(HStackLayout(spacing: 50))
            layout {
                VStack(alignment: .leading, spacing: 16) {
                    IslandWordmark(size: compact ? 34 : 46).padding(.bottom, compact ? 4 : 20)
                    ForEach([("play", "Play"), ("homeCampaign", "Campaign"), ("character", "Locker"), ("settings", "Settings")], id: \.0) { id, label in
                        IslandAction(title: label, focused: menu.isFocused(id), primary: id == "play", compact: compact, identifier: "home-\(id)") { menu.tap(id) }
                    }
                }.frame(width: compact ? nil : 300).padding(compact ? 20 : 0)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }.padding(.horizontal, compact ? 16 : 90).padding(.vertical, compact ? 30 : 76)
            VStack { HStack { Spacer(); Label(menu.player?.name ?? "Player 1", systemImage: "person.crop.circle.fill").font(IslandUI.font(17, bold: true)).foregroundStyle(IslandUI.navy) }; Spacer() }
                .padding(compact ? 20 : 40).allowsHitTesting(false)
        }.preferredColorScheme(.light)
    }
}

struct IslandSportsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Select a sport", compact: compact) {
            VStack(alignment: .leading, spacing: 20) {
                let layout = compact ? AnyLayout(VStackLayout(spacing: 20)) : AnyLayout(HStackLayout(spacing: 28))
                layout {
                    ForEach(Sport.allCases.filter(\.playable), id: \.self) { sport in
                        Button { menu.tap("sport-\(sport.rawValue)") } label: {
                            VStack(spacing: 14) {
                                SceneImage(name: Club.scene(for: sport)).frame(height: compact ? 165 : 290).clipShape(RoundedRectangle(cornerRadius: 16))
                                Text(sport.title.capitalized).font(IslandUI.font(28, bold: true))
                                Capsule().fill(menu.isFocused("sport-\(sport.rawValue)") ? IslandUI.navy : .clear).frame(width: 110, height: 4)
                            }.foregroundStyle(IslandUI.navy)
                        }.buttonStyle(.plain).accessibilityLabel(sport.title.capitalized)
                    }
                }
                Spacer(minLength: 0)
                IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 170)
            }
        }
    }
}

struct IslandHubScreen: View {
    let menu: TennisMenu
    let sport: Sport
    let compact: Bool
    private func label(_ id: String) -> String {
        switch id { case "tutorial": "Tutorial"; case "campaign": "Campaign"; case "exhibition": "Quick Match"; case "training": "Training"; case "round": "Play Golf"; case "golfCampaign": "Golf Tour · Soon"; default: "Driving Range · Soon" }
    }
    var body: some View {
        IslandShell(title: sport.title.capitalized, compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(spacing: 70))
            layout {
                VStack(alignment: .leading, spacing: 14) {
                    ForEach(TennisMenu.hubItems(sport), id: \.self) { id in
                        IslandAction(title: label(id), focused: menu.isFocused(id), primary: menu.isFocused(id), compact: compact, identifier: "hub-\(id)") { menu.tap(id) }
                    }
                    if !menu.notice.isEmpty { Text(menu.notice).font(IslandUI.font(16)).foregroundStyle(IslandUI.navy).fixedSize(horizontal: false, vertical: true) }
                    Spacer(minLength: 16)
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                }.frame(width: compact ? nil : 370)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0), sport: sport == .golf ? .golf : nil)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
    }
}

struct IslandLadderScreen: View {
    let menu: TennisMenu
    let compact: Bool
    let exhibition: Bool
    private let points: [CGPoint] = [CGPoint(x:0.10,y:0.28),CGPoint(x:0.28,y:0.52),CGPoint(x:0.43,y:0.22),CGPoint(x:0.59,y:0.55),CGPoint(x:0.80,y:0.29),CGPoint(x:0.91,y:0.65),CGPoint(x:0.72,y:0.84),CGPoint(x:0.48,y:0.77),CGPoint(x:0.26,y:0.87),CGPoint(x:0.08,y:0.70)]
    var body: some View {
        if exhibition {
            IslandShell(title: "Quick Match", compact: compact) {
                let layout = compact ? AnyLayout(VStackLayout(spacing: 18)) : AnyLayout(HStackLayout(spacing: 56))
                layout {
                    VStack(alignment: .leading, spacing: 18) {
                        IslandSelector(label: "Opponent", value: TennisCampaign.draw[menu.quickOpponent].name, focused: menu.isFocused("quickOpponent"), compact: compact) { _ = menu.adjust("quickOpponent", by: $0) }
                        IslandSelector(label: "Difficulty", value: TennisMenu.trainingLevels[menu.quickDifficulty].name, focused: menu.isFocused("quickDifficulty"), compact: compact) { _ = menu.adjust("quickDifficulty", by: $0) }
                        IslandSelector(label: "Match length", value: ["3 games", "1 set", "Best of 3"][menu.quickLength], focused: menu.isFocused("quickLength"), compact: compact) { _ = menu.adjust("quickLength", by: $0) }
                        IslandAction(title: "Start Match", focused: menu.isFocused("quickStart"), primary: true, compact: compact) { menu.tap("quickStart") }.padding(.top, 20)
                        Spacer(minLength: 12)
                        IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                    }.frame(width: compact ? nil : 460)
                    IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0)).frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
        } else {
            GeometryReader { g in
                ZStack {
                    Image(uiImage: UIImage(named: "island-campaign.png") ?? UIImage(named: "island-terrace.png") ?? UIImage()).resizable().scaledToFill().frame(width:g.size.width,height:g.size.height).clipped().ignoresSafeArea()
                    if compact { IslandUI.paper.opacity(0.65).ignoresSafeArea() }
                    let layout = compact ? AnyLayout(VStackLayout(spacing: 14)) : AnyLayout(HStackLayout(spacing: 30))
                    layout {
                        VStack(alignment: .leading, spacing: 18) {
                            Text("Campaign").font(IslandUI.font(compact ? 32 : 42, bold: true))
                            Text("Round \(menu.selectedRound + 1)").font(IslandUI.font(20, bold: true))
                            Text(TennisCampaign.draw[menu.selectedRound].name).font(IslandUI.font(25, bold: true))
                            Text(TennisCampaign.draw[menu.selectedRound].formatTitle).font(IslandUI.font(16))
                            Image(uiImage: UIImage(named:"map-resort.jpg") ?? UIImage()).resizable().scaledToFill().frame(height:compact ? 95 : 150).clipped().clipShape(RoundedRectangle(cornerRadius:12))
                            IslandAction(title: "Play Round", focused: menu.isFocused("campaignPlay"), primary: true, compact: compact) { menu.tap("campaignPlay") }
                            if !menu.notice.isEmpty { Text(menu.notice).font(IslandUI.font(14)) }
                            Spacer(minLength: 0)
                            IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                            Button("New tournament") { menu.tap("restart") }.font(IslandUI.font(14)).padding(.leading,22)
                                .accessibilityAddTraits(menu.isFocused("restart") ? .isSelected : [])
                        }.foregroundStyle(IslandUI.navy).frame(width: compact ? nil : 305)
                        GeometryReader { map in
                            Path { path in
                                for (i, p) in points.enumerated() {
                                    let q = CGPoint(x:map.size.width*p.x,y:map.size.height*p.y)
                                    if i == 0 { path.move(to:q) } else { path.addLine(to:q) }
                                }
                            }.stroke(.white.opacity(0.85),style:StrokeStyle(lineWidth:4,lineCap:.round,dash:[2,11]))
                            ForEach(0..<TennisCampaign.draw.count,id:\.self) { i in
                                let unlocked = TennisCampaign.shared.unlocked(i)
                                Button { menu.tap("round\(i)") } label: {
                                    Group { if unlocked { Text("\(i+1)") } else { Image(systemName:"lock.fill") } }
                                        .font(IslandUI.font(23,bold:true)).foregroundStyle(menu.selectedRound == i ? IslandUI.navy : .white)
                                        .frame(width:48,height:48).background(menu.selectedRound == i ? IslandUI.lime : IslandUI.navy,in:Circle())
                                        .overlay(Circle().strokeBorder(.white,lineWidth:3)).shadow(color:.black.opacity(0.18),radius:3,y:2)
                                }.buttonStyle(.plain).position(x:map.size.width*points[i].x,y:map.size.height*points[i].y)
                                    .accessibilityLabel("Round \(i+1), \(TennisCampaign.draw[i].name)\(unlocked ? "" : ", locked")")
                            }
                        }.frame(minHeight:compact ? 250 : 0)
                    }.padding(compact ? 22 : 52)
                }
            }.preferredColorScheme(.light)
        }
    }
}

struct IslandTrainingScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Training", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 20)) : AnyLayout(HStackLayout(spacing: 70))
            layout {
                VStack(alignment: .leading, spacing: 24) {
                    Text("Rally practice").font(IslandUI.font(27, bold: true)).foregroundStyle(IslandUI.navy)
                    Text("Practice timing and placement with the coach.").font(IslandUI.font(18)).foregroundStyle(IslandUI.muted)
                    IslandSelector(label: "Pace", value: TennisMenu.trainingLevels[menu.trainingLevel].name, focused: menu.isFocused("level"), compact: compact) { _ = menu.adjust("level", by: $0) }
                    IslandAction(title: "Start Practice", focused: menu.isFocused("start"), primary: true, compact: compact) { menu.tap("start") }
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                    Spacer(minLength: 0)
                }.frame(width: compact ? nil : 450)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0)).frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
    }
}

struct IslandLockerScreen: View {
    let menu: TennisMenu
    let compact: Bool
    private var rows: [(String, String, String, Color?)] {
        let p = menu.player ?? Player(name: "Player 1", colorIndex: 0)
        return [("body", "Player", p.standardFemale ? "Girl" : "Boy", nil),
                ("haircut", "Hairstyle", HeroV4.haircuts[p.shownHaircut], nil),
                ("skin", "Skin tone", "\(Int(p.skinT * 100))%", Color(hex: p.skinHex)),
                ("hair", "Headwear", HeroV4.headwear[p.hairStyle], nil),
                ("hairColor", "Hair colour", p.hairDyed ? "Dyed" : "Natural", Color(hex: p.hairHex)),
                ("hand", "Plays", p.handedness == .left ? "Left-handed" : "Right-handed", nil),
                ("shirt", "Shirt", p.outfitName("shirt"), p.outfitColor("shirt")),
                ("shorts", "Shorts & trim", p.outfitName("shorts"), p.outfitColor("shorts")),
                ("accent", "Shoes", p.outfitName("accent"), p.outfitColor("accent")),
                ("racket", "Racket", p.outfitName("racket"), p.outfitColor("racket"))]
    }
    var body: some View {
        IslandShell(title: "Locker", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 10)) : AnyLayout(HStackLayout(spacing: 30))
            layout {
                VStack(spacing: 12) {
                    ScrollViewReader { proxy in
                        ScrollView {
                            VStack(spacing: 4) {
                                ForEach(rows, id: \.0) { row in
                                    IslandSelector(label: row.1, value: row.2, swatch: row.3, focused: menu.isFocused(row.0), compact: compact) { _ = menu.adjust(row.0, by: $0) }.id(row.0)
                                }
                            }.padding(3)
                        }.onChange(of: menu.focused) { _, id in withAnimation(.easeOut(duration: 0.15)) { proxy.scrollTo(id, anchor: .center) } }
                    }
                    HStack(spacing: 8) {
                        ForEach([("randomize", "Surprise me"), ("reset", "Kit colours"), ("back", "Done")], id: \.0) { id, label in
                            Button { menu.tap(id) } label: {
                                Text(label).font(IslandUI.font(compact ? 14 : 20, bold: true)).lineLimit(1).minimumScaleFactor(0.85)
                                    .frame(maxWidth: .infinity, minHeight: 48).foregroundStyle(IslandUI.navy)
                                    .background(id == "back" ? IslandUI.lime : .white.opacity(0.85), in: Capsule())
                                    .overlay(Capsule().strokeBorder(IslandUI.navy.opacity(menu.isFocused(id) ? 1 : 0), lineWidth: 2))
                            }.buttonStyle(.plain).accessibilityLabel(label)
                        }
                    }
                }.frame(width: compact ? nil : 640)
                VStack(spacing: 4) {
                    IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0))
                    Text(menu.player?.name ?? "Player 1").font(IslandUI.font(20, bold: true))
                    Text("Drag to rotate").font(IslandUI.font(14)).foregroundStyle(IslandUI.muted)
                }.foregroundStyle(IslandUI.navy).frame(maxWidth: .infinity).frame(height: compact ? 230 : nil)
            }
        }
    }
}

struct IslandSettingsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Settings", compact: compact) {
            VStack(alignment: .leading, spacing: 24) {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 12) {
                        ForEach(SettingsTab.allCases, id: \.self) { tab in
                            Button { menu.tap("tab-\(tab.rawValue)") } label: {
                                Text(tab.title).font(IslandUI.font(compact ? 16 : 19, bold: true)).foregroundStyle(IslandUI.navy)
                                    .padding(.horizontal, 14).padding(.vertical, 12)
                                    .background(menu.settingsTab == tab ? IslandUI.lime : .white.opacity(0.8), in: Capsule())
                                    .overlay(Capsule().strokeBorder(menu.isFocused("tab-\(tab.rawValue)") ? IslandUI.navy : .clear, lineWidth: 2))
                            }.buttonStyle(.plain)
                        }
                    }
                }
                ScrollView {
                    VStack(spacing: 12) {
                        ForEach(TennisMenu.settingsRows(menu.settingsTab), id: \.self) { id in
                            let item = SettingsScreen.describe(id, .shared, menu.player)
                            IslandSelector(label: item.0, value: item.1, focused: menu.isFocused(id), compact: compact) { delta in
                                if !menu.adjust(id, by: delta) { menu.tap(id) }
                            }
                        }
                        if !menu.notice.isEmpty { Text(menu.notice).font(IslandUI.font(16)).foregroundStyle(IslandUI.navy) }
                    }
                }.frame(maxWidth: compact ? .infinity : 720)
                IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 180)
            }
        }
    }
}

struct IslandGuideScreen: View {
    let menu: TennisMenu
    let compact: Bool
    let golf: Bool
    var body: some View {
        let cards = golf ? GolfLesson.cards : HowTo.pages
        let index = min(golf ? menu.lessonCard : menu.howToPage, cards.count - 1)
        let card = cards[index]
        IslandShell(title: golf ? "Golf Lesson" : "How to Play", compact: compact) {
            VStack(alignment: .leading, spacing: 24) {
                ScrollView {
                    VStack(alignment: .leading, spacing: 20) {
                        Text("\(index + 1) / \(cards.count)").font(IslandUI.font(17)).foregroundStyle(IslandUI.muted)
                        Text(card.title).font(IslandUI.font(compact ? 27 : 34, bold: true))
                        ForEach(Array(card.steps.enumerated()), id: \.offset) { i, text in
                            HStack(alignment: .top, spacing: 16) {
                                Text("\(i + 1)").font(IslandUI.font(20, bold: true)).frame(width: 34, height: 34).background(IslandUI.lime, in: Circle())
                                Text(text).font(IslandUI.font(compact ? 18 : 23)).fixedSize(horizontal: false, vertical: true)
                            }
                        }
                    }.foregroundStyle(IslandUI.navy).padding(28).frame(maxWidth: compact ? .infinity : 800, alignment: .leading)
                        .background(.white.opacity(0.92), in: RoundedRectangle(cornerRadius: 20))
                }
                HStack {
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }
                    if index > 0 { IslandAction(title: "Previous", focused: menu.isFocused("prev"), compact: compact) { menu.tap("prev") } }
                    let id = golf ? "nextCard" : "nextPage"
                    if index < cards.count - 1 || golf {
                        IslandAction(title: golf && index == cards.count - 1 ? "Practice" : "Next", focused: menu.isFocused(id), primary: true, compact: compact) { menu.tap(id) }
                    }
                }.frame(maxWidth: 780)
            }
        }
    }
}

struct IslandConnectScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Connect to TV", compact: compact) {
            ScrollView {
                VStack(alignment: .leading, spacing: 24) {
                    ForEach(Array(["Open Screen Mirroring in Control Centre.", "Choose your TV or Mac.", "Use your phone to play."].enumerated()), id: \.offset) { i, text in
                        HStack(spacing: 18) {
                            Text("\(i + 1)").font(IslandUI.font(23, bold: true)).frame(width: 42, height: 42).background(.white.opacity(0.85), in: Circle())
                            Text(text).font(IslandUI.font(compact ? 20 : 26, bold: true))
                        }
                    }
                    Text("On a Mac, enable AirPlay Receiver in System Settings → General → AirDrop & Handoff. Use the same Wi-Fi network.")
                        .font(IslandUI.font(17)).frame(maxWidth: 580, alignment: .leading)
                    Text("Waiting for a screen…").font(IslandUI.font(17))
                    IslandAction(title: "Play on this phone", focused: menu.isFocused("phone"), primary: true, compact: compact) { menu.tap("phone") }.frame(maxWidth: 340)
                    IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 170)
                }.foregroundStyle(IslandUI.navy).padding(24).background(.white.opacity(0.84), in: RoundedRectangle(cornerRadius: 20))
            }
        }
    }
}

struct IslandResultsScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var postMatch = false
    var body: some View {
        let won = postMatch ? menu.postMatch?.won ?? false : menu.result?.won ?? false
        let score = postMatch ? menu.postMatch?.score ?? "" : menu.result?.score ?? ""
        IslandShell(title: won ? "Match Won" : "Match Lost", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(spacing: 60))
            layout {
                VStack(alignment: .leading, spacing: 18) {
                    Text(score).font(IslandUI.font(compact ? 38 : 56, bold: true)).foregroundStyle(IslandUI.navy)
                    if let summary = menu.postMatch, postMatch {
                        Text("\(summary.opponent) · +\(summary.total) XP").font(IslandUI.font(18)).foregroundStyle(IslandUI.muted)
                        Text("\(summary.stats.aces) aces   ·   \(summary.stats.winners) winners").font(IslandUI.font(16)).foregroundStyle(IslandUI.muted)
                    }
                    let ids = postMatch ? menu.postMatchChoices : menu.rows(.results).flatMap { $0 }
                    ForEach(Array(ids.enumerated()), id: \.element) { i, id in
                        IslandAction(title: label(id), focused: menu.isFocused(id), primary: i == 0, compact: compact, identifier: id) { menu.tap(id) }
                    }
                    Spacer(minLength: 0)
                }.frame(width: compact ? nil : 390)
                IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0)).frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }.onAppear { if postMatch { menu.finishPostMatchReveal(immediate: true) } }
    }
    private func label(_ id: String) -> String {
        switch id {
        case "pm-next": menu.launch?.mode == .campaign ? "Next Round" : "Play Again"
        case "continue": "Next Round"
        case "pm-replay", "retry": "Rematch"
        case "pm-court": "Change Court"
        case "restart": "New Tournament"
        default: "Main Menu"
        }
    }
}

struct IslandCourtScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Choose a court", compact: compact) {
            VStack(alignment: .leading, spacing: 20) {
                ScrollView(compact ? .vertical : .horizontal) {
                    let layout = compact ? AnyLayout(VStackLayout(spacing: 18)) : AnyLayout(HStackLayout(spacing: 24))
                    layout {
                        ForEach(TennisVenueChoice.allCases) { venue in
                            let id = "map-\(venue.rawValue)"
                            Button { menu.tap(id) } label: {
                                VStack(alignment: .leading, spacing: 14) {
                                    Image(uiImage: UIImage(named: "\(venue.art).jpg") ?? UIImage()).resizable().scaledToFill()
                                        .frame(width: compact ? 300 : 366, height: compact ? 150 : 265).clipped()
                                    Text(venue.title).font(IslandUI.font(24, bold: true)).padding(.horizontal, 18)
                                    Text(SportsSession.shared.tennisVenue == venue.rawValue ? "Selected court" : "Select court")
                                        .font(IslandUI.font(16)).padding(.horizontal, 18).padding(.bottom, 18)
                                }.foregroundStyle(IslandUI.navy)
                                    .background(menu.isFocused(id) ? IslandUI.lime : .white, in: RoundedRectangle(cornerRadius: 18))
                                    .clipShape(RoundedRectangle(cornerRadius: 18))
                            }.buttonStyle(.plain).accessibilityLabel("Play at \(venue.title)")
                        }
                    }.padding(3)
                }
                IslandAction(title: "Back", focused: menu.isFocused("back"), compact: compact) { menu.tap("back") }.frame(width: 170)
            }
        }
    }
}

struct IslandStoryScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: "Campaign", compact: compact) {
            VStack(alignment: .leading, spacing: 24) {
                if let line = menu.storyLine {
                    Text(TennisStory.name(for: line.speaker)).font(IslandUI.font(26, bold: true))
                    Text(line.text).font(IslandUI.font(compact ? 22 : 30)).fixedSize(horizontal: false, vertical: true)
                        .padding(28).frame(maxWidth: 760, alignment: .leading)
                        .background(.white.opacity(0.94), in: RoundedRectangle(cornerRadius: 20))
                }
                Spacer()
                HStack {
                    IslandAction(title: "Continue", focused: menu.isFocused("next"), primary: true, compact: compact) { menu.advanceStory() }.frame(maxWidth: 320)
                    IslandAction(title: "Skip", focused: menu.isFocused("skip"), compact: compact) { menu.tap("skip") }.frame(maxWidth: 180)
                }
            }.foregroundStyle(IslandUI.navy)
        }
    }
}

struct IslandPauseScreen: View {
    var compact: Bool
    var dismiss: () -> Void = {}
    @State private var options = false
    private var session: SportsSession { .shared }
    var body: some View {
        IslandShell(title: "Paused", compact: compact) {
            VStack(alignment: .leading, spacing: 16) {
                IslandAction(title: "Resume", primary: true, compact: compact) { session.readyToPlay(); dismiss() }
                IslandAction(title: "Settings", compact: compact) { options.toggle() }
                if options {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 12) {
                            Toggle("Sound", isOn: Binding(get:{session.sound},set:{session.sound=$0}))
                            if !session.touch {
                                Button("Re-aim at the TV") { session.menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true; session.beginAxisCapture(); dismiss() }
                                Button("Flip left / right") { session.flipSteering() }
                            }
                            if session.displayConnected && session.sport == "tennis" {
                                Button("Re-check swing timing") { session.menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true; session.recheckTiming(); dismiss() }
                            }
                            Button(session.touch ? "Use motion controls" : "Use touch controls") { if session.touch { session.useMotion() } else { session.useTouch() }; dismiss() }
                        }.font(IslandUI.font(18)).padding(20).background(.white.opacity(0.95),in:RoundedRectangle(cornerRadius:16))
                    }
                }
                IslandAction(title: "Main Menu", compact: compact) { TennisMenu.shared.finishMatch(.menu); dismiss() }
                Spacer(minLength: 0)
                if !compact { Text("Use your phone to continue.").font(IslandUI.font(17)) }
            }.foregroundStyle(IslandUI.navy).frame(maxWidth:compact ? .infinity : 400,alignment:.leading)
        }
    }
}
