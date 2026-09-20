import SceneKit
import Foundation

/// Opt-in render-completion telemetry, including optimized device builds.
/// Records timings only, never camera images.
final class GolfRenderAudit: NSObject, SCNSceneRendererDelegate, @unchecked Sendable {
    private let lock=NSLock()
    private let writer=DispatchQueue(label:"golf.render-audit.writer",qos:.utility)
    private let enabled:Bool
    private let name:String
    private let id=UUID().uuidString
    private var previous:Double?
    private var first:Double?
    private var windowStart:Double?
    private var intervals:[Double]=[]
    private var windows:[[String:Double]]=[]
    private var frameCount=0
    init(name:String) {
        self.name=name
        enabled=ProcessInfo.processInfo.arguments.contains("-visualPerformanceAudit")
        super.init()
    }
    func renderer(_ renderer:any SCNSceneRenderer,didRenderScene scene:SCNScene,atTime time:TimeInterval) {
        guard enabled else { return }
        let now=CACurrentMediaTime()
        lock.lock();defer{lock.unlock()}
        if first == nil { first=now;windowStart=now }
        if let previous { intervals.append(now-previous) }
        previous=now;frameCount+=1
        guard now-(windowStart ?? now)>=30,!intervals.isEmpty else { return }
        let sorted=intervals.sorted(),duration=now-(windowStart ?? now)
        windows.append(["startSeconds":(windowStart ?? now)-(first ?? now),"durationSeconds":duration,
            "fps":Double(intervals.count)/duration,"p95FrameMS":sorted[min(sorted.count-1,Int(Double(sorted.count)*0.95))]*1000,
            "framesOver33MS":Double(intervals.filter{$0>0.0335}.count),
            "thermalState":Double(ProcessInfo.processInfo.thermalState.rawValue)])
        windowStart=now;intervals.removeAll(keepingCapacity:true)
        let payload:[String:Any]=["renderer":name,"session":id,"elapsedSeconds":now-(first ?? now),
            "frames":frameCount,"windows":windows,"note":"Completed-render timing; camera tracking and user/TV observations require separate evidence."]
        guard let data=try? JSONSerialization.data(withJSONObject:payload,options:[.prettyPrinted,.sortedKeys]) else { return }
        let filename="render-audit-"+name+"-"+id+".json"
        writer.async {
            let directory=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0]
            try? data.write(to:directory.appendingPathComponent(filename),options:.atomic)
        }
    }
}
