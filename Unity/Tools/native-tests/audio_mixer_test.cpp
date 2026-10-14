// Unit tests for Unity/Assets/Plugins/iOS/SportsAudioMixer.h (macOS, no Unity, no Xcode project).
// Build and run with run.sh (plain, ThreadSanitizer and AddressSanitizer variants).
#include "SportsAudioMixer.h"
#include <cstdio>
#include <cstdlib>
#include <functional>
#include <random>
#include <string>
#include <thread>
#include <chrono>

using namespace sports;

static int gChecks = 0, gFailed = 0;
#define CHECK(cond) do { gChecks++; if (!(cond)) { gFailed++; std::printf("    CHECK FAILED %s:%d: %s\n", __FILE__, __LINE__, #cond); } } while (0)
#define CHECK_NEAR(a, b, tol) do { gChecks++; double _a=(a), _b=(b); if (!(std::fabs(_a-_b) <= (tol))) { gFailed++; std::printf("    CHECK_NEAR FAILED %s:%d: %s=%g vs %s=%g (tol %g)\n", __FILE__, __LINE__, #a, _a, #b, _b, (double)(tol)); } } while (0)

static int gTests = 0, gTestsFailed = 0;
static void run(const char *name, const std::function<void()> &fn) {
    int before = gFailed; gTests++;
    fn();
    bool ok = gFailed == before;
    if (!ok) gTestsFailed++;
    std::printf("[%s] %s\n", ok ? "PASS" : "FAIL", name);
}

// Exact, predictable buffering for the functional tests.
static SourceRing::Config exact(double capacityMs = 1500) {
    SourceRing::Config c; c.prefillMs = 0; c.capacityMs = capacityMs; c.adaptivePrefill = false; return c;
}
static void configureOnly(AudioMixer &m, SourceId only, float gain = 1.0f, double capacityMs = 1500) {
    bool en[kSourceCount] = {false, false, false}; en[only] = true;
    float g[kSourceCount] = {1, 1, 1}; g[only] = gain;
    SourceRing::Config c[kSourceCount] = {exact(capacityMs), exact(capacityMs), exact(capacityMs)};
    m.configure(en, g, c);
}
static void configureAll(AudioMixer &m, const float gain[3], bool g = true, bool v = true, bool mic = true, double capacityMs = 1500) {
    bool en[kSourceCount] = {g, v, mic};
    SourceRing::Config c[kSourceCount] = {exact(capacityMs), exact(capacityMs), exact(capacityMs)};
    m.configure(en, gain, c);
}

static std::vector<float> sine(double freq, double amp, int rate, int frames, int channels, double phaseR = 0.0) {
    std::vector<float> v((size_t)frames * channels);
    for (int i = 0; i < frames; i++) {
        double t = (double)i / rate;
        v[(size_t)i * channels] = (float)(amp * std::sin(2 * M_PI * freq * t));
        if (channels > 1) v[(size_t)i * channels + 1] = (float)(amp * std::sin(2 * M_PI * freq * t + phaseR));
    }
    return v;
}

// Pull `blocks` blocks, return interleaved stereo.
static std::vector<float> pullBlocks(AudioMixer &m, int blocks) {
    std::vector<float> all((size_t)blocks * kBlockFrames * 2), blk((size_t)kBlockFrames * 2);
    for (int b = 0; b < blocks; b++) {
        m.pull(blk.data(), kBlockFrames);
        std::memcpy(all.data() + (size_t)b * kBlockFrames * 2, blk.data(), blk.size() * sizeof(float));
    }
    return all;
}

