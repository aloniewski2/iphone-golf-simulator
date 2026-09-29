import SwiftUI

/// After a match: the result, then "rep" in the style of a basketball sim's post-game: each
/// reason you earned XP drops in and counts up, the level bar fills, and a level-up gets its own
/// celebration. Any button (A / tap) skips the show; then Next match / Replay / Menu appear.
struct ClubPostMatchScreen: View {
    let menu: TennisMenu
    let compact: Bool
    @State private var headerIn = false
    @State private var shown = 0          // XP lines revealed so far
    @State private var running = 0        // XP total shown
    @State private var level = 1
    @State private var xp = 0             // into the current level
    @State private var levelUp: Int?
    @State private var finished = false
    @State private var skipped = false
    @State private var started = false

    var body: some View {
        let summary = menu.postMatch
        ZStack {
            ClubBackdrop(scene: (summary?.won ?? true) ? "stands" : "court", tint: (summary?.won ?? true) ? 0.3 : 0.55)
            if let summary {
                content(summary)
                if let n = levelUp { LevelUpOverlay(level: n, compact: compact).transition(.opacity) }
            }
        }
        .contentShape(Rectangle())
        .onTapGesture { if !finished { menu.tap("pm-skip") } }
        .task {
            guard !started, let summary = menu.postMatch else { return }
            started = true
            await run(summary)
        }
        .onChange(of: menu.postMatchSkips) { _, _ in if let summary = menu.postMatch { skip(summary) } }
    }

    // MARK: Layout

    @ViewBuilder private func content(_ s: PostMatchSummary) -> some View {
        if compact {
            ScrollView {
                VStack(spacing: 16) { banner(s); chips(s); xpPanel(s); buttons(s) }.padding(18)
            }
        } else {
            HStack(alignment: .center, spacing: 36) {
                VStack(spacing: 20) { banner(s); chips(s) }.frame(width: 470)
                VStack(spacing: 18) { xpPanel(s); buttons(s) }.frame(maxWidth: .infinity)
            }
            .padding(.horizontal, 56).padding(.vertical, 36)
        }
    }

