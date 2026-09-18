import Foundation

/// Saved cosmetic choices. Never used by tracking, assistance, handedness or shot physics.
struct GolferAppearance: Codable, Equatable, Sendable {
    enum Preset: String, CaseIterable, Codable, Identifiable, Sendable {
        case cove, dune, orchard, sunset
        var id: Self { self }
        var title: String { rawValue.capitalized }
    }
    enum Skin: String, CaseIterable, Codable, Identifiable, Sendable {
        case porcelain, warm, tan, bronze, brown, deep
        var id: Self { self }; var title: String { rawValue.capitalized }
        var hex: UInt32 {
            switch self { case .porcelain: 0xE9C6AC; case .warm: 0xDFA97C; case .tan: 0xC58D61
            case .bronze: 0xA66C47; case .brown: 0x7A4B34; case .deep: 0x503125 }
        }
    }
    enum Hair: String, CaseIterable, Codable, Identifiable, Sendable {
        case espresso, chestnut, auburn, sand, silver
        var id: Self { self }; var title: String { rawValue.capitalized }
        var hex: UInt32 {
            switch self { case .espresso: 0x282321; case .chestnut: 0x573824; case .auburn: 0x854529
            case .sand: 0xBA9256; case .silver: 0xB9BDBE }
        }
    }
    enum Outfit: String, CaseIterable, Codable, Identifiable, Sendable {
        case lagoon, coral, fern, lilac, navy, ivory
        var id: Self { self }; var title: String { rawValue.capitalized }
        var hex: UInt32 {
            switch self { case .lagoon: 0x207F82; case .coral: 0xD76D55; case .fern: 0x658648
            case .lilac: 0x8B77B0; case .navy: 0x304A6A; case .ivory: 0xEAE5D2 }
        }
    }
    enum Headwear: String, CaseIterable, Codable, Identifiable, Sendable {
        case cap, visor
        var id: Self { self }; var title: String { rawValue.capitalized }
    }
    var preset: Preset
    var skin: Skin
    var hair: Hair
    var outfit: Outfit
    var headwear: Headwear
    var trousersHex: UInt32 { preset == .dune || preset == .sunset ? 0xD0C8AE : 0x273C50 }
    var accentHex: UInt32 { preset == .orchard ? 0xD8B767 : preset == .sunset ? 0xCB7154 : 0xF2EFE1 }

    static func preset(_ value: Preset) -> Self {
        switch value {
        case .cove: .init(preset:value,skin:.tan,hair:.espresso,outfit:.lagoon,headwear:.cap)
        case .dune: .init(preset:value,skin:.brown,hair:.chestnut,outfit:.coral,headwear:.visor)
        case .orchard: .init(preset:value,skin:.warm,hair:.auburn,outfit:.fern,headwear:.cap)
        case .sunset: .init(preset:value,skin:.deep,hair:.silver,outfit:.lilac,headwear:.visor)
        }
    }
    static func forPlayerColor(_ index: Int) -> Self {
        preset(Preset.allCases[max(0,index) % Preset.allCases.count])
    }
}
