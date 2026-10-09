import Foundation
import AVFoundation
let args = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: args[1]))
let tracks = try await asset.load(.tracks)
for track in tracks {
    let type = track.mediaType
    let rate = try await track.load(.nominalFrameRate)
    let size = try await track.load(.naturalSize)
    let range = try await track.load(.timeRange)
    let reader = try AVAssetReader(asset: asset)
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: nil)
    reader.add(output); reader.startReading()
    var count=0
    while output.copyNextSampleBuffer() != nil { count += 1 }
    print("\(type.rawValue) size=\(size) fps=\(rate) duration=\(range.duration.seconds) samples=\(count)")
    if type == .video { precondition(rate == 60 && size.width == 1920 && size.height == 1080 && count > 100) }
    if type == .audio { precondition(count > 0) }
}
precondition(tracks.contains { $0.mediaType == .audio })
print("PASS: real video samples at 1080p60 plus game-audio track")
