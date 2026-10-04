import SwiftUI

/// The courts a tennis match can be played on. Raw values are Unity's `TennisVenue` keys (the
/// `venue` field of the start message); the picture is MenuArt/map-<key>.jpg, an in-game shot.
enum TennisVenueChoice: String, CaseIterable, Identifiable {
    case resort, skyscraper, volcano
    var id: String { rawValue }
    var title: String {
        switch self { case .resort: "Tropical Resort"; case .skyscraper: "Sky Tower"; case .volcano: "Magma Crater" }
    }
    var tagline: String {
        switch self {
        case .resort: "The original: sunset over the bay, stands full of fans"
        case .skyscraper: "A rooftop above the clouds. Hit it wide and the ball falls forever"
        case .volcano: "A court floating over a lava lake. Don't miss long"
        }
    }
    var badge: String {
        switch self { case .resort: "CLASSIC"; case .skyscraper: "OPEN EDGE"; case .volcano: "OVER LAVA" }
    }
    var tint: Color {
        switch self { case .resort: Club.sky; case .skyscraper: Club.violet; case .volcano: Club.coral }
    }
    /// The image file, without extension.
    var art: String { "map-\(rawValue)" }
}