// Frequency from positive-going zero crossings (linear interpolated), amplitude from max |x| and RMS.
struct Measure { double freq, peak, rms; };
static Measure measure(const std::vector<float> &stereo, int channel, int from, int to) {
    int n = 0; double first = -1, last = -1, peak = 0, sum2 = 0;
    for (int i = from; i < to; i++) {
        double x = stereo[(size_t)i * 2 + channel];
        peak = std::max(peak, std::fabs(x)); sum2 += x * x;
        if (i > from) {
            double p = stereo[(size_t)(i - 1) * 2 + channel];
            if (p < 0 && x >= 0) {
                double t = (i - 1) + (-p) / (x - p);
                if (first < 0) first = t; last = t; n++;
            }
        }
    }
    Measure r; r.peak = peak; r.rms = std::sqrt(sum2 / (to - from));
    r.freq = n > 1 ? (n - 1) / ((last - first) / kMixRate) : 0;
    return r;
}

// -----------------------------------------------------------------------------------------

static void testResample() {
    const int rates[] = {16000, 44100, 48000, 22050, 8000};
    for (int rate : rates) for (int ch = 1; ch <= 2; ch++) {
        AudioMixer m; configureOnly(m, kSourceGame);
        const double f = std::min(1000.0, rate * 0.06), amp = 0.5;   // linear interpolation: keep f well below Nyquist
        std::vector<float> in = sine(f, amp, rate, rate, ch, ch == 2 ? 1.0 : 0.0); // 1 s
        // Feed 10 ms of input per 10 ms pull, like the real thing (1 s of signal in total).
        std::vector<float> out; int pos = 0;
        for (int blk = 0; blk < 95; blk++) {
            int n = std::min((int)((int64_t)(blk + 3) * rate / 100) - pos, rate - pos);   // stays 30 ms ahead
            if (n > 0) { m.push(kSourceGame, in.data() + (size_t)pos * ch, n, ch, rate); pos += n; }
            std::vector<float> b = pullBlocks(m, 1); out.insert(out.end(), b.begin(), b.end());
        }
        // Skip first 100 and last 100 ms; analyse the steady middle.
        int from = 4800, to = 4800 * 8;   // 100 ms .. 800 ms of the 1 s signal
        Measure l = measure(out, 0, from, to);
        CHECK_NEAR(l.freq, f, f * 0.01);
        CHECK_NEAR(l.peak, amp, amp * 0.03);
        CHECK_NEAR(l.rms, amp / std::sqrt(2.0), amp * 0.03);
        if (ch == 2) {
            // right channel carries the phase-shifted copy: same frequency/amplitude.
            Measure r = measure(out, 1, from, to);
            CHECK_NEAR(r.freq, f, f * 0.01);
            CHECK_NEAR(r.peak, amp, amp * 0.03);
        } else {
            Measure r = measure(out, 1, from, to);   // mono -> duplicated to both channels
            CHECK_NEAR(r.rms, l.rms, 1e-7);
        }
    }
}

static void testResampleStereoChannelsStayApart() {
    AudioMixer m; configureOnly(m, kSourceGame);
    std::vector<float> in(4410 * 2);
    for (int i = 0; i < 4410; i++) { in[(size_t)i * 2] = 0.25f; in[(size_t)i * 2 + 1] = -0.5f; }
    m.push(kSourceGame, in.data(), 4410, 2, 44100);
    std::vector<float> out = pullBlocks(m, 8);
    for (int i = 10; i < 8 * kBlockFrames - 10; i++) { CHECK_NEAR(out[(size_t)i * 2], 0.25, 1e-6); CHECK_NEAR(out[(size_t)i * 2 + 1], -0.5, 1e-6); }
}

static void testExactCopyAt48k() {
    AudioMixer m; configureOnly(m, kSourceGame);
    std::vector<float> in(2400);
    for (int i = 0; i < 2400; i++) in[(size_t)i] = (float)(i + 1) * 1e-4f * 0.5f;   // mono ramp
    CHECK(m.push(kSourceGame, in.data(), 2400, 1, 48000) == 2400);
    std::vector<float> out = pullBlocks(m, 5);
    for (int i = 0; i < 2400; i++) { CHECK(out[(size_t)i * 2] == in[(size_t)i]); CHECK(out[(size_t)i * 2 + 1] == in[(size_t)i]); }
}

