// Reproduces the Swift -> Unity bridge queue filling up while a guest waits between matches.
// Compiles the REAL Unity/Assets/Plugins/iOS/SportsBridge.mm (it is plain C++) together with this driver:
//
//   clang++ -std=c++17 -x c++ Unity/Assets/Plugins/iOS/SportsBridge.mm -x c++ proof/multiplayer/online_crash_audit/bridge_queue_driver.cpp -o /tmp/bridge_queue_driver && /tmp/bridge_queue_driver
//
// Expected output (2026-10-10, before the fix in MultiplayerService.pushRuntimePacket):
//   push #129 is the first refused  => 64.0 s of idling at 2 pongs/s fills the 128-slot queue
//   SportsNetworkPollOutput -> 160 bytes: {"version":1,...,"kind":"bridgeError",...
//   a reliable 'run' pushed now is REFUSED (queue still full)
//   Unity drains 128 stale packets before it sees anything current
//   after SportsClear a push is accepted
#include <cstdio>
#include <cstring>
#include <string>
extern "C" {
int SportsNetworkPush(const char* json);          // Swift -> Unity queue (what pushNetwork calls)
int SportsNetworkPoll(char* output,int capacity); // Unity drains it here (only while SportsMultiplayer.Active)
int SportsNetworkPollOutput(char* output,int capacity); // Unity -> Swift queue, also where bridgeError appears
void SportsClear();
}
// The exact JSON shape MultiplayerService.packet(kind:...) encodes for a pong's "clock" push (reliable defaults to true).
static std::string clockPacket(int n) {
    return std::string("{\"version\":4,\"lobbyID\":\"L\",\"matchID\":\"\",\"sender\":\"G:1\",\"sequence\":") + std::to_string(n) +
           ",\"kind\":\"clock\",\"reliable\":true,\"sentAt\":1.0,\"payload\":\"12345.678\"}";
}
int main() {
    char buf[65536];
    // 1. Guest idles in the lobby after an earlier Unity session: a pong every 0.5 s, each one pushes a reliable 'clock' packet.
    int firstFail = -1;
    for (int i = 1; i <= 140; i++) {
        int ok = SportsNetworkPush(clockPacket(i).c_str());
        if (!ok && firstFail < 0) firstFail = i;
    }
    std::printf("push #%d is the first refused  => %.1f s of idling at 2 pongs/s fills the 128-slot queue\n", firstFail, (firstFail - 1) / 2.0);
    // 2. The next poll from Swift (every 1/60 s) now returns the synthetic bridgeError.
    int n = SportsNetworkPollOutput(buf, sizeof buf);
    std::printf("SportsNetworkPollOutput -> %d bytes: %.*s...\n", n, 120, n > 0 ? buf : "(nothing)");
    // 3. Is the queue still full when the match is launched (Unity not yet Active, so nobody has drained it)?
    int ok = SportsNetworkPush("{\"kind\":\"run\",\"reliable\":true}");
    std::printf("a reliable 'run' pushed now is %s\n", ok ? "accepted" : "REFUSED (queue still full)");
    // 4. What Unity would read once Active: 128 stale clock packets, then nothing new.
    int drained = 0; while (SportsNetworkPoll(buf, sizeof buf) > 0) drained++;
    std::printf("Unity drains %d stale packets before it sees anything current\n", drained);
    // 5. After SportsClear() (never called today) the queue is usable again.
    SportsNetworkPush(clockPacket(1).c_str()); SportsClear();
    std::printf("after SportsClear a push is %s\n", SportsNetworkPush(clockPacket(2).c_str()) ? "accepted" : "refused");
    return 0;
}
