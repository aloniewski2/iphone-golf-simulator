#include <deque>
#include <string>
#include <mutex>
#include <cstring>
#include <mach/mach_time.h>

// Bounded queues owned by the framework: no callbacks into released Swift objects.
static std::mutex gate;
static std::deque<std::string> inputs, events;
// Mirrors SportsSample in the host's SportsRuntime.h and NativeSportsSession.Sample in C#.
struct SportsSample {
    int32_t version, session; double time;
    float target, power, aim;
    int32_t swing, swingStart, swingAbort, flags;
    float handSide, lift, strokeFacing;
    float qx, qy, qz, qw, rx, ry, rz, gx, gy, gz;
};
// Fixed ring: pushing and polling never allocate on either side of the bridge.
static SportsSample samples[64];
static int sampleHead = 0, sampleCount = 0;
static int pop(std::deque<std::string>& queue, char* output, int capacity) {
    std::lock_guard<std::mutex> lock(gate);
    if (!output || capacity < 2) return 0;
    output[0]='\0';
    if (queue.empty()) return 0;
    auto value = queue.front(); queue.pop_front();
    if (value.size() >= (size_t)capacity) return 0;
    memcpy(output, value.c_str(), value.size()+1); return (int)value.size();
}
extern "C" {
__attribute__((visibility("default"))) double SportsClock() {
    static mach_timebase_info_data_t info = [] { mach_timebase_info_data_t i; mach_timebase_info(&i); return i; }();
    return mach_absolute_time() * (double)info.numer / info.denom / 1e9;
}
__attribute__((visibility("default"))) void SportsPushInput(const char* json) {
    if (!json || strlen(json)>4096) return;
    std::lock_guard<std::mutex> lock(gate);
    if(inputs.size()>=64) inputs.pop_front(); inputs.emplace_back(json);
}
__attribute__((visibility("default"))) void SportsPushSample(const SportsSample* sample) {
    if (!sample) return;
    std::lock_guard<std::mutex> lock(gate);
    if (sampleCount == 64) { sampleHead = (sampleHead + 1) % 64; sampleCount--; }
    samples[(sampleHead + sampleCount) % 64] = *sample; sampleCount++;
}
__attribute__((visibility("default"))) int SportsPollSample(SportsSample* output) {
    std::lock_guard<std::mutex> lock(gate);
    if (!output || sampleCount == 0) return 0;
    *output = samples[sampleHead]; sampleHead = (sampleHead + 1) % 64; sampleCount--;
    return 1;
}
__attribute__((visibility("default"))) int SportsPollInput(char* output, int capacity) { return pop(inputs,output,capacity); }
__attribute__((visibility("default"))) void SportsEmit(const char* json) {
    if(!json || strlen(json)>4096) return;
    std::lock_guard<std::mutex> lock(gate);
    if(events.size()>=64) events.pop_front(); events.emplace_back(json);
}
__attribute__((visibility("default"))) int SportsPollEvent(char* output,int capacity) { return pop(events,output,capacity); }
__attribute__((visibility("default"))) void SportsClear() {
    std::lock_guard<std::mutex> lock(gate); inputs.clear(); events.clear(); sampleHead = sampleCount = 0;
}
}
