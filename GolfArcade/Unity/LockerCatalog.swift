import SwiftUI

/// What a player can equip, per sport. Gear is data: the locker shelf draws `LockerCatalog.items(...)` and a `Player`
/// stores only item ids (`Player.loadout`). Today the catalog holds the Standard item of every slot and nothing else —
/// the shelf, the slots and the save format exist, but no gear ships yet. Adding gear later means adding rows here
/// (and the matching asset on the Unity side); no screen or save format changes.
enum LockerSlot: String, CaseIterable, Identifiable, Codable, Sendable {
    /// A whole-character costume (the kit). Not the skin tone — that is a colour on the Customize tab.
    case skin
    case racket, club
    case shoes
    var id: String { rawValue }
    func title(for sport: Sport) -> String {
        switch self {
        case .skin: "Skin"
        case .racket: "Racket"
        case .club: "Club"
        case .shoes: "Shoes"
        }
    }
    var icon: String {
        switch self {
        case .skin: "tshirt.fill"
        case .racket: "tennis.racket"
        case .club: "figure.golf"
        case .shoes: "shoeprints.fill"
        }
    }
    /// The continuous-colour key (`Player.look`) an equipped item of this slot is tinted with; nil = colours are edited elsewhere.
    var colourSlot: String? {
        switch self {
        case .skin: "shirt"
        case .racket, .club: "racket"
        case .shoes: "accent"
        }
    }
}

struct LockerItem: Identifiable, Equatable, Sendable {
    let id: String
    let slot: LockerSlot
    let name: String
    /// Sports that can equip it.
    let sports: Set<Sport>
    /// Whether the locker offers a colour row for it.
    let tintable: Bool
    var isStandard: Bool { id == LockerCatalog.standardID }
}

enum LockerCatalog {
    static let standardID = "standard"
    static let golfKitID = "golf-classic-kit"
    static let golfShoesID = "golf-spikeless"
    static func defaultID(sport: Sport, slot: LockerSlot) -> String {
        guard sport == .golf else { return standardID }
        switch slot { case .skin: return golfKitID; case .shoes: return golfShoesID; default: return standardID }
    }
    /// Sports with a locker (and a gear shelf).
    static let sports: [Sport] = [.tennis, .golf]

    /// Slots a sport shows, in shelf order.
    static func slots(for sport: Sport) -> [LockerSlot] {
        sport == .golf ? [.skin, .club, .shoes] : [.skin, .racket, .shoes]
    }

    /// Every item the game knows. One Standard item per slot, nothing else yet.
    static let all: [LockerItem] = [
        LockerItem(id: standardID, slot: .skin, name: "Standard", sports: [.tennis], tintable: false),
        LockerItem(id: golfKitID, slot: .skin, name: "Golf Polo", sports: [.golf], tintable: true),
        LockerItem(id: standardID, slot: .racket, name: "Standard", sports: [.tennis], tintable: true),
        LockerItem(id: standardID, slot: .club, name: "Standard", sports: [.golf], tintable: true),
        LockerItem(id: standardID, slot: .shoes, name: "Standard", sports: [.tennis], tintable: true),
        LockerItem(id: golfShoesID, slot: .shoes, name: "Spikeless", sports: [.golf], tintable: true),
    ]

    static func items(sport: Sport, slot: LockerSlot) -> [LockerItem] {
        all.filter { $0.slot == slot && $0.sports.contains(sport) }
    }

    /// The item for an id, or the slot's Standard item when the catalog no longer has it (saves outlive catalog changes).
    static func item(id: String?, sport: Sport, slot: LockerSlot) -> LockerItem {
        let pool = items(sport: sport, slot: slot)
        let fallback = defaultID(sport: sport, slot: slot)
        let resolved = id == nil || id == standardID ? fallback : id
        return pool.first { $0.id == resolved } ?? pool.first { $0.id == fallback } ?? LockerItem(id: fallback, slot: slot, name: "Standard", sports: [sport], tintable: false)
    }
}

extension Player {
    var multiplayerLoadout: MultiplayerLoadout {
        MultiplayerLoadout(gear: Dictionary(uniqueKeysWithValues: LockerCatalog.sports.map { ($0.rawValue, loadoutPayload(sport: $0)) }),
                           skinHex: skinHex, colours: Dictionary(uniqueKeysWithValues: Player.outfitSlots.compactMap { slot in outfitHex(slot).map { (slot, $0) } }))
    }
    func equipped(_ slot: LockerSlot, sport: Sport) -> LockerItem {
        LockerCatalog.item(id: loadout?[sport.rawValue]?[slot.rawValue], sport: sport, slot: slot)
    }
    mutating func equip(_ item: LockerItem, sport: Sport) {
        var all = loadout ?? [:]
        var forSport = all[sport.rawValue] ?? [:]
        if item.isStandard { forSport[item.slot.rawValue] = nil } else { forSport[item.slot.rawValue] = item.id }
        all[sport.rawValue] = forSport.isEmpty ? nil : forSport
        loadout = all.isEmpty ? nil : all
    }
    /// What the launch message carries for a sport: slot -> item id for every slot (Standard included), so the game never guesses.
    func loadoutPayload(sport: Sport) -> [String: String] {
        Dictionary(uniqueKeysWithValues: LockerCatalog.slots(for: sport).map { ($0.rawValue, equipped($0, sport: sport).id) })
    }
}

extension MultiplayerParticipant {
    /// A transient locker profile for the preview; each peer supplies cosmetic values only.
    var lobbyPlayer: Player {
        var p = Player(id:UUID(uuidString:id) ?? UUID(uuidString:"00000000-0000-0000-0000-000000000000")!,name:name,colorIndex:0,handedness:left ? .left : .right)
        p.standardFemale = female
        if let loadout {
            p.loadout = loadout.gear
            let skin = LockerColor.rgb(loadout.skinHex)
            p.look["skin"] = [0,skin.0,skin.1,skin.2]
            for (slot,hex) in loadout.colours { p.setOutfitHex(slot,hex) }
        }
        return p
    }
}
