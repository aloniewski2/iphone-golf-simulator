import SwiftUI

/// Locker colour ranges. Every colour a player picks is a point on a continuous range, not one of a few names:
/// skin and natural hair walk a ramp of real tones; outfit pieces, the racket and hair dye are hue + shade
/// (shade -1 = near black, 0 = the vivid colour, +1 = pastel / white). The pick is stored as those numbers
/// (`Player.looks`) so the sliders come back where they were, and sent to the game as the exact hex.
enum LockerColor {
    /// Very fair to deep, the ends a little past the old six presets.
    static let skinStops = ["FFEBDA", "FFE0C4", "F2C9A5", "E6AE7E", "D19062", "B7744A", "96593A", "74432C", "55301F", "3E2317"]
    /// Natural hair: black, browns, auburn, copper, blonds, platinum, silver.
    static let hairStops = ["141010", "2B1D17", "4A2F20", "6B4128", "8E4A25", "B35E2E", "C98B4C", "DDB468", "ECD59C", "F3EBD7", "C9CDD3"]
    /// Skin / hair presets shown as quick swatches (positions on the ramps).
    static let skinPresets: [Double] = [0.05, 0.2, 0.36, 0.52, 0.68, 0.84, 0.98]
    static let hairPresets: [(String, Double)] = [("Black", 0.0), ("Dark brown", 0.14), ("Brown", 0.28), ("Auburn", 0.42), ("Copper", 0.52),
                                                   ("Honey", 0.63), ("Blond", 0.72), ("Platinum", 0.9), ("Silver", 1.0)]

    static func ramp(_ stops: [String], _ t: Double) -> String {
        let x = min(1, max(0, t)) * Double(stops.count - 1)
        let i = min(stops.count - 2, Int(x)), f = x - Double(i)
        let a = rgb(stops[i]), b = rgb(stops[i + 1])
        return hex((a.0 + (b.0 - a.0) * f, a.1 + (b.1 - a.1) * f, a.2 + (b.2 - a.2) * f))
    }
    /// Hue 0...1, shade -1...1.
    static func hueShade(_ hue: Double, _ shade: Double) -> String {
        let h = hue - floor(hue), s = min(1, max(-1, shade))
        let sat = s > 0 ? 0.86 * (1 - s * 0.92) : 0.86
        let bri = s < 0 ? 0.97 + s * 0.84 : 0.97
        let c = UIColor(hue: h, saturation: sat, brightness: bri, alpha: 1)
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0; c.getRed(&r, green: &g, blue: &b, alpha: &a)
        return hex((Double(r), Double(g), Double(b)))
    }
    /// Best hue / shade for an existing hex (used when a palette preset is tapped).
    static func hueShadeOf(_ hexString: String) -> (Double, Double) {
        let c = rgb(hexString); var h: CGFloat = 0, s: CGFloat = 0, v: CGFloat = 0, a: CGFloat = 0
        UIColor(red: c.0, green: c.1, blue: c.2, alpha: 1).getHue(&h, saturation: &s, brightness: &v, alpha: &a)
        if v < 0.9 { return (Double(h), max(-1, (Double(v) - 0.97) / 0.84)) }
        return (Double(h), min(1, max(0, (1 - Double(s) / 0.86) / 0.92)))
    }
    static func rgb(_ h: String) -> (Double, Double, Double) {
        var v: UInt64 = 0; Scanner(string: h).scanHexInt64(&v)
        return (Double((v >> 16) & 255) / 255, Double((v >> 8) & 255) / 255, Double(v & 255) / 255)
    }
    static func hex(_ c: (Double, Double, Double)) -> String {
        func b(_ x: Double) -> Int { Int((min(1, max(0, x)) * 255).rounded()) }
        return String(format: "%02X%02X%02X", b(c.0), b(c.1), b(c.2))
    }
    static func nearest(_ stops: [String], _ hexString: String) -> Int {
        let c = rgb(hexString)
        return stops.indices.min { i, j in
            let a = rgb(stops[i]), b = rgb(stops[j])
            return pow(a.0 - c.0, 2) + pow(a.1 - c.1, 2) + pow(a.2 - c.2, 2) < pow(b.0 - c.0, 2) + pow(b.1 - c.1, 2) + pow(b.2 - c.2, 2)
        } ?? 0
    }
    /// A readable name for any pick (the TV rows and accessibility).
    static func describe(_ hexString: String) -> String {
        let (h, s) = hueShadeOf(hexString); let c = rgb(hexString)
        let lum = 0.2126 * c.0 + 0.7152 * c.1 + 0.0722 * c.2
        let sat = max(c.0, c.1, c.2) - min(c.0, c.1, c.2)
        if sat < 0.1 { return lum > 0.85 ? "White" : lum < 0.18 ? "Black" : "Grey" }
        let names = ["Red", "Orange", "Gold", "Lime", "Green", "Teal", "Sky", "Blue", "Violet", "Pink", "Red"]
        let base = names[Int((h * 10).rounded()) % names.count]
        return s < -0.4 ? "Deep \(base.lowercased())" : s > 0.45 ? "Pastel \(base.lowercased())" : base
    }
    static let rainbow: [Color] = stride(from: 0.0, through: 1.0, by: 1.0 / 12).map { Color(hue: $0, saturation: 0.86, brightness: 0.97) }
}