static void testMixingGains() {
    AudioMixer m; const float g[3] = {1.0f, 2.0f, 0.5f}; configureAll(m, g);
    std::vector<float> game(4800 * 2), voice(4800), mic(4800);
    for (int i = 0; i < 4800; i++) { game[(size_t)i * 2] = 0.1f; game[(size_t)i * 2 + 1] = 0.2f; voice[(size_t)i] = 0.1f; mic[(size_t)i] = 0.2f; }
    m.push(kSourceGame, game.data(), 4800, 2, 48000);
    m.push(kSourceVoice, voice.data(), 4800, 1, 48000);
    m.push(kSourceMic, mic.data(), 4800, 1, 48000);
    std::vector<float> out = pullBlocks(m, 10);
    // L = 0.1*1 + 0.1*2 + 0.2*0.5 = 0.4 ; R = 0.2 + 0.2 + 0.1 = 0.5
    for (int i = 0; i < 10 * kBlockFrames; i++) { CHECK_NEAR(out[(size_t)i * 2], 0.4, 1e-6); CHECK_NEAR(out[(size_t)i * 2 + 1], 0.5, 1e-6); }
}

static void testMixingDifferentRatesTogether() {
    AudioMixer m; const float g[3] = {1, 1, 1}; configureAll(m, g);
    auto game = sine(440, 0.2, 48000, 48000, 2), voice = sine(1500, 0.2, 16000, 16000, 1), mic = sine(3000, 0.2, 44100, 44100, 1);
    // Push everything that fits; capacity is 1.5 s so 1 s of each fits.
    m.push(kSourceGame, game.data(), 48000, 2, 48000);
    m.push(kSourceVoice, voice.data(), 16000, 1, 16000);
    m.push(kSourceMic, mic.data(), 44100, 1, 44100);
    std::vector<float> out = pullBlocks(m, 90);
    // Sum of three sines of amp 0.2 peaks <= 0.6 (< knee) -> bit exact sum, RMS = sqrt(3)*0.2/sqrt2
    Measure r = measure(out, 0, 4800, 4800 * 8);
    CHECK_NEAR(r.rms, std::sqrt(3.0) * 0.2 / std::sqrt(2.0), 0.01);
    CHECK(r.peak <= 0.6 + 0.01);
}

static void testSoftLimiter() {
    // identity below the knee, bit exact
    for (float x = -0.8f; x <= 0.8f; x += 0.001f) { CHECK(softLimit(x) == x); }
    // never above 1.0, monotone, odd, continuous
    float prev = softLimit(-100.0f); bool mono = true, odd = true, bounded = true; float maxStep = 0;
    for (double x = -100; x <= 100; x += 0.0007) {
        float y = softLimit((float)x);
        if (std::fabs(y) > 1.0f) bounded = false;
        if (y < prev) mono = false;
        if (softLimit((float)-x) != -y) odd = false;
        maxStep = std::max(maxStep, std::fabs(y - prev));
        prev = y;
    }
    CHECK(bounded); CHECK(mono); CHECK(odd); CHECK(maxStep < 0.01f);
    CHECK(softLimit(1e9f) <= 1.0f); CHECK(softLimit(INFINITY) <= 1.0f); CHECK(softLimit(-INFINITY) >= -1.0f);
    CHECK(softLimit(NAN) == 0.0f);
    CHECK(softLimit(0.9f) < 0.9f && softLimit(0.9f) > 0.8f);   // compresses, does not clip flat
    CHECK(softLimit(2.0f) > softLimit(1.2f));                  // still strictly increasing (no hard clip)
    // quiet signal through the mixer is not distorted
    AudioMixer m; configureOnly(m, kSourceGame);
    auto quiet = sine(500, 0.5, 48000, 4800, 2);
    m.push(kSourceGame, quiet.data(), 4800, 2, 48000);
    std::vector<float> out = pullBlocks(m, 10);
    for (int i = 0; i < 4800; i++) CHECK(out[(size_t)i * 2] == quiet[(size_t)i * 2]);
    // heavy overdrive through the mixer stays inside [-1, 1]
    AudioMixer m2; const float g[3] = {1, 1, 1}; configureAll(m2, g);
    auto loud = sine(300, 0.95, 48000, 4800, 2);
    for (int s = 0; s < 3; s++) m2.push((SourceId)s, loud.data(), 4800, 2, 48000);
    std::vector<float> o2 = pullBlocks(m2, 10);
    float pk = 0; for (float v : o2) pk = std::max(pk, std::fabs(v));
    CHECK(pk <= 1.0f); CHECK(pk > 0.95f);
}

