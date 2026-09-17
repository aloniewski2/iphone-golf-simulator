import Foundation

/// Manually annotated from an external high-speed recording containing the physical
/// movement and display in the SAME shot. Never derived from detector pipeline time.
struct DisplayLatencyMeasurement: Codable, Equatable, Identifiable {
    enum Destination: String, CaseIterable, Codable, Identifiable {
        case phone, airPlay, wired
        var id: Self { self }
        var title: String { switch self { case .phone: "Phone"; case .airPlay: "AirPlay"; case .wired: "Wired display" } }
    }
    let id: UUID
    let destination: Destination
    let referenceClip: String
    let recordingFPS: Double
    let motionFrame: Int
    let responseFrame: Int
    let receiverAndConditions: String

    var isValid: Bool {
        recordingFPS.isFinite && (30...1_000).contains(recordingFPS) && motionFrame >= 0 &&
        responseFrame >= motionFrame && responseFrame - motionFrame <= Int(recordingFPS * 10) &&
        !referenceClip.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !receiverAndConditions.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
    var milliseconds: Double? { isValid ? Double(responseFrame - motionFrame) / recordingFPS * 1_000 : nil }
    var oneFrameMilliseconds: Double { 1_000 / recordingFPS }
}
