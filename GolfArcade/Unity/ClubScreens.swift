import SwiftUI

// Every menu screen on the Motion Club system (ClubDesign.swift). Each area has its own
// painted scene; all share the header, cards, focus, entrances and hint bar. TV layouts are for
// the 1280×720 canvas (fitted to 16:10 Macs); `compact` is the phone held upright.

extension TennisMenu {
    var player: Player? {
        let s = SportsSession.shared
        return s.players.indices.contains(s.playerIndex) ? s.players[s.playerIndex] : nil
    }
}

// MARK: - Title


// MARK: - Home


// MARK: - Game select


// MARK: - Sport hub


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
                    ClubCard(art: "golf", title: "Golf", subtitle: "Choose your course", tint: Club.green,
                             focused: menu.isFocused("quickGolf"), compact: compact) { menu.tap("quickGolf") }.clubEntrance(1)
                }.frame(height: compact ? 220 : 320)
                ClubButton(title: "Back", icon: "", focused: menu.isFocused("back"), style: .quiet, size: 20) { menu.tap("back") }
            }
        }
    }
}

// MARK: - Adventure ladder and quick match


// MARK: - Training


// MARK: - Settings


// MARK: - How to play and the golf lesson


// MARK: - Connect a screen


// MARK: - Story and results


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
    var body: some View { MotionClubHome(menu: menu, compact: compact) }
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
                    Text("A connected screen is required to play. Your phone stays the controller.").font(IslandUI.font(17, bold: true))
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
        IslandShell(title: "Centre Court · Results", compact: compact) {
            let layout = compact ? AnyLayout(VStackLayout(spacing: 12)) : AnyLayout(HStackLayout(spacing: 40))
            layout {
                VStack(alignment: .leading, spacing: 20) {
                    ScrollView {
                        ClubVictorySummary(won: won, score: score,
                            summary: menu.postMatch.flatMap { $0.score == score && $0.won == won ? $0 : nil }, compact: compact)
                            .padding(compact ? 0 : 20)
                            .background(IslandUI.paper.opacity(compact ? 0 : 0.94), in: RoundedRectangle(cornerRadius: 24))
                    }.scrollBounceBehavior(.basedOnSize)
                    let ids = postMatch ? menu.postMatchChoices : menu.rows(.results).flatMap { $0 }
                    let buttons = compact ? AnyLayout(VStackLayout(spacing: 8)) : AnyLayout(HStackLayout(spacing: 10))
                    buttons {
                        ForEach(Array(ids.enumerated()), id: \.element) { i, id in
                            IslandAction(title: label(id), focused: menu.isFocused(id), primary: i == 0, compact: compact, identifier: id) { menu.tap(id) }
                        }
                    }
                }.frame(maxWidth: compact ? .infinity : 660)
                if !compact {
                    IslandPlayer(player: menu.player ?? Player(name: "Player 1", colorIndex: 0))
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
        }.onAppear { if postMatch { menu.finishPostMatchReveal(immediate: true) } }
    }
    private func label(_ id: String) -> String {
        switch id {
        case "pm-next": menu.launch?.mode == .campaign ? "Next Round" : "Play Again"
        case "continue": "Next Round"
        case "pm-replay", "retry": "Rematch"
        case "pm-court", "court": menu.launch?.sport == .golf ? "Change Course" : "Change Court"
        case "restart": "New Tournament"
        default: "Main Menu"
        }
    }
}

struct IslandCourtScreen: View {
    let menu: TennisMenu
    let compact: Bool
    var body: some View {
        IslandShell(title: menu.mapSport == .golf ? "Choose a course" : "Choose a court", compact: compact) {
            VStack(alignment: .leading, spacing: 20) {
                if !menu.notice.isEmpty { IslandMenuNotice(menu: menu, compact: compact) }
                ScrollViewReader { proxy in
                ScrollView(compact ? .vertical : .horizontal) {
                    let layout = compact ? AnyLayout(VStackLayout(spacing: 18)) : AnyLayout(HStackLayout(spacing: 24))
                    layout {
                        ForEach(menu.mapChoices) { venue in
                            let id = "map-\(venue.id)"
                            Button { menu.tap(id) } label: {
                                VStack(alignment: .leading, spacing: 14) {
                                    Image(uiImage: UIImage(named: "\(venue.art).jpg") ?? UIImage(named: "\(venue.art).png") ?? UIImage()).resizable().scaledToFill()
                                        .frame(width: compact ? 300 : 366, height: compact ? 150 : 265).clipped()
                                    Text(venue.title).font(IslandUI.font(24, bold: true)).padding(.horizontal, 18)
                                    Text(venue.detail)
                                        .font(IslandUI.font(16)).padding(.horizontal, 18).padding(.bottom, 18)
                                }.foregroundStyle(IslandUI.navy)
                                    .background(menu.isFocused(id) ? IslandUI.lime : .white, in: RoundedRectangle(cornerRadius: 18))
                                    .clipShape(RoundedRectangle(cornerRadius: 18))
                            }.buttonStyle(.plain).accessibilityLabel("Play at \(venue.title)").accessibilityIdentifier(id).id(id)
                        }
                    }.padding(3)
                }
                .onChange(of: menu.focused) { _, id in
                    if !compact { withAnimation(.easeOut(duration: 0.2)) { proxy.scrollTo(id, anchor: .center) } }
                }
                .onAppear { if !compact { proxy.scrollTo(menu.focused, anchor: .center) } }
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
    @State private var leaving = false
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
                            if session.sport == "tennis" {
                                Divider()
                                Text("Recording").font(.headline)
                                PointRecordingSettings(session: session)
                                Divider()
                            }
                            if !session.touch {
                                Button("Re-aim at the TV") { session.menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true; session.beginAxisCapture(); dismiss() }
                                if session.sport == "tennis" {
                                    Button("Recalibrate aiming") { session.recalibrateAiming(); dismiss() }
                                        .disabled(!session.motion.axisLocked || session.checkingTiming || session.measuringDelay)
                                }
                                Button("Flip left / right") { session.flipSteering() }
                            }
                            if session.displayConnected && session.sport == "tennis" {
                                Button("Re-check swing timing") { session.menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true; session.recheckTiming(); dismiss() }
                            }
                            Button(session.touch ? "Use motion controls" : "Use touch controls") { if session.touch { session.useMotion() } else { session.useTouch() }; dismiss() }
                        }.font(IslandUI.font(18)).padding(20).background(.white.opacity(0.95),in:RoundedRectangle(cornerRadius:16))
                    }
                }
                if leaving {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Leave this match?").font(IslandUI.font(20, bold: true))
                        Text("Your score in this match will be lost.").font(IslandUI.font(15)).foregroundStyle(IslandUI.muted)
                        HStack(spacing: 10) {
                            IslandAction(title: "Keep playing", primary: true, compact: compact) { leaving = false }
                            IslandAction(title: "Leave", compact: compact) { TennisMenu.shared.finishMatch(.menu); dismiss() }
                        }
                    }.padding(16).background(.white.opacity(0.95), in: RoundedRectangle(cornerRadius: 16))
                } else {
                    IslandAction(title: "Main Menu", compact: compact) { leaving = true }
                }
                Spacer(minLength: 0)
                if !compact { Text("Use your phone to continue.").font(IslandUI.font(17)) }
            }.foregroundStyle(IslandUI.navy).frame(maxWidth:compact ? .infinity : 400,alignment:.leading)
        }
    }
}