static void testUnderrunSilence() {
    AudioMixer m; configureOnly(m, kSourceGame);
    std::vector<float> out = pullBlocks(m, 3);
    for (float v : out) CHECK(v == 0.0f);
    CHECK(m.framesPulled() == 3 * kBlockFrames);
    // partial block: 100 frames then silence
    std::vector<float> in(100, 0.3f);
    m.push(kSourceGame, in.data(), 100, 1, 48000);
    out = pullBlocks(m, 1);
    for (int i = 0; i < 100; i++) CHECK(out[(size_t)i * 2] == 0.3f);
    for (int i = 100; i < kBlockFrames; i++) CHECK(out[(size_t)i * 2] == 0.0f);
    CHECK(m.source(kSourceGame).stats().underruns >= 1);
    // recovers
    m.push(kSourceGame, in.data(), 100, 1, 48000);
    out = pullBlocks(m, 1);
    CHECK(out[0] == 0.3f);
}

static void testPrefill() {
    AudioMixer m;
    bool en[3] = {true, false, false}; float g[3] = {1, 1, 1};
    SourceRing::Config c[3]; c[0].prefillMs = 40; c[0].capacityMs = 500; c[0].adaptivePrefill = false; c[1] = c[2] = c[0];
    m.configure(en, g, c);
    std::vector<float> in(480, 0.2f);                  // 10 ms
    m.push(kSourceGame, in.data(), 480, 1, 48000);
    auto out = pullBlocks(m, 1);
    CHECK(out[0] == 0.0f);                             // below the 40 ms prefill: silence, data kept
    for (int i = 0; i < 3; i++) m.push(kSourceGame, in.data(), 480, 1, 48000);   // now 40 ms buffered
    out = pullBlocks(m, 1);
    CHECK(out[0] == 0.2f && out[(kBlockFrames - 1) * 2] == 0.2f);
    // adaptive prefill: chunk of 2048 needs 1.25*2048 buffered
    AudioMixer a; SourceRing::Config ac; ac.prefillMs = 10; ac.capacityMs = 500; ac.adaptivePrefill = true;
    SourceRing::Config cs[3] = {ac, ac, ac}; a.configure(en, g, cs);
    std::vector<float> big(2048, 0.1f);
    a.push(kSourceGame, big.data(), 2048, 1, 48000);
    out = pullBlocks(a, 1);
    CHECK(out[0] == 0.0f);                             // 2048 < 2560
    a.push(kSourceGame, big.data(), 2048, 1, 48000);
    out = pullBlocks(a, 1);
    CHECK(out[0] == 0.1f);
}

