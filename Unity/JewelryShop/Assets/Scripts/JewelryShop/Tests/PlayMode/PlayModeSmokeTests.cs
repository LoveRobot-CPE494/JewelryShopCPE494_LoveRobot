using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JewelryShop.Tests
{
    // Proves the PlayMode suite is wired: Play Mode starts and advances a frame.
    [TestFixture]
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayModeSuite_YieldOneFrame_FrameCountAdvances()
        {
            var before = Time.frameCount;

            yield return null;

            Assert.That(Time.frameCount, Is.GreaterThan(before));
        }
    }
}
