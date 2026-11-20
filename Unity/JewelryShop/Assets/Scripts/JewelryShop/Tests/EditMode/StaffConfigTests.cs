using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class StaffConfigTests
    {
        [Test]
        public void MovementDefaults_EqualOldConstants()
        {
            var d = MovementSettings.Defaults;
            Assert.That(d.WalkSpeedMetersPerSecond, Is.EqualTo(1f));
            Assert.That(d.AccelerationMetersPerSecondSquared, Is.EqualTo(8f));
            Assert.That(d.TurnDegreesPerSecond, Is.EqualTo(180f));
            Assert.That(d.EdgeMarginMeters, Is.EqualTo(0.25f));
        }

        [Test]
        public void CameraDefaults_EqualOldConstants()
        {
            var d = CameraSettings.Defaults;
            Assert.That(d.PivotHeightMeters, Is.EqualTo(0.9f));
            Assert.That(d.LookDegreesPerPixel, Is.EqualTo(0.15f));
            Assert.That(d.MinPitchDegrees, Is.EqualTo(-20f));
            Assert.That(d.MaxPitchDegrees, Is.EqualTo(25f));
        }

        [Test]
        public void Defaults_AreValidWithoutWarnings()
        {
            var warnings = new List<string>();
            MovementSettings.Defaults.Validated(warnings);
            CameraSettings.Defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void Movement_OutOfRange_IsClampedWithWarning()
        {
            var s = MovementSettings.Defaults;
            s.WalkSpeedMetersPerSecond = -3f;
            s.AirControl = 5f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.WalkSpeedMetersPerSecond, Is.EqualTo(0f));
            Assert.That(v.AirControl, Is.EqualTo(1f));
            Assert.That(warnings.Count, Is.EqualTo(2));
            Assert.That(warnings[0], Does.Contain("WalkSpeedMetersPerSecond"));
        }

        [Test]
        public void Movement_NaN_FallsBackToDefault()
        {
            var s = MovementSettings.Defaults;
            s.TurnDegreesPerSecond = float.NaN;
            var v = s.Validated(new List<string>());
            Assert.That(v.TurnDegreesPerSecond, Is.EqualTo(180f));
        }

        [Test]
        public void Camera_InvertedPitchLimits_ResetToDefaults()
        {
            var s = CameraSettings.Defaults;
            s.MinPitchDegrees = 40f;
            s.MaxPitchDegrees = 10f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.MinPitchDegrees, Is.EqualTo(-20f));
            Assert.That(v.MaxPitchDegrees, Is.EqualTo(25f));
            Assert.That(warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void Camera_NegativeCollisionRadius_IsClamped()
        {
            var s = CameraSettings.Defaults;
            s.CollisionRadiusMeters = -1f;
            Assert.That(s.Validated(new List<string>()).CollisionRadiusMeters, Is.EqualTo(0f));
        }
    }
}