    private func banner(_ s: PostMatchSummary) -> some View {
        VStack(spacing: compact ? 6 : 10) {
            ClubArt(name: s.won ? "trophy" : "whistle", fallback: s.won ? "trophy.fill" : "flag.fill")
                .frame(width: compact ? 110 : 190, height: compact ? 110 : 190)
                .scaleEffect(headerIn ? 1 : 0.3).rotationEffect(.degrees(headerIn ? 0 : -25))
                .shadow(color: (s.won ? Club.sun : Club.sky).opacity(0.6), radius: 24)
            Text(s.won ? "VICTORY!" : "SO CLOSE").font(Club.display(compact ? 54 : 88))
                .foregroundStyle(s.won ? Club.sun : .white).shadow(color: .black.opacity(0.4), radius: 0, x: 0, y: 5)
                .scaleEffect(headerIn ? 1 : 1.7).opacity(headerIn ? 1 : 0)
            Text(s.score.isEmpty ? " " : s.score).font(Club.title(compact ? 26 : 40)).foregroundStyle(.white)
                .opacity(headerIn ? 1 : 0)
            if !s.opponent.isEmpty {
                Text(s.won ? "You beat \(s.opponent)" : "\(s.opponent) takes this one").font(Club.ui(compact ? 15 : 19, 600))
                    .foregroundStyle(.white.opacity(0.85)).opacity(headerIn ? 1 : 0)
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func chips(_ s: PostMatchSummary) -> some View {
        let items: [(String, String, String)] = [
            ("bolt.fill", "\(s.stats.aces)", "Aces"), ("scope", "\(s.stats.winners)", "Winners"),
            ("arrow.left.arrow.right", "\(s.stats.longest)", "Best rally"), ("timer", "\(s.stats.perfect)", "Perfect"),
            ("checkmark.circle.fill", "\(s.stats.pointsWon)", "Points won"),
        ]
        return LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 10), count: compact ? 3 : 3), spacing: 10) {
            ForEach(Array(items.enumerated()), id: \.offset) { i, item in
                VStack(spacing: 2) {
                    Image(systemName: item.0).font(.system(size: compact ? 16 : 20, weight: .heavy)).foregroundStyle(Club.sun)
                    Text(item.1).font(Club.display(compact ? 26 : 34)).foregroundStyle(.white).monospacedDigit()
                    Text(item.2.uppercased()).font(Club.caps(10)).tracking(1).foregroundStyle(.white.opacity(0.7))
                }
                .frame(maxWidth: .infinity).padding(.vertical, 10)
                .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(.white.opacity(0.12)))
                .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).strokeBorder(.white.opacity(0.2), lineWidth: 1))
                .opacity(headerIn ? 1 : 0).offset(y: headerIn ? 0 : 20)
                .animation(Club.spring.delay(0.3 + Double(i) * 0.06), value: headerIn)
            }
        }
    }

    private func xpPanel(_ s: PostMatchSummary) -> some View {
        VStack(alignment: .leading, spacing: compact ? 10 : 12) {
            HStack(alignment: .firstTextBaseline) {
                Text("MATCH REP").font(Club.caps(compact ? 13 : 16)).tracking(3).foregroundStyle(Club.sun)
                if s.practice { Text("PRACTICE · HALF XP").font(Club.caps(10)).tracking(1.2).foregroundStyle(.white.opacity(0.75)) }
                Spacer()
                Text("+\(running) XP").font(Club.display(compact ? 32 : 52)).foregroundStyle(.white).monospacedDigit().lineLimit(1).minimumScaleFactor(0.5)
                    .contentTransition(.numericText(value: Double(running)))
            }
            VStack(spacing: compact ? 5 : 6) {
                ForEach(s.lines.prefix(shown)) { line in
                    HStack {
                        Text(line.title.uppercased()).font(Club.caps(compact ? 13 : 15)).tracking(1).foregroundStyle(.white)
                        if !line.detail.isEmpty { Text(line.detail).font(Club.ui(compact ? 12 : 14, 500)).foregroundStyle(.white.opacity(0.65)) }
                        Spacer()
                        Text("+\(line.xp)").font(Club.title(compact ? 17 : 20)).foregroundStyle(Club.sun).monospacedDigit()
                    }
                    .transition(.asymmetric(insertion: .move(edge: .trailing).combined(with: .opacity), removal: .opacity))
                }
            }
            .frame(minHeight: compact ? 0 : CGFloat(min(s.lines.count, 10)) * 30, alignment: .top)
            Divider().overlay(.white.opacity(0.25))
            levelRow(s)
            if finished && !s.levelsGained.isEmpty {
                HStack(spacing: 10) {
                    Image(systemName: "gift.fill").foregroundStyle(Club.sun)
                    Text(LevelRewards.rewards(for: s.endLevel).first?.title ?? "Rewards for levelling up are coming soon")
                        .font(Club.ui(compact ? 13 : 15, 600)).foregroundStyle(.white.opacity(0.9))
                }
                .padding(.horizontal, 12).padding(.vertical, 8)
                .background(Capsule().fill(.white.opacity(0.12)))
                .transition(.scale.combined(with: .opacity))
            }
        }
        .padding(compact ? 16 : 22)
        .background(RoundedRectangle(cornerRadius: 26, style: .continuous).fill(Club.lagoonDeep.opacity(0.82)))
        .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).strokeBorder(.white.opacity(0.25), lineWidth: 2))
        .opacity(headerIn ? 1 : 0)
    }

    private func levelRow(_ s: PostMatchSummary) -> some View {
        let need = LevelCurve.needed(level)
        return HStack(spacing: 14) {
            LevelBadge(level: level, size: compact ? 54 : 70)
            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    Text(LevelCurve.rank(level).uppercased()).font(Club.caps(compact ? 12 : 14)).tracking(2).foregroundStyle(.white)
                    Spacer()
                    Text("\(xp) / \(need) XP").font(Club.ui(compact ? 12 : 14, 600)).foregroundStyle(.white.opacity(0.8)).monospacedDigit()
                }
                GeometryReader { g in
                    ZStack(alignment: .leading) {
                        Capsule().fill(.white.opacity(0.18))
                        Capsule().fill(LinearGradient(colors: [Club.sun, Club.coral], startPoint: .leading, endPoint: .trailing))
                            .frame(width: max(8, g.size.width * CGFloat(xp) / CGFloat(need)))
                            .shadow(color: Club.sun.opacity(0.7), radius: xp >= need ? 12 : 4)
                    }
                }
                .frame(height: compact ? 14 : 18)
            }
        }
    }

    private func buttons(_ s: PostMatchSummary) -> some View {
        Group {
            if finished {
                HStack(spacing: 12) {
                    ForEach(menu.postMatchChoices, id: \.self) { id in
                        let (title, icon, style): (String, String, ClubButton.Style) = switch id {
                        case "pm-next": ("Next match", "arrow.right", .primary)
                        case "pm-replay": (s.won ? "Replay" : "Rematch", "arrow.counterclockwise", .secondary)
                        case "pm-court": ("Change court", "map.fill", .secondary)
                        default: (menu.launch?.mode == .campaign ? "Adventure" : "Menu", "list.bullet", .quiet)
                        }
                        ClubButton(title: title, icon: icon, focused: menu.isFocused(id), style: style, size: compact ? 17 : 22) { menu.tap(id) }
                            .accessibilityIdentifier(id)
                    }
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
            } else {
                Text(compact ? "Tap to skip" : "Press  Ⓐ  to skip").font(Club.ui(compact ? 13 : 16, 600)).foregroundStyle(.white.opacity(0.65))
            }
        }
        .animation(Club.spring, value: finished)
    }

    // MARK: The show

    private func pause(_ seconds: Double) async { try? await Task.sleep(for: .seconds(seconds)) }
    private var stopped: Bool { skipped || Task.isCancelled }

    @MainActor private func run(_ s: PostMatchSummary) async {
        level = s.startLevel; xp = s.startXP; shown = 0; running = 0
        withAnimation(.spring(response: 0.5, dampingFraction: 0.58).delay(0.1)) { headerIn = true }
        ClubSound.play("whoosh", volume: 0.4)
        await pause(1.4); if stopped { return }
        for line in s.lines {
            withAnimation(.spring(response: 0.4, dampingFraction: 0.7)) { shown += 1 }
            withAnimation(.easeOut(duration: 0.4)) { running += line.xp }
            ClubSound.play("tick", volume: 0.5)
            await gain(line.xp); if stopped { return }
            await pause(0.32); if stopped { return }
        }
        await pause(0.4); if stopped { return }
        finished = true; menu.finishPostMatchReveal()
    }

    /// Move the bar by `add` XP, celebrating each level it fills.
    @MainActor private func gain(_ add: Int) async {
        var remaining = add
        while remaining > 0 {
            let need = LevelCurve.needed(level), room = need - xp
            if remaining >= room, level < LevelCurve.maxLevel {
                withAnimation(.easeOut(duration: 0.45)) { xp = need }
                await pause(0.55); if stopped { return }
                remaining -= room
                level += 1; xp = 0
                await celebrate(level); if stopped { return }
            } else {
                withAnimation(.easeOut(duration: 0.45)) { xp += remaining }
                remaining = 0
            }
        }
    }

    @MainActor private func celebrate(_ newLevel: Int) async {
        ClubSound.play("pop", volume: 0.85)
        UINotificationFeedbackGenerator().notificationOccurred(.success)
        withAnimation(.spring(response: 0.45, dampingFraction: 0.55)) { levelUp = newLevel }
        await pause(2.4)
        withAnimation(.easeOut(duration: 0.3)) { levelUp = nil }
        await pause(0.35)
    }

    private func skip(_ s: PostMatchSummary) {
        skipped = true
        withAnimation(.easeOut(duration: 0.25)) {
            headerIn = true; shown = s.lines.count; running = s.total; level = s.endLevel; xp = s.endXP; levelUp = nil; finished = true
        }
        menu.finishPostMatchReveal()
    }
}

