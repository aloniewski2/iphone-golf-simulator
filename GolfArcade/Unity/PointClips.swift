import SwiftUI
import AVKit

/// Recording state comes from the encoder, never from a device-name whitelist.
@MainActor @Observable
final class PointClips {
    var state = "off"
    var message = "Enable point clips before playing."
    var hasClip = false
    var exported: URL?
    var exporting = false
    var bufferingEnabled = false
    func reset() { bufferingEnabled = false; state = "off"; hasClip = false; exported = nil; exporting = false; message = "Enable point clips before playing." }
    func receive(_ event: [String: Any]) {
        if let state = event["state"] as? String { self.state = state }
        bufferingEnabled = event["enabled"] as? Bool ?? false
        message = event["message"] as? String ?? ""
        hasClip = event["hasClip"] as? Bool ?? false
        exporting = false
        if state == "saved", let path = event["url"] as? String {
            let candidate = URL(fileURLWithPath: path).standardizedFileURL
            if candidate.deletingLastPathComponent() == Self.directory.standardizedFileURL,
               candidate.pathExtension == "mp4", FileManager.default.fileExists(atPath: candidate.path) { exported = candidate }
        }
    }
    static var directory: URL { FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("PointClips", isDirectory: true) }
    static func library() -> [URL] {
        ((try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: [.creationDateKey], options: .skipsHiddenFiles)) ?? [])
            .filter { $0.pathExtension == "mp4" }
            .sorted { ((try? $0.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) > ((try? $1.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) }
    }
}

struct PointClipControls: View {
    var session: SportsSession
    @State private var showLibrary = false
    var body: some View {
        HStack(spacing: 12) {
            Button {
                session.pointClips.exporting = true
                session.command("savePoint")
            } label: {
                Label(session.pointClips.exporting ? "Saving…" : "Save last point", systemImage: "arrow.down.circle")
                    .font(.subheadline.weight(.semibold)).frame(minHeight: 44)
            }
            .disabled(!session.pointClips.hasClip || session.pointClips.exporting)
            .accessibilityIdentifier("saveLastPoint")
            Spacer(minLength: 0)
            if session.pointClips.bufferingEnabled {
                Image(systemName: "record.circle.fill").foregroundStyle(.red)
                    .accessibilityLabel("Point recording enabled at 60 frames per second")
            }
            Button { showLibrary = true } label: { Label("Clips", systemImage: "film.stack").frame(minHeight: 44) }
                .accessibilityIdentifier("pointClipsLibrary")
        }
        .onChange(of: session.pointClips.exported) { _, url in
            if url != nil { if !session.paused && session.finishedMatch == nil { session.pause(reason: "Clip preview — tap Ready to return to play.") }; showLibrary = true }
        }
        .sheet(isPresented: $showLibrary) { PointClipsSheet(session: session) }
    }
}

/// Shared by controller settings and the clip library: one command path and live encoder state.
struct PointRecordingSettings: View {
    var session: SportsSession
    var showsLibrary = true
    @State private var showLibrary = false
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Toggle("Record points at 60 fps", isOn: Binding(
                get: { session.pointClips.bufferingEnabled },
                set: { session.setPointRecording($0) }
            ))
            .disabled(!session.active || !session.ready || session.sport != "tennis" || session.finishedMatch != nil)
            .accessibilityIdentifier("enablePointClips")
            .accessibilityHint("Buffers complete tennis points for saving. Requires sustained 60 frames per second.")
            Text(session.pointClips.message).font(.subheadline).foregroundStyle(.secondary)
                .accessibilityIdentifier("pointRecordingStatus")
            Text("Resume play to check 60 fps. After the check, recording starts with the next point. Tap Save last point on your controller to keep it.")
                .font(.footnote).foregroundStyle(.secondary)
            Text("TV gameplay and game audio only. Recording turns off if 60 fps cannot be maintained. No microphone or camera capture.")
                .font(.footnote).foregroundStyle(.secondary)
            if showsLibrary {
                Button { showLibrary = true } label: {
                    Label("Saved clips", systemImage: "film.stack").frame(minHeight: 44)
                }.accessibilityIdentifier("recordingSettingsClips")
            }
        }
        .sheet(isPresented: $showLibrary) { PointClipsSheet(session: session) }
    }
}

struct PointClipsSheet: View {
    var session: SportsSession
    @Environment(\.dismiss) private var dismiss
    @State private var files: [URL] = []
    @State private var selected: URL?
    @State private var player: AVPlayer?
    @State private var removalError = ""
    var body: some View {
        NavigationStack {
            List {
                Section {
                    PointRecordingSettings(session: session, showsLibrary: false)
                } header: { Text("TV gameplay · 1080p60") }
                if let selected, let player {
                    Section("Preview") {
                        VideoPlayer(player: player).aspectRatio(16/9, contentMode: .fit)
                        ShareLink(item: selected) { Label("Share or save video", systemImage: "square.and.arrow.up") }
                            .accessibilityIdentifier("sharePointClip")
                    }
                }
                Section("Saved clips") {
                    if files.isEmpty { Text("Your saved points will appear here.").foregroundStyle(.secondary) }
                    ForEach(files, id: \.self) { url in
                        Button {
                            selected = url; player?.pause(); player = AVPlayer(url: url)
                        } label: {
                            HStack { Image(systemName: "play.rectangle"); Text(title(url)); Spacer(); Text("60 fps").font(.caption).foregroundStyle(.secondary) }
                        }
                    }.onDelete { indices in
                        for index in indices {
                            let url = files[index]
                            do { try FileManager.default.removeItem(at: url); if selected == url { player?.pause(); player=nil; selected=nil } }
                            catch { removalError = "Could not delete that clip. Please try again." }
                        }
                        files = PointClips.library()
                    }
                    if !removalError.isEmpty { Text(removalError).foregroundStyle(.red) }
                }
            }
            .navigationTitle("Point clips")
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }
        .onAppear {
            files = PointClips.library()
            if let url = session.pointClips.exported { selected = url; player = AVPlayer(url: url) }
        }
        .onDisappear { player?.pause() }
    }
    private func title(_ url: URL) -> String {
        let date = (try? url.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .now
        return date.formatted(date: .abbreviated, time: .shortened)
    }
}
