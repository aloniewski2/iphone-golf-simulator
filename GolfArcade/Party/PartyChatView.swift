import SwiftUI

/// The party chat: type or talk, only to the phones in your own party. Quick phrases for everyone; typing and voice once the player has
/// said they are 13+, accepted the rules and Game Center allows it. Report, Mute and Block sit next to every person.
struct PartyChatView: View {
    var chat = PartyChat.shared
    var voice = PartyVoiceCoordinator.shared
    var recording = RecordingService.shared
    var dismiss: () -> Void = {}
    @State private var draft = ""
    @State private var showSettings = false
    @State private var showMembers = false
    @State private var showSetup = false
    @FocusState private var typing: Bool

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                if let name = chat.someoneRecording { recordingBanner("\(name) is recording this session") }
                if !chat.channel.inParty { emptyState }
                else {
                    voiceBar
                    messageList
                    quickRow
                    composer
                }
            }
            .background(LinearGradient(colors: [Color(hex: "16294A"), IslandUI.dark], startPoint: .top, endPoint: .bottom).ignoresSafeArea())
            .navigationTitle("Party chat").navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button { showSettings = true } label: { Image(systemName: "gearshape.fill") }.accessibilityLabel("Chat settings").accessibilityIdentifier("party-chat-settings") }
                ToolbarItem(placement: .principal) { Text("Party chat").font(IslandUI.font(17, bold: true)) }
                ToolbarItemGroup(placement: .confirmationAction) {
                    Button { showMembers = true } label: { Image(systemName: "person.2.fill") }.accessibilityLabel("People in your party").accessibilityIdentifier("party-chat-members")
                    Button("Done") { dismiss() }.accessibilityIdentifier("party-chat-done")
                }
            }
            .toolbarBackground(Color(hex: "16294A"), for: .navigationBar).toolbarBackground(.visible, for: .navigationBar)
        }
        .preferredColorScheme(.dark).foregroundStyle(.white)
        .onAppear { chat.chatOpen = true; chat.safety.refresh() }
        .onDisappear { chat.chatOpen = false }
        .sheet(isPresented: $showSettings) { PartySettingsView(dismiss: { showSettings = false }).presentationDetents([.large]) }
        .sheet(isPresented: $showMembers) { PartyMembersView(dismiss: { showMembers = false }).presentationDetents([.medium, .large]) }
        .sheet(isPresented: $showSetup) { PartySetupView(dismiss: { showSetup = false }).presentationDetents([.large]) }
        .accessibilityIdentifier("party-chat")
    }

    // MARK: pieces

    private func recordingBanner(_ text: String) -> some View {
        HStack(spacing: 8) { Circle().fill(.red).frame(width: 10, height: 10); Text(text).font(IslandUI.font(13, bold: true)); Spacer() }
            .padding(.horizontal, 16).padding(.vertical, 8).background(Color.red.opacity(0.22))
            .accessibilityIdentifier("party-recording-banner")
    }

    private var emptyState: some View {
        VStack(spacing: 14) {
            Image(systemName: "person.2.wave.2.fill").font(.system(size: 44)).foregroundStyle(IslandUI.lime)
            Text("Chat is for your party").font(IslandUI.font(22, bold: true))
            Text("Invite a friend from Friends. Once they join, you can type or talk here. Only the people in your party can see or hear you.")
                .font(IslandUI.font(15)).foregroundStyle(.white.opacity(0.7)).multilineTextAlignment(.center)
            Button { dismiss(); ClubHub.shared.hubOpen = true } label: {
                Text("Open Friends").font(IslandUI.font(17, bold: true)).foregroundStyle(IslandUI.navy).padding(.horizontal, 28).frame(minHeight: 48).background(IslandUI.lime, in: Capsule())
            }.accessibilityIdentifier("party-chat-open-friends")
        }.padding(32).frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var voiceBar: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 12) {
                VoiceMicButton(voice: voice, big: true, onNeedsSetup: { showSetup = true })
                VStack(alignment: .leading, spacing: 2) {
                    Text(voice.statusTitle).font(IslandUI.font(15, bold: true)).accessibilityIdentifier("party-voice-status")
                    Text(voice.statusDetail).font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.6)).lineLimit(2)
                }
                Spacer()
                MicLevelMeter(level: voice.micLevel, live: voice.isTransmitting)
            }
            if !voice.speakingNames.isEmpty {
                HStack(spacing: 6) {
                    Image(systemName: "speaker.wave.2.fill").font(.system(size: 12)).foregroundStyle(IslandUI.lime)
                    Text(voice.speakingNames.joined(separator: ", ")).font(IslandUI.font(13, bold: true)).foregroundStyle(IslandUI.lime)
                }.accessibilityIdentifier("party-voice-speaking")
            }
        }
        .padding(12).background(.white.opacity(0.07), in: RoundedRectangle(cornerRadius: 16, style: .continuous)).padding(.horizontal, 14).padding(.top, 10)
    }

    private var messageList: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(spacing: 8) {
                    if chat.messages.isEmpty { Text("Say hi! Nothing here is saved: messages disappear when the party ends.").font(IslandUI.font(13)).foregroundStyle(.white.opacity(0.5)).multilineTextAlignment(.center).padding(.top, 30) }
                    ForEach(chat.messages) { m in bubble(m).id(m.id) }
                }.padding(14)
            }
            .onChange(of: chat.messages.count) { _, _ in if let last = chat.messages.last { withAnimation { proxy.scrollTo(last.id, anchor: .bottom) } } }
        }
    }

    @ViewBuilder private func bubble(_ m: PartyChatMessage) -> some View {
        if m.kind == .system {
            Text(m.text).font(IslandUI.font(12)).foregroundStyle(.white.opacity(0.5)).frame(maxWidth: .infinity)
        } else {
            VStack(alignment: m.mine ? .trailing : .leading, spacing: 3) {
                if !m.mine { Text(m.senderName).font(IslandUI.font(11, bold: true)).foregroundStyle(IslandUI.lime) }
                Text(m.text).font(IslandUI.font(m.kind == .quick ? 17 : 16, bold: m.kind == .quick)).padding(.horizontal, 12).padding(.vertical, 8)
                    .background(m.mine ? IslandUI.lime : Color.white.opacity(0.14), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                    .foregroundStyle(m.mine ? IslandUI.navy : .white)
                    .contextMenu { if !m.mine { reportMenu(m.senderID) } }
                if m.filtered { Text("Some words were hidden").font(IslandUI.font(10)).foregroundStyle(.white.opacity(0.4)) }
            }.frame(maxWidth: .infinity, alignment: m.mine ? .trailing : .leading)
        }
    }

    @ViewBuilder private func reportMenu(_ id: String) -> some View {
        Button { chat.mute(id) } label: { Label("Mute \(chat.name(of: id))", systemImage: "speaker.slash") }
        Button(role: .destructive) { chat.block(id) } label: { Label("Block \(chat.name(of: id))", systemImage: "hand.raised.fill") }
        ForEach(PartyReport.Reason.allCases.prefix(4), id: \.self) { reason in
            Button { sendReport(id, reason) } label: { Label("Report: \(reason.rawValue)", systemImage: "exclamationmark.bubble") }
        }
    }

    private func sendReport(_ id: String, _ reason: PartyReport.Reason) {
        let report = chat.report(id, reason: reason, voice: false)
        if let url = PartyContact.reportURL(report) { UIApplication.shared.open(url) }
        chat.say("Report ready: send the message to finish. \(chat.name(of: id)) is muted.")
    }

    private var quickRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                ForEach(QuickChat.phrases, id: \.id) { p in
                    Button { chat.sendQuick(p.id) } label: {
                        Text(p.text).font(IslandUI.font(14, bold: true)).padding(.horizontal, 14).frame(minHeight: 38).background(.white.opacity(0.14), in: Capsule())
                    }.accessibilityIdentifier("party-quick-\(p.id)")
                }
            }.padding(.horizontal, 14)
        }.padding(.vertical, 8)
    }

    private var composer: some View {
        VStack(spacing: 6) {
            if !chat.notice.isEmpty { Text(chat.notice).font(IslandUI.font(12, bold: true)).foregroundStyle(IslandUI.lime).onTapGesture { chat.clearNotice() } }
            if let reason = chat.safety.access.reason {
                HStack(spacing: 8) {
                    Image(systemName: "lock.fill").font(.system(size: 13))
                    Text(reason).font(IslandUI.font(12)).lineLimit(3)
                    Spacer(minLength: 0)
                    if case .quickOnly = chat.safety.access, chat.safety.restrictions == GameCenterRestrictions(), chat.safety.ageBand != .under13 {
                        Button("Set up") { showSetup = true }.font(IslandUI.font(13, bold: true)).foregroundStyle(IslandUI.lime).accessibilityIdentifier("party-chat-setup")
                    }
                }.foregroundStyle(.white.opacity(0.7)).padding(.horizontal, 16)
            }
            HStack(spacing: 10) {
                TextField("Message your party", text: $draft, axis: .vertical).lineLimit(1...3).focused($typing)
                    .textInputAutocapitalization(.sentences).submitLabel(.send).onSubmit(send)
                    .font(IslandUI.font(16)).padding(.horizontal, 14).padding(.vertical, 10)
                    .background(.white.opacity(0.12), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                    .disabled(!chat.safety.access.canType).opacity(chat.safety.access.canType ? 1 : 0.4)
                    .onChange(of: draft) { _, v in if v.count > ChatFilter.maxLength { draft = String(v.prefix(ChatFilter.maxLength)) } }
                    .accessibilityIdentifier("party-chat-field")
                Button(action: send) {
                    Image(systemName: "arrow.up.circle.fill").font(.system(size: 34)).foregroundStyle(IslandUI.lime)
                }.disabled(!chat.safety.access.canType || draft.trimmingCharacters(in: .whitespaces).isEmpty).opacity(chat.safety.access.canType ? 1 : 0.3)
                    .accessibilityLabel("Send").accessibilityIdentifier("party-chat-send")
            }.padding(.horizontal, 14).padding(.bottom, 10)
        }
    }

    private func send() {
        switch chat.send(text: draft) {
        case .sent: draft = ""
        case .tooFast: chat.say("Slow down a little.")
        case .notAllowed(let why): chat.say(why)
        case .noParty: chat.say("Join or invite a friend first.")
        case .empty: break
        }
    }
}

