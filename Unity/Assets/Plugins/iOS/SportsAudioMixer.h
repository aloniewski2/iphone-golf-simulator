// SportsAudioMixer.h -- header-only, no Apple or Unity includes (unit-tested on macOS, see
// Unity/Tools/native-tests). Used by SportsSessionRecorder.mm.
//
// Three live sources (game stereo, remote party voice, own microphone) each arrive on their
// own real-time thread at their own sample rate. Each source owns a wait-free single-producer /
// single-consumer ring of stereo frames at its INPUT rate; a single consumer (a 10 ms timer) pulls
// 480 frames at a time, resampled (linear) to 48 kHz, mixed with per-source gain and soft-limited.
//
//   * push()  : any thread, real time. Wait-free: never blocks or allocates. Disabled sources are
//               dropped before touching the ring.
//   * pull()  : the single consumer. Underrun = silence. Overrun = oldest frames dropped.
//   * PumpClock slaves the pull cadence to the wall clock, so the audio timeline can never
//     drift from real elapsed time (and therefore from the video timeline).
#pragma once
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <vector>

namespace sports {

constexpr int kMixRate = 48000;       // output sample rate
constexpr int kBlockFrames = 480;     // 10 ms at 48 kHz
constexpr double kBlockSeconds = 0.010;
constexpr int kRingFrames = 96000;    // storage per source: 2 s at 48 kHz, 1 s at 96 kHz

enum SourceId { kSourceGame = 0, kSourceVoice = 1, kSourceMic = 2, kSourceCount = 3 };

/// Smooth limiter. Identity (bit-exact) for |x| <= knee; above the knee a tanh shoulder
/// that approaches but never exceeds 1.0. C1-continuous at the knee. NaN -> 0.
inline float softLimit(float x, float knee = 0.8f) {
    if (x != x) return 0.0f;
    float a = std::fabs(x);
    if (a <= knee) return x;
    float room = 1.0f - knee;
    float y = knee + room * std::tanh((a - knee) / room);
    if (y > 1.0f) y = 1.0f;
    return x < 0.0f ? -y : y;
}

struct SourceStats {
    uint64_t pushedFrames = 0;       // input frames accepted
    uint64_t droppedOverrun = 0;     // input frames discarded because more than the capacity was waiting (oldest dropped)
    uint64_t droppedContention = 0;  // input frames discarded because another producer was inside push() at the same time
    uint64_t underruns = 0;          // pulls that could not be completely filled
};

/// One source: wait-free for the producer, single consumer.
///   * Producer (real-time audio thread): copies into the ring and publishes the write index.
///     It never waits for the consumer. Two producers calling push() at the same instant on the
///     same source is not expected; the second one simply drops its chunk (counted) instead of blocking.
///   * Consumer (the 10 ms pump): owns the read index. Overrun handling happens there: when more
///     than `capacityMs` is waiting, the OLDEST frames are discarded, so order is always kept.
///   * A change of input rate is published as an epoch; the consumer then discards what was
///     buffered at the old rate.
class SourceRing {
public:
    struct Config {
        double prefillMs = 40.0;     // how much to buffer before (re)starting playback
        double capacityMs = 500.0;   // overrun threshold (at most kRingFrames/2 frames)
        bool adaptivePrefill = true; // prefill is at least 1.25x the largest chunk seen
    };

    SourceRing() : buf_((size_t)kRingFrames * 2, 0.0f) {}

    /// Not real-time safe: call while the source is disabled and the consumer is stopped.
    void configure(const Config &c) { cfg_ = c; }
    void setEnabled(bool e) { enabled_.store(e, std::memory_order_release); }
    bool enabled() const { return enabled_.load(std::memory_order_relaxed); }
    void setGain(float g) { gain_.store(g, std::memory_order_relaxed); }
    float gain() const { return gain_.load(std::memory_order_relaxed); }

