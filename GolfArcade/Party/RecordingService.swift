import Foundation
import Observation
import Photos
import UIKit

/// How the TV is recorded and what goes into the sound track. Stored on the phone.
struct CaptureSettings: Codable, Equatable, Sendable {
    enum Quality: String, Codable, CaseIterable, Identifiable, Sendable {
        case p720at30, p720at60, p1080at30, p1080at60
        var id: String { rawValue }
        var width: Int { self == .p720at30 || self == .p720at60 ? 1280 : 1920 }
        var height: Int { self == .p720at30 || self == .p720at60 ? 720 : 1080 }
        var fps: Int { self == .p720at30 || self == .p1080at30 ? 30 : 60 }
        var bitrateMbps: Int { switch self { case .p720at30: 5; case .p720at60: 8; case .p1080at30: 10; case .p1080at60: 15 } }
        var title: String { "\(height)p · \(fps) fps" }
        var detail: String {
            switch self {
            case .p720at30: "Smallest files, easiest on the battery"
            case .p720at60: "Smooth motion, small files"
            case .p1080at30: "Sharp picture (recommended)"
            case .p1080at60: "Sharpest and smoothest, warmest phone"
            }
        }
    }
    var quality: Quality = .p1080at30
    var gameAudio = true
    /// The other players' voices in your party (they are told you are recording).
    var partyVoice = true
    var myMic = true
    var saveToPhotos = true
}

/// The native TV recorder (Unity plugin), behind a protocol so the service is tested without a TV.
@MainActor protocol CaptureRuntime: AnyObject {
    /// The game is running on a TV and the recorder is there.
    var available: Bool { get }
    var active: Bool { get }
    func start(_ configJSON: String) -> Bool
    func stop()
    func setHandler(_ handler: (@MainActor (String) -> Void)?)
}

@MainActor protocol PhotoSaving: AnyObject {
    /// Asks for add-only access (we never read the library); true when videos can be saved.
    func requestAccess() async -> Bool
    var hasAccess: Bool { get }
    func save(_ url: URL) async throws
}

@MainActor final class PhotosLibrary: PhotoSaving {
    var hasAccess: Bool { let s = PHPhotoLibrary.authorizationStatus(for: .addOnly); return s == .authorized || s == .limited }
    func requestAccess() async -> Bool {
        let s = await PHPhotoLibrary.requestAuthorization(for: .addOnly)
        return s == .authorized || s == .limited
    }
    func save(_ url: URL) async throws {
        try await PHPhotoLibrary.shared().performChanges {
            _ = PHAssetCreationRequest.creationRequestForAssetFromVideo(atFileURL: url)
        }
    }
}

/// One-tap recording of what is on the TV (never the phone controller), with the game's sound and the party's voices, saved to the
/// camera roll when you stop. The picture and sound are made by the native recorder in the Unity plugin; this decides when, tells the
/// party, and saves the file. PLAN_PartyChat_Recording.md.
@MainActor @Observable
final class RecordingService {
    static let shared = RecordingService()

    enum State: Equatable { case idle, starting, recording, saving, saved, failed }
    private(set) var state: State = .idle
    private(set) var message = ""
    private(set) var elapsed: TimeInterval = 0
    /// The last recording that could not go to Photos (access off): kept in the app so it can be shared or deleted.
    private(set) var keptFile: URL?
    var settings: CaptureSettings { didSet { if let d = try? JSONEncoder().encode(settings) { defaults.set(d, forKey: "capture.settings") } } }
    /// True from the first tap until the file is saved: the dock shows the red indicator.
    var isRecording: Bool { state == .recording || state == .starting }

