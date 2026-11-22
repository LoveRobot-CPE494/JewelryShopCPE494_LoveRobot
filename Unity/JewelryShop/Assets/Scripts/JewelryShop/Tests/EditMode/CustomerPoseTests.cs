using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class CustomerPoseTests
    {
        [Test]
        public void Step_FirstSample_SnapsToIt()
        {
            var smoother = new CustomerPoseSmoother();
            var result = smoother.Step(2f, 1.6f, 3f, 90f, 0.016f);
            Assert.That(result.X, Is.EqualTo(2f));
            Assert.That(result.Z, Is.EqualTo(3f));
            Assert.That(result.YawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void Step_SmallMove_SmoothsWithoutSnapping()
        {
            var smoother = new CustomerPoseSmoother();
            smoother.Step(0f, 0f, 0f, 0f, 0.016f);
            var result = smoother.Step(0.5f, 0f, 0f, 0f, 0.016f);
            Assert.That(result.X, Is.GreaterThan(0f).And.LessThan(0.5f));
        }

        [Test]
        public void Step_JumpAboveThreshold_SnapsWithNoSmoothing()
        {
            var smoother = new CustomerPoseSmoother();
            smoother.Step(0f, 0f, 0f, 0f, 0.016f);
            var jump = CustomerPoseSmoother.TeleportSnapMeters + 0.5f;
            var result = smoother.Step(jump, 0f, 0f, 45f, 0.016f);
            Assert.That(result.X, Is.EqualTo(jump));
            Assert.That(result.YawDegrees, Is.EqualTo(45f));
        }

        [Test]
        public void Step_YawAcrossWrap_TurnsTheShortWay()
        {
            var smoother = new CustomerPoseSmoother();
            smoother.Step(0f, 0f, 0f, 350f, 0.016f);
            var result = smoother.Step(0f, 0f, 0f, 10f, 0.016f);
            // Short way is +20 deg through 360, so the result sits just past 350, not near 180.
            var delta = CustomerPoseSmoother.ShortestDeltaDegrees(350f, result.YawDegrees);
            Assert.That(delta, Is.GreaterThan(0f).And.LessThan(20f));
        }

        [Test]
        public void Step_YawKeepsTurningPastTwoFullTurns_NeverSpinsTheLongWay()
        {
            var smoother = new CustomerPoseSmoother();
            var target = 0f;
            var previous = smoother.Step(0f, 0f, 0f, 0f, 0.016f).YawDegrees;
            for (var i = 0; i < 400; i++)
            {
                target = (target + 5f) % 360f; // the Customer keeps turning the same way
                var yaw = smoother.Step(0f, 0f, 0f, target, 0.016f).YawDegrees;
                Assert.That(yaw, Is.InRange(0f, 360f));
                Assert.That(CustomerPoseSmoother.ShortestDeltaDegrees(previous, yaw),
                    Is.GreaterThanOrEqualTo(0f).And.LessThan(10f), $"step {i}");
                previous = yaw;
            }
        }

        [Test]
        public void Step_HeightJumpAlone_DoesNotSnap()
        {
            var smoother = new CustomerPoseSmoother();
            smoother.Step(0f, 0f, 0f, 0f, 0.016f);
            var result = smoother.Step(0f, CustomerPoseSmoother.TeleportSnapMeters + 1f, 0f, 0f, 0.016f);
            Assert.That(result.Y, Is.LessThan(CustomerPoseSmoother.TeleportSnapMeters + 1f));
        }

        [Test]
        public void Reset_NextSampleSnaps()
        {
            var smoother = new CustomerPoseSmoother();
            smoother.Step(0f, 0f, 0f, 0f, 0.016f);
            smoother.Reset();
            Assert.That(smoother.Step(0.1f, 0f, 0f, 0f, 0.016f).X, Is.EqualTo(0.1f));
        }

        [Test]
        public void GemCode_RoundTripsEveryGem_AndNoneIsEmpty()
        {
            foreach (GemCut cut in System.Enum.GetValues(typeof(GemCut)))
            foreach (GemColor color in System.Enum.GetValues(typeof(GemColor)))
            {
                var key = new GemKey(cut, color);
                Assert.That(GemKeyCode.TryDecode(GemKeyCode.Encode(key), out var back), Is.True);
                Assert.That(back, Is.EqualTo(key));
            }
            Assert.That(GemKeyCode.TryDecode(GemKeyCode.None, out _), Is.False);
            Assert.That(GemKeyCode.TryDecode(200, out _), Is.False);
        }

        [Test]
        public void HasChanged_BelowEpsilonIsQuiet_AboveOrGemChangeIsNot()
        {
            var a = new CustomerPose { GemCode = GemKeyCode.None, HeadYawDegrees = 359.9f };
            var b = a;
            b.HandX = 0.002f;           // 2 mm of headset jitter
            b.HeadYawDegrees = 0.1f;    // 0.2 deg across the wrap
            b.HandRotW = 0.001f;
            Assert.That(b.HasChanged(a), Is.False);
            b.HandX = 0.02f;
            Assert.That(b.HasChanged(a), Is.True);
            var c = a;
            c.HeadYawDegrees = 2f;
            Assert.That(c.HasChanged(a), Is.True);
            var d = a;
            d.HandRotW = 0.01f;
            Assert.That(d.HasChanged(a), Is.True);
            var e = a;
            e.GemCode = 3;
            Assert.That(e.HasChanged(a), Is.True);
        }

        [Test]
        public void Mailbox_PublishThenClear_ReadsAvailableThenNot()
        {
            var box = new CustomerPoseMailbox();
            Assert.That(box.IsAvailable, Is.False);
            box.Publish(new CustomerPose { HeadX = 4f });
            Assert.That(box.IsAvailable, Is.True);
            Assert.That(box.Latest.HeadX, Is.EqualTo(4f));
            box.Clear();
            Assert.That(box.IsAvailable, Is.False);
        }
    }
}
