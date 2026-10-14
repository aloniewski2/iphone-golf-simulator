import Foundation

/// A friends' party over the Club Hub relay (club-hub/PROTOCOL.md, Party rooms): the lobby, where everyone walks in the Plaza, and
/// the match all go through the service's room for the party's code, not Game Center. Invites from the Friends list make one: the
/// host opens the room, an invited friend joins it, and the service lets nobody else in.
///
/// Ids are the players' hub ids (their Game Center teamPlayerID, the id Apple's identity signature proves).
@MainActor final class HubRoomTransport: PartyTransport {
    let code: String
    /// This phone opened the room (the party's first host).
    let opener: Bool
    private weak var hub: ClubHub?
    let localID: String
    private(set) var peers: [String] = []
    /// Everyone in the room, names included (yourself too).
    private(set) var members: [HubPlayerRef] = []
    var onData: ((Data, String) -> Void)?
    var onPeersChanged: (() -> Void)?
    var onError: ((Error) -> Void)?
    /// The service's answer to joining: in (true) or refused (false, with why). Nil until it answers.
    private(set) var entered: Bool?
    private(set) var refusal: String?
    private(set) var closed = false

    init(hub: ClubHub, code: String, opener: Bool) {
        self.hub = hub; self.code = code; self.opener = opener; localID = hub.selfID
    }

    func send(_ data: Data, to ids: [String]?, reliable: Bool) throws {
        guard data.count <= MultiplayerPacket.maximumBytes else { throw MultiplayerError.invalidOperation("Network packet is too large.") }
        guard !closed, let hub, hub.relayLive(self) else { throw MultiplayerError.unavailable("Not connected to your party.") }
        hub.relay(code, to: ids, String(decoding: data, as: UTF8.self))
    }

    func disconnect() {
        guard !closed else { return }
        closed = true; peers = []; members = []
        hub?.roomClosed(self)
    }

    // from the hub

    func roomPeers(_ list: [HubPlayerRef]) {
        guard !closed else { return }
        if list.contains(where: { $0.id == localID }) { entered = true }
        members = list
        let ids = list.map(\.id).filter { $0 != localID }
        guard ids != peers else { return }
        peers = ids; onPeersChanged?()
    }
    func received(_ text: String, from id: String) {
        guard !closed, peers.contains(id) else { return }
        onData?(Data(text.utf8), id)
    }
    func refused(_ code: String, message: String) {
        guard !closed else { return }
        entered = false
        refusal = switch code {
        case "room_gone": "That party has ended."
        case "room_denied": "That party is invite-only. Ask your friend to invite you again."
        case "room_full": "That party is full."
        default: message.isEmpty ? "Could not reach that party." : message
        }
        onError?(MultiplayerError.unavailable(refusal ?? ""))
    }
    /// The hub's connection dropped: everyone looks gone until it is back (the room is joined again on reconnect).
    func linkLost() {
        guard !closed, !peers.isEmpty else { return }
        peers = []; members = []; onPeersChanged?()
    }
}
