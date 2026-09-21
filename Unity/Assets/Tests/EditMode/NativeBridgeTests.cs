using GolfArcade.Game;
using NUnit.Framework;

namespace GolfArcade.Tests {
    public class NativeBridgeTests {
        NativeSportsSession.Sample Sample()=>new() {version=1,session="current",time=10,target=.3f,power=.5f};
        [Test] public void CurrentSampleAccepted()=>Assert.IsTrue(NativeSportsSession.AcceptSample(Sample(),"current",9.9,10.1));
        [Test] public void WrongSessionRejected()=>Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),"other",9.9,10.1));
        [Test] public void OldAndFutureSamplesRejected() {
            Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),"current",9,11));
            Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),"current",9,9.8));
        }
        [Test] public void DuplicateRejected()=>Assert.IsFalse(NativeSportsSession.AcceptSample(Sample(),"current",10,10.1));
        [Test] public void NonFiniteRejected() {var s=Sample();s.target=float.NaN;Assert.IsFalse(NativeSportsSession.AcceptSample(s,"current",9,10));}
    }
}