    @ObservationIgnored let runtime: CaptureRuntime
    @ObservationIgnored let photos: PhotoSaving
    @ObservationIgnored let chat: PartyChat
    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored var displayConnected: @MainActor () -> Bool = { SportsSession.shared.displayConnected }
    /// Told true just before the recorder starts and false once it has stopped: the voice engine feeds the recording only in between.
    @ObservationIgnored var captureHook: (@MainActor (Bool) -> Void)?
    @ObservationIgnored var now: @MainActor () -> Date = { Date() }
    @ObservationIgnored var freeBytes: @MainActor () -> Int64 = { RecordingService.freeDiskBytes() }
    @ObservationIgnored private var startedAt: Date?
    @ObservationIgnored private var ticker: Task<Void, Never>?
    @ObservationIgnored private var photosGranted = false
    @ObservationIgnored private var announced = false

    init(runtime: CaptureRuntime? = nil, photos: PhotoSaving? = nil, chat: PartyChat = .shared, defaults: UserDefaults = .standard) {
        self.runtime = runtime ?? LiveCaptureRuntime()
        self.photos = photos ?? PhotosLibrary()
        self.chat = chat; self.defaults = defaults
        settings = (defaults.data(forKey: "capture.settings").flatMap { try? JSONDecoder().decode(CaptureSettings.self, from: $0) }) ?? CaptureSettings()
        self.runtime.setHandler { [weak self] json in self?.receive(json) }
    }

    // MARK: can I record

    /// Why recording is not possible right now (nil = it is). Shown on the button.
    var unavailableReason: String? {
        if isRecording || state == .saving { return nil }
        guard displayConnected() else { return "Connect a TV to record. Recordings capture the TV picture, never this phone." }
        guard runtime.available else { return "Recording is ready once the game is up on the TV." }
        return nil
    }
    var canToggle: Bool { isRecording || state == .saving ? state == .recording : unavailableReason == nil }

    var timeText: String { let s = Int(elapsed); return String(format: "%d:%02d", s / 60, s % 60) }

    // MARK: start / stop

    func toggle() { if state == .recording { stop() } else if state == .idle || state == .saved || state == .failed { Task { await start() } } }

    func start() async {
        guard state == .idle || state == .saved || state == .failed else { return }
        if let why = unavailableReason { state = .failed; message = why; return }
        if freeBytes() < 1_200_000_000 { state = .failed; message = "Free up some storage first: recording needs about 1 GB free."; return }
        state = .starting; message = "Starting…"; elapsed = 0; keptFile = nil
        photosGranted = settings.saveToPhotos ? await photos.requestAccess() : false
        // asked up front so the system prompt never interrupts the moment you stop
        captureHook?(true)
        guard runtime.start(configJSON()) else {
            captureHook?(false)
            if state == .starting { state = .failed; message = "The recorder could not start. Try again once the TV picture is up." }
            return
        }
        // the native recorder confirms with a "started" event; until then the dock already shows the indicator
        state = .recording; startedAt = now(); message = settings.saveToPhotos && !photosGranted ? "Photos access is off: this recording will stay in the app." : "Recording the TV"
        announced = chat.channel.inParty
        if announced { chat.announceRecording(true) }
        startTicker()
    }

    func stop() {
        guard state == .recording || state == .starting else { return }
        state = .saving; message = "Saving…"
        runtime.stop()
        ticker?.cancel(); ticker = nil
        if announced { chat.announceRecording(false); announced = false }
    }

    /// The TV went away: finish what was recorded.
    func displayLost() { if isRecording { stop() } }

    func configJSON() -> String {
        let q = settings.quality
        let object: [String: Any] = ["width": q.width, "height": q.height, "fps": q.fps, "bitrateMbps": q.bitrateMbps, "gameAudio": settings.gameAudio,
                                     "voice": settings.partyVoice, "mic": settings.myMic, "display": 1]
        return (try? JSONSerialization.data(withJSONObject: object)).flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
    }