    /// Consumer side (consumer stopped): discard everything buffered and the statistics.
    void reset() {
        rd_.store(wr_.load(std::memory_order_acquire), std::memory_order_release);
        pos_ = 0; primed_ = false; seenEpoch_ = epoch_.load(std::memory_order_acquire);
        maxChunk_.store(0, std::memory_order_relaxed);
        pushed_ = 0; overrun_ = 0; underruns_ = 0; contention_ = 0;
    }

    /// Producer. Interleaved `channels` (>=1); channel 0 = left, channel 1 (if any) = right,
    /// others ignored. Returns frames stored (0 if disabled, invalid, or dropped).
    int push(const float *in, int frames, int channels, int rate) {
        if (!enabled_.load(std::memory_order_acquire)) return 0;
        if (!in || frames <= 0 || channels < 1 || rate < 8000 || rate > 192000) return 0;
        if (producing_.test_and_set(std::memory_order_acquire)) { contention_.fetch_add((uint64_t)frames, std::memory_order_relaxed); return 0; }
        struct Release { std::atomic_flag &f; ~Release() { f.clear(std::memory_order_release); } } release{producing_};
        const uint64_t w = wr_.load(std::memory_order_relaxed);   // only producers (serialized above) write it
        if (rate != rate_.load(std::memory_order_relaxed)) {
            rateStart_ = w;                                        // frames from here on are at the new rate
            rate_.store(rate, std::memory_order_relaxed);
            maxChunk_.store(0, std::memory_order_relaxed);
            epoch_.fetch_add(1, std::memory_order_release);
        }
        // Never write over frames the consumer has not read yet. With a consumer that keeps up (every
        // 10 ms) this only triggers after a stall of more than a second; the newest data is then dropped.
        const uint64_t r = rd_.load(std::memory_order_acquire);
        const uint64_t used = w - std::min(r, w);
        int stored = frames;
        if (used + (uint64_t)stored > (uint64_t)kRingFrames) stored = (int)std::max<int64_t>(0, (int64_t)kRingFrames - (int64_t)used);
        if (stored < frames) overrun_.fetch_add((uint64_t)(frames - stored), std::memory_order_relaxed);
        size_t idx = (size_t)(w % kRingFrames);
        for (int i = 0; i < stored; i++) {
            const float *f = in + (size_t)i * (size_t)channels;
            buf_[idx * 2] = f[0]; buf_[idx * 2 + 1] = channels > 1 ? f[1] : f[0];
            if (++idx == (size_t)kRingFrames) idx = 0;
        }
        pushed_.fetch_add((uint64_t)stored, std::memory_order_relaxed);
        if (frames > maxChunk_.load(std::memory_order_relaxed)) maxChunk_.store(frames, std::memory_order_relaxed);
        wr_.store(w + (uint64_t)stored, std::memory_order_release);
        return stored;
    }

