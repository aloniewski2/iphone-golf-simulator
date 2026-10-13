import Foundation

/// The hit feel lives in the Unity plugin (GolfHaptics.mm, shapes in GolfHitFeel.h). This app sets its strength and the
/// phone-speaker knock, and plays the Settings feel test. Reached through dlsym like the capture calls, so every call is
/// a quiet no-op until Unity is loaded (and always on the simulator).
@MainActor
enum RacketFeel {
    static let strengths: [(name: String, level: Double)] = [("Soft", 0.55), ("Medium", 0.8), ("Full", 1)]

    /// Which of `strengths` a saved level is.
    static func strengthIndex(_ level: Double) -> Int {
        strengths.indices.min { abs(strengths[$0].level - level) < abs(strengths[$1].level - level) } ?? strengths.count - 1
    }

    private typealias Configure = @convention(c) (Float, Int32) -> Void
    private typealias DemoCount = @convention(c) () -> Int32
    private typealias Demo = @convention(c) (Int32) -> UnsafePointer<CChar>?

    private static func symbol<T>(_ name: String, as type: T.Type) -> T? {
        // RTLD_DEFAULT: search every image loaded into the process, UnityFramework included.
        guard let pointer = dlsym(UnsafeMutableRawPointer(bitPattern: -2), name) else { return nil }
        return unsafeBitCast(pointer, to: type)
    }

    /// Hands the saved strength and knock setting to the plugin: when either changes and as a match starts.
    static func apply(_ session: SportsSession = .shared) {
        symbol("GolfHaptics_Configure", as: Configure.self)?(Float(session.hapticStrength), session.racketSound ? 1 : 0)
    }

    /// Plays entry `index` of the feel test (wrapping round) and names it: "3/22 · Great drive". Nil without Unity.
    static func playDemo(_ index: Int) -> String? {
        guard let count = symbol("GolfHaptics_DemoCount", as: DemoCount.self)?(), count > 0,
              let demo = symbol("GolfHaptics_Demo", as: Demo.self) else { return nil }
        apply()
        let i = (index % Int(count) + Int(count)) % Int(count)
        guard let name = demo(Int32(i)) else { return nil }
        return "\(i + 1)/\(count) · \(String(cString: name))"
    }
}
