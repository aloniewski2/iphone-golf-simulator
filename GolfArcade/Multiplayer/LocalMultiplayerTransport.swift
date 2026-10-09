import Foundation
import Network

@MainActor
final class LocalMultiplayerTransport: MultiplayerTransport {
    struct DiscoveredLobby: Identifiable { var id: String; var name: String; var endpoint: NWEndpoint; var sport: String = "tennis"; var players: Int = 1 }
    let localID = UUID().uuidString
    var peers: [String] { Array(connections.keys) }
    var onData: ((Data, String) -> Void)?
    var onPeersChanged: (() -> Void)?
    var onError: ((Error) -> Void)?
    var onDiscovery: (([DiscoveredLobby]) -> Void)?
    private var listener: NWListener?
    private var browser: NWBrowser?
    private var connections: [String: NWConnection] = [:]
    private var pending: [UUID: NWConnection] = [:]
    private let queue = DispatchQueue(label: "MotionClub.multiplayer.local")
    private let service = "_motionparty._tcp"
    private var advertisedHost = "Friends"
    private var advertisedMetadata = ""
    private var registered = false
    private var pendingMetadata: (String,Int)?
    private var host = false
    private var generation = 0
    private var sending: Set<String> = []
    private var outbound: [String: [Data]] = [:]
    private var latest: [String: Data] = [:]
    private struct Hello: Codable { var id: String; var version: Int; var token: String; var reason: String? }
    private var token = ""
    var joinCode: String { Self.joinCode(for: token) }
    static func joinCode(for token: String) -> String {
        String(token.replacingOccurrences(of: "-", with: "").prefix(6)).uppercased()
    }

    static func advertisedName(_ name: String, token: String) -> String {
        var prefix = ""
        for character in name.replacingOccurrences(of: "~", with: " ") {
            if prefix.utf8.count + String(character).utf8.count > 26 { break }
            prefix.append(character)
        }
        return (prefix.isEmpty ? "Friends" : prefix) + "~" + token
    }