    private func startTicker() {
        ticker?.cancel()
        ticker = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(500))
                guard let self, self.state == .recording, let t = self.startedAt else { return }
                self.elapsed = self.now().timeIntervalSince(t)
            }
        }
    }

    // MARK: events from the native recorder

    func receive(_ json: String) {
        guard let o = (try? JSONSerialization.jsonObject(with: Data(json.utf8))) as? [String: Any], o["type"] as? String == "capture" else { return }
        let text = o["message"] as? String ?? ""
        switch o["state"] as? String {
        case "started": if state == .starting { state = .recording }
        case "warning": if !text.isEmpty { message = text }
        case "failed":
            ticker?.cancel(); ticker = nil; captureHook?(false)
            if announced { chat.announceRecording(false); announced = false }
            state = .failed; message = text.isEmpty ? "The recording could not be made." : text
        case "finished":
            ticker?.cancel(); ticker = nil; captureHook?(false)
            if announced { chat.announceRecording(false); announced = false }
            let seconds = o["seconds"] as? Double ?? elapsed
            if let path = o["url"] as? String { Task { await finish(URL(fileURLWithPath: path), seconds: seconds, reason: o["reason"] as? String ?? "user") } }
            else { state = .failed; message = "The recording file is missing." }
        default: break
        }
    }

    private func finish(_ url: URL, seconds: TimeInterval, reason: String) async {
        state = .saving; elapsed = seconds
        let why: String
        switch reason {
        case "background": why = "Recording stopped because Motion Club left the screen. "
        case "storage": why = "Recording stopped: the phone is almost out of storage. "
        case "thermal": why = "Recording stopped: the phone is too warm. "
        case "display": why = "Recording stopped: the TV disconnected. "
        case "limit": why = "Recording stopped at the 90 minute limit. "
        default: why = ""
        }
        if settings.saveToPhotos, photosGranted || photos.hasAccess {
            do {
                try await photos.save(url)
                try? FileManager.default.removeItem(at: url)
                state = .saved; message = why + "Saved to Photos · \(durationText(seconds))"
                return
            } catch { /* fall through: keep the file */ }
        }
        keptFile = Self.keep(url)
        state = .saved
        message = why + (settings.saveToPhotos ? "Photos could not save it, so it is kept in Motion Club. Allow Photos in Settings, or tap Share." : "Saved in Motion Club · \(durationText(seconds))")
    }

    private func durationText(_ s: TimeInterval) -> String { String(format: "%d:%02d", Int(s) / 60, Int(s) % 60) }

    // MARK: files

    static var folder: URL { FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("Recordings", isDirectory: true) }
    static var tempFolder: URL { URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("MotionClubRecordings", isDirectory: true) }

    /// Moves a recording out of the temporary folder (the system may clear it) into the app's own.
    static func keep(_ url: URL) -> URL? {
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let dest = folder.appendingPathComponent(url.lastPathComponent)
        do { try FileManager.default.moveItem(at: url, to: dest); return dest } catch { return FileManager.default.fileExists(atPath: url.path) ? url : nil }
    }
    static func library() -> [URL] {
        ((try? FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: [.creationDateKey], options: .skipsHiddenFiles)) ?? [])
            .filter { $0.pathExtension == "mp4" }
            .sorted { ((try? $0.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) > ((try? $1.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) }
    }
    static func freeDiskBytes() -> Int64 {
        let values = try? URL(fileURLWithPath: NSTemporaryDirectory()).resourceValues(forKeys: [.volumeAvailableCapacityForImportantUsageKey])
        return values?.volumeAvailableCapacityForImportantUsage ?? .max
    }

    /// A recording left behind (the app was killed while recording): saved now if Photos already allows it, kept otherwise.
    func recoverLeftovers() async {
        guard !isRecording, state != .saving else { return }
        let files = ((try? FileManager.default.contentsOfDirectory(at: Self.tempFolder, includingPropertiesForKeys: nil)) ?? []).filter { $0.pathExtension == "mp4" }
        for url in files {
            if photos.hasAccess, (try? await photos.save(url)) != nil { try? FileManager.default.removeItem(at: url) }
            else { keptFile = Self.keep(url) }
        }
    }

    func deleteKept(_ url: URL) { try? FileManager.default.removeItem(at: url); if keptFile == url { keptFile = nil } }

    #if DEBUG
    func debugReset() { ticker?.cancel(); ticker = nil; state = .idle; message = ""; elapsed = 0; keptFile = nil; announced = false }
    #endif
}
