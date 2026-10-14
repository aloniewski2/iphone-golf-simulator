using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tests
{
    /// 120 Hz swing input: only on a TV when asked, the TV still drawn at 60, and back to a plain 60 Hz loop when TV frames
    /// run late or the match ends.
    public class InputRateTests
    {
        GameObject host;
        [SetUp] public void Make() { host = new GameObject("InputRate host"); }
        [TearDown] public void Clear() { InputRate.Stop(); Object.DestroyImmediate(host); }

        [Test]
        public void OnATvWhenAskedTheLoopRunsAt120AndDrawsEveryOther()
        {
            InputRate.Begin(host, external: true, requestedHz: 120);
            Assert.IsTrue(InputRate.Fast);
            Assert.AreEqual(120, Application.targetFrameRate);
            Assert.AreEqual(2, OnDemandRendering.renderFrameInterval, "the TV gets 60 drawn frames");
        }

        [Test]
        public void NotWithoutATvOrWhenNotAsked()
        {
            InputRate.Begin(host, external: false, requestedHz: 120);
            Assert.IsFalse(InputRate.Fast, "the phone's own screen keeps its existing frame-rate setting");
            InputRate.Begin(host, external: true, requestedHz: 60);
            Assert.IsFalse(InputRate.Fast, "turned off, or a phone without ProMotion");
        }

        [Test]
        public void StoppingPutsBackOneDrawnFrameAt60AndMarksTheChange()
        {
            InputRate.Begin(host, true, 120);
            int generation = InputRate.Generation;
            InputRate.Stop();
            Assert.IsFalse(InputRate.Fast);
            Assert.AreEqual(1, OnDemandRendering.renderFrameInterval);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreNotEqual(generation, InputRate.Generation, "frame watchers restart their window");
            Assert.IsNull(host.GetComponent<InputRate>());
        }

        [Test]
        public void ATenthOfTvFramesLateEndsTheFastLoop()
        {
            Assert.IsTrue(InputRate.ShouldFallBack(120, 12));
            Assert.IsFalse(InputRate.ShouldFallBack(120, 11));
            Assert.IsFalse(InputRate.ShouldFallBack(0, 0));
        }
    }
}