    private func parameters() -> NWParameters {
        let tcp = NWProtocolTCP.Options(); tcp.noDelay = true
        let p = NWParameters(tls: nil, tcp: tcp); p.includePeerToPeer = true
        return p
    }
    func host(name: String) throws {
        disconnect(); host = true; advertisedHost = name; token = UUID().uuidString
        let listener = try NWListener(using: parameters())
        advertisedMetadata = "tennis-1"
        listener.service = NWListener.Service(name: Self.advertisedName(name, token: token), type: service, txtRecord:NWTXTRecord(["sport":"tennis","players":"1","version":String(MultiplayerPacket.version)]).data)
        listener.serviceRegistrationUpdateHandler = { [weak self] change in
            Task { @MainActor in
                guard let self else { return }
                if case .add = change { self.registered = true; if let (sport,players) = self.pendingMetadata { self.pendingMetadata = nil; self.advertise(sport:sport,players:players) } }
                #if DEBUG
                OnlineLobbyProofDriver.event("advertisement",String(describing:change))
                #endif
            }
        }
        let epoch = generation
        listener.newConnectionHandler = { [weak self] c in Task { @MainActor in guard self?.generation == epoch else { c.cancel(); return }; self?.connect(c) } }
        listener.stateUpdateHandler = { [weak self] state in
            #if DEBUG
            Task { @MainActor in OnlineLobbyProofDriver.event("listener",String(describing:state)) }
            #endif
            if case .failed(let error) = state { Task { @MainActor in self?.onError?(error) } }
        }
        self.listener = listener; listener.start(queue: queue)
    }
    func advertise(sport: String, players: Int) {
        guard host, let listener else { return }; let key = "\(sport)-\(players)"
        guard key != advertisedMetadata else { return }
        guard registered else { pendingMetadata = (sport,players); return }; advertisedMetadata = key
        listener.service = NWListener.Service(name: Self.advertisedName(advertisedHost, token: token), type: service, txtRecord: NWTXTRecord(["sport":sport,"players":String(players),"version":String(MultiplayerPacket.version)]).data)
    }
    func browse() {
        browser?.cancel()
        let b = NWBrowser(for: .bonjourWithTXTRecord(type: service, domain: nil), using: parameters())
        b.browseResultsChangedHandler = { [weak self] results, _ in
            let lobbies = results.compactMap { result -> DiscoveredLobby? in
                guard case .service(let name, _, _, _) = result.endpoint else { return nil }
                let txt: NWTXTRecord? = { if case .bonjour(let txt) = result.metadata { return txt }; return nil }()
                return DiscoveredLobby(id: name, name: name.components(separatedBy: "~").first ?? name, endpoint: result.endpoint, sport: txt?["sport"] ?? "tennis", players: max(1,min(4,Int(txt?["players"] ?? "1") ?? 1)))
            }.sorted { $0.name < $1.name }
            Task { @MainActor in self?.onDiscovery?(lobbies) }
        }
        b.stateUpdateHandler = { [weak self] state in
            #if DEBUG
            Task { @MainActor in OnlineLobbyProofDriver.event("browser",String(describing:state)) }
            #endif
            if case .failed(let e) = state { Task { @MainActor in self?.onError?(e) } } }
        browser = b; b.start(queue: queue)
    }
    func join(_ lobby: DiscoveredLobby) throws {
        guard connections.isEmpty, pending.isEmpty, listener == nil else { throw MultiplayerError.invalidOperation("Leave the current lobby first.") }
        guard let secret = lobby.id.components(separatedBy: "~").last, UUID(uuidString: secret) != nil else { throw MultiplayerError.incompatible }
        browser?.cancel(); browser = nil; host = false; token = secret
        connect(NWConnection(to: lobby.endpoint, using: parameters()))
    }
    private func connect(_ c: NWConnection) {
        guard pending.count + connections.count < (host ? 3 : 1) else {
            guard host else { c.cancel(); return }
            c.stateUpdateHandler = { [weak self] state in
                if case .ready = state { Task { @MainActor in
                    guard let self, let data = try? JSONEncoder().encode(Hello(id:self.localID,version:MultiplayerPacket.version,token:self.token,reason:MultiplayerError.full.localizedDescription)) else { c.cancel(); return }
                    self.write(data,connection:c) { c.cancel() }
                } }
            }; c.start(queue:queue); return
        }
        let key = UUID(), epoch = generation; pending[key] = c
        c.stateUpdateHandler = { [weak self] state in
            Task { @MainActor in
                guard let self, epoch == self.generation else { return }
                switch state {
                case .ready:
                    guard let data = try? JSONEncoder().encode(Hello(id: self.localID, version: MultiplayerPacket.version, token: self.token)) else { return }
                    self.write(data, connection: c) { }
                    self.readHello(c, key: key, epoch: epoch)
                case .failed(let error): self.remove(c, key: key); if !self.host { self.onError?(error) }
                case .cancelled: self.remove(c, key: key); if !self.host { self.onError?(MultiplayerError.unavailable("The local lobby closed the connection.")) }
                default: break
                }
            }
        }
        c.start(queue: queue)
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(10))
            guard let self, self.generation == epoch, self.pending[key] != nil else { return }
            c.cancel(); self.remove(c, key: key); self.onError?(MultiplayerError.unavailable("Local lobby connection timed out. Check that both phones are on the same Wi-Fi network."))
        }
    }
    private func readHello(_ c: NWConnection, key: UUID, epoch: Int) {
        read(c) { [weak self] data in
            guard let self, self.generation == epoch, let data, let hello = try? JSONDecoder().decode(Hello.self, from: data) else { c.cancel(); return }
            if let reason = hello.reason { self.onError?(MultiplayerError.unavailable(reason)); c.cancel(); return }
            guard hello.version == MultiplayerPacket.version else { self.onError?(MultiplayerError.incompatible); c.cancel(); return }
            guard hello.token == self.token, UUID(uuidString: hello.id) != nil,
                  hello.id != self.localID, self.connections[hello.id] == nil else { c.cancel(); return }
            self.pending.removeValue(forKey: key); self.connections[hello.id] = c; self.onPeersChanged?(); self.readPacket(c, id: hello.id, epoch: epoch)
        }
    }
    private func readPacket(_ c: NWConnection, id: String, epoch: Int) {
        read(c) { [weak self] data in
            guard let self, self.generation == epoch, self.connections[id] === c else { return }
            guard let data else { c.cancel(); return }
            self.onData?(data, id); self.readPacket(c, id: id, epoch: epoch)
        }
    }
    /// TCP has no message boundaries. Read the exact header, then a bounded body.
    private func read(_ c: NWConnection, completion: @escaping @MainActor (Data?) -> Void) {
        c.receive(minimumIncompleteLength: 4, maximumLength: 4) { data, _, complete, error in
            guard error == nil, !complete, let data, data.count == 4 else { Task { @MainActor in completion(nil) }; return }
            let n = data.reduce(0) { ($0 << 8) | Int($1) }
            guard n > 0, n <= MultiplayerPacket.maximumBytes else { c.cancel(); Task { @MainActor in completion(nil) }; return }
            c.receive(minimumIncompleteLength: n, maximumLength: n) { body, _, done, error in
                Task { @MainActor in completion(error == nil && !done && body?.count == n ? body : nil) }
            }
        }
    }
    private func write(_ data: Data, connection: NWConnection, completion: @escaping @MainActor () -> Void) {
        let n = UInt32(data.count)
        var framed = Data([UInt8((n >> 24) & 255), UInt8((n >> 16) & 255), UInt8((n >> 8) & 255), UInt8(n & 255)]); framed.append(data)
        connection.send(content: framed, completion: .contentProcessed { [weak self] error in
            Task { @MainActor in if let error { if self?.host == true { connection.cancel() } else { self?.onError?(error) } }; completion() }
        })
    }
    func send(_ data: Data, to ids: [String]?, reliable: Bool) throws {
        guard data.count <= MultiplayerPacket.maximumBytes else { throw MultiplayerError.invalidOperation("Network packet is too large.") }
        for id in ids ?? peers {
            guard connections[id] != nil else { continue }
            if reliable {
                guard (outbound[id]?.count ?? 0) < 64 else { throw MultiplayerError.unavailable("A local peer is not keeping up.") }
                outbound[id, default: []].append(data)
            } else { latest[id] = data } // Coalesce snapshots under backpressure, never grow indefinitely.
            flush(id)
        }
    }
    private func flush(_ id: String) {
        guard !sending.contains(id), let c = connections[id] else { return }
        let data: Data?
        if !(outbound[id]?.isEmpty ?? true) { data = outbound[id]?.removeFirst() }
        else { data = latest.removeValue(forKey: id) }
        guard let data else { return }; sending.insert(id)
        let epoch = generation
        write(data, connection: c) { [weak self] in guard let self, self.generation == epoch else { return }; self.sending.remove(id); self.flush(id) }
    }
    private func remove(_ c: NWConnection, key: UUID) {
        pending.removeValue(forKey: key)
        for id in Array(connections.keys) where connections[id] === c { connections.removeValue(forKey: id); outbound.removeValue(forKey: id); latest.removeValue(forKey: id); sending.remove(id) }
        onPeersChanged?()
    }
    func disconnect() {
        generation += 1; registered = false; pendingMetadata = nil; advertisedMetadata = ""; browser?.cancel(); browser = nil; listener?.cancel(); listener = nil
        let all = Array(connections.values) + Array(pending.values); connections.removeAll(); pending.removeAll()
        outbound.removeAll(); latest.removeAll(); sending.removeAll(); all.forEach { $0.cancel() }; onPeersChanged?()
    }
}