/// The mic button used in the chat, the dock and the settings: hold to talk, or tap to mute / unmute in open-mic mode. The red state
/// is the visible "microphone is live" indicator Apple requires.
struct VoiceMicButton: View {
    var voice: PartyVoiceCoordinator
    var big = false
    var onNeedsSetup: () -> Void = {}
    @State private var holding = false
    var body: some View {
        let size: CGFloat = big ? 52 : 38
        ZStack {
            Circle().fill(voice.isTransmitting ? Color.red : voice.isLive ? Color.white.opacity(0.18) : Color.white.opacity(0.1))
            Image(systemName: icon).font(.system(size: size * 0.42, weight: .bold)).foregroundStyle(.white)
        }
        .frame(width: size, height: size)
        .overlay(Circle().strokeBorder(voice.isTransmitting ? Color.white.opacity(0.9) : .clear, lineWidth: 2))
        .contentShape(Circle())
        .gesture(DragGesture(minimumDistance: 0)
            .onChanged { _ in
                guard voice.prefs.mode == .pushToTalk, voice.isLive, !holding else { return }
                holding = true; voice.pushToTalk(true)
            }
            .onEnded { _ in
                if holding { holding = false; voice.pushToTalk(false); return }
                tap()
            })
        .accessibilityElement().accessibilityLabel(label).accessibilityAddTraits(.isButton).accessibilityIdentifier("party-mic")
    }
    private var icon: String { !voice.isLive ? "mic.slash.fill" : voice.selfMuted ? "mic.slash.fill" : "mic.fill" }
    private var label: String {
        if !voice.isLive { return "Turn on voice chat" }
        return voice.prefs.mode == .pushToTalk ? "Hold to talk" : voice.selfMuted ? "Unmute microphone" : "Mute microphone"
    }
    private func tap() {
        if !voice.isLive { Task { if !(await voice.turnOn()) { onNeedsSetup() } } }
        else if voice.prefs.mode == .openMic { voice.toggleMute() }
    }
}

