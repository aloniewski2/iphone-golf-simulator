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
        /// size and field offsets are a contract: 96 bytes of state plus three event clocks.
        [Test] public void SampleLayoutMatchesTheNativeStruct() {
            Assert.AreEqual(120,Marshal.SizeOf<NativeSportsSession.Sample>());
            Assert.AreEqual(8,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("time"));
            Assert.AreEqual(28,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("swing"));
            Assert.AreEqual(56,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("qx"));
            Assert.AreEqual(96,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("onsetTime"));
            Assert.AreEqual(104,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("confirmationTime"));
            Assert.AreEqual(112,(int)Marshal.OffsetOf<NativeSportsSession.Sample>("abortTime"));
        }
        [Test] public void FreshPacketCannotResurrectAnOldSwing() {
            Assert.IsTrue(NativeSportsSession.FreshEvent(9.9,10,9.8));
            Assert.IsFalse(NativeSportsSession.FreshEvent(9.5,10,9.8));
            Assert.IsFalse(NativeSportsSession.FreshEvent(9.9,10,9.95), "A stroke made before Ready cannot launch after Ready");
            Assert.IsFalse(NativeSportsSession.FreshEvent(double.NaN,10,0));
            Assert.That(NativeSportsSession.EventAge(9.9,10),Is.EqualTo(.1f).Within(.0001f));
        }
        [Test] public void InvalidEventClocksAndThePreviousBinaryLayoutAreRejected() {
            var s=Sample();s.version=2;
            Assert.IsFalse(NativeSportsSession.AcceptSample(s,Current,9,10));
            s=Sample();s.onsetTime=double.NaN;
            Assert.IsFalse(NativeSportsSession.AcceptSample(s,Current,9,10));
        }
        [Test] public void FlagsDecode() {
            var s=Sample(); s.flags=3;
            Assert.IsTrue(s.valid); Assert.IsTrue(s.degraded);
            s.flags=0; Assert.IsFalse(s.valid); Assert.IsFalse(s.degraded);
        }
    }
}
