from pathlib import Path
p=Path('GolfArcade/Multiplayer/LocalMultiplayerTransport.swift');s=p.read_text().replace('var token: String }','var token: String; var reason: String? }')
s=s.replace('''        guard pending.count + connections.count < (host ? 3 : 1) else { c.cancel(); return }''','''        guard pending.count + connections.count < (host ? 3 : 1) else {
            guard host else { c.cancel(); return }
            c.stateUpdateHandler = { [weak self] state in
                if case .ready = state { Task { @MainActor in
                    guard let self, let data = try? JSONEncoder().encode(Hello(id:self.localID,version:MultiplayerPacket.version,token:self.token,reason:MultiplayerError.full.localizedDescription)) else { c.cancel(); return }
                    self.write(data,connection:c) { c.cancel() }
                } }
            }; c.start(queue:queue); return
        }''')
a='''            guard let self, self.generation == epoch, let data, let hello = try? JSONDecoder().decode(Hello.self, from: data),
                  hello.version == MultiplayerPacket.version, hello.token == self.token, UUID(uuidString: hello.id) != nil,
                  hello.id != self.localID, self.connections[hello.id] == nil else { c.cancel(); return }'''
b='''            guard let self, self.generation == epoch, let data, let hello = try? JSONDecoder().decode(Hello.self, from: data) else { c.cancel(); return }
            if let reason = hello.reason { self.onError?(MultiplayerError.unavailable(reason)); c.cancel(); return }
            guard hello.version == MultiplayerPacket.version else { self.onError?(MultiplayerError.incompatible); c.cancel(); return }
            guard hello.token == self.token, UUID(uuidString: hello.id) != nil,
                  hello.id != self.localID, self.connections[hello.id] == nil else { c.cancel(); return }'''
assert a in s;s=s.replace(a,b);p.write_text(s)
