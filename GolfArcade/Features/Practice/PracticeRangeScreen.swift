import SwiftUI
import Combine
import UniformTypeIdentifiers
import Darwin

struct PracticeRangeScreen: View {
    let camera: CameraSwingController
    let onExit: () -> Void
    @State private var lab = PracticeLab()
    @State private var page = 0
    @State private var cameraWanted = false
    @State private var exporting = false
    @State private var document: BenchmarkDocument?
    @State private var notice: String?
    @State private var confirmingExit = false
    @State private var confirmingClear = false
    @State private var replay: BenchmarkTrial?
    @Environment(\.scenePhase) private var scenePhase
    @State private var timer = Timer.publish(every: 0.1, on: .main, in: .common).autoconnect()

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Text("PRACTICE LAB").font(.caption.weight(.black)).tracking(2).foregroundStyle(.mint)
                    Text("Make every swing measurable.").font(.title2.bold())
                    Picker("Lab page", selection: $page) {
                        Text("Live range").tag(0)
                        Text("Results (\(lab.trials.count))").tag(1)
                    }.pickerStyle(.segmented).accessibilityIdentifier("labPage")
                    if page == 0 { rangeContent } else { resultsContent }
                    Text("Camera motion is estimated in 2D. Shot direction, contact and distance are simulated—not measured clubface or ball data.")
                        .font(.caption).foregroundStyle(.secondary)
                }.padding(20)
            }
            .background(Palette.background.ignoresSafeArea())
            .navigationTitle("Practice Lab")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Menu") {
                        if lab.trials.isEmpty && lab.displayLatencyMeasurements.isEmpty && lab.active == nil { onExit() } else { confirmingExit = true }
                    }.accessibilityIdentifier("labExit")
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Export", systemImage: "square.and.arrow.up", action: export)
                        .disabled(lab.active != nil || (lab.trials.isEmpty && lab.displayLatencyMeasurements.isEmpty)).accessibilityIdentifier("labExport")
                }
            }
            .safeAreaInset(edge: .bottom) { trialControls }
            .confirmationDialog("Leave this session? Unexported trials will be discarded.", isPresented: $confirmingExit, titleVisibility: .visible) {
                Button("Discard session and leave", role: .destructive) { onExit() }
            }
            .confirmationDialog("Discard all trials in this session? Exported files will not be removed.", isPresented: $confirmingClear, titleVisibility: .visible) {
                Button("Discard trials", role: .destructive) { lab.clear() }
            }
            .alert("Practice Lab", isPresented: Binding(get: { notice != nil }, set: { if !$0 { notice = nil } })) {
                Button("OK") { notice = nil }
            } message: { Text(notice ?? "") }
            .sheet(item: $replay) { BenchmarkReplayView(trial: $0) }
            .fileExporter(isPresented: $exporting, document: document, contentType: .json,
                defaultFilename: "golf-benchmark-\(Date().formatted(.iso8601).replacingOccurrences(of: ":", with: "-"))") { result in
                    switch result {
                    case .success: notice = "Export saved. Pose traces are included only for trials recorded with consent."
                    case .failure(let error): notice = "Export failed: \(error.localizedDescription)"
                    }
                    document = nil
                }
        }
        .onAppear {
            camera.stop()
            camera.requiresPositionReview = false
            camera.requiresSwingCheck = false
            camera.contactAssistance = false
            camera.onEvent = nil
            camera.gesturesEnabled = false
            camera.tracker.setCalibration(nil)
            camera.ballAddress = nil
            camera.resetAddress()
            camera.onObservation = { [lab] in lab.ingest($0) }
            camera.tracker.loadSourceClips()
        }
        .onDisappear {
            stopCamera(reason: "Left practice lab")
            camera.onObservation = nil
            camera.tracker.setTracking(mode: .body2D, framesPerSecond: 30, profile: .baseline)
        }
        .onChange(of: scenePhase) { _, phase in
            if phase != .active { stopCamera(reason: "App interrupted") }
        }
        .onChange(of: lab.poseMode) { _, _ in stopCamera(reason: "Tracking mode changed") }
        .onChange(of: lab.requestedCaptureFPS) { _, _ in stopCamera(reason: "Capture rate changed") }
        .onChange(of: lab.captureProfile) { _, _ in stopCamera(reason: "Capture profile changed") }
        .onChange(of: lab.active?.id) { old, new in
            if old != nil, new == nil {
                camera.tracker.finishSourceRecording(reason: "Trial finished")
                if let trial = lab.trials.last { camera.tracker.saveSourceTrial(trial) }
            }
        }
        .onReceive(timer) { _ in lab.tick(at: ProcessInfo.processInfo.systemUptime) }
        .accessibilityIdentifier("practiceLab")
    }

    private var rangeContent: some View {
        VStack(alignment: .leading, spacing: 16) {
            LabCameraPanel(camera: camera, wanted: cameraWanted)
            CaptureEvidencePanel(tracker: camera.tracker)
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label(lab.livePhase, systemImage: "waveform.path").font(.headline)
                    Spacer()
                    Text("\(Int(lab.confidence * 100))% confidence").font(.caption.monospacedDigit())
                }
                HStack {
                    metric("Pose FPS", lab.fps.map { String(format: "%.1f", $0) } ?? "—")
                    metric("Vision work", milliseconds(lab.inferenceMS))
                    metric("Pipeline", milliseconds(lab.pipelineMS))
                }
                Text("Pipeline = camera callback → detector state. Display and sensor latency are not measured.")
                    .font(.caption2).foregroundStyle(.secondary)
                if let impact = lab.lastImpact {
                    Divider()
                    Text(String(format: "%@ · %.0f%% power · %+.1f° start · %.1f yd simulated",
                        impact.strike.capitalized, impact.power * 100, impact.startLineDegrees, impact.distanceYards))
                        .font(.subheadline.monospacedDigit()).accessibilityIdentifier("labLastImpact")
                }
            }.labCard()
            VStack(alignment: .leading, spacing: 12) {
                LabeledContent("Challenge") {
                    Picker("Challenge", selection: $lab.challenge) {
                        ForEach(BenchmarkChallenge.allCases) { Text($0.title).tag($0) }
                    }.labelsHidden().accessibilityIdentifier("labChallenge")
                }
                LabeledContent("Handedness") {
                    Picker("Handedness", selection: $lab.handedness) {
                        ForEach(Handedness.allCases) { Text($0.displayName).tag($0) }
                    }.labelsHidden()
                }
                Text(lab.challenge.instruction).font(.subheadline)
                Picker("Tracking mode", selection: $lab.poseMode) {
                    ForEach(CameraPoseMode.allCases) { Text($0.title).tag($0) }
                }.accessibilityIdentifier("labTrackingMode")
                Picker("Capture profile", selection: $lab.captureProfile) {
                    ForEach(CameraCaptureProfile.allCases) { Text($0.title).tag($0) }
                }.accessibilityIdentifier("labCaptureProfile")
                Picker("Requested capture rate", selection: $lab.requestedCaptureFPS) {
                    Text("30 fps").tag(30)
                    Text("60 fps if supported").tag(60)
                    Text("120 fps if supported").tag(120)
                }
                Text("Both profiles use the same 2D contact detector. 3D depth remains visual-only. Requested capture rate may fall back to preserve coverage; it is not pose FPS. New tracking profiles are not certified.")
                    .font(.caption).foregroundStyle(.secondary)
                Text("12 seconds per trial. One swing only; wait for completion. The detector resets at the start—hold still first.")
                    .font(.caption).foregroundStyle(.secondary)
                TextField("Anonymous participant code", text: $lab.participant)
                    .textInputAutocapitalization(.characters).autocorrectionDisabled()
                    .accessibilityIdentifier("labParticipant")
                TextField("Conditions: lighting, distance, phone position", text: $lab.conditions, axis: .vertical)
                    .lineLimit(2...3)
                Toggle("Record pose trace for replay", isOn: $lab.recordingConsent).accessibilityIdentifier("labRecordConsent")
                Text("Optional joint coordinates only—no video or audio. Held in memory until you leave. Export explicitly to retain it. Do not enter names or personal details.")
                    .font(.caption).foregroundStyle(.secondary)
                Toggle("I consent to source video for this session", isOn: $lab.sourceVideoConsent)
                    .accessibilityIdentifier("labSourceConsent")
                Text("Off by default. Each trial records up to 12 seconds of silent camera video, including your room and anyone visible. Clips and trial metadata stay on this device, excluded from backup, until you delete them below or share them explicitly. No automatic uploads or Photos access.")
                    .font(.caption).foregroundStyle(.secondary)
            }.labCard().disabled(lab.active != nil)
            Text("Use empty hands in a clear space. Keep one player in frame; player identity is not validated in this lab.")
                .font(.caption).foregroundStyle(.secondary)
        }
    }

    private var resultsContent: some View {
        VStack(alignment: .leading, spacing: 16) {
            SourceClipLibrary(tracker: camera.tracker)
            DisplayLatencyEntry(lab: lab)
            VStack(alignment: .leading, spacing: 12) {
                Text("Camera benchmark summary").font(.headline)
                rate("First-try single outcome", lab.summary.recognition)
                LabeledContent("Tracking/contact retries", value: "\(lab.summary.retries)")
                LabeledContent("Duplicate-outcome trials", value: "\(lab.summary.duplicateTrials)")
                rate("Direction target", lab.summary.direction)
                rate("Power band", lab.summary.power)
                rate("Putting distance", lab.summary.putting)
                Divider()
                LabeledContent("Accidental strokes", value: lab.summary.negativeTrials == 0 ? "Not measured" : "\(lab.summary.falseStrokes) / \(lab.summary.negativeTrials) trials")
                LabeledContent("Non-swing exposure", value: String(format: "%.0f seconds", lab.summary.negativeSeconds))
                LabeledContent("Pipeline p95", value: milliseconds(lab.summary.pipelineP95))
                Text("\(lab.summary.excludedCount) synthetic, interrupted or empty trials excluded. No release gates certified. These are prompted attempts, not independent ground truth.")
                    .font(.caption).foregroundStyle(.secondary)
                Text("Straight: ±5°. Left/right: 5–45°. Power: <35%, 35–70%, ≥70%. Putts: ±25% of target distance. Whiffs and duplicate detections fail target challenges.")
                    .font(.caption2).foregroundStyle(.secondary)
            }.labCard().accessibilityIdentifier("labSummary")
            HStack {
                Button("Run synthetic fixture") { lab.addFixture() }
                    .disabled(lab.isFull)
                    .accessibilityIdentifier("labFixture")
                Spacer()
                Button("Clear", role: .destructive) { confirmingClear = true }
                    .disabled(lab.trials.isEmpty && lab.displayLatencyMeasurements.isEmpty)
            }.disabled(lab.active != nil)
            Text("Fixtures verify software replay only. They never improve camera scores. Session limit: 50 trials; export and clear to continue.")
                .font(.caption).foregroundStyle(.secondary)
            ForEach(lab.trials.reversed()) { trial in
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text(trial.challenge.title).font(.headline)
                        Spacer()
                        Text(trial.source.rawValue.uppercased()).font(.caption2.bold()).foregroundStyle(.mint)
                    }
                    Text(trial.outcome).font(.subheadline)
                    Text("\(trial.usableFrameCount)/\(trial.frameCount) usable poses · \(trial.multipleBodyFrameCount) multi-person frames")
                        .font(.caption).foregroundStyle(.secondary)
                    if let diagnostics = trial.diagnostics {
                        Text(String(format: "%@ · %d fps requested · %d dropped frames · %.0f ms largest gap · %d depth frames",
                            diagnostics.poseMode.title, diagnostics.requestedCaptureFPS, diagnostics.droppedFrames,
                            diagnostics.maximumDeliveryGapMS, diagnostics.depthFrames))
                            .font(.caption).foregroundStyle(.secondary)
                    }
                    if !trial.poses.isEmpty {
                        Button("Replay trace") { replay = trial }.accessibilityIdentifier("labReplay-\(trial.id)")
                        if trial.recordingTruncated { Text("Trace limit reached; replay is incomplete.").font(.caption).foregroundStyle(.orange) }
                    }
                }.labCard()
            }
        }
    }

    private var trialControls: some View {
        VStack(spacing: 8) {
            if lab.active != nil {
                HStack {
                    Text(String(format: "%.0fs · %@", ceil(lab.remaining), lab.challenge.title)).font(.headline.monospacedDigit())
                    Spacer()
                    Button("Abort") { lab.interrupt("User aborted", at: ProcessInfo.processInfo.systemUptime) }
                        .accessibilityIdentifier("labAbort")
                }
                ProgressView(value: PracticeLab.trialDuration - lab.remaining, total: PracticeLab.trialDuration)
            } else {
                HStack {
                    Button(cameraWanted ? "Stop camera" : "Start camera") {
                        if cameraWanted { stopCamera(reason: "Camera stopped") }
                        else {
                            cameraWanted = true
                            UIApplication.shared.isIdleTimerDisabled = true
                            camera.tracker.setTracking(mode: lab.poseMode, framesPerSecond: lab.requestedCaptureFPS, profile: lab.captureProfile)
                            camera.handedness = lab.handedness
                            camera.setClub(lab.challenge.club)
                            camera.resetAddress()
                            camera.start()
                        }
                    }.accessibilityIdentifier("labCamera")
                    Spacer()
                    Button("Start 12s trial", action: beginTrial)
                        .buttonStyle(.borderedProminent).tint(.mint)
                        .foregroundStyle(canStart ? Color.black : Color.secondary)
                        .disabled(!canStart)
                        .accessibilityIdentifier("labStartTrial")
                }
            }
        }.padding().background(.ultraThinMaterial)
    }

    private func beginTrial() {
        guard !camera.tracker.sourceRecordingActive else { notice = "Please wait for the source recording to finish."; return }
        guard lab.begin(at: ProcessInfo.processInfo.systemUptime) else {
            notice = "A recent usable pose is required. Step into frame with shoulders and hands visible."
            return
        }
        camera.handedness = lab.handedness
        camera.setClub(lab.challenge.club)
        camera.resetAddress()
        if lab.sourceVideoConsent, let id = lab.active?.id {
            camera.tracker.beginSourceRecording(trialID: id, consented: true)
        }
        page = 0
    }

    private var canStart: Bool { cameraWanted && lab.confidence >= 0.45 && !lab.isFull }

    private func stopCamera(reason: String) {
        let activeID = lab.active?.id
        lab.interrupt(reason, at: ProcessInfo.processInfo.systemUptime)
        if let activeID, let trial = lab.trials.last, trial.id == activeID { camera.tracker.saveSourceTrial(trial) }
        cameraWanted = false
        UIApplication.shared.isIdleTimerDisabled = false
        camera.stop()
        lab.cameraStopped()
    }

    private func export() {
        var system = utsname()
        uname(&system)
        let device = withUnsafeBytes(of: &system.machine) { bytes in
            String(decoding: bytes.prefix { $0 != 0 }, as: UTF8.self)
        }
        let report = BenchmarkReport(device: device, os: ProcessInfo.processInfo.operatingSystemVersionString,
            appVersion: Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "unknown",
            trials: lab.trials, cameraInventory: camera.tracker.captureInventory,
            displayLatencyMeasurements: lab.displayLatencyMeasurements)
        do {
            document = BenchmarkDocument(data: try report.encodedJSON())
            exporting = true
        } catch { notice = "Could not encode report: \(error.localizedDescription)" }
    }

    private func milliseconds(_ value: Double?) -> String { value.map { String(format: "%.1f ms", $0) } ?? "Not measured" }
    private func metric(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).font(.caption2).foregroundStyle(.secondary)
            Text(value).font(.subheadline.bold().monospacedDigit())
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
    private func rate(_ label: String, _ rate: BenchmarkRate) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            LabeledContent(label, value: rate.label).font(.subheadline)
            if let interval = rate.interval {
                Text(String(format: "95%% trial interval %.0f–%.0f%%; not participant-level certainty", interval.lowerBound * 100, interval.upperBound * 100))
                    .font(.caption2).foregroundStyle(.secondary)
            }
        }
    }
}