static void testOverrunDropsOldestKeepsOrder() {
    AudioMixer m; configureOnly(m, kSourceGame, 1.0f, 100.0);   // 100 ms = 4800 frames
    const int N = 10000;
    std::vector<float> in(N);
    for (int i = 0; i < N; i++) in[(size_t)i] = (float)(i + 1) * 1e-5f;      // 1e-5 .. 0.1, ascending
    // push in uneven chunks
    int pos = 0; const int chunks[] = {1, 777, 2048, 300, 4096, 2778};
    for (int c : chunks) { int n = std::min(c, N - pos); m.push(kSourceGame, in.data() + pos, n, 1, 48000); pos += n; }
    CHECK(pos == N);
    CHECK(m.source(kSourceGame).bufferedFrames() == N);              // trimming happens on the consumer side...
    std::vector<float> out = pullBlocks(m, 11);                       // ...at the next pull
    SourceStats st = m.source(kSourceGame).stats();
    CHECK(st.droppedOverrun == (uint64_t)(N - 4800));
    CHECK(m.source(kSourceGame).bufferedFrames() == 0);
    // first 4800 output frames are exactly the newest 4800 inputs, in order
    for (int i = 0; i < 4800; i++) CHECK(out[(size_t)i * 2] == in[(size_t)(N - 4800 + i)]);
    CHECK(out[(size_t)4800 * 2] == 0.0f);
    // a single chunk bigger than the capacity keeps its newest part
    AudioMixer m2; configureOnly(m2, kSourceGame, 1.0f, 100.0);
    m2.push(kSourceGame, in.data(), N, 1, 48000);
    out = pullBlocks(m2, 10);
    for (int i = 0; i < 4800; i++) CHECK(out[(size_t)i * 2] == in[(size_t)(N - 4800 + i)]);
}

static void testConsumerStallBeyondPhysicalRing() {
    // A consumer stalled for much longer than the ring: the producer must not overwrite unread data
    // (no corruption, no reorder); what is kept is the oldest part and the loss is counted.
    AudioMixer m; configureOnly(m, kSourceGame, 1.0f, 1000.0);
    std::vector<float> chunk(1000);
    uint64_t counter = 1; uint64_t accepted = 0;
    for (int c = 0; c < 150; c++) {
        for (int i = 0; i < 1000; i++) chunk[(size_t)i] = (float)((counter + i) % 700000) * 1e-6f;
        accepted += (uint64_t)m.push(kSourceGame, chunk.data(), 1000, 1, 48000); counter += 1000;
    }
    SourceStats st = m.source(kSourceGame).stats();
    CHECK(accepted == (uint64_t)kRingFrames);
    CHECK(st.pushedFrames == accepted);
    CHECK(st.droppedOverrun == 150000 - accepted);
    float prev = 0; bool ordered = true; uint64_t got = 0;
    for (int b = 0; b < 250; b++) {
        auto out = pullBlocks(m, 1);
        for (int i = 0; i < kBlockFrames; i++) { float v = out[(size_t)i * 2]; if (v == 0.0f) continue; got++; if (!(v > prev)) ordered = false; prev = v; }
    }
    CHECK(ordered); CHECK(got == (uint64_t)kRingFrames / 2);   // trimmed to the capacity limit (half the ring) at the first pull
}

static void testCadenceAndPts() {
    AudioMixer m; configureOnly(m, kSourceGame);
    const int N = 1234;
    std::vector<float> blk((size_t)kBlockFrames * 2);
    for (int i = 0; i < N; i++) m.pull(blk.data());
    CHECK(m.framesPulled() == (uint64_t)N * kBlockFrames);

    AudioPts pts; CHECK(!pts.started());
    pts.start(0.1234);
    CHECK(pts.started());
    CHECK(pts.nextFrames() == (int64_t)std::llround(0.1234 * 48000));
    for (int i = 0; i < 100; i++) pts.advance(kBlockFrames);
    CHECK(pts.written() == 48000);
    CHECK_NEAR(pts.nextSeconds(), 0.1234 + 1.0, 1.0 / 48000);
    AudioPts neg; neg.start(-5.0); CHECK(neg.nextFrames() == 0);
    CHECK_NEAR(audioStartOffset(10.0, 10.25), 0.25, 1e-12);
    CHECK_NEAR(audioStartOffset(10.0, 9.9), 0.0, 1e-12);
}

