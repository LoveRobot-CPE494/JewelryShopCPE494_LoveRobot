using System.Collections.Generic;
using System.Linq;
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
            Assert.That(d.RunSpeedMetersPerSecond, Is.GreaterThan(d.WalkSpeedMetersPerSecond));
            Assert.That(d.RunBlendSeconds, Is.GreaterThan(0f));
            Assert.That(d.RunPlaybackRate, Is.EqualTo(1f));
            Assert.That(d.AccelerationMetersPerSecondSquared, Is.EqualTo(8f));
            Assert.That(d.TurnDegreesPerSecond, Is.EqualTo(540f));
            Assert.That(d.KillPlaneYMeters, Is.EqualTo(-10f));
        }

        [Test]
        public void Validated_LegacyRunFields_UsesDefaultsAndKeepsRunFasterThanWalk()
        {
            var settings = MovementSettings.Defaults;
            settings.WalkSpeedMetersPerSecond = 3f;
            settings.RunSpeedMetersPerSecond = 0f;
            settings.RunBlendSeconds = 0f;
            settings.RunPlaybackRate = float.NaN;
            var warnings = new List<string>();

            var result = settings.Validated(warnings);

            Assert.That(result.RunSpeedMetersPerSecond, Is.GreaterThan(3f));
            Assert.That(result.RunBlendSeconds,
                Is.EqualTo(MovementSettings.Defaults.RunBlendSeconds));
            Assert.That(result.RunPlaybackRate, Is.EqualTo(1f));
            Assert.That(warnings, Has.Count.EqualTo(3));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(1.75f, 0.5f, 0.7f)]
        [TestCase(2.5f, 1f, 1f)]
        public void RunGait_GroundSpeed_MatchesBlendAndPlayback(float speedMetersPerSecond,
            float expectedWeight, float expectedPlayback)
        {
            var settings = MovementSettings.Defaults;
            settings.RunSpeedMetersPerSecond = 2.5f;

            var (weight, playbackRate) = settings.RunGait(speedMetersPerSecond);

            Assert.That(weight, Is.EqualTo(expectedWeight).Within(0.001f));
            Assert.That(playbackRate, Is.EqualTo(expectedPlayback).Within(0.001f));
        }

        [TestCase(0.1f, 0f)]
        [TestCase(0.2f, 0.1f)]
        [TestCase(0.3f, 0.2f)]
        public void RunKneeClearance_KneeLead_ScalesBetweenZeroAndFullClearance(
            float kneeLeadMeters, float expectedMeters)
        {
            Assert.That(SkirtSettings.Defaults.RunKneeClearance(kneeLeadMeters),
                Is.EqualTo(expectedMeters).Within(0.001f));
        }

        [TestCase(0f, 0.2f, 1f, 0.2f)]
        [TestCase(0.2f, 0f, 1f, 0.165f)]
        [TestCase(0.2f, 0f, 0f, 0.11f)]
        public void ReleaseRunClearance_KneeMoves_OpensImmediatelyAndSettlesGradually(
            float currentMeters, float requiredMeters, float runWeight, float expectedMeters)
        {
            var clearance = SkirtSettings.ReleaseRunClearance(currentMeters, requiredMeters,
                runWeight, 0.1f);
            Assert.That(clearance, Is.EqualTo(expectedMeters).Within(0.001f));
        }

        [Test]
        public void CameraDefaults_AreEyeHeightLayerAndFirstPersonValues()
        {
            var d = CameraSettings.Defaults;
            Assert.That(d.EyeHeightMeters, Is.EqualTo(1.6f));
            Assert.That(d.CollisionLayerMask, Is.EqualTo(1 << CameraSettings.CameraBlockingLayer));
            Assert.That(d.FirstPersonThresholdMeters, Is.EqualTo(0.6f));
            Assert.That(d.FirstPersonForwardMeters, Is.EqualTo(0.1f));
            Assert.That(d.FirstPersonMaxLookDownDegrees, Is.EqualTo(20f));
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
        public void Movement_KillPlaneOutOfRange_IsClampedWithWarning()
        {
            var s = MovementSettings.Defaults;
            s.KillPlaneYMeters = -5000f;
            var warnings = new List<string>();
            Assert.That(s.Validated(warnings).KillPlaneYMeters, Is.EqualTo(-1000f));
            Assert.That(warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void Movement_NaN_FallsBackToDefault()
        {
            var s = MovementSettings.Defaults;
            s.TurnDegreesPerSecond = float.NaN;
            var v = s.Validated(new List<string>());
            Assert.That(v.TurnDegreesPerSecond, Is.EqualTo(540f));
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

        [Test]
        public void Camera_UnsetRelaxSpeed_UsesDefault()
        {
            var s = CameraSettings.Defaults;
            s.CollisionRelaxMetersPerSecond = 0f; // an asset saved before the field existed
            var warnings = new List<string>();
            Assert.That(s.Validated(warnings).CollisionRelaxMetersPerSecond,
                Is.EqualTo(CameraSettings.Defaults.CollisionRelaxMetersPerSecond));
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void Camera_TinyRelaxSpeed_IsClamped()
        {
            var s = CameraSettings.Defaults;
            s.CollisionRelaxMetersPerSecond = 0.01f;
            Assert.That(s.Validated(new List<string>()).CollisionRelaxMetersPerSecond,
                Is.EqualTo(0.1f));
        }

        [Test]
        public void Camera_FirstPersonValuesOutOfRange_AreClampedOrDefaultedWithWarning()
        {
            var s = CameraSettings.Defaults;
            s.EyeHeightMeters = 9f;
            s.FirstPersonThresholdMeters = 50f;
            s.FirstPersonForwardMeters = -1f;
            s.FirstPersonMaxLookDownDegrees = 200f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.EyeHeightMeters, Is.EqualTo(CameraSettings.Defaults.EyeHeightMeters));
            Assert.That(v.FirstPersonThresholdMeters,
                Is.EqualTo(CameraSettings.Defaults.FirstPersonThresholdMeters));
            Assert.That(v.FirstPersonForwardMeters, Is.EqualTo(0f));
            Assert.That(v.FirstPersonMaxLookDownDegrees, Is.EqualTo(89f));
            Assert.That(warnings, Has.Count.EqualTo(4));
        }

        [Test]
        public void Camera_ZeroEyeHeightAndThreshold_UseDefaultsWithWarning()
        {
            var s = CameraSettings.Defaults;
            s.EyeHeightMeters = 0f; // also what an asset saved before the fields existed reads
            s.FirstPersonThresholdMeters = 0f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.EyeHeightMeters, Is.EqualTo(CameraSettings.Defaults.EyeHeightMeters));
            Assert.That(v.FirstPersonThresholdMeters,
                Is.EqualTo(CameraSettings.Defaults.FirstPersonThresholdMeters));
            Assert.That(warnings, Has.Count.EqualTo(2));
            Assert.That(warnings[0], Does.Contain("EyeHeightMeters"));
        }

        [Test]
        public void GestureMapDefaults_EqualOldKeys()
        {
            var d = GestureMapSettings.Defaults;
            var names = new[] { "Wave", "Bow", "Point", "Nod", "Shake", "Embarrassed" };
            Assert.That(d.Entries, Has.Length.EqualTo(6));
            for (var i = 0; i < 6; i++)
            {
                Assert.That(d.Entries[i].Digit, Is.EqualTo(i + 1));
                Assert.That(d.Entries[i].StateName, Is.EqualTo("Base Layer." + names[i]));
            }

            Assert.That(d.BlendSeconds, Is.EqualTo(0.08f));
        }

        [Test]
        public void IdleDefaults_WaitFiveToNineSecondsBeforeFidgeting()
        {
            var d = IdleSettings.Defaults;
            Assert.That(d.MinIntervalSeconds, Is.EqualTo(5f));
            Assert.That(d.MaxIntervalSeconds, Is.EqualTo(9f));
            Assert.That(d.BlendSeconds, Is.EqualTo(0.3f));
        }

        [Test]
        public void SkirtDefaults_EqualOldConstants()
        {
            var d = SkirtSettings.Defaults;
            Assert.That(d.SwingDegrees, Is.EqualTo(6f));
            Assert.That(d.FlareDegrees, Is.EqualTo(8f));
            Assert.That(d.FlareSpeedDegreesPerSecond, Is.EqualTo(80f));
            Assert.That(d.BowUprightWeight, Is.EqualTo(0.8f));
        }

        [Test]
        public void NewDefaults_AreValidWithoutWarnings()
        {
            var warnings = new List<string>();
            GestureMapSettings.Defaults.Validated(warnings);
            IdleSettings.Defaults.Validated(warnings);
            SkirtSettings.Defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void GestureMap_BadEntries_AreDroppedWithWarning()
        {
            var s = new GestureMapSettings
            {
                Entries = new[]
                {
                    new GestureEntry { Digit = 1, StateName = "Base Layer.Wave" },
                    new GestureEntry { Digit = 1, StateName = "Base Layer.Bow" },
                    new GestureEntry { Digit = 0, StateName = "Base Layer.Nod" },
                    new GestureEntry { Digit = 10, StateName = "Base Layer.Nod" },
                    new GestureEntry { Digit = 3, StateName = " " },
                    new GestureEntry { Digit = 4, StateName = "Base Layer.Nod" },
                },
                BlendSeconds = 0.08f,
            };
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.Entries.Select(e => e.Digit), Is.EqualTo(new[] { 1, 4 }));
            Assert.That(warnings, Has.Count.EqualTo(4));
        }

        [Test]
        public void GestureMap_EmptyOrNull_FallsBackToDefaultsWithWarning()
        {
            foreach (var entries in new[] { null, new GestureEntry[0] })
            {
                var s = new GestureMapSettings { Entries = entries, BlendSeconds = 0.08f };
                var warnings = new List<string>();
                var v = s.Validated(warnings);
                Assert.That(v.Entries, Has.Length.EqualTo(6));
                Assert.That(warnings, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void GestureMap_BlendOutOfRange_IsClamped()
        {
            var s = GestureMapSettings.Defaults;
            s.BlendSeconds = 5f;
            Assert.That(s.Validated(new List<string>()).BlendSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void Idle_InvertedInterval_ResetsToDefaults()
        {
            var s = IdleSettings.Defaults;
            s.MinIntervalSeconds = 20f;
            s.MaxIntervalSeconds = 5f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.MinIntervalSeconds, Is.EqualTo(5f));
            Assert.That(v.MaxIntervalSeconds, Is.EqualTo(9f));
            Assert.That(warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void Idle_NegativeBlend_IsClamped()
        {
            var s = IdleSettings.Defaults;
            s.BlendSeconds = -1f;
            Assert.That(s.Validated(new List<string>()).BlendSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void Skirt_OutOfRange_IsClampedWithWarning()
        {
            var s = SkirtSettings.Defaults;
            s.BowUprightWeight = 2f;
            s.FlareSpeedDegreesPerSecond = float.NaN;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.BowUprightWeight, Is.EqualTo(1f));
            Assert.That(v.FlareSpeedDegreesPerSecond, Is.EqualTo(80f));
            Assert.That(warnings, Has.Count.EqualTo(2));
        }
    }
}