private struct CaptureEvidencePanel: View {
    @ObservedObject var tracker: CameraPoseTracker
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Capture evidence · not performance certification").font(.caption.bold())
            if let capture = tracker.captureConfiguration {
                Text("\(capture.cameraName) · \(capture.configuredFPS) fps configured")
                    .font(.caption.monospacedDigit())
                Text("\(capture.formatWidth)×\(capture.formatHeight) format · \(Int(capture.nominalFieldOfView))° nominal field of view")
                    .font(.caption.monospacedDigit())
                if let reason = capture.fallbackReason { Text(reason).font(.caption).foregroundStyle(.orange) }
            } else { Text("Start the camera to inspect actual hardware.").font(.caption) }
            if let error = tracker.configurationError { Text(error).foregroundStyle(.orange).font(.caption) }
            if tracker.sourceRecordingActive {
                Label("Source video recording / finishing", systemImage: "record.circle.fill").foregroundStyle(.red).font(.caption.bold())
            }
            if let error = tracker.sourceRecordingError { Text("Video: \(error)").foregroundStyle(.orange).font(.caption) }
        }.labCard().accessibilityIdentifier("labCaptureEvidence")
    }
}

private struct DisplayLatencyEntry: View {
    let lab: PracticeLab
    @State private var expanded = false
    @State private var destination: DisplayLatencyMeasurement.Destination = .phone
    @State private var clip = ""
    @State private var fps = "240"
    @State private var motion = ""
    @State private var response = ""
    @State private var conditions = ""
    @State private var error: String?
    var body: some View {
        DisclosureGroup("External display-latency measurements (\(lab.displayLatencyMeasurements.count))", isExpanded: $expanded) {
            VStack(alignment: .leading, spacing: 10) {
                Text("Use a separate high-speed camera to film the moving player and screen together. Enter the frame of the physical event and its first visible response. This is manual evidence, not automatic certification. Do not enter detector pipeline timing.")
                    .font(.caption).foregroundStyle(.secondary)
                Picker("Display", selection: $destination) {
                    ForEach(DisplayLatencyMeasurement.Destination.allCases) { Text($0.title).tag($0) }
                }
                TextField("Reference clip / event ID", text: $clip)
                TextField("Recording FPS", text: $fps).keyboardType(.decimalPad)
                TextField("Physical event frame number", text: $motion).keyboardType(.numberPad)
                TextField("First screen-response frame number", text: $response).keyboardType(.numberPad)
                TextField("Receiver model, network / cable, game mode", text: $conditions)
                Button("Add measured sample") {
                    guard let rate = Double(fps), let start = Int(motion), let end = Int(response),
                          lab.addDisplayLatency(.init(id: UUID(), destination: destination,
                            referenceClip: String(clip.prefix(160)), recordingFPS: rate, motionFrame: start,
                            responseFrame: end, receiverAndConditions: String(conditions.prefix(240)))) else {
                        error = "Enter a clip ID, receiver conditions, valid FPS and ordered frame numbers. Limit: 200 samples."
                        return
                    }
                    error = nil; motion = ""; response = ""
                }.disabled(lab.active != nil)
                if let error { Text(error).font(.caption).foregroundStyle(.orange) }
                ForEach(DisplayLatencyMeasurement.Destination.allCases) { route in
                    let values = lab.displayLatencyMeasurements.filter { $0.destination == route }.compactMap(\.milliseconds)
                    if let p95 = BenchmarkSummary.percentile(values, fraction: 0.95) {
                        Text(String(format: "%@: %d samples · p95 %.1f ms (manually annotated)", route.title, values.count, p95))
                            .font(.caption.monospacedDigit())
                    }
                }
            }.padding(.top, 8)
        }.labCard().accessibilityIdentifier("labDisplayLatency")
    }
}

