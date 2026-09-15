import Foundation

enum GolfClub: String, CaseIterable, Identifiable, Sendable {
    case driver
    case iron
    case wedge
    case putter

    var id: Self { self }
    var displayName: String { rawValue.capitalized }

    var symbol: String {
        switch self {
        case .driver: "figure.golf"
        case .iron: "scope"
        case .wedge: "mountain.2.fill"
        case .putter: "flag.pattern.checkered"
        }
    }

    var loftDegrees: Double {
        switch self {
        case .driver: 11
        case .iron: 30
        case .wedge: 52
        case .putter: 3
        }
    }

    var speedMultiplier: Double {
        switch self {
        case .driver: 1.0
        case .iron: 0.78
        case .wedge: 0.58
        case .putter: 0.10
        }
    }

    var smashFactor: Double {
        switch self {
        case .driver: 1.46
        case .iron: 1.33
        case .wedge: 1.18
        case .putter: 1.0
        }
    }
}