    /// Consumer. Writes exactly `frames` interleaved stereo frames at 48 kHz (zero filled
    /// where there is no data). Returns the number of frames that carry real data.
    int pull(float *out, int frames) {
        std::memset(out, 0, sizeof(float) * (size_t)frames * 2);
        uint64_t e = epoch_.load(std::memory_order_acquire);
        uint64_t r = rd_.load(std::memory_order_relaxed);
        if (e != seenEpoch_) { seenEpoch_ = e; r = std::max(r, rateStart_); pos_ = 0; primed_ = false; rd_.store(r, std::memory_order_release); }
        const int rate = rate_.load(std::memory_order_relaxed);
        if (rate == 0) return 0;
        const uint64_t w = wr_.load(std::memory_order_acquire);
        int64_t avail = (int64_t)(w - r);
        const int64_t limit = std::max<int64_t>(2, std::min<int64_t>((int64_t)(cfg_.capacityMs * rate / 1000.0), kRingFrames / 2));
        if (avail > limit) {                                       // overrun: drop the oldest
            uint64_t drop = (uint64_t)(avail - limit);
            r += drop; avail = limit; overrun_.fetch_add(drop, std::memory_order_relaxed);
            rd_.store(r, std::memory_order_release);
        }
        if (!primed_) {
            double pre = cfg_.prefillMs * rate / 1000.0;
            if (cfg_.adaptivePrefill) pre = std::max(pre, 1.25 * (double)maxChunk_.load(std::memory_order_relaxed));
            pre = std::min(pre, 0.8 * (double)limit);
            if (avail < 2 || (double)avail < pre) return 0;
            primed_ = true;
        }
        int produced = 0;
        uint64_t newR = r;
        if (rate == kMixRate) {
            produced = (int)std::min<int64_t>(frames, avail);
            size_t idx = (size_t)(r % kRingFrames);
            for (int i = 0; i < produced; i++) {
                out[i * 2] = buf_[idx * 2]; out[i * 2 + 1] = buf_[idx * 2 + 1];
                if (++idx == (size_t)kRingFrames) idx = 0;
            }
            newR = r + (uint64_t)produced; pos_ = 0;
        } else {
            const double step = (double)rate / (double)kMixRate;
            for (int k = 0; k < frames; k++) {
                double p = pos_ + (double)k * step;
                int64_t i = (int64_t)p;
                if (i + 1 >= avail) break;          // needs frame i and i+1
                double frac = p - (double)i;
                size_t a = (size_t)((r + (uint64_t)i) % kRingFrames), b = a + 1 == (size_t)kRingFrames ? 0 : a + 1;
                out[k * 2]     = (float)(buf_[a * 2]     + (buf_[b * 2]     - buf_[a * 2])     * frac);
                out[k * 2 + 1] = (float)(buf_[a * 2 + 1] + (buf_[b * 2 + 1] - buf_[a * 2 + 1]) * frac);
                produced++;
            }
            double p = pos_ + (double)produced * step;
            int64_t adv = (int64_t)p;
            if (adv > avail) { adv = avail; p = (double)adv; }
            newR = r + (uint64_t)adv; pos_ = p - (double)adv;
        }
        rd_.store(newR, std::memory_order_release);
        if (produced < frames) { primed_ = false; underruns_.fetch_add(1, std::memory_order_relaxed); }
        return produced;
    }

    SourceStats stats() const {
        SourceStats s;
        s.pushedFrames = pushed_.load(std::memory_order_relaxed);
        s.droppedOverrun = overrun_.load(std::memory_order_relaxed);
        s.droppedContention = contention_.load(std::memory_order_relaxed);
        s.underruns = underruns_.load(std::memory_order_relaxed);
        return s;
    }
    /// Frames waiting (including any beyond the capacity that the next pull will drop).
    int64_t bufferedFrames() const { return (int64_t)(wr_.load(std::memory_order_acquire) - rd_.load(std::memory_order_acquire)); }

private:
    std::vector<float> buf_;   // interleaved L,R at the input rate
    Config cfg_;
    std::atomic<uint64_t> wr_{0}, rd_{0};   // monotonic frame counters
    std::atomic_flag producing_ = ATOMIC_FLAG_INIT;
    std::atomic<int> rate_{0};
    std::atomic<uint64_t> epoch_{0};
    uint64_t rateStart_ = 0;                // written by the producer before the epoch is published
    std::atomic<int64_t> maxChunk_{0};
    // consumer only
    uint64_t seenEpoch_ = 0;
    double pos_ = 0;           // fractional read position, input frames after rd_
    bool primed_ = false;
    std::atomic<uint64_t> pushed_{0}, overrun_{0}, contention_{0}, underruns_{0};
    std::atomic<bool> enabled_{false};
    std::atomic<float> gain_{1.0f};
};

class AudioMixer {
public:
    /// Enables the sources, sets gains, and clears all buffers. Call before pushing.
    void configure(const bool enabled[kSourceCount], const float gain[kSourceCount], const SourceRing::Config cfg[kSourceCount]) {
        for (int i = 0; i < kSourceCount; i++) {
            rings_[i].setEnabled(false);
            rings_[i].reset();
            rings_[i].configure(cfg[i]);
            rings_[i].setGain(gain[i]);
            rings_[i].setEnabled(enabled[i]);
        }
        framesPulled_ = 0;
    }
    void disableAll() { for (auto &r : rings_) r.setEnabled(false); }