private struct SourceClipLibrary: View {
    @ObservedObject var tracker: CameraPoseTracker
    @State private var pendingDelete: SourceVideoClip?
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Local source-video library").font(.headline)
            Text("\(tracker.sourceClips.count)/50 clips · never automatically uploaded").font(.caption).foregroundStyle(.secondary)
            ForEach(tracker.sourceClips) { clip in
                VStack(alignment: .leading, spacing: 5) {
                    Text(clip.createdAt.formatted(date: .abbreviated, time: .shortened)).font(.caption.bold())
                    Text("Trial \(clip.id.uuidString.prefix(8)) · \(clip.writtenFrames) frames · \(clip.writerDroppedFrames) writer drops").font(.caption)
                    Text(clip.endedReason).font(.caption2).foregroundStyle(.secondary)
                    HStack {
                        if let urls = try? SourceVideoRecorder.urls(for: clip.id) {
                            ShareLink(items: urls.filter { FileManager.default.fileExists(atPath: $0.path) }) {
                                Label("Share video + evidence", systemImage: "square.and.arrow.up")
                            }
                        }
                        Spacer()
                        Button("Delete", role: .destructive) { pendingDelete = clip }
                    }.font(.caption).disabled(tracker.sourceRecordingActive)
                }.padding(.vertical, 6)
            }
        }.labCard()
        .confirmationDialog("Delete this local clip and its evidence? This cannot be undone. Shared copies are not removed.",
            isPresented: Binding(get: { pendingDelete != nil }, set: { if !$0 { pendingDelete = nil } }), titleVisibility: .visible) {
                Button("Delete local clip", role: .destructive) {
                    if let clip = pendingDelete { tracker.deleteSourceClip(clip.id) }
                    pendingDelete = nil
                }
            }
    }
}