static void testPumpClock() {
    // regular
    PumpClock c; c.start(100.0); double t; int n = 0;
    for (int i = 0; i <= 100; i++) { while (c.next(100.0 + i * 0.01 + 0.0005, &t)) n++; }
    CHECK(n == 100);
    // late timer: one call 55 ms late yields the 5 missed blocks, with the right nominal times
    PumpClock d; d.start(0.0); std::vector<double> times;
    while (d.next(0.0105, &t)) times.push_back(t);
    CHECK(times.size() == 1 && times[0] == 0.0);
    while (d.next(0.0655, &t)) times.push_back(t);
    CHECK(times.size() == 6);
    for (size_t i = 0; i < times.size(); i++) CHECK_NEAR(times[i], i * 0.01, 1e-9);
    // jittery timer for 10 minutes: the block count follows the wall clock exactly (no drift)
    PumpClock e; e.start(50.0); std::mt19937 rng(7); uint64_t blocks = 0; double now = 50.0;
    while (now < 650.0) { now += 0.010 + ((int)(rng() % 9) - 4) * 0.0005; while (e.next(now, &t)) blocks++; }
    CHECK(std::llabs((long long)blocks - (long long)std::floor((now - 50.0) / 0.01)) <= 1);
    // a long freeze resyncs instead of bursting
    PumpClock f; f.start(0.0); int burst = 0; while (f.next(30.0, &t)) burst++;
    CHECK(burst <= 101); CHECK(f.skippedResyncs() == 1);
}

static void testSourceFlags() {
    AudioMixer m; const float g[3] = {1, 1, 1};
    configureAll(m, g, /*game*/ true, /*voice*/ false, /*mic*/ false);
    std::vector<float> in(4800, 0.2f);
    CHECK(m.push(kSourceGame, in.data(), 4800, 1, 48000) == 4800);
    CHECK(m.push(kSourceVoice, in.data(), 4800, 1, 48000) == 0);
    CHECK(m.push(kSourceMic, in.data(), 4800, 1, 48000) == 0);
    CHECK(m.source(kSourceVoice).stats().pushedFrames == 0);
    auto out = pullBlocks(m, 10);
    for (int i = 0; i < 10 * kBlockFrames; i++) CHECK(out[(size_t)i * 2] == 0.2f);   // only game, not 0.6
    // invalid input is rejected without effect
    CHECK(m.push(kSourceGame, nullptr, 100, 1, 48000) == 0);
    CHECK(m.push(kSourceGame, in.data(), 0, 1, 48000) == 0);
    CHECK(m.push(kSourceGame, in.data(), 100, 0, 48000) == 0);
    CHECK(m.push(kSourceGame, in.data(), 100, 1, 100) == 0);
    // enabling later works; disabling stops intake immediately
    m.source(kSourceVoice).setEnabled(true);
    CHECK(m.push(kSourceVoice, in.data(), 480, 1, 48000) == 480);
    m.source(kSourceVoice).setEnabled(false);
    CHECK(m.push(kSourceVoice, in.data(), 480, 1, 48000) == 0);
    // multichannel input uses the first two channels
    AudioMixer s; configureOnly(s, kSourceGame);
    std::vector<float> six(480 * 6, 0.9f);
    for (int i = 0; i < 480; i++) { six[(size_t)i * 6] = 0.1f; six[(size_t)i * 6 + 1] = 0.2f; }
    s.push(kSourceGame, six.data(), 480, 6, 48000);
    out = pullBlocks(s, 1);
    CHECK(out[0] == 0.1f && out[1] == 0.2f);
}

static void testRateChangeAndGarbage() {
    AudioMixer m; configureOnly(m, kSourceVoice);
    auto a = sine(500, 0.3, 16000, 1600, 1), b = sine(500, 0.3, 48000, 4800, 1);
    m.push(kSourceVoice, a.data(), 1600, 1, 16000);
    pullBlocks(m, 3);
    m.push(kSourceVoice, b.data(), 4800, 1, 48000);   // rate switches mid-stream
    auto out = pullBlocks(m, 10);
    float pk = 0; for (float v : out) pk = std::max(pk, std::fabs(v));
    CHECK(pk > 0.25f && pk <= 0.31f);
    // NaN / inf in the input never reach the output as non-finite values
    AudioMixer n; configureOnly(n, kSourceGame);
    std::vector<float> bad(480, 0.1f); bad[10] = NAN; bad[20] = INFINITY; bad[30] = -INFINITY;
    n.push(kSourceGame, bad.data(), 480, 1, 48000);
    out = pullBlocks(n, 1);
    for (float v : out) CHECK(std::isfinite(v) && std::fabs(v) <= 1.0f);
}

