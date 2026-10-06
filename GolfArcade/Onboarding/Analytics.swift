import Foundation

/// Local diagnostic events only. Identity and camera contents never enter this log.
enum Analytics {
    @MainActor private static let installedElapsed = max(0, Date().timeIntervalSince1970 - OnboardingStore().installedAt)
    @MainActor private static let began = ProcessInfo.processInfo.systemUptime
    @MainActor static var secondsSinceInstall: Double { installedElapsed + max(0, ProcessInfo.processInfo.systemUptime - began) }
    @MainActor static func track(_ name: String, _ props: [String: String] = [:]) {
        let time = secondsSinceInstall
        func escaped(_ value: String) -> String { value.addingPercentEncoding(withAllowedCharacters: .alphanumerics) ?? "" }
        let fields = props.sorted { $0.key < $1.key }.map { "\(escaped($0.key))=\(escaped($0.value))" }.joined(separator: " ")
        let line = "analytics \(name) t=\(String(format: "%.3f", time))\(fields.isEmpty ? "" : " " + fields)"
        SportsDiagnostics.write(line)
        #if DEBUG
        events = Array((events + [line]).suffix(200))
        #endif
    }
    #if DEBUG
    @MainActor private(set) static var events: [String] = []
    @MainActor static func clearEvents() { events = [] }
    #endif
}