struct MicLevelMeter: View {
    var level: Float
    var live: Bool
    var body: some View {
        HStack(spacing: 2) {
            ForEach(0..<8, id: \.self) { i in
                RoundedRectangle(cornerRadius: 1.5).fill(Float(i) / 8 < level ? (live ? Color.red : IslandUI.lime) : Color.white.opacity(0.15)).frame(width: 4, height: CGFloat(6 + i * 2))
            }
        }.frame(height: 22, alignment: .bottom).accessibilityHidden(true)
    }
}

/// Everyone in the party: who is talking, per-person volume, Mute, Block and Report.
struct PartyMembersView: View {
    var chat = PartyChat.shared
    var voice = PartyVoiceCoordinator.shared
    var dismiss: () -> Void = {}
    @State private var reporting: String?
    var body: some View {
        NavigationStack {
            List {
                if chat.members.isEmpty { Text("Nobody else is in your party right now.").foregroundStyle(.secondary) }
                ForEach(chat.members, id: \.id) { m in
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Image(systemName: voice.speakingIDs.contains(m.id) ? "speaker.wave.2.fill" : "person.fill").foregroundStyle(voice.speakingIDs.contains(m.id) ? IslandUI.lime : .secondary)
                            Text(m.name).font(.headline)
                            if chat.recording[m.id] != nil { Label("Recording", systemImage: "record.circle").font(.caption.bold()).foregroundStyle(.red) }
                            Spacer()
                            Button(chat.isMuted(m.id) ? "Unmute" : "Mute") { chat.isMuted(m.id) ? chat.unmute(m.id) : chat.mute(m.id); voice.syncPeers() }
                                .buttonStyle(.bordered).accessibilityIdentifier("party-member-mute-\(m.id)")
                        }
                        if voice.isLive, !chat.isMuted(m.id) {
                            HStack { Image(systemName: "speaker.fill").font(.caption); Slider(value: Binding(get: { Double(voice.prefs.perPeerVolume[m.id] ?? 1) }, set: { voice.setVolume(Float($0), for: m.id) }), in: 0...1.5); Image(systemName: "speaker.wave.3.fill").font(.caption) }
                        }
                        HStack(spacing: 16) {
                            Button("Report") { reporting = m.id }.foregroundStyle(.orange).accessibilityIdentifier("party-member-report-\(m.id)")
                            Button("Block", role: .destructive) { chat.block(m.id); voice.syncPeers() }.accessibilityIdentifier("party-member-block-\(m.id)")
                        }.font(.subheadline.bold())
                    }.padding(.vertical, 4)
                }
                Section { Text("Only people in your party can read or hear you. Blocked and muted people can't reach you here, and blocking also stops their friend invites.").font(.footnote).foregroundStyle(.secondary) }
            }
            .navigationTitle("Your party").navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done", action: dismiss) } }
            .confirmationDialog("Why are you reporting \(reporting.map { chat.name(of: $0) } ?? "them")?", isPresented: Binding(get: { reporting != nil }, set: { if !$0 { reporting = nil } }), titleVisibility: .visible) {
                ForEach(PartyReport.Reason.allCases, id: \.self) { reason in
                    Button(reason.rawValue) {
                        if let id = reporting {
                            let report = chat.report(id, reason: reason, voice: voice.isLive)
                            if let url = PartyContact.reportURL(report) { UIApplication.shared.open(url) }
                            voice.syncPeers()
                        }
                        reporting = nil
                    }
                }
                Button("Cancel", role: .cancel) { reporting = nil }
            } message: { Text("A message with their name and recent chat opens for you to send. They are muted for you right away.") }
        }
    }
}

