import Foundation

/// One timestamped presentation sample, shared by phone and TV through CourseScene.
/// It cannot create a shot; scoring continues to consume the authoritative contact event.
struct GolferAnimationState: Equatable, Sendable {
    enum Source: Equatable, Sendable { case live, waiting, canned, recorded }
    let timestamp: TimeInterval
    let pose: BodyPose3D
    let source: Source
}
