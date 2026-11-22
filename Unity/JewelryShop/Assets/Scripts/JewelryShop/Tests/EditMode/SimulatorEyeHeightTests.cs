using JewelryShop.Storefront;
using NUnit.Framework;
using UnityEngine.XR;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class SimulatorEyeHeightTests
    {
        [Test]
        public void SimulatorOn_UnknownMode_NeedsLift() =>
            Assert.That(SimulatorEyeHeight.NeedsLift(true, TrackingOriginModeFlags.Unknown), Is.True);

        [Test]
        public void SimulatorOff_UnknownMode_DoesNotNeedLift() =>
            Assert.That(SimulatorEyeHeight.NeedsLift(false, TrackingOriginModeFlags.Unknown), Is.False);

        [TestCase(TrackingOriginModeFlags.Floor)]
        [TestCase(TrackingOriginModeFlags.Device)]
        public void SimulatorOn_KnownMode_DoesNotNeedLift(TrackingOriginModeFlags mode) =>
            Assert.That(SimulatorEyeHeight.NeedsLift(true, mode), Is.False);

        [TestCase(TrackingOriginModeFlags.Floor)]
        [TestCase(TrackingOriginModeFlags.Device)]
        public void RealHeadset_KnownMode_DoesNotNeedLift(TrackingOriginModeFlags mode) =>
            Assert.That(SimulatorEyeHeight.NeedsLift(false, mode), Is.False);
    }
}
