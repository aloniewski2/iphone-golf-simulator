import Foundation
import AVFoundation
import ImageIO
import UniformTypeIdentifiers
let root = URL(fileURLWithPath: CommandLine.arguments[1])
let asset = AVURLAsset(url: URL(fileURLWithPath:"/tmp/motion-club-native.mp4"))
let track = try await asset.loadTracks(withMediaType:.video)[0]
let size = try await track.load(.naturalSize)
let fps = try await track.load(.nominalFrameRate)
let duration = try await asset.load(.duration).seconds
let began = Double(try String(contentsOf: URL(fileURLWithPath:"/tmp/motion-record-start.txt"),encoding:.utf8).trimmingCharacters(in:.whitespacesAndNewlines))!
let tour = Double(try String(contentsOf:root.appendingPathComponent("tour-start.txt"),encoding:.utf8))!
// simctl begins its media timeline after the command-start timestamp.
let start = max(0,tour-began-1.0)
print("Native",size,"fps",fps,"duration",duration,"trim",start)
let composition = AVMutableComposition()
let ctrack = composition.addMutableTrack(withMediaType:.video,preferredTrackID:kCMPersistentTrackID_Invalid)!
try ctrack.insertTimeRange(CMTimeRange(start:CMTime(seconds:start,preferredTimescale:60000),duration:CMTime(seconds:min(68,duration-start),preferredTimescale:60000)),of:track,at:.zero)
let h = size.width * 9 / 16, y = (size.height-h)/2
let video = AVMutableVideoComposition()
video.renderSize = CGSize(width:1280,height:720); video.frameDuration = CMTime(value:1,timescale:60)
let layer = AVMutableVideoCompositionLayerInstruction(assetTrack:ctrack)
layer.setTransform(CGAffineTransform(translationX:0,y:-y).concatenating(CGAffineTransform(scaleX:1280/size.width,y:1280/size.width)),at:.zero)
let instruction = AVMutableVideoCompositionInstruction(); instruction.timeRange = CMTimeRange(start:.zero,duration:composition.duration); instruction.layerInstructions = [layer]; video.instructions = [instruction]
let url=root.appendingPathComponent("Motion_Club_Menus_and_Transitions.mp4")
try? FileManager.default.removeItem(at:url)
let exporter=AVAssetExportSession(asset:composition,presetName:AVAssetExportPresetHighestQuality)!
exporter.videoComposition=video
try await exporter.export(to:url,as:.mp4)
print("Exported",url.path)
// Extract exact frames from the finished runtime recording, without re-rendering menus.
let finished=AVURLAsset(url:url), generator=AVAssetImageGenerator(asset:AVURLAsset(url:url))
generator.appliesPreferredTrackTransform=true
for (time,name) in [(0.8,"01-intro"),(4.2,"02-home-play"),(7.0,"03-home-locker"),(10.0,"04-home-settings"),(12.4,"05-store-locked"),(15.0,"06-sports"),(18.0,"07-tennis"),(21.0,"08-campaign"),(24.0,"09-quick-match"),(27.0,"10-courts"),(30.0,"11-training"),(31.9,"12-loading-tennis"),(33.0,"13-practice"),(35.0,"14-practice-repeat"),(39.0,"16-golf"),(42.0,"17-golf-loading"),(46.0,"18-locker"),(49.0,"19-settings"),(52.0,"20-guide"),(55.0,"21-golf-lesson"),(58.0,"22-connect"),(61.0,"23-results"),(64.0,"24-pause"),(67.0,"25-home"),(13.5,"camera-swing")] {
    let frame=try await generator.image(at:CMTime(seconds:time,preferredTimescale:60000))
    let dest=CGImageDestinationCreateWithURL(root.appendingPathComponent(name+".png") as CFURL,UTType.png.identifier as CFString,1,nil)!
    CGImageDestinationAddImage(dest,frame.image,nil); CGImageDestinationFinalize(dest)
}