/// First use of typing or voice: the age question (only a birth year, kept on this phone) and the chat rules.
struct PartySetupView: View {
    var safety = PartySafety.shared
    var dismiss: () -> Void = {}
    @State private var year = Calendar.current.component(.year, from: Date()) - 18
    @State private var message = ""
    private var thisYear: Int { Calendar.current.component(.year, from: Date()) }
    var body: some View {
        NavigationStack {
            Form {
                if safety.ageBand == .unknown || safety.ageBand == .under13 {
                    Section {
                        Picker("Birth year", selection: $year) { ForEach((thisYear - 100...thisYear).reversed(), id: \.self) { Text(String($0)).tag($0) } }
                            .pickerStyle(.wheel).frame(height: 130).accessibilityIdentifier("party-age-picker")
                        Button("Continue") { if !safety.setBirthYear(year) { message = safety.ageLocked ? "You can change this again in a week." : "Pick your birth year." } }
                            .accessibilityIdentifier("party-age-continue")
                    } header: { Text("How old are you?") } footer: {
                        Text("Typing and voice chat are for ages 13 and up. Your birth year stays on this phone: it is never uploaded or shared.")
                    }
                    if safety.ageBand == .under13 {
                        Section { Label("Quick phrases are always on. Typing and voice unlock at 13.", systemImage: "hand.wave.fill") }
                    }
                } else {
                    Section("Chat rules") {
                        rule("heart.fill", "Be kind. No bullying, hate or threats.")
                        rule("person.crop.circle.badge.exclamationmark", "Never share your phone number, address, school or other apps' names.")
                        rule("mic.fill", "Voice is live and only goes to your party. Motion Club does not record or keep it, but anyone can record the TV and they will tell you when they do.")
                        rule("hand.raised.fill", "Report, Mute and Block are next to every person. Use them.")
                        rule("eye.slash.fill", "Bad language is hidden by default (always for teens).")
                    }
                    Section {
                        Button { safety.acceptRules(); dismiss() } label: { Text("I agree: unlock typing and voice").frame(maxWidth: .infinity).font(.headline) }
                            .accessibilityIdentifier("party-rules-accept")
                    }
                }
                if !message.isEmpty { Section { Text(message).foregroundStyle(.orange) } }
            }
            .navigationTitle("Party chat setup").navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .cancellationAction) { Button("Not now", action: dismiss) } }
        }
        .accessibilityIdentifier("party-setup")
    }
    private func rule(_ icon: String, _ text: String) -> some View { Label(text, systemImage: icon).font(.subheadline).labelStyle(.titleAndIcon) }
}
