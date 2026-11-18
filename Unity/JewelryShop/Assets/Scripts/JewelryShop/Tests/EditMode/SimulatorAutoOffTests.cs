using NUnit.Framework;
using JewelryShop.Storefront;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class SimulatorAutoOffTests
    {
        static readonly (bool IsHmd, bool IsSimulated) Real = (true, false);
        static readonly (bool IsHmd, bool IsSimulated) Fake = (true, true);
        static readonly (bool IsHmd, bool IsSimulated) Controller = (false, false);

        [Test]
        public void NoDevices_NoDisplay_IsNotRealHeadset() =>
            Assert.That(SimulatorAutoOff.HasRealHeadset(new (bool, bool)[0], false), Is.False);

        [Test]
        public void OnlySimulatedHmdAndControllers_IsNotRealHeadset() =>
            Assert.That(SimulatorAutoOff.HasRealHeadset(new[] { Fake, Controller }, false), Is.False);

        [Test]
        public void RealHmd_IsRealHeadset() =>
            Assert.That(SimulatorAutoOff.HasRealHeadset(new[] { Controller, Real }, false), Is.True);

        [Test]
        public void RunningXrDisplay_IsRealHeadsetEvenBeforeDeviceRegisters() =>
            Assert.That(SimulatorAutoOff.HasRealHeadset(new (bool, bool)[0], true), Is.True);
    }
}
