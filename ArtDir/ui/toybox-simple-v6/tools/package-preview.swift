import Foundation
// Run from repo root after HeroLockerExport.RunMenuPreview in Unity.
let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let raw = cwd.appendingPathComponent("ArtDir/ui/toybox-simple-v6/source/preview-raw")
let destination = cwd.appendingPathComponent("GolfArcade/Unity/CharacterAssets")
for name in ["HeroMenu"] + [0,3,6,9,12,15,18,19].map({String(format:"HeroSwing_%02d",$0)}) {
    let bytes = try Data(contentsOf: raw.appendingPathComponent(name + ".bin"))
    let packed = try (bytes as NSData).compressed(using: .lzfse)
    try packed.write(to: destination.appendingPathComponent(name + ".lzfse"))
    print("\(name): \(bytes.count) → \(packed.length) bytes")
}
let manifest = raw.appendingPathComponent("HeroMenu.json")
if FileManager.default.fileExists(atPath: manifest.path) {
    try Data(contentsOf: manifest).write(to: destination.appendingPathComponent("HeroMenu.json"))
}
