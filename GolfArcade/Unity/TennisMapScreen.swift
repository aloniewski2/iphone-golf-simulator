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
        case .resort: "A bright tropical bay, lush gardens and stands full of fans"
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



/// Keys match Unity's Course.All(); every listed course has modelled playable holes.
enum GolfCourseChoice: String, CaseIterable {
    case cliffside, postcards, wildisles, magma
    var title: String {
        switch self { case .cliffside: "Cliffside"; case .postcards: "Postcards"; case .wildisles: "Wild Isles"; case .magma: "Magma Open" }
    }
    var detail: String {
        switch self {
        case .cliffside: "Ocean cliffs and island carries"
        case .postcards: "Needle, The Steps and Volcano Rim"
        case .wildisles: "A tour through the wild islands"
        case .magma: "Lava hazards and volcanic greens"
        }
    }
    var art: String { "map-golf-\(rawValue)" }
}
struct SportMapChoice: Identifiable {
    let id: String
    let title: String
    let detail: String
    let art: String
    static func choices(for sport: Sport) -> [Self] {
        if sport == .golf {
            return GolfCourseChoice.allCases.map { Self(id: $0.rawValue, title: $0.title, detail: $0.detail, art: $0.art) }
        }
        return TennisVenueChoice.allCases.map { Self(id: $0.rawValue, title: $0.title, detail: $0.tagline, art: $0.art) }
    }
}
