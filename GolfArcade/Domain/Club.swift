import Foundation

enum GolfClub: String, CaseIterable, Identifiable, Codable, Sendable {
    case driver
    case wood3
    case iron5
    case iron
    case iron9
    case wedge
    case putter

    var id: Self { self }
    var displayName: String {
        switch self {
        case .driver: "Driver"
        case .wood3: "3-wood"
        case .iron5: "5-iron"
        case .iron: "7-iron"
        case .iron9: "9-iron"
        case .wedge: "Sand wedge"
        case .putter: "Putter"
        }
    }

    var symbol: String {
        switch self {
        case .driver, .wood3: "figure.golf"
        case .iron5, .iron, .iron9: "scope"
        case .wedge: "mountain.2.fill"
        case .putter: "flag.pattern.checkered"
        }
    }

    var loftDegrees: Double {
        switch self {
        case .driver: 11
        case .wood3: 15
        case .iron5: 25
        case .iron: 30
        case .iron9: 42
        case .wedge: 52
        case .putter: 3
        }
    }

    var speedMultiplier: Double {
        switch self {
        case .driver: 1.0
        case .wood3: 0.92
        case .iron5: 0.84
        case .iron: 0.78
        case .iron9: 0.68
        case .wedge: 0.58
        case .putter: 0.10
        }
    }

    var smashFactor: Double {
        switch self {
        case .driver: 1.46
        case .wood3: 1.44
        case .iron5: 1.38
        case .iron: 1.33
        case .iron9: 1.25
        case .wedge: 1.18
        case .putter: 1.0
        }
    }
}
