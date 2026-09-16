import SwiftUI

/// Floating rail on the right edge: settings menu, clubs, and aim.
struct ClubRail<MenuContent: View>: View {
    @ObservedObject var round: CourseRound
    /// Club highlighted for gesture navigation (swipe up/down), or nil when clubs are locked.
    let focusedClub: GolfClub?
    @ViewBuilder let menu: () -> MenuContent

    var body: some View {
        VStack(spacing: 7) {
            Menu { menu() } label: {
                Image(systemName: "ellipsis").font(.headline).frame(width: 45, height: 38)
            }
            .accessibilityLabel("Course menu")
            .accessibilityIdentifier("courseMenu")

            Divider().overlay(.white.opacity(0.18))

            ForEach(GolfClub.allCases) { club in
                Button { round.club = club } label: {
                    VStack(spacing: 3) {
                        Image(systemName: club.symbol).font(.system(size: 16, weight: .bold))
                        Text(club.shortName).font(.system(size: 9, weight: .black, design: .rounded))
                    }
                    .frame(width: 45, height: 46)
                    .background(round.club == club ? Palette.cream : .white.opacity(0.08), in: RoundedRectangle(cornerRadius: 11))
                    .foregroundStyle(round.club == club ? Palette.ink : Palette.cream)
                }
                .disabled(round.phase != .ready)
                .accessibilityLabel("\(club.displayName), \(Int(club.mockDistance)) yards")
                .accessibilityAddTraits(round.club == club ? .isSelected : [])
                .accessibilityIdentifier("club-\(club.rawValue)")
            }

            Divider().overlay(.white.opacity(0.18))

            HStack(spacing: 0) {
                Button { round.aim = max(-22, round.aim - 2) } label: {
                    Image(systemName: "chevron.left").frame(width: 22, height: 32)
                }
                .accessibilityLabel("Aim left")
                Button { round.aim = min(22, round.aim + 2) } label: {
                    Image(systemName: "chevron.right").frame(width: 22, height: 32)
                }
                .accessibilityLabel("Aim right")
            }
            .font(.subheadline.bold())
            .disabled(round.phase != .ready)
            Text(round.aim == 0 ? "PIN" : String(format: "%+.0f°", round.aim))
                .font(.system(size: 9, weight: .black, design: .rounded)).monospacedDigit()
                .opacity(0.8)
        }
        .frame(width: 59)
        .padding(7)
        .background(.ultraThinMaterial.opacity(0.9), in: RoundedRectangle(cornerRadius: 18))
        .background(.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 18))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(.white.opacity(0.12)))
        .environment(\.colorScheme, .dark)
    }
}
