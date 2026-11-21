using System;
using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class LookAtTests
    {
        private sealed class FakeStore : ILookAtStore
        {
            public bool? Value;
            public bool? Get() => Value;
            public void Set(bool on) => Value = on;
        }

        private static readonly LookAtSettings s_defaults = LookAtSettings.Defaults;

        [Test]
        public void Defaults_AreValidWithoutWarnings()
        {
            var warnings = new List<string>();
            s_defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);
            Assert.That(s_defaults.DefaultOn, Is.False);
        }

        [Test]
        public void Validated_CorrectsNegativeAndHugeValuesWithWarnings()
        {
            var bad = new LookAtSettings
            {
                YawLimitDegrees = -5f, PitchLimitDegrees = 200f, SmoothingSeconds = -1f,
            };
            var warnings = new List<string>();
            var v = bad.Validated(warnings);
            Assert.That(v.YawLimitDegrees, Is.GreaterThanOrEqualTo(0f));
            Assert.That(v.PitchLimitDegrees, Is.LessThanOrEqualTo(90f));
            Assert.That(v.SmoothingSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(warnings.Count, Is.EqualTo(3));
        }

        [Test]
        public void IsOn_NothingStored_UsesConfigDefault()
        {
            var store = new FakeStore();
            Assert.That(LookAt.IsOn(store, s_defaults), Is.False);
            var on = s_defaults;
            on.DefaultOn = true;
            Assert.That(LookAt.IsOn(store, on), Is.True);
        }

        [Test]
        public void IsOn_StoredValueAlwaysWins()
        {
            var on = s_defaults;
            on.DefaultOn = true;
            var store = new FakeStore { Value = false };
            Assert.That(LookAt.IsOn(store, on), Is.False);
            store.Value = true;
            Assert.That(LookAt.IsOn(store, s_defaults), Is.True);
        }

        [Test]
        public void Toggle_FlipsTheFlag_RemembersIt()
        {
            var store = new FakeStore();
            Assert.That(LookAt.Toggle(store, s_defaults), Is.True);
            Assert.That(store.Value, Is.True);
            Assert.That(LookAt.Toggle(store, s_defaults), Is.False);
            Assert.That(store.Value, Is.False);
        }

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(30f, 10f, 30f, 10f)]
        [TestCase(80f, 50f, 45f, 25f)]
        [TestCase(-80f, -50f, -45f, -25f)]
        [TestCase(170f, 0f, 45f, 0f)]
        [TestCase(-170f, 0f, -45f, 0f)]
        [TestCase(-180f, 0f, 45f, 0f)]
        [TestCase(180f, 0f, 45f, 0f)]
        public void Clamp_PastLimits_StaysAtLimit(float yaw, float pitch, float ey, float ep)
        {
            var (y, p) = LookAt.Clamp(yaw, pitch, s_defaults);
            Assert.That(y, Is.EqualTo(ey).Within(1e-4));
            Assert.That(p, Is.EqualTo(ep).Within(1e-4));
        }

        [Test]
        public void Angles_DirectlyBehind_TakesRightHandLimitEvenForNegativeZeroX()
        {
            Assert.That(LookAt.Angles(0f, 0f, -1f, s_defaults).Yaw, Is.EqualTo(45f));
            Assert.That(LookAt.Angles(-0f, 0f, -1f, s_defaults).Yaw, Is.EqualTo(45f));
        }

        [Test]
        public void Angles_UsesShortestSignedAngle()
        {
            // Slightly behind-left is a left turn, clamped to the left limit.
            Assert.That(LookAt.Angles(-0.1f, 0f, -1f, s_defaults).Yaw, Is.EqualTo(-45f));
            // 20 degrees right and 10 degrees up passes through unclamped.
            var cosP = (float)Math.Cos(10 * Math.PI / 180);
            var d = LookAt.Angles(
                cosP * (float)Math.Sin(20 * Math.PI / 180),
                (float)Math.Sin(10 * Math.PI / 180),
                cosP * (float)Math.Cos(20 * Math.PI / 180), s_defaults);
            Assert.That(d.Yaw, Is.EqualTo(20f).Within(0.1f));
            Assert.That(d.Pitch, Is.EqualTo(10f).Within(0.1f));
        }

        [Test]
        public void Ease_TowardTarget_MovesWithoutOvershoot()
        {
            var v = LookAt.Ease(0f, 10f, 0.05f, 0.15f);
            Assert.That(v, Is.GreaterThan(0f).And.LessThan(10f));
            Assert.That(LookAt.Ease(0f, 10f, 0.15f, 0.15f), Is.GreaterThan(9f));
            Assert.That(LookAt.Ease(3f, 10f, 0f, 0.15f), Is.EqualTo(3f));
        }

        [Test]
        public void Ease_ZeroSmoothing_Snaps()
        {
            Assert.That(LookAt.Ease(0f, 10f, 0.016f, 0f), Is.EqualTo(10f));
        }

        [Test]
        public void Head_OnThenOff_WeightEasesInThenBackToZero()
        {
            var head = new LookAtHead();
            var s = head.Tick(0.016f, true, 30f, 0f, s_defaults);
            Assert.That(s.Weight, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(s.Yaw, Is.EqualTo(30f), "first frame snaps the angle; the weight does the easing");
            for (var i = 0; i < 100; i++)
                s = head.Tick(0.016f, true, 30f, 0f, s_defaults);
            Assert.That(s.Weight, Is.EqualTo(1f).Within(0.01f));
            var off = head.Tick(0.016f, false, 30f, 0f, s_defaults);
            Assert.That(off.Weight, Is.LessThan(1f).And.GreaterThan(0f));
            for (var i = 0; i < 100; i++)
                off = head.Tick(0.016f, false, 30f, 0f, s_defaults);
            Assert.That(off.Weight, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Head_On_AnglesEaseTowardMovingTarget()
        {
            var head = new LookAtHead();
            for (var i = 0; i < 100; i++)
                head.Tick(0.016f, true, 10f, 0f, s_defaults);
            var s = head.Tick(0.016f, true, 40f, 0f, s_defaults);
            Assert.That(s.Yaw, Is.GreaterThan(10f).And.LessThan(40f));
            for (var i = 0; i < 100; i++)
                s = head.Tick(0.016f, true, 40f, 0f, s_defaults);
            Assert.That(s.Yaw, Is.EqualTo(40f).Within(0.1f));
        }

        [Test]
        public void Head_Off_AnglesStayFrozen_OnlyWeightFades()
        {
            var head = new LookAtHead();
            LookAtPose on = default;
            for (var i = 0; i < 100; i++)
                on = head.Tick(0.016f, true, 30f, 5f, s_defaults);
            var off = head.Tick(0.016f, false, -40f, -20f, s_defaults);
            Assert.That(off.Yaw, Is.EqualTo(on.Yaw));
            Assert.That(off.Pitch, Is.EqualTo(on.Pitch));
            Assert.That(off.Weight, Is.LessThan(on.Weight));
        }

        [Test]
        public void StatusText_OffAndOn_MatchesTicketStrings()
        {
            var focus = new LookAtFocus();
            Assert.That(LookAt.StatusText(false, focus, false, LookAtTarget.Camera, ""),
                Is.EqualTo("Look-At: OFF"));
            Assert.That(LookAt.StatusText(true, focus, false, LookAtTarget.Camera, ""),
                Is.EqualTo("Look-At: ON · Focus: OFF · Target: camera"));
            Assert.That(LookAt.StatusText(true, focus, false, LookAtTarget.Interest, "Display_01"),
                Is.EqualTo("Look-At: ON · Focus: OFF · Target: Display_01"));
        }

        [Test]
        public void StatusText_FocusOn_NamesCustomerOrTheFallbackReason()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, false, 0f, s_defaults);
            Assert.That(LookAt.StatusText(true, focus, false, LookAtTarget.Camera, ""),
                Is.EqualTo("Look-At: ON · Focus: ON (no customer) · Target: camera"));
            focus.Tick(true, false, true, 3.8f, s_defaults);
            Assert.That(LookAt.StatusText(true, focus, true, LookAtTarget.Interest, "Display_01"),
                Is.EqualTo("Look-At: ON · Focus: ON (out of range) · Target: Display_01"));
            focus.Tick(true, false, true, 2f, s_defaults);
            Assert.That(LookAt.StatusText(true, focus, true, LookAtTarget.Customer, "Display_01"),
                Is.EqualTo("Look-At: ON · Focus: ON · Target: Customer"));
        }

        [TestCase(true, true, LookAtTarget.Customer)]
        [TestCase(true, false, LookAtTarget.Customer)]
        [TestCase(false, true, LookAtTarget.Interest)]
        [TestCase(false, false, LookAtTarget.Camera)]
        public void PickTarget_FocusThenInterestThenCamera(bool latched, bool interest,
            LookAtTarget expected) =>
            Assert.That(LookAt.PickTarget(latched, interest), Is.EqualTo(expected));

        [Test]
        public void Focus_WhileLookAtOff_PressDoesNothing()
        {
            var focus = new LookAtFocus();
            focus.Tick(false, true, true, 1f, s_defaults);
            Assert.That(focus.IsOn, Is.False);
            Assert.That(focus.InRange, Is.False);
        }

        [Test]
        public void Focus_LookAtTurnedOff_TurnsFocusOff_AndStaysOffWhenBackOn()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, true, 1f, s_defaults);
            Assert.That(focus.InRange, Is.True);
            focus.Tick(false, false, true, 1f, s_defaults);
            Assert.That(focus.IsOn, Is.False);
            focus.Tick(true, false, true, 1f, s_defaults);
            Assert.That(focus.IsOn, Is.False);
        }

        [Test]
        public void Focus_PressTwice_TogglesOff()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, true, 1f, s_defaults);
            focus.Tick(true, true, true, 1f, s_defaults);
            Assert.That(focus.IsOn, Is.False);
            Assert.That(focus.InRange, Is.False);
        }

        [Test]
        public void Focus_Hysteresis_EnterAt35_ExitPast40()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, true, 5f, s_defaults);
            Assert.That(focus.InRange, Is.False);
            focus.Tick(true, false, true, 3.6f, s_defaults);
            Assert.That(focus.InRange, Is.False);
            focus.Tick(true, false, true, 3.5f, s_defaults);
            Assert.That(focus.InRange, Is.True);
            focus.Tick(true, false, true, 4.0f, s_defaults);
            Assert.That(focus.InRange, Is.True);
            focus.Tick(true, false, true, 4.01f, s_defaults);
            Assert.That(focus.InRange, Is.False);
            focus.Tick(true, false, true, 3.8f, s_defaults);
            Assert.That(focus.InRange, Is.False);
        }

        [Test]
        public void Focus_SwitchedOnBetweenDistances_StartsOutOfRange()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, true, 3.8f, s_defaults);
            Assert.That(focus.IsOn, Is.True);
            Assert.That(focus.InRange, Is.False);
        }

        [Test]
        public void Focus_CustomerGoneThenBack_MustReenterAtEnterDistance()
        {
            var focus = new LookAtFocus();
            focus.Tick(true, true, true, 1f, s_defaults);
            focus.Tick(true, false, false, 0f, s_defaults);
            Assert.That(focus.InRange, Is.False);
            focus.Tick(true, false, true, 3.8f, s_defaults);
            Assert.That(focus.InRange, Is.False);
        }

        [Test]
        public void Validated_ExitBelowEnter_IsRaisedWithWarning()
        {
            var bad = LookAtSettings.Defaults;
            bad.FocusEnterMeters = 4f;
            bad.FocusExitMeters = 3f;
            var warnings = new List<string>();
            var v = bad.Validated(warnings);
            Assert.That(v.FocusExitMeters, Is.EqualTo(4f));
            Assert.That(warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void Validated_NewFieldsOutOfRange_AreCorrectedWithWarnings()
        {
            var bad = LookAtSettings.Defaults;
            bad.FrontHalfAngleDegrees = 200f;
            bad.FrontHysteresisDegrees = -1f;
            bad.BodyTurnSeconds = -2f;
            var warnings = new List<string>();
            var v = bad.Validated(warnings);
            Assert.That(v.FrontHalfAngleDegrees, Is.EqualTo(180f));
            Assert.That(v.FrontHysteresisDegrees, Is.EqualTo(0f));
            Assert.That(v.BodyTurnSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(warnings.Count, Is.EqualTo(3));
        }

        [Test]
        public void Validated_HysteresisAboveHalfAngle_IsCappedWithWarning()
        {
            var bad = LookAtSettings.Defaults;
            bad.FrontHalfAngleDegrees = 20f;
            bad.FrontHysteresisDegrees = 30f;
            var warnings = new List<string>();
            var v = bad.Validated(warnings);
            Assert.That(v.FrontHysteresisDegrees, Is.EqualTo(20f));
            Assert.That(warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExcessYaw_InsideLimit_IsZero_PastItIsTheSignedRemainder()
        {
            Assert.That(LookAt.ExcessYaw(30f, s_defaults), Is.EqualTo(0f));
            Assert.That(LookAt.ExcessYaw(100f, s_defaults), Is.EqualTo(55f));
            Assert.That(LookAt.ExcessYaw(-100f, s_defaults), Is.EqualTo(-55f));
            Assert.That(LookAt.ExcessYaw(180f, s_defaults), Is.EqualTo(135f));
            Assert.That(LookAt.ExcessYaw(-180f, s_defaults), Is.EqualTo(135f));
        }

        [Test]
        public void BodyStep_EasesTowardTheExcess_NeverPastIt()
        {
            var step = LookAt.BodyStep(60f, 0.05f, s_defaults);
            Assert.That(step, Is.GreaterThan(0f).And.LessThan(60f));
            Assert.That(LookAt.BodyStep(-60f, 0.05f, s_defaults), Is.EqualTo(-step));
            Assert.That(LookAt.BodyStep(0f, 0.05f, s_defaults), Is.EqualTo(0f));
            var snap = s_defaults;
            snap.BodyTurnSeconds = 0f;
            Assert.That(LookAt.BodyStep(60f, 0.016f, snap), Is.EqualTo(60f));
        }

        [Test]
        public void Gesture_NoBodyTurnWhileItPlays_TurnStartsAtTheEnd_WithoutAJump()
        {
            var g = new GestureLook();
            Assert.That(g.Tick(true, true), Is.False, "nothing to latch at the start");
            Assert.That(g.Tick(true, true), Is.False);
            Assert.That(g.Turning, Is.False);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.EqualTo(0f), "no turn mid-gesture");
            Assert.That(g.Tick(false, true), Is.True, "latch on the end frame");
            Assert.That(g.Tick(false, true), Is.False, "no second latch");
            Assert.That(g.Turning, Is.True);
            var step = g.Step(90f, 0f, 0.016f, s_defaults);
            Assert.That(step, Is.GreaterThan(0f).And.LessThan(90f));
            var yaw = 90f - step;
            for (var i = 0; i < 60 && g.Turning; i++) yaw -= g.Step(yaw, 0f, 0.016f, s_defaults);
            Assert.That(g.Turning, Is.False, "done after the turn time");
            Assert.That(yaw, Is.EqualTo(0f).Within(0.2f));
            Assert.That(g.Step(yaw, 0f, 0.016f, s_defaults), Is.EqualTo(0f), "stays put");
        }

        [Test]
        public void Gesture_TurnTakesTheShortestWayRound()
        {
            var g = new GestureLook();
            g.Tick(true, true);
            g.Tick(false, true);
            Assert.That(g.Step(-100f, 0f, 0.016f, s_defaults), Is.LessThan(0f));
        }

        [Test]
        public void Gesture_BodyTurnIgnoresTargetHeight()
        {
            var above = LookAt.RawAngles(1f, 5f, 1f).Yaw;
            var below = LookAt.RawAngles(1f, -5f, 1f).Yaw;
            Assert.That(above, Is.EqualTo(below));
        }

        [Test]
        public void Gesture_LookAtOff_NoPostGestureTurn_NoLatch()
        {
            var g = new GestureLook();
            g.Tick(true, false);
            Assert.That(g.Tick(false, false), Is.False);
            Assert.That(g.Turning, Is.False);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.EqualTo(0f));
        }

        [Test]
        public void Gesture_LookAtOffMidGesture_NoTurnAfter_HeadStaysAtZero()
        {
            var g = new GestureLook();
            var head = new LookAtHead();
            for (var i = 0; i < 100; i++) head.Tick(0.016f, true, 0f, 0f, s_defaults);
            g.Tick(true, true);
            g.Tick(true, false);
            LookAtPose pose = default;
            for (var i = 0; i < 100; i++) pose = head.Tick(0.016f, false, 0f, 0f, s_defaults, true);
            Assert.That(pose.Weight, Is.EqualTo(0f));
            Assert.That(g.Tick(false, false), Is.False);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.EqualTo(0f));
        }

        [Test]
        public void Gesture_LookAtOffDuringThePostTurn_StopsItWhereItIs()
        {
            var g = new GestureLook();
            g.Tick(true, true);
            g.Tick(false, true);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.GreaterThan(0f));
            g.Tick(false, false);
            Assert.That(g.Turning, Is.False);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.EqualTo(0f));
            g.Tick(false, true);
            Assert.That(g.Turning, Is.False, "turning Look-At back on does not resume it");
        }

        [Test]
        public void Gesture_NewGestureDuringThePostTurn_CancelsIt()
        {
            var g = new GestureLook();
            g.Tick(true, true);
            g.Tick(false, true);
            Assert.That(g.Turning, Is.True);
            g.Tick(true, true);
            Assert.That(g.Turning, Is.False);
            Assert.That(g.Step(90f, 0f, 0.016f, s_defaults), Is.EqualTo(0f));
        }

        [Test]
        public void Head_Gesture_WeightEasesToZeroThenBackToOne()
        {
            var head = new LookAtHead();
            var pose = default(LookAtPose);
            for (var i = 0; i < 100; i++) pose = head.Tick(0.016f, true, 20f, 0f, s_defaults);
            var yawBefore = pose.Yaw;
            pose = head.Tick(0.016f, true, 40f, 0f, s_defaults, true);
            Assert.That(pose.Weight, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(pose.Yaw, Is.EqualTo(yawBefore), "angles hold during the gesture");
            for (var i = 0; i < 100; i++) pose = head.Tick(0.016f, true, 40f, 0f, s_defaults, true);
            Assert.That(pose.Weight, Is.EqualTo(0f).Within(0.01f));
            pose = head.Tick(0.016f, true, 40f, 0f, s_defaults);
            Assert.That(pose.Weight, Is.GreaterThan(0f).And.LessThan(1f));
            for (var i = 0; i < 100; i++) pose = head.Tick(0.016f, true, 40f, 0f, s_defaults);
            Assert.That(pose.Weight, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void Head_GestureStartsBeforeWeightIsBack_EasesFromWhereItIs()
        {
            var head = new LookAtHead();
            for (var i = 0; i < 100; i++) head.Tick(0.016f, true, 0f, 0f, s_defaults, true);
            var half = 0f;
            for (var i = 0; i < 4; i++) half = head.Tick(0.016f, true, 0f, 0f, s_defaults).Weight;
            Assert.That(half, Is.GreaterThan(0.1f).And.LessThan(0.9f));
            var next = head.Tick(0.016f, true, 0f, 0f, s_defaults, true).Weight;
            Assert.That(next, Is.LessThan(half).And.GreaterThan(0f), "no jump to 0 or 1");
        }

        [Test]
        public void ResetLook_TurnsToTheCameraDirection_WithLookAtOnOrOff()
        {
            var g = new GestureLook();
            g.Tick(false, false); // Look-At OFF
            g.RequestReset();
            Assert.That(g.Resetting, Is.True);
            var yaw = 60f;
            var first = g.Step(0f, yaw, 0.016f, s_defaults);
            Assert.That(first, Is.GreaterThan(0f).And.LessThan(60f));
            yaw -= first;
            for (var i = 0; i < 200 && g.Resetting; i++) yaw -= g.Step(0f, yaw, 0.016f, s_defaults);
            Assert.That(g.Resetting, Is.False);
            Assert.That(yaw, Is.EqualTo(0f).Within(0.2f));
            Assert.That(g.Step(0f, yaw, 0.016f, s_defaults), Is.EqualTo(0f), "one-shot: done");
        }

        [Test]
        public void ResetLook_DuringAGesture_DoesNothing_AndAGestureStartCancelsIt()
        {
            var g = new GestureLook();
            g.Tick(true, true);
            g.RequestReset();
            Assert.That(g.Resetting, Is.False);
            g.Tick(false, true);
            g.RequestReset();
            Assert.That(g.Resetting, Is.True);
            g.Tick(true, true);
            Assert.That(g.Resetting, Is.False);
            g.RequestReset();
            g.Tick(false, true);
            g.RequestReset();
            g.CancelReset();
            Assert.That(g.Step(0f, 60f, 0.016f, s_defaults), Is.EqualTo(0f));
        }

        [Test]
        public void CameraAim_InFront_LooksAtThePosition()
        {
            var aim = new CameraAim();
            // Camera 2 m ahead and 2 m right, facing away (+z): the position wins, so yaw is 45.
            var (yaw, _) = aim.Aim(2f, 0f, 2f, 0f, 0f, 1f, s_defaults);
            Assert.That(aim.InFront, Is.True);
            Assert.That(yaw, Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void CameraAim_Behind_TakesTheFacingDirection()
        {
            var aim = new CameraAim();
            // Camera behind-right, facing 30 degrees right.
            var (yaw, _) = aim.Aim(1f, 0f, -3f, 0.5f, 0f, 0.8660254f, s_defaults);
            Assert.That(aim.InFront, Is.False);
            Assert.That(yaw, Is.EqualTo(30f).Within(0.01f));
        }

        [Test]
        public void CameraAim_NearTheBoundary_DoesNotFlick()
        {
            var aim = new CameraAim();
            AimAt(aim, 60f);
            Assert.That(aim.InFront, Is.True);
            AimAt(aim, 95f); // past 90 but inside the 10 degree band: stays in front
            Assert.That(aim.InFront, Is.True);
            AimAt(aim, 105f); // past 100: switches to behind
            Assert.That(aim.InFront, Is.False);
            AimAt(aim, 85f); // below 90 but above 80: stays behind
            Assert.That(aim.InFront, Is.False);
            AimAt(aim, 75f); // below 80: back in front
            Assert.That(aim.InFront, Is.True);
        }

        [Test]
        public void EyeAngles_EyesCoverWhatTheHeadLeavesOver()
        {
            // Target 60 right; the head sits at its 45 limit, so the eyes take 15.
            var head = new LookAtPose { Yaw = 45f, Pitch = 0f, Weight = 1f };
            var (yaw, pitch) = LookAt.EyeAngles(60f, 10f, head);
            Assert.That(yaw, Is.EqualTo(15f).Within(0.001f));
            Assert.That(pitch, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void EyeAngles_ZeroWeight_EyesAtRest()
        {
            var head = new LookAtPose { Yaw = 0f, Pitch = 0f, Weight = 0f };
            Assert.That(LookAt.EyeAngles(60f, 20f, head), Is.EqualTo((0f, 0f)));
        }

        [Test]
        public void EyeAngles_PartWeight_ScalesTheResidual()
        {
            var head = new LookAtPose { Yaw = 20f, Pitch = 0f, Weight = 0.5f };
            Assert.That(LookAt.EyeAngles(40f, 0f, head).Yaw, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void EyeAngles_RawYawPastHalfTurn_TakesTheShortestWay()
        {
            // 190 is -170; the head is at its -45 limit, so the eyes take -125.
            var head = new LookAtPose { Yaw = -45f, Pitch = 0f, Weight = 1f };
            Assert.That(LookAt.EyeAngles(190f, 0f, head).Yaw, Is.EqualTo(-125f).Within(0.001f));
        }

        private static void AimAt(CameraAim aim, float yawDegrees)
        {
            var r = yawDegrees * Math.PI / 180;
            aim.Aim((float)Math.Sin(r), 0f, (float)Math.Cos(r), 0f, 0f, 1f, s_defaults);
        }
    }
}