extension Player {
    /// Continuous locker picks: "skin" [t], "hair" [t] (natural) or [hue, shade, 1] (dye), outfit slots [hue, shade].
    var look: [String: [Double]] { get { looks ?? [:] } set { looks = newValue.isEmpty ? nil : newValue } }
    static let outfitSlots = ["shirt", "shorts", "accent", "racket"]
    /// The haircut the game and locker show: a saved cut the menus no longer offer falls back to the body default.
    var shownHaircut: Int { haircut < HeroV4.offered ? haircut : (standardFemale ? 1 : 0) }

    var skinHex: String {
        if let t = look["skin"]?.first { return LockerColor.ramp(LockerColor.skinStops, t) }
        return Outfit.skins[standardSkin]
    }
    var hairHex: String {
        if let v = look["hair"], let t = v.first {
            return v.count >= 3 && v[2] > 0.5 ? LockerColor.hueShade(v[0], v[1]) : LockerColor.ramp(LockerColor.hairStops, t)
        }
        return CharacterOptions.hairHex[hairColor]
    }
    /// nil = the kit's own colour.
    func outfitHex(_ slot: String) -> String? {
        if let v = look[slot], v.count >= 2 { return LockerColor.hueShade(v[0], v[1]) }
        let index: Int? = slot == "shirt" ? shirt : slot == "shorts" ? shorts : slot == "accent" ? accent : racket
        return index.flatMap { Outfit.palette.indices.contains($0) ? Outfit.palette[$0].hex : nil }
    }
    func outfitColor(_ slot: String) -> Color? { outfitHex(slot).map { Color(hex: $0) } }
    func outfitName(_ slot: String) -> String { outfitHex(slot).map(LockerColor.describe) ?? "Kit colour" }

    mutating func setSkin(_ t: Double) {
        look["skin"] = [min(1, max(0, t))]
        standardSkin = LockerColor.nearest(Outfit.skins, skinHex)   // golf + legacy paths keep a close preset
    }
    mutating func setHair(natural t: Double) {
        look["hair"] = [min(1, max(0, t))]; hairColor = LockerColor.nearest(CharacterOptions.hairHex, hairHex)
    }
    mutating func setHair(dyeHue hue: Double, shade: Double) {
        look["hair"] = [hue, shade, 1]; hairColor = LockerColor.nearest(CharacterOptions.hairHex, hairHex)
    }
    mutating func setOutfit(_ slot: String, hue: Double, shade: Double) { look[slot] = [hue - floor(hue), min(1, max(-1, shade))] }
    mutating func clearOutfit(_ slot: String) {
        look[slot] = nil
        switch slot { case "shirt": shirt = nil; case "shorts": shorts = nil; case "accent": accent = nil; default: racket = nil }
    }
    /// Where the sliders sit for the current colours.
    var skinT: Double { look["skin"]?.first ?? Double(standardSkin) / 5 * 0.8 + 0.1 }
    var hairT: Double { if let v = look["hair"], v.count == 1 { return v[0] }; return [0.0, 0.3, 0.45, 0.72, 1.0, 0.5][hairColor] }
    var hairDyed: Bool { (look["hair"]?.count ?? 0) >= 3 }
    func hueShade(_ slot: String) -> (Double, Double) {
        if let v = look[slot], v.count >= 2 { return (v[0], v[1]) }
        return outfitHex(slot).map(LockerColor.hueShadeOf) ?? (0.58, 0)
    }
}
