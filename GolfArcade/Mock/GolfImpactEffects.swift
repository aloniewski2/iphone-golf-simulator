import SceneKit
import UIKit

/// A bounded pool: no emitter allocation, particle simulation, or textures per frame.
@MainActor final class GolfImpactEffects {
    let node=SCNNode()
    private var pieces:[SCNNode]=[]
    init() {
        node.name="sunwardImpactEffects"
        for i in 0..<18 {
            let geometry=SCNSphere(radius:0.025+Double(i%3)*0.009);geometry.segmentCount=6
            let piece=SCNNode(geometry:geometry);piece.castsShadow=false
            geometry.firstMaterial?.lightingModel = .constant
            node.addChildNode(piece);pieces.append(piece)
        }
        node.isHidden=true
    }
    func update(origin:SCNVector3,elapsed:Double,lie:CourseLie,heading:Double,enabled:Bool) {
        let sand=lie == .bunker
        let duration=sand ? 0.55 : 0.32
        node.isHidden = !enabled || elapsed < 0 || elapsed > duration
        guard !node.isHidden else { return }
        node.position=origin
        let t=Float(elapsed), yaw=Float(-heading * .pi/180)
        let color = sand ? UIColor(red:0.98,green:0.83,blue:0.58,alpha:1) : UIColor(red:0.53,green:0.72,blue:0.30,alpha:1)
        for (i,p) in pieces.enumerated() {
            let angle=Float(i)*2.39996+yaw, speed=Float(0.3+Double(i%5)*0.14)
            let height=max(0,Float(sand ? 1.8 : 1.1)*t-3*t*t)
            p.position=SCNVector3(cos(angle)*speed*t,height+0.03,sin(angle)*speed*t)
            p.opacity=CGFloat(1-elapsed/duration)
            p.geometry?.firstMaterial?.diffuse.contents=color
        }
    }
}

