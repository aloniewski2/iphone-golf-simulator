import AVFoundation
import AppKit
// usage: frames <video> <outdir> <t1> [t2 ...]   (seconds; writes <outdir>/f_<t>.jpg)
let a = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: a[1]))
let gen = AVAssetImageGenerator(asset: asset)
gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
try? FileManager.default.createDirectory(atPath: a[2], withIntermediateDirectories: true)
for s in a[3...] {
    let t = CMTime(seconds: Double(s)!, preferredTimescale: 600)
    guard let cg = try? gen.copyCGImage(at: t, actualTime: nil) else { print("fail", s); continue }
    let rep = NSBitmapImageRep(cgImage: cg)
    let data = rep.representation(using: .jpeg, properties: [.compressionFactor: 0.85])!
    try! data.write(to: URL(fileURLWithPath: "\(a[2])/f_\(s).jpg"))
}
print("duration", CMTimeGetSeconds(asset.duration))
