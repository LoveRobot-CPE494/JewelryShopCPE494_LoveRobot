using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class JumpMathTests
    {
        [Test]
        public void TakeoffSpeed_FromHeightAndGravity_ReachesConfiguredHeight()
        {
            const float height = 0.5f;
            const float gravity = 12f;
            var speed = JumpMath.TakeoffSpeed(height, gravity);
            Assert.That(speed * speed / (2f * gravity), Is.EqualTo(height).Within(1e-5f));
        }

        [Test]
        public void ControlScale_GroundedIsFull_AirIsAirControlClamped()
        {
            Assert.That(JumpMath.ControlScale(true, 0.3f), Is.EqualTo(1f));
            Assert.That(JumpMath.ControlScale(false, 0.3f), Is.EqualTo(0.3f));
            Assert.That(JumpMath.ControlScale(false, 5f), Is.EqualTo(1f));
            Assert.That(JumpMath.ControlScale(false, -1f), Is.EqualTo(0f));
        }

        [Test]
        public void Movement_JumpDefaults_AreValid_AndOutOfRangeIsClamped()
        {
            var warnings = new List<string>();
            MovementSettings.Defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);

            var s = MovementSettings.Defaults;
            s.JumpHeightMeters = -1f;
            s.JumpTakeoffDelaySeconds = float.NaN;
            var v = s.Validated(warnings);
            Assert.That(v.JumpHeightMeters, Is.EqualTo(0f));
            Assert.That(v.JumpTakeoffDelaySeconds,
                Is.EqualTo(MovementSettings.Defaults.JumpTakeoffDelaySeconds));
            Assert.That(warnings, Has.Count.EqualTo(2));
        }
    }
}
