using System.Runtime.InteropServices;
using GolfArcade.Game;
using NUnit.Framework;

namespace GolfArcade.Tests {
    public class NativeBridgeTests {
        const int Current = 7331;
        NativeSportsSession.Sample Sample()=>new() {version=NativeSportsSession.Sample.Version,session=Current,time=10,target=.3f,power=.5f,flags=1};
        [Test] public void CurrentSampleAccepted()=>Assert.IsTrue(NativeSportsSession.AcceptSample(Sample(),Current,9.9,10.1));
        [Test] public void WrongSessionRejected()=>Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),Current+1,9.9,10.1));
        [Test] public void OldAndFutureSamplesRejected() {
            Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),Current,9,11));
            Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),Current,9,9.8));
        }
        [Test] public void DuplicateRejected()=>Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),Current,10,10.1));
        [Test] public void NonFiniteRejected() {var s=Sample();s.target=float.NaN;Assert.IsFalse(NativeSportsSession.AcceptSample(s,Current,9,10));}
        [Test] public void OldJsonVersionRejected() {var s=Sample();s.version=1;Assert.IsFalse(NativeSportsSession.AcceptSample(s,Current,9,10.1));}

        /// The struct is copied byte-for-byte from the C struct in SportsBridge.mm, so its
        /// size and field offsets are a contract. 4+4+8 + 3*4 + 4*4 + 3*4 + 10*4 = 96 bytes.
        [Test] public void SampleLayoutMatchesTheNativeStruct() {
            Assert.AreEqual(96,Marshal.SizeOf<NativeSportsSession.Sample>());
            Assert.AreEqual(8,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("time"));
            Assert.AreEqual(28,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("swing"));
            Assert.AreEqual(56,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("qx"));
        }
        [Test] public void FlagsDecode() {
            var s=Sample(); s.flags=3;
            Assert.IsTrue(s.valid); Assert.IsTrue(s.degraded);
            s.flags=0; Assert.IsFalse(s.valid); Assert.IsFalse(s.degraded);
        }
    }
}