// Producer thread pushes a strictly increasing ramp in random-sized chunks while the consumer
// pulls 10 ms blocks flat out. Under pressure data may be dropped (contention, overrun) or
// zero-filled (underrun), but must never be reordered, duplicated or corrupted.
static void testThreadStress() {
    AudioMixer m;
    bool en[3] = {true, true, false}; float g[3] = {1, 1, 1};
    SourceRing::Config c; c.prefillMs = 5; c.capacityMs = 200; c.adaptivePrefill = true;
    SourceRing::Config cs[3] = {c, c, c}; m.configure(en, g, cs);
    std::atomic<bool> done{false};
    const int total = 600000;
    std::thread producer([&] {
        std::mt19937 rng(1);
        int counter = 1; std::vector<float> chunk;
        while (counter < total) {
            int n = 1 + (int)(rng() % 1024);
            n = std::min(n, total - counter);
            chunk.resize((size_t)n);
            for (int i = 0; i < n; i++) chunk[(size_t)i] = (float)(counter + i) * 1e-6f;   // up to 0.6
            m.push(kSourceGame, chunk.data(), n, 1, 48000);
            counter += n;
            if (rng() % 8 == 0) std::this_thread::sleep_for(std::chrono::microseconds(50));
        }
        done = true;
    });
    // Voice source is pushed from a second producer at 16 kHz, then ignored for ordering.
    std::thread voice([&] {
        std::vector<float> v(320, 0.05f);
        while (!done) { m.push(kSourceVoice, v.data(), 320, 1, 16000); std::this_thread::sleep_for(std::chrono::microseconds(300)); }
    });
    float lastSeen = 0; bool ordered = true; uint64_t dataFrames = 0; bool finite = true;
    std::vector<float> blk((size_t)kBlockFrames * 2);
    int idle = 0;
    while (idle < 200) {
        m.pull(blk.data());
        bool any = false;
        for (int i = 0; i < kBlockFrames; i++) {
            float v = blk[(size_t)i * 2];
            if (!std::isfinite(v)) finite = false;
            // game ramp values are >= 1e-6; the voice adds 0.05 (>= ramp noise) -> subtract it where active
            if (v != 0.0f) { any = true; }
        }
        if (!any) idle++; else idle = 0;
        if (done && !any) idle++;
        // ordering is checked on the game source alone below (second pass), here only sanity
        if (!any) std::this_thread::sleep_for(std::chrono::microseconds(100));
        (void)lastSeen; (void)ordered; (void)dataFrames;
    }
    producer.join(); voice.join();
    CHECK(finite);
    // Ordering pass with only the game source (deterministic assertions on the output).
    AudioMixer m2; bool en2[3] = {true, false, false};
    m2.configure(en2, g, cs);
    std::atomic<bool> done2{false};
    std::thread p2([&] {
        std::mt19937 rng(2); int counter = 1; std::vector<float> chunk;
        while (counter < total) {
            int n = std::min(1 + (int)(rng() % 2000), total - counter);
            chunk.resize((size_t)n);
            for (int i = 0; i < n; i++) chunk[(size_t)i] = (float)(counter + i) * 1e-6f;
            m2.push(kSourceGame, chunk.data(), n, 1, 48000);
            counter += n;
            if (rng() % 4 == 0) std::this_thread::sleep_for(std::chrono::microseconds(80));
        }
        done2 = true;
    });
    float prev = 0; bool inOrder = true; uint64_t got = 0; int quiet = 0;
    while (quiet < 300) {
        m2.pull(blk.data());
        bool any = false;
        for (int i = 0; i < kBlockFrames; i++) {
            float v = blk[(size_t)i * 2];
            if (v == 0.0f) continue;
            any = true; got++;
            if (!(v > prev)) inOrder = false;      // strictly increasing: no reorder, no duplicates
            prev = v;
            if (blk[(size_t)i * 2 + 1] != v) inOrder = false;
        }
        if (!any && done2) quiet++;
        if (!any) std::this_thread::sleep_for(std::chrono::microseconds(50));
    }
    p2.join();
    CHECK(inOrder);
    CHECK(got > 100000);                           // the stress actually moved data
    SourceStats st = m2.source(kSourceGame).stats();
    std::printf("    stress: pushed=%llu got=%llu droppedOverrun=%llu droppedContention=%llu underruns=%llu\n",
                (unsigned long long)st.pushedFrames, (unsigned long long)got, (unsigned long long)st.droppedOverrun,
                (unsigned long long)st.droppedContention, (unsigned long long)st.underruns);
    CHECK(got <= st.pushedFrames);
}