private struct LabCameraPanel: View {
    @ObservedObject var camera: CameraSwingController
    let wanted: Bool
    var body: some View {
        ZStack {
            Color.black
            if wanted {
                CameraPreview(session: camera.tracker.session, videoRotationAngle: camera.tracker.videoRotationAngle,
                    isVideoMirrored: camera.tracker.isVideoMirrored, videoGravity: .resizeAspect)
                PoseSkeletonView(frame: camera.frame, frameAspect: camera.tracker.frameAspect, contentMode: .fit)
                if let address = camera.displayAddress {
                    CameraClubOverlay(frame: camera.frame, address: address, frameAspect: camera.tracker.frameAspect,
                        swingAngle: camera.swingAngle, handedness: camera.handedness,
                        linedUp: camera.hasPlayableTracking, strike: camera.lastStrike,
                        strikeOffset: camera.lastStrikeOffset, virtualClub: camera.virtualClub)
                }
            }
            if !wanted || camera.frame == nil {
                VStack(spacing: 8) {
                    Image(systemName: "figure.golf").font(.largeTitle).foregroundStyle(.mint)
                    Text(message).font(.subheadline).multilineTextAlignment(.center)
                }.padding(24)
            }
            if wanted, camera.frame != nil {
                VStack {
                    Spacer()
                    Text(camera.readiness.title)
                        .font(.caption.bold()).padding(8)
                        .background(.black.opacity(0.75), in: Capsule())
                        .foregroundStyle(camera.hasPlayableTracking ? .mint : .white)
                }.padding(8)
            }
        }.frame(height: 220).clipShape(RoundedRectangle(cornerRadius: 18))
    }
    private var message: String {
        guard wanted else { return "Start the camera to measure a real swing.\nNo camera needed to try a synthetic replay." }
        switch camera.status {
        case .denied: return "Camera access denied. Enable Camera in iPhone Settings to run real trials."
        case .unavailable(let reason): return reason
        case .requestingPermission: return "Waiting for camera permission…"
        default: return "Step back until shoulders and hands are visible. Keep only one player in frame."
        }
    }
}