/// A round level badge.
struct LevelBadge: View {
    let level: Int
    var size: CGFloat = 70
    var body: some View {
        ZStack {
            Circle().fill(LinearGradient(colors: [Club.sun, Club.coral], startPoint: .top, endPoint: .bottom))
            Circle().strokeBorder(.white, lineWidth: max(2, size * 0.05))
            VStack(spacing: -4) {
                Text("LV").font(Club.caps(size * 0.2)).foregroundStyle(Club.ink.opacity(0.75))
                Text("\(level)").font(Club.display(size * 0.5)).foregroundStyle(Club.ink).contentTransition(.numericText(value: Double(level)))
            }
        }
        .frame(width: size, height: size)
        .shadow(color: Club.sun.opacity(0.5), radius: size * 0.2)
    }
}

/// The level-up moment: confetti, the new level bursting in, and where the reward will go.
struct LevelUpOverlay: View {
    let level: Int
    var compact = false
    @State private var pop = false
    var body: some View {
        ZStack {
            Color.black.opacity(0.55).ignoresSafeArea()
            Confetti()
            VStack(spacing: compact ? 10 : 16) {
                Text("LEVEL UP!").font(Club.display(compact ? 56 : 96)).foregroundStyle(Club.sun)
                    .shadow(color: .black.opacity(0.45), radius: 0, x: 0, y: 6)
                    .scaleEffect(pop ? 1 : 2.2).opacity(pop ? 1 : 0)
                LevelBadge(level: level, size: compact ? 130 : 190).scaleEffect(pop ? 1 : 0.2).rotationEffect(.degrees(pop ? 0 : -40))
                Text(LevelCurve.rank(level).uppercased()).font(Club.caps(compact ? 16 : 22)).tracking(4).foregroundStyle(.white)
                let rewards = LevelRewards.rewards(for: level)
                HStack(spacing: 10) {
                    Image(systemName: "gift.fill").foregroundStyle(Club.sun)
                    Text(rewards.first?.title ?? "Rewards coming soon").font(Club.ui(compact ? 14 : 18, 600)).foregroundStyle(.white)
                }
                .padding(.horizontal, 18).padding(.vertical, 10).background(Capsule().fill(.white.opacity(0.16)))
            }
        }
        .onAppear { withAnimation(.spring(response: 0.5, dampingFraction: 0.5)) { pop = true } }
    }
}
