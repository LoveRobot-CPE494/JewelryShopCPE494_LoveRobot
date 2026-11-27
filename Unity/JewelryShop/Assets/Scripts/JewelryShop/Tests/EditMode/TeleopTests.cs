using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class TeleopTests
    {
        [Test]
        public void Messages_MatchTheBridgeWireFormat()
        {
            Assert.AreEqual("{\"type\":\"release\"}", TeleopMessages.Release);
            Assert.AreEqual("{\"type\":\"stop\"}", TeleopMessages.Stop);
            Assert.AreEqual("{\"type\":\"home\"}", TeleopMessages.Home);
            Assert.AreEqual("{\"type\":\"grip\",\"value\":1}", TeleopMessages.Grip(true));
            Assert.AreEqual("{\"type\":\"grip\",\"value\":0}", TeleopMessages.Grip(false));
            Assert.AreEqual("{\"type\":\"engage\",\"x\":0.1235,\"y\":-0.5000,\"z\":2.0000}",
                TeleopMessages.Engage(0.12346f, -0.5f, 2f));
            Assert.AreEqual(
                "{\"type\":\"pose\",\"frame\":\"headset\",\"x\":0.1000,\"y\":0.2000,\"z\":0.3000}",
                TeleopMessages.Pose(0.1f, 0.2f, 0.3f));
        }

        [Test]
        public void HasSentRecently_FalseUntilFirstSend()
        {
            Assert.IsFalse(new TeleopSendTracker().HasSentRecently(0));
        }

        [Test]
        public void HasSentRecently_TrueWithinWindowFalseAfter()
        {
            var t = new TeleopSendTracker();
            t.MarkSent(10);
            Assert.IsTrue(t.HasSentRecently(10.9));
            Assert.IsTrue(t.HasSentRecently(11.0));
            Assert.IsFalse(t.HasSentRecently(11.1));
            Assert.AreEqual(10, t.LastSentTime);
        }

        [Test]
        public void Target_FirstAddress_ConnectsWithoutReleasingAnything()
        {
            var t = new TeleopTarget();
            Assert.IsTrue(t.TryRetarget("10.0.0.5", engaged: false, out var releaseOld));
            Assert.AreEqual("10.0.0.5", t.Address);
            Assert.IsFalse(releaseOld);
        }

        [Test]
        public void Target_SameAddressEveryFrame_DoesNotReconnect()
        {
            var t = new TeleopTarget();
            t.TryRetarget("10.0.0.5", false, out _);
            Assert.IsFalse(t.TryRetarget("10.0.0.5", true, out _));
        }

        [Test]
        public void Target_ChangeWhileEngaged_ReleasesTheOldTargetFirst()
        {
            var t = new TeleopTarget();
            t.TryRetarget("10.0.0.5", false, out _);
            Assert.IsTrue(t.TryRetarget("10.0.0.9", engaged: true, out var releaseOld));
            Assert.IsTrue(releaseOld);
            Assert.AreEqual("10.0.0.9", t.Address);
        }

        [Test]
        public void Target_ChangeWhileNotEngaged_SendsNoRelease()
        {
            var t = new TeleopTarget();
            t.TryRetarget("10.0.0.5", false, out _);
            t.TryRetarget("10.0.0.9", engaged: false, out var releaseOld);
            Assert.IsFalse(releaseOld);
        }

        [Test]
        public void Target_NoAddress_ChangesOnceSoTheWarningIsLoggedOnce()
        {
            var t = new TeleopTarget();
            Assert.IsTrue(t.TryRetarget(null, false, out _));   // at start: warn
            Assert.IsNull(t.Address);
            Assert.IsFalse(t.TryRetarget(null, false, out _));  // next frames: silent
            Assert.IsTrue(t.TryRetarget("10.0.0.5", true, out var releaseOld));
            Assert.IsFalse(releaseOld);                          // nothing was connected
            Assert.IsTrue(t.TryRetarget(null, true, out releaseOld));
            Assert.IsTrue(releaseOld);                           // lost the address while engaged
        }
    }
}
