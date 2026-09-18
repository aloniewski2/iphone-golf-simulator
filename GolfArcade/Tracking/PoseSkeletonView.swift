import SwiftUI

struct PoseSkeletonView: View {
    let frame: PoseFrame?
    var frameAspect: CGFloat = 3.0 / 4.0
    var contentMode: ContentMode = .fill
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
        PoseSkeletonMapper.screenPoint(
            point,
            in: size,
            frameAspect: frameAspect,
            fitsEntireFrame: contentMode == .fit
        )
    }
}

enum PoseSkeletonMapper {
    static func screenPoint(
        _ point: CGPoint,
        in size: CGSize,
        frameAspect: CGFloat,
        fitsEntireFrame: Bool
    ) -> CGPoint {
        guard size.width > 0, size.height > 0, frameAspect > 0 else { return .zero }
        let viewAspect = size.width / size.height
        let scaleToWidth = fitsEntireFrame ? frameAspect > viewAspect : frameAspect < viewAspect
        let imageSize: CGSize
        if scaleToWidth {
            imageSize = CGSize(width: size.width, height: size.width / frameAspect)
        } else {
            imageSize = CGSize(width: size.height * frameAspect, height: size.height)
        }
        let origin = CGPoint(x: (size.width - imageSize.width) / 2, y: (size.height - imageSize.height) / 2)
        return CGPoint(
            x: origin.x + point.x * imageSize.width,
            y: origin.y + (1 - point.y) * imageSize.height
        )
    }
}
