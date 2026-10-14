import Foundation

/// The Club Hub wire protocol, v1 (club-hub/PROTOCOL.md is the contract; change it there first). Small JSON messages over one
/// WebSocket: who is online, invites, and a friends' party's own traffic (the relay room: the lobby, the Plaza, the match).

enum HubPresenceState: String, Codable, Sendable { case plaza, lobby, match, away, dnd }

/// A lobby a friend is hosting and has invited people to.
struct HubLobbyInfo: Codable, Equatable, Sendable {
    var code: String
    var sport: String
    var size: Int
    var capacity: Int
    var open: Bool
    var group: UInt32
}

struct HubPlayerRef: Codable, Equatable, Sendable { var id: String; var name: String }

/// An invitation to a friend's lobby (also what a push notification carries, and what a pasted code resolves to).
struct HubInvite: Codable, Equatable, Identifiable, Sendable {
    var inviteID: String
    var from: HubPlayerRef
    var sport: String
    var lobbyCode: String
    var group: UInt32
    var capacity: Int
    /// Milliseconds since 1970, by the service's clock (the app corrects for the offset it measured at welcome).
    var expiresAt: Double
    var id: String { inviteID }
    var sportTitle: String { sport == "golf" ? "Golf" : "Tennis" }
}

enum HubServerMessage: Equatable, Sendable {
    case welcome(you: String, serverTime: Double)
    case presence(id: String, online: Bool, state: HubPresenceState?, lobby: HubLobbyInfo?, name: String?)
    case invite(HubInvite)
    /// To the sender: `to` is the invited friend (the sender only learns the invite id from this message).
    case inviteUpdate(inviteID: String, status: String, to: String?)
    case inviteGone(inviteID: String, status: String)
    case resolved(code: String, found: Bool, from: HubPlayerRef?, sport: String?, group: UInt32?, capacity: Int?)
    case error(code: String, message: String, ref: String?)
    /// Who is in this party room now (yourself included); empty once you left it.
    case roomPeers(room: String, peers: [HubPlayerRef])
    /// One frame of a party's traffic from another member.
    case relay(room: String, from: String, data: String)
    case pong
    case unknown(String)

    /// `from` is a player ({id, name}) in invites and resolves, and a bare id in relay frames.
    private struct From: Decodable {
        var ref: HubPlayerRef?, id: String?
        init(from decoder: Decoder) throws {
            if let ref = try? HubPlayerRef(from: decoder) { self.ref = ref; id = ref.id }
            else { id = try decoder.singleValueContainer().decode(String.self) }
        }
    }
    private struct Raw: Decodable {
        var t: String
        var you: String?, serverTime: Double?
        var id: String?, online: Bool?, state: String?, lobby: HubLobbyInfo?, name: String?
        var inviteID: String?, status: String?, to: String?
        var code: String?, found: Bool?, from: From?, sport: String?, group: UInt32?, capacity: Int?
        var message: String?, ref: String?
        var room: String?, peers: [HubPlayerRef]?, d: String?
    }

    /// Nil for a frame that is not JSON or has no type; `.unknown` for a type this version does not know (ignored by the app).
    static func decode(_ data: Data) -> HubServerMessage? {
        guard let raw = try? JSONDecoder().decode(Raw.self, from: data) else { return nil }
        switch raw.t {
        case "welcome": return .welcome(you: raw.you ?? "", serverTime: raw.serverTime ?? 0)
        case "presence":
            guard let id = raw.id else { return nil }
            return .presence(id: id, online: raw.online ?? false, state: raw.state.flatMap(HubPresenceState.init(rawValue:)), lobby: raw.lobby, name: raw.name)
        case "invite": return (try? JSONDecoder().decode(HubInvite.self, from: data)).map(HubServerMessage.invite)
        case "inviteUpdate":
            guard let id = raw.inviteID, let status = raw.status else { return nil }
            return .inviteUpdate(inviteID: id, status: status, to: raw.to)
        case "inviteGone":
            guard let id = raw.inviteID else { return nil }
            return .inviteGone(inviteID: id, status: raw.status ?? "cancelled")
        case "resolved":
            guard let code = raw.code else { return nil }
            return .resolved(code: code, found: raw.found ?? false, from: raw.from?.ref, sport: raw.sport, group: raw.group, capacity: raw.capacity)
        case "error": return .error(code: raw.code ?? "error", message: raw.message ?? "", ref: raw.ref)   // (`code` is the error's, not a lobby's)
        case "roomPeers":
            guard let room = raw.room else { return nil }
            return .roomPeers(room: room, peers: raw.peers ?? [])
        case "rd":
            guard let room = raw.room, let from = raw.from?.id, let d = raw.d else { return nil }
            return .relay(room: room, from: from, data: d)
        case "pong": return .pong
        default: return .unknown(raw.t)
        }
    }
}

