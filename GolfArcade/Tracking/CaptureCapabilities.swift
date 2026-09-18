import Foundation

/// Inventory, not a performance certification. Frame rates describe capture formats,
/// never the number of fresh poses the detector can process.
struct CaptureFormatCapability: Codable, Equatable, Sendable {
    struct RateRange: Codable, Equatable, Sendable {
        let minimum: Double
        let maximum: Double
        func supports(_ fps: Int) -> Bool { minimum <= Double(fps) && maximum >= Double(fps) }
    }
    let index: Int
    let width: Int
    let height: Int
    let fieldOfView: Double
    let rates: [RateRange]
    var dynamicAspectRatios: [String] = []
    var depthFormatCount = 0
    var centerStageSupported = false

    func supports(_ fps: Int) -> Bool { rates.contains { $0.supports(fps) } }
    var isTrackingCandidate: Bool {
        min(width, height) >= 720 && max(width, height) <= 1920 && supports(30)
    }
}

struct CaptureDeviceCapability: Codable, Equatable, Sendable {
    let name: String
    let deviceType: String
    let position: String
    let formats: [CaptureFormatCapability]
}

enum CameraCaptureProfile: String, CaseIterable, Codable, Identifiable, Sendable {
    case baseline, wideFront
    var id: Self { self }
    var title: String {
        self == .baseline ? "Current camera baseline" : "Wider front camera (experimental)"
    }
}

enum CaptureFormatSelection {
    struct Choice: Equatable, Sendable {
        let index: Int
        let fps: Int
    }

    /// Keep swing coverage ahead of frame rate. Within one degree of the widest
    /// format, prefer the requested rate, then the smaller processing workload.
    static func choose(_ formats: [CaptureFormatCapability], requestedFPS: Int) -> Choice? {
        let eligible = formats.filter(\.isTrackingCandidate)
        guard let widest = eligible.map(\.fieldOfView).max() else { return nil }
        let candidates = eligible.filter { $0.fieldOfView >= widest - 1 }
        let requested = [30, 60, 120].contains(requestedFPS) ? requestedFPS : 30
        let fps = [120, 60, 30].first { rate in
            rate <= requested && candidates.contains { $0.supports(rate) }
        } ?? 30
        guard let selected = candidates.filter({ $0.supports(fps) }).min(by: {
            let left = $0.width * $0.height, right = $1.width * $1.height
            return left == right ? $0.index < $1.index : left < right
        }) else { return nil }
        return Choice(index: selected.index, fps: fps)
    }
}

struct CaptureConfiguration: Codable, Equatable, Sendable {
    let profile: CameraCaptureProfile
    let cameraName: String
    let deviceType: String
    let formatIndex: Int
    let requestedFPS: Int
    let configuredFPS: Int
    let formatWidth: Int
    let formatHeight: Int
    let nominalFieldOfView: Double
    let dynamicAspectRatio: String?
    let centerStageActive: Bool
    let fallbackReason: String?
}

/// Changes in these fields invalidate an address. An image-space ball cannot stay
/// calibrated across rotation, mirroring, resolution or sensor changes.
struct CaptureGeometry: Codable, Equatable, Sendable {
    let width: Int
    let height: Int
    let rotationDegrees: Double
    let mirrored: Bool
    let configurationGeneration: Int
}

struct CaptureEvidence: Codable, Equatable, Sendable {
    let configuration: CaptureConfiguration
    let geometry: CaptureGeometry
    let thermalState: String
}
