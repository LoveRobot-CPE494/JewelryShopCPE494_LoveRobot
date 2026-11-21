using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class IdleBreathingTests
    {
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        public void ShouldBreathe_AnimationAndMovementState_MatchesStandingIdle(
            bool isInIdleState, bool isGrounded, bool isMoving, bool shouldBreathe)
        {
            Assert.That(
                IdleBreathing.ShouldBreathe(isInIdleState, isGrounded, isMoving),
                Is.EqualTo(shouldBreathe));
        }

        [Test]
        public void NextWeight_RampsUpAndDownAtTheRampRate_AndClamps()
        {
            Assert.That(IdleBreathing.NextWeight(0f, true, 0.1f, 0.3f),
                Is.EqualTo(1f / 3f).Within(1e-5f));
            Assert.That(IdleBreathing.NextWeight(0.9f, true, 1f, 0.3f), Is.EqualTo(1f));
            Assert.That(IdleBreathing.NextWeight(0.5f, false, 0.15f, 0.3f),
                Is.EqualTo(0f).Within(1e-5f));
            Assert.That(IdleBreathing.NextWeight(0.1f, false, 1f, 0.3f), Is.EqualTo(0f));
        }

        [Test]
        public void NextWeight_ZeroRamp_SnapsInsteadOfDividingByZero()
        {
            Assert.That(IdleBreathing.NextWeight(0f, true, 0.1f, 0f), Is.EqualTo(1f));
            Assert.That(IdleBreathing.NextWeight(1f, false, 0.1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Wave_IsASineOverThePeriod_AndZeroForBadPeriod()
        {
            Assert.That(IdleBreathing.Wave(0f, 4f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(IdleBreathing.Wave(1f, 4f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(IdleBreathing.Wave(3f, 4f), Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(IdleBreathing.Wave(1f, 0f), Is.EqualTo(0f));
        }
    }
}