// Steady-state at a realistic rate: a producer delivering exactly real-time in 21.3 ms
// chunks (Unity DSP buffer) against a 10 ms consumer must not underrun after priming.
static void testSteadyStateNoUnderrun() {
    AudioMixer m;
    bool en[3] = {true, false, false}; float g[3] = {1, 1, 1};
    SourceRing::Config c; c.prefillMs = 40; c.capacityMs = 500; c.adaptivePrefill = true;
    SourceRing::Config cs[3] = {c, c, c}; m.configure(en, g, cs);
    std::vector<float> chunk(1024 * 2, 0.1f), blk((size_t)kBlockFrames * 2);
    // simulated time in samples: producer delivers every 1024 frames, consumer every 480
    uint64_t produced = 0, consumed = 0; int blocksData = 0, blocksTotal = 0;
    for (uint64_t t = 0; t < 48000ull * 60; t += 16) {      // 16-frame simulation tick
        if (t / 1024 * 1024 == t) { m.push(kSourceGame, chunk.data(), 1024, 2, 48000); produced += 1024; }
        if (t % 480 == 0) { m.pull(blk.data()); consumed += 480; blocksTotal++; if (blk[0] != 0.0f) blocksData++; }
    }
    SourceStats st = m.source(kSourceGame).stats();
    CHECK(st.underruns <= 1);                  // at most the start-up transient
    CHECK(blocksData >= blocksTotal - 8);
    CHECK(st.droppedOverrun == 0);
    (void)produced; (void)consumed;
}

int main() {
    run("resample 8k/16k/22.05k/44.1k/48k, mono + stereo: frequency and amplitude", testResample);
    run("resample keeps stereo channels apart", testResampleStereoChannelsStayApart);
    run("48 kHz source is a bit-exact copy", testExactCopyAt48k);
    run("mixing: per-source gains", testMixingGains);
    run("mixing: sources at 48k/16k/44.1k together", testMixingDifferentRatesTogether);
    run("soft limiter: bounded, monotone, transparent below the knee", testSoftLimiter);
    run("underrun gives silence and recovers", testUnderrunSilence);
    run("prefill and adaptive prefill", testPrefill);
    run("overrun drops the oldest frames and keeps order", testOverrunDropsOldestKeepsOrder);
    run("consumer stall beyond the physical ring: no corruption", testConsumerStallBeyondPhysicalRing);
    run("10 ms cadence: N pulls = N*480 frames; PTS counting", testCadenceAndPts);
    run("pump clock follows the wall clock without drift", testPumpClock);
    run("source flags, invalid input, multichannel input", testSourceFlags);
    run("rate change mid-stream and NaN/inf input", testRateChangeAndGarbage);
    run("steady state: no underrun against Unity-sized chunks", testSteadyStateNoUnderrun);
    run("thread stress: producers vs consumer, no reorder", testThreadStress);
    std::printf("\n%d/%d tests passed, %d/%d checks passed\n", gTests - gTestsFailed, gTests, gChecks - gFailed, gChecks);
    return gFailed == 0 ? 0 : 1;
}