    int push(SourceId s, const float *in, int frames, int channels, int rate) { return rings_[s].push(in, frames, channels, rate); }

    /// Mix `frames` (default one 10 ms block) of interleaved stereo 48 kHz into `out`.
    /// Single consumer thread only. Always writes exactly `frames` frames.
    void pull(float *out, int frames = kBlockFrames) {
        if ((int)scratch_.size() < frames * 2) scratch_.assign((size_t)frames * 2, 0.0f);
        std::memset(out, 0, sizeof(float) * (size_t)frames * 2);
        for (auto &r : rings_) {
            if (r.pull(scratch_.data(), frames) == 0) continue;
            float g = r.gain();
            for (int i = 0; i < frames * 2; i++) out[i] += scratch_[(size_t)i] * g;
        }
        for (int i = 0; i < frames * 2; i++) out[i] = softLimit(out[i]);
        framesPulled_ += (uint64_t)frames;
    }

    uint64_t framesPulled() const { return framesPulled_; }
    SourceRing &source(SourceId s) { return rings_[s]; }

private:
    SourceRing rings_[kSourceCount];
    std::vector<float> scratch_;
    uint64_t framesPulled_ = 0;
};

/// Slaves the 10 ms pull cadence to a wall clock (seconds, monotonic). A late timer pulls
/// several blocks in one go; an early one pulls none. Block k nominally starts at t0 + k * 10 ms.
class PumpClock {
public:
    void start(double now) { t0_ = now; pulled_ = 0; }
    /// If a block is due at `now`, consumes it and returns true with its nominal start time.
    /// If the consumer is more than `maxBehind` blocks late (e.g. the process was frozen),
    /// the clock skips ahead instead of producing a burst.
    bool next(double now, double *blockStart, int maxBehind = 100) {
        double due = std::floor((now - t0_) / kBlockSeconds);
        if (due - (double)pulled_ > (double)maxBehind) { pulled_ = (uint64_t)(due - maxBehind); skipped_++; }
        if ((double)pulled_ >= due) return false;
        if (blockStart) *blockStart = t0_ + (double)pulled_ * kBlockSeconds;
        pulled_++;
        return true;
    }
    uint64_t pulled() const { return pulled_; }
    uint64_t skippedResyncs() const { return skipped_; }
private:
    double t0_ = 0; uint64_t pulled_ = 0, skipped_ = 0;
};

/// Audio presentation time = offset + frames written / 48000.
class AudioPts {
public:
    /// offsetSeconds = (time of the first kept audio block) - (time of the first video frame), >= 0.
    void start(double offsetSeconds) { offsetFrames_ = (int64_t)std::llround(std::max(0.0, offsetSeconds) * kMixRate); written_ = 0; started_ = true; }
    bool started() const { return started_; }
    int64_t nextFrames() const { return offsetFrames_ + written_; }
    double nextSeconds() const { return (double)nextFrames() / kMixRate; }
    void advance(int frames) { written_ += frames; }
    int64_t written() const { return written_; }
    /// Re-anchor so the next frame lands at absolute position `frames` (a gap or overlap in the timeline).
    void seek(int64_t frames) { offsetFrames_ = std::max<int64_t>(0, frames) - written_; }
private:
    int64_t offsetFrames_ = 0, written_ = 0; bool started_ = false;
};

/// Offset of an audio block that starts at `blockStart` relative to the first video frame.
inline double audioStartOffset(double firstVideoTime, double blockStart) { return std::max(0.0, blockStart - firstVideoTime); }

} // namespace sports
