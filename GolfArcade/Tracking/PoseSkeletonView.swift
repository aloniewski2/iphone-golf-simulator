import SwiftUI

struct PoseSkeletonView: View {
    let frame: PoseFrame?
    private let bones: [(BodyJoint, BodyJoint)] = [
        (.nose, .neck), (.neck, .leftShoulder), (.neck, .rightShoulder),
        (.leftShoulder, .leftElbow), (.leftElbow, .leftWrist),
        (.rightShoulder, .rightElbow), (.rightElbow, .rightWrist),
        (.neck, .root), (.root, .leftHip), (.root, .rightHip),
        (.leftHip, .leftKnee), (.leftKnee, .leftAnkle),
        (.rightHip, .rightKnee), (.rightKnee, .rightAnkle)
    ]

    var body: some View {
        Canvas { context, size in
            guard let frame else { return }
            for (startJoint, endJoint) in bones {
                guard let start = frame.point(startJoint), let end = frame.point(endJoint) else { continue }
                var path = Path()
                path.move(to: screenPoint(start, size: size))
                path.addLine(to: screenPoint(end, size: size))
                context.stroke(path, with: .color(.mint), lineWidth: 4)
            }
            for joint in BodyJoint.allCases {
                guard let point = frame.point(joint) else { continue }
                let center = screenPoint(point, size: size)
                context.fill(Path(ellipseIn: CGRect(x: center.x - 4, y: center.y - 4, width: 8, height: 8)), with: .color(.white))
            }
        }
        .allowsHitTesting(false)
    }

    private func screenPoint(_ point: CGPoint, size: CGSize) -> CGPoint {
        CGPoint(x: point.x * size.width, y: (1 - point.y) * size.height)
    }
}
