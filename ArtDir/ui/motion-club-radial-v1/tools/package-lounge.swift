import Foundation
let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let data = try Data(contentsOf: root.appendingPathComponent("ArtDir/ui/toybox-simple-v6/source/preview-raw/HeroLounge.bin"))
try (data as NSData).compressed(using: .lzfse).write(to: root.appendingPathComponent("GolfArcade/Unity/CharacterAssets/HeroLounge.lzfse"))