private extension View {
    func labCard() -> some View {
        padding(16).frame(maxWidth: .infinity, alignment: .leading)
            .background(.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 16))
    }
}

struct BenchmarkDocument: FileDocument {
    static var readableContentTypes: [UTType] { [.json] }
    let data: Data
    init(data: Data) { self.data = data }
    init(configuration: ReadConfiguration) throws { data = configuration.file.regularFileContents ?? Data() }
    func fileWrapper(configuration: WriteConfiguration) throws -> FileWrapper { FileWrapper(regularFileWithContents: data) }
}

private struct BenchmarkReplayView: View {
    let trial: BenchmarkTrial
    @Environment(\.dismiss) private var dismiss
    @State private var index = 0.0
    @State private var replayMatches: Bool?
    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Text(trial.source == .synthetic ? "SYNTHETIC — NOT CAMERA VALIDATION" : "RECORDED POSE — NOT VIDEO")
                        .font(.caption.bold()).foregroundStyle(.mint)
                    if let pose = currentPose {
                        PoseSkeletonView(frame: pose.frame, frameAspect: pose.aspect, contentMode: .fit)
                            .frame(height: 280).background(.black, in: RoundedRectangle(cornerRadius: 16))
                        Text(String(format: "Frame %d/%d · %.3fs%@", Int(index) + 1, trial.poses.count,
                            pose.time, pose.frame == nil ? " · tracking missing" : "")).font(.caption.monospacedDigit())
                        Slider(value: $index, in: 0...Double(max(1, trial.poses.count - 1)), step: 1)
                            .accessibilityLabel("Recorded pose frame").accessibilityIdentifier("labReplayScrubber")
                    }
                    Text("Detector replay: \(replayMatches.map { $0 ? "matches recorded output" : "DIFFERS or incomplete" } ?? "checking…")")
                        .font(.headline).accessibilityIdentifier("labReplayResult")
                    Text("\(trial.impacts.count) recorded impacts. Reprocessing starts with a fresh detector and includes missing frames, original aspect, timestamps and handedness.")
                    Text("A matching replay proves repeatability of this software—not physical swing accuracy.").font(.caption).foregroundStyle(.secondary)
                }.padding(20)
            }
            .navigationTitle("Pose replay").navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
        }.task { replayMatches = BenchmarkReplay.matches(trial) }
    }
    private var currentPose: BenchmarkPose? {
        guard !trial.poses.isEmpty else { return nil }
        return trial.poses[min(Int(index), trial.poses.count - 1)]
    }
}