/// Messages the app sends. Built as dictionaries so optional fields simply leave the key out.
enum HubClientMessage {
    /// Apple's identity verification for the signed-in Game Center player, or a dev id (only a dev service accepts that).
    enum Auth {
        case dev(String)
        case apple(playerID: String, bundleID: String, publicKeyURL: String, signature: Data, salt: Data, timestamp: UInt64)
        var json: [String: Any] {
            switch self {
            case .dev(let id): return ["dev": id]
            case .apple(let player, let bundle, let url, let signature, let salt, let timestamp):
                return ["playerID": player, "bundleID": bundle, "publicKeyURL": url, "signature": signature.base64EncodedString(),
                        "salt": salt.base64EncodedString(), "timestamp": timestamp]
            }
        }
    }
    static func data(_ object: [String: Any]) -> Data { (try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])) ?? Data() }

    static func hello(auth: Auth, name: String, friends: [String], presence: HubPresence, push: (token: String, env: String)?) -> Data {
        var m: [String: Any] = ["t": "hello", "v": 1, "auth": auth.json, "name": name, "friends": friends, "presence": presence.json,
                                "app": ["version": Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0",
                                        "build": Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0",
                                        "protocol": MultiplayerPacket.version]]
        if let push { m["push"] = ["token": push.token, "env": push.env] }
        return data(m)
    }
    static func presence(_ p: HubPresence) -> Data { data(p.json.merging(["t": "presence"]) { a, _ in a }) }
    static func friends(_ ids: [String]) -> Data { data(["t": "friends", "ids": ids]) }
    static func invite(to: String, sport: String, lobbyCode: String, group: UInt32, capacity: Int) -> Data {
        data(["t": "invite", "to": to, "sport": sport, "lobbyCode": lobbyCode, "group": group, "capacity": capacity])
    }
    static func inviteResponse(_ inviteID: String, accept: Bool) -> Data { data(["t": "inviteResponse", "inviteID": inviteID, "accept": accept]) }
    static func inviteCancel(_ inviteID: String) -> Data { data(["t": "inviteCancel", "inviteID": inviteID]) }
    static func resolve(_ code: String) -> Data { data(["t": "resolve", "code": code]) }
    static func block(_ id: String, _ on: Bool) -> Data { data(["t": on ? "block" : "unblock", "id": id]) }
    static func push(token: String, env: String) -> Data { data(["t": "push", "token": token, "env": env]) }
    /// Party rooms (the relay): open one for my party, join a friend's, leave, and one frame of the party's traffic.
    static func roomOpen(_ code: String) -> Data { data(["t": "roomOpen", "room": code]) }
    static func roomJoin(_ code: String) -> Data { data(["t": "roomJoin", "room": code]) }
    static func roomLeave(_ code: String) -> Data { data(["t": "roomLeave", "room": code]) }
    /// (built by hand: it goes out many times a second, and `d` is already JSON text)
    static func relay(_ code: String, to: [String]?, _ d: String) -> Data {
        var m: [String: Any] = ["t": "rd", "room": code, "d": d]
        if let to { m["to"] = to }
        return (try? JSONSerialization.data(withJSONObject: m)) ?? Data()
    }
    static let bye = data(["t": "bye"])
    static let ping = data(["t": "ping"])
}

/// What this player is doing, as friends see it.
struct HubPresence: Equatable, Sendable {
    var state: HubPresenceState
    var lobby: HubLobbyInfo?
    var invisible = false
    var json: [String: Any] {
        var m: [String: Any] = ["state": state.rawValue, "invisible": invisible]
        if let lobby, let data = try? JSONEncoder().encode(lobby), let o = try? JSONSerialization.jsonObject(with: data) { m["lobby"] = o }
        return m
    }
}
