using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class GemReturnTimerTests
    {
        private const float DelaySeconds = 3f;
        private const float FlightSeconds = 0.5f;

        private static GemReturnTimer Make() => new GemReturnTimer(DelaySeconds, FlightSeconds);

        [Test]
        public void Tick_NeverReleased_StaysIdle()
        {
            var t = Make();
            t.Tick(10f);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Idle));
        }

        [Test]
        public void FlightProgress_WhileWaiting_IsZero()
        {
            var t = Make();
            t.Release();
            t.Tick(1f);
            Assert.That(t.FlightProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Release_WhileFlying_KeepsFlying()
        {
            var t = Make();
            t.ReturnNow();
            t.Release();
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Flying));
        }

        [Test]
        public void Tick_BeforeDelay_StillWaiting()
        {
            var t = Make();
            t.Release();
            t.Tick(2.9f);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Waiting));
        }

        [Test]
        public void Tick_DelayElapses_StartsFlying()
        {
            var t = Make();
            t.Release();
            t.Tick(3f);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Flying));
            Assert.That(t.FlightProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_DelayOvershoot_CarriesLeftoverIntoFlight()
        {
            var t = Make();
            t.Release();
            t.Tick(3.25f);
            Assert.That(t.FlightProgress, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Grab_WhileWaiting_CancelsReturn()
        {
            var t = Make();
            t.Release();
            t.Tick(2f);
            t.Grab();
            t.Tick(10f);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Idle));
        }

        [Test]
        public void Grab_MidFlight_CancelsReturn()
        {
            var t = Make();
            t.Release();
            t.Tick(3.2f);
            t.Grab();
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Idle));
        }

        [Test]
        public void ReturnNow_SkipsDelay()
        {
            var t = Make();
            t.ReturnNow();
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Flying));
        }

        [Test]
        public void Release_Again_RestartsDelay()
        {
            var t = Make();
            t.Release();
            t.Tick(2.5f);
            t.Release();
            t.Tick(2.5f);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Waiting));
        }

        [Test]
        public void Tick_FlightFinishes_ReportsArrivedOnceAndGoesIdle()
        {
            var t = Make();
            t.ReturnNow();
            Assert.That(t.Tick(0.25f), Is.False);
            Assert.That(t.Tick(0.25f), Is.True);
            Assert.That(t.Phase, Is.EqualTo(GemReturnTimer.PhaseKind.Idle));
            Assert.That(t.Tick(1f), Is.False);
        }

        [Test]
        public void Tick_ZeroFlightDuration_ArrivesImmediately()
        {
            var t = new GemReturnTimer(0f, 0f);
            t.ReturnNow();
            Assert.That(t.Tick(0f), Is.True);
        }
    }
}
