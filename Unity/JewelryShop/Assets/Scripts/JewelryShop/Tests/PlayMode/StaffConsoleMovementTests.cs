using System.Collections;
using System.Reflection;
using JewelryShop.Core;
using JewelryShop.StaffConsole;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace JewelryShop.Tests
{
    // Cheryll walks on throwaway colliders built in memory; keys are injected into a fake keyboard.
    // The config is left unassigned, so every test runs on MovementSettings.Defaults.
    [TestFixture]
    public class StaffConsoleMovementTests
    {
        private const string ControllerPath =
            "Assets/Avatar/Animations/Cheryll/CheryllPreview.controller";

        private GameObject _root;
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _originalBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _originalEditorInputBehavior;
        private CharacterController _controller;
        private Animator _animator;
        private StaffConsoleMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MovementTestRoot");
            // The test runner may be unfocused while it feeds the simulated keyboard.
            var settings = InputSystem.settings;
            _originalBackgroundBehavior = settings.backgroundBehavior;
            _originalEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            // Host keyboard input may arrive while the Editor runs the tests.
            InputSystem.onAfterUpdate += _keyboard.MakeCurrent;
        }

        [TearDown]
        public void TearDown()
        {
            Hold(); // release held keys before the device goes
            InputSystem.onAfterUpdate -= _keyboard.MakeCurrent;
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _originalBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _originalEditorInputBehavior;
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator Cheryll_AboveFloor_SettlesAndStaysGrounded()
        {
            AddFloor();
            var cheryll = AddCheryll(new Vector3(0f, 2f, 0f));

            yield return Wait(1.5f);

            var settledY = cheryll.position.y;
            Assert.That(settledY, Is.EqualTo(0f).Within(0.1f));
            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
                Assert.That(_controller.isGrounded, Is.True, $"frame {frame}");
                Assert.That(cheryll.position.y, Is.EqualTo(settledY).Within(0.01f),
                    $"frame {frame}");
            }
        }

        [UnityTest]
        public IEnumerator Cheryll_WalkingIntoWall_DoesNotPassThrough()
        {
            AddFloor();
            AddBox(new Vector3(0f, 1.5f, 2f), new Vector3(10f, 3f, 0.5f)); // face at z = 1.75
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.W);
            yield return Wait(3f);

            Assert.That(cheryll.position.z, Is.LessThan(1.75f));
            Assert.That(cheryll.position.z, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator Cheryll_WalkingAtAngleIntoWall_SlidesAlongIt()
        {
            AddFloor();
            AddBox(new Vector3(0f, 1.5f, 2f), new Vector3(20f, 3f, 0.5f));
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.W, Key.D);
            yield return Wait(3f);

            Assert.That(cheryll.position.z, Is.LessThan(1.75f));
            Assert.That(cheryll.position.x, Is.GreaterThan(1.5f));
        }

        // Geometry: a wide, deep slab whose front face is at z = 1 and whose top is a step height
        // above the floor. Cheryll rests one skinWidth above any surface, and the controller
        // measures a step from there, so the reachable height is StepHeightMeters + skinWidth.
        // The two cases differ only by 0.05 m either side of that, so the block is the step
        // limit and not a different obstacle.
        [UnityTest]
        public IEnumerator Cheryll_StepJustUnderLimit_IsClimbed()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);
            var stepMeters = MovementSettings.Defaults.StepHeightMeters + _controller.skinWidth -
                0.05f;
            AddStep(stepMeters);

            Hold(Key.W);
            yield return Wait(3f);

            Assert.That(cheryll.position.y,
                Is.EqualTo(stepMeters + _controller.skinWidth).Within(0.03f));
            Assert.That(cheryll.position.z, Is.GreaterThan(1.5f));
        }

        [UnityTest]
        public IEnumerator Cheryll_StepJustOverLimit_IsNotClimbed()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);
            AddStep(MovementSettings.Defaults.StepHeightMeters + _controller.skinWidth + 0.05f);

            Hold(Key.W);
            yield return Wait(3f);

            Assert.That(cheryll.position.y, Is.EqualTo(_controller.skinWidth).Within(0.03f));
            Assert.That(cheryll.position.z, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator Cheryll_WalkingOffEdge_FallsThenRespawnsAtStart()
        {
            AddBox(new Vector3(0f, -0.5f, 0.25f), new Vector3(4f, 1f, 3.5f)); // far edge z = 2
            var start = new Vector3(0f, 0.05f, 0f);
            var cheryll = AddCheryll(start);

            Hold(Key.W);
            var fell = false;
            var respawned = false;
            for (var elapsed = 0f; elapsed < 8f && !respawned; elapsed += Time.deltaTime)
            {
                yield return null;
                fell |= cheryll.position.y < -1f;
                respawned = fell && Vector3.Distance(cheryll.position, start) < 0.5f;
            }

            Assert.That(fell, Is.True);
            Assert.That(respawned, Is.True);
        }

        [UnityTest]
        public IEnumerator Cheryll_RespawnWithKeyHeld_StateIsClean()
        {
            AddBox(new Vector3(0f, -0.5f, 0.25f), new Vector3(4f, 1f, 3.5f));
            var start = new Vector3(0f, 0.05f, 0f);
            var cheryll = AddCheryll(start);

            Hold(Key.W); // stays held through the respawn
            var fell = false;
            var respawned = false;
            for (var elapsed = 0f; elapsed < 8f && !respawned; elapsed += Time.deltaTime)
            {
                yield return null;
                if (!fell && cheryll.position.y < -1f)
                {
                    fell = true;
                    SetField(_movement, "_yawDegrees", 90f); // as if the camera had been orbited
                    SetField(_movement, "_pitchDegrees", 10f);
                }

                respawned = fell && Vector3.Distance(cheryll.position, start) < 0.5f;
            }

            Assert.That(respawned, Is.True);
            Assert.That(GetField<float>(_movement, "_yawDegrees"), Is.EqualTo(0f));
            Assert.That(GetField<float>(_movement, "_pitchDegrees"), Is.EqualTo(0f));
            Assert.That(GetField<Vector3>(_movement, "_walkVelocityMetersPerSecond"),
                Is.EqualTo(Vector3.zero));
            Assert.That(_animator.GetBool("IsMoving"), Is.False);
            Assert.That(_animator.GetFloat("WalkForward"), Is.EqualTo(0f));
            Assert.That(_animator.GetFloat("WalkHorizontal"), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Cheryll_WalkingForwardOnFlatGround_BlendsForwardWalk()
        {
            AddFloor();
            AddCheryll(Vector3.up * 0.05f);

            Hold(Key.W);
            yield return Wait(1f);

            Assert.That(_animator.GetBool("IsMoving"), Is.True);
            Assert.That(_animator.GetFloat("WalkForward"), Is.GreaterThan(0.9f));
            Assert.That(_animator.GetFloat("WalkHorizontal"), Is.EqualTo(0f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingS_TurnsToFaceCameraAndWalksTowardIt()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.S);
            yield return Wait(1f);

            Assert.That(cheryll.position.z, Is.LessThan(-0.5f)); // the camera is toward -z
            Assert.That(Vector3.Dot(cheryll.forward, Vector3.back), Is.GreaterThan(0.99f));
            Assert.That(_animator.GetFloat("WalkForward"), Is.GreaterThan(0.9f));
            Assert.That(_animator.GetFloat("WalkHorizontal"), Is.EqualTo(0f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingSAndD_WalksDiagonallyTowardCameraFacingThatWay()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.S, Key.D);
            yield return Wait(1f);

            var heading = new Vector3(1f, 0f, -1f).normalized;
            Assert.That(cheryll.position.x, Is.GreaterThan(0.3f));
            Assert.That(cheryll.position.z, Is.LessThan(-0.3f));
            Assert.That(Vector3.Dot(cheryll.forward, heading), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingD_TurnsToFaceRight()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.D);
            yield return Wait(1f);

            Assert.That(Vector3.Dot(cheryll.forward, Vector3.right), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator Cheryll_WalkingIntoDynamicCube_DoesNotPushIt()
        {
            AddFloor();
            var cube = AddBox(new Vector3(0f, 0.25f, 1.5f), Vector3.one * 0.5f);
            cube.AddComponent<Rigidbody>();
            var cheryll = AddCheryll(Vector3.up * 0.05f);

            Hold(Key.W);
            yield return Wait(3f);

            Assert.That(cube.transform.position.z, Is.EqualTo(1.5f).Within(0.02f));
            Assert.That(cheryll.position.z, Is.LessThan(1.5f)); // she was stopped by it
        }

        [UnityTest]
        public IEnumerator Cheryll_Space_LiftsToJumpHeightAndLandsGrounded()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);
            yield return Wait(0.5f);
            var floorY = cheryll.position.y;

            yield return Tap(Key.Space);
            var peakY = floorY;
            for (var elapsed = 0f; elapsed < 2f; elapsed += Time.deltaTime)
            {
                yield return null;
                peakY = Mathf.Max(peakY, cheryll.position.y);
            }

            Assert.That(peakY - floorY,
                Is.EqualTo(MovementSettings.Defaults.JumpHeightMeters).Within(0.1f));
            Assert.That(_controller.isGrounded, Is.True);
            // She may settle up to skinWidth off the start height.
            Assert.That(cheryll.position.y, Is.EqualTo(floorY).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Cheryll_JumpOntoLedge_LeavesJumpByGroundedNotClipEnd()
        {
            AddFloor();
            // Ledge face at z = 1.3, top at 0.4.
            AddBox(new Vector3(0f, 0.2f, 3.3f), new Vector3(10f, 0.4f, 4f));
            var cheryll = AddCheryll(Vector3.up * 0.05f, GroundedController());
            Hold(Key.W);
            yield return Wait(0.4f);

            yield return Tap(Key.Space, Key.W);
            Hold(Key.W);
            // The Animator leaves Jump when the controller lands on the ledge.
            yield return Wait(1.5f);

            Assert.That(_controller.isGrounded, Is.True);
            Assert.That(_animator.GetBool("IsGrounded"), Is.True);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsTag("Jump"), Is.False);
        }

        private const float MostlyFacingSideDot = 0.9f;
        private const float OldAirControl = 0.3f; // the former default; must be clearly beaten
        private const float SteeringGainOverOld = 1.3f;
        private const float PoseUnchangedDot = 0.99f;

        private float _airHoldSeconds;

        // Jumps, waits until she has left the ground, then holds D until about the apex.
        // The hold length comes from the defaults, so it stays inside the arc.
        private IEnumerator JumpAndHoldSideways()
        {
            AddFloor();
            AddCheryll(Vector3.up * 0.05f);
            yield return Wait(0.5f);
            yield return Tap(Key.Space);
            for (var frame = 0; frame < 120 && _controller.isGrounded; frame++)
                yield return null;
            Assert.That(_controller.isGrounded, Is.False, "precondition: airborne");
            var apexSeconds = Mathf.Sqrt(2f * MovementSettings.Defaults.JumpHeightMeters /
                MovementSettings.Defaults.GravityMetersPerSecondSquared);
            Hold(Key.D);
            for (_airHoldSeconds = 0f; _airHoldSeconds < apexSeconds;
                _airHoldSeconds += Time.deltaTime)
                yield return null;
            Assert.That(_controller.isGrounded, Is.False, "still airborne at the end of the hold");
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingSideKeyInAir_TurnsToFaceIt()
        {
            yield return JumpAndHoldSideways();

            Assert.That(_movement.transform.forward.x, Is.GreaterThan(MostlyFacingSideDot));
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingSideKeyInAir_SteersFartherThanOldAirControl()
        {
            yield return JumpAndHoldSideways();

            // Distance the old Air Control could cover from rest in the same time (it never
            // reaches walk speed in an apex-long hold), with a margin for the new default.
            var oldAcceleration = MovementSettings.Defaults.AccelerationMetersPerSecondSquared *
                OldAirControl;
            var oldMeters = 0.5f * oldAcceleration * _airHoldSeconds * _airHoldSeconds;
            Assert.That(_movement.transform.position.x,
                Is.GreaterThan(SteeringGainOverOld * oldMeters));
        }

        [UnityTest]
        public IEnumerator Cheryll_HoldingSideKeyDuringTakeoff_KeepsPoseUntilLiftOff()
        {
            AddFloor();
            var cheryll = AddCheryll(Vector3.up * 0.05f);
            yield return Wait(0.5f);
            var facing = cheryll.forward;

            yield return Tap(Key.Space);
            Hold(Key.D);
            // Well inside the take-off delay, by time rather than frame count.
            var takeoffDelay = MovementSettings.Defaults.JumpTakeoffDelaySeconds;
            for (var elapsed = 0f; elapsed < takeoffDelay / 4f; elapsed += Time.deltaTime)
                yield return null;

            Assert.That(_controller.isGrounded, Is.True, "precondition: still taking off");
            Assert.That(Vector3.Dot(cheryll.forward, facing), Is.GreaterThan(PoseUnchangedDot));
        }

        [UnityTest]
        public IEnumerator Cheryll_GestureAndIdleLockKeys_DoNothingWhileAirborne()
        {
            AddFloor();
            AddCheryll(Vector3.up * 0.05f);
            yield return Wait(0.5f);
            yield return Tap(Key.Space);
            yield return Wait(0.45f); // past take-off, near the top of the arc
            Assert.That(_controller.isGrounded, Is.False, "precondition: airborne");

            yield return Tap(Key.Digit1);
            yield return Tap(Key.Digit7);

            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsTag("Gesture"), Is.False);
            Assert.That(_animator.GetNextAnimatorStateInfo(0).IsTag("Gesture"), Is.False);
            Assert.That(_animator.GetBool("IdleLock"), Is.False);
        }

        [UnityTest]
        public IEnumerator Cheryll_IdleLockKey_OnFirstFrameWhileStillFalling_IsNotBlocked()
        {
            AddFloor();
            AddCheryll(new Vector3(0f, 2f, 0f)); // not grounded yet, but only just
            Assert.That(_animator.GetBool("IdleLock"), Is.False);

            yield return Tap(Key.Digit7);

            Assert.That(_animator.GetBool("IdleLock"), Is.True);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Cheryll_Jump_LimitsBustSpringSwing()
        {
            AddFloor();
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Avatar/Cheryll_3.vrm");
            Assert.That(avatar, Is.Not.Null);
            var cheryll = AddCheryll(Vector3.up * 0.05f, avatar: avatar,
                hasJumpSpringLimits: true);
            var joints = System.Array.FindAll(cheryll.GetComponentsInChildren<Transform>(),
                bone => bone.name.EndsWith("_Bust1", System.StringComparison.Ordinal) ||
                    bone.name.EndsWith("_Bust2", System.StringComparison.Ordinal));
            Assert.That(joints, Has.Length.EqualTo(4));
            var rest = System.Array.ConvertAll(joints, joint => joint.localRotation);

            yield return Wait(0.5f);
            var standingHeightMeters = cheryll.position.y;
            yield return Tap(Key.Space);
            var hasAirborneSample = false;
            var maxSwingDegrees = 0f;
            for (var elapsedSeconds = 0f; elapsedSeconds < 1.2f;
                elapsedSeconds += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                if (!_movement.IsJumping || cheryll.position.y - standingHeightMeters < 0.15f)
                    continue;
                hasAirborneSample = true;
                for (var i = 0; i < joints.Length; i++)
                    maxSwingDegrees = Mathf.Max(maxSwingDegrees,
                        Quaternion.Angle(rest[i], joints[i].localRotation));
            }

            Assert.That(hasAirborneSample, Is.True);
            Assert.That(maxSwingDegrees, Is.LessThan(2.2f));
        }

        [UnityTest]
        public IEnumerator Cheryll_ReturningToIdle_BlendsIntoTheAuthoredArmPose()
        {
            AddFloor();
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Avatar/Cheryll_3.vrm");
            Assert.That(avatar, Is.Not.Null);
            AddCheryll(Vector3.up * 0.05f, avatar: avatar);
            yield return Wait(0.5f);
            _animator.GetComponent<StaffConsoleIdleBreathing>().enabled = false;
            _animator.Play("Base Layer.Idle", 0, 0f);
            yield return null;
            AssertUpperArmsAlongsideBody();

            var knee = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var foot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var kneeAtRest = knee.position;
            var plantedFoot = foot.position;
            yield return Wait(1.2f);
            Assert.That(Vector3.Distance(kneeAtRest, knee.position), Is.GreaterThan(0.0003f));
            Assert.That(Vector3.Distance(plantedFoot, foot.position), Is.LessThan(0.003f));

            var arm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var elbow = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            float ArmDepthDegrees()
            {
                var direction = elbow.position - arm.position;
                return Vector3.Angle(direction,
                    Vector3.ProjectOnPlane(direction, _animator.transform.forward));
            }

            _animator.Play("Base Layer.Standby01", 0, 0f);
            yield return Wait(0.45f);
            var standbyDepthDegrees = ArmDepthDegrees();
            Assert.That(standbyDepthDegrees, Is.GreaterThan(1f));

            _animator.CrossFadeInFixedTime("Base Layer.Idle", 0.8f, 0);
            yield return Wait(0.35f);
            Assert.That(_animator.IsInTransition(0), Is.True);
            Assert.That(_animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
            var blendedDepthDegrees = ArmDepthDegrees();
            Assert.That(blendedDepthDegrees, Is.InRange(0.2f, standbyDepthDegrees - 0.2f));

            yield return Wait(0.6f);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"),
                Is.True);
            AssertUpperArmsAlongsideBody();
        }

        [UnityTest]
        public IEnumerator Cheryll_IdleLock_StopsStandbyAndKeepsBreathing()
        {
            AddFloor();
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Avatar/Cheryll_3.vrm");
            Assert.That(avatar, Is.Not.Null);
            AddCheryll(Vector3.up * 0.05f, avatar: avatar);
            yield return Wait(0.5f);
            Assert.That(_animator.GetComponent<StaffConsoleIdleBreathing>(), Is.Not.Null);
            var chest = _animator.GetBoneTransform(HumanBodyBones.Chest);

            _animator.Play("Base Layer.Standby01", 0, 0f);
            yield return null;
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Standby01"),
                Is.True);
            yield return Tap(Key.Digit7);
            yield return Wait(0.9f);
            Assert.That(_animator.GetBool("IdleLock"), Is.True);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"),
                Is.True);
            // Sample the last LateUpdate before the next regular Update clears its offset.
            yield return new WaitForFixedUpdate();
            var chestRotation = chest.localRotation;
            var arm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var elbow = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            var armRotation = arm.localRotation;
            var elbowRotation = elbow.localRotation;
            var handRotation = hand.localRotation;
            var thigh = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var knee = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var foot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var thighRotation = thigh.localRotation;
            var kneePosition = knee.position;
            var footPosition = foot.position;
            AssertUpperArmsAlongsideBody();

            yield return Wait(2f);
            yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(chestRotation, chest.localRotation), Is.GreaterThan(0.3f));

            yield return Wait(1f);
            yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(armRotation, arm.localRotation), Is.GreaterThan(0.5f));
            Assert.That(Quaternion.Angle(elbowRotation, elbow.localRotation),
                Is.InRange(0.4f, 2.1f));
            Assert.That(Quaternion.Angle(handRotation, hand.localRotation), Is.InRange(0.4f, 5.1f));
            Assert.That(Quaternion.Angle(thighRotation, thigh.localRotation),
                Is.InRange(0.5f, 10.1f), "subtle knee sway");
            Assert.That(Vector3.Distance(kneePosition, knee.position),
                Is.GreaterThan(0.0003f), "the knee moves during idle");
            Assert.That(Vector3.Distance(footPosition, foot.position),
                Is.LessThan(0.003f), "the foot stays planted");
            AssertUpperArmsAlongsideBody();

            for (var elapsed = 0f; elapsed < 9f; elapsed += Time.deltaTime)
            {
                Assert.That(_animator.IsInTransition(0), Is.False, "while Idle Lock is on");
                yield return null;
            }
            yield return Tap(Key.Digit7);
            Assert.That(_animator.GetBool("IdleLock"), Is.False);
            for (var elapsed = 0f; elapsed < 4.8f; elapsed += Time.deltaTime)
            {
                Assert.That(_animator.IsInTransition(0), Is.False, "before the new interval");
                yield return null;
            }
            for (var elapsed = 0f; elapsed < 4.4f && !_animator.IsInTransition(0);
                elapsed += Time.deltaTime)
                yield return null;
            Assert.That(_animator.IsInTransition(0), Is.True, "after the new interval");
        }

        private void AssertUpperArmsAlongsideBody()
        {
            var forward = _animator.transform.forward;
            var leftDirection = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position -
                _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            var rightDirection = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position -
                _animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            Assert.That(Vector3.Dot(leftDirection.normalized, forward),
                Is.EqualTo(0f).Within(0.02f));
            Assert.That(Vector3.Dot(rightDirection.normalized, forward),
                Is.EqualTo(0f).Within(0.02f));
            Assert.That(Vector3.Dot(leftDirection.normalized, -_animator.transform.up),
                Is.GreaterThan(0.8f));
            Assert.That(Vector3.Dot(rightDirection.normalized, -_animator.transform.up),
                Is.GreaterThan(0.8f));
        }
#endif

        [UnityTest]
        public IEnumerator Cheryll_KeysRightAfterLeavingEdge_DoNothing()
        {
            AddBox(new Vector3(0f, -0.5f, 0.25f), new Vector3(4f, 1f, 3.5f)); // far edge z = 2
            AddCheryll(new Vector3(0f, 0.05f, 1.9f));
            yield return null;
            yield return null; // past the first Move, where isGrounded is not yet meaningful
            Assert.That(_controller.isGrounded, Is.True, "precondition: on the platform");
            Hold(Key.W);
            for (var frame = 0; frame < 300 && _controller.isGrounded; frame++)
                yield return null;
            Assert.That(_controller.isGrounded, Is.False, "precondition: just left the edge");

            yield return Tap(Key.Space);
            Assert.That(JumpPlaying(), Is.False, "Space");
            yield return Tap(Key.Digit1);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsTag("Gesture"), Is.False);
            Assert.That(_animator.GetNextAnimatorStateInfo(0).IsTag("Gesture"), Is.False);
            yield return Tap(Key.Digit7);
            Assert.That(_animator.GetBool("IdleLock"), Is.False);
        }

        private bool JumpPlaying() =>
            _animator.GetCurrentAnimatorStateInfo(0).IsTag("Jump") ||
            _animator.GetNextAnimatorStateInfo(0).IsTag("Jump");

        [UnityTest]
        public IEnumerator Cheryll_Space_WhileAirborne_DoesNotJumpAgain()
        {
            AddBox(new Vector3(0f, -0.5f, 0.25f), new Vector3(4f, 1f, 3.5f)); // far edge z = 2
            AddCheryll(new Vector3(0f, 0.05f, 1.9f));
            Hold(Key.W);
            yield return Wait(0.8f); // walked off the edge and is falling
            Assert.That(_controller.isGrounded, Is.False, "precondition: airborne");
            Hold();

            yield return Tap(Key.Space);

            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsTag("Jump"), Is.False);
            Assert.That(_animator.GetNextAnimatorStateInfo(0).IsTag("Jump"), Is.False);
        }

        // The camera starts at CameraStart, behind Cheryll toward -z. Wall faces: z = -2 and -2.5.
        private static readonly Vector3 CameraStart = new Vector3(0f, 2f, -3f);
        private const float CameraSpawnHeightMeters = 0.05f;
        private const float WallNearFaceZMeters = -2f;

        private Transform AddCameraWall() =>
            AddBlockingBox(new Vector3(0f, 1.5f, -2.25f), new Vector3(10f, 3f, 0.5f)).transform;

        // Walls and ceilings sit on the camera-blocking layer; ordinary objects do not.
        private GameObject AddBlockingBox(Vector3 center, Vector3 size)
        {
            var box = AddBox(center, size);
            box.layer = CameraSettings.CameraBlockingLayer;
            return box;
        }

        private static Vector3 StartPivot =>
            Vector3.up * (CameraSpawnHeightMeters + CameraSettings.Defaults.EyeHeightMeters);

        private Vector3 CurrentPivot => _movement.transform.position +
            Vector3.up * CameraSettings.Defaults.EyeHeightMeters;

        private Transform CameraOf() => GetField<Transform>(_movement, "_staffCamera");

        private static StaffCameraConfig CameraConfig(float radiusMeters, int layerMask)
        {
            var settings = CameraSettings.Defaults;
            settings.CollisionRadiusMeters = radiusMeters;
            settings.CollisionLayerMask = layerMask;
            var config = ScriptableObject.CreateInstance<StaffCameraConfig>();
            SetField(config, "_settings", settings);
            return config;
        }

        [UnityTest]
        public IEnumerator Camera_WallBehindCheryll_StopsOneRadiusInFrontOfWall()
        {
            AddFloor();
            AddCameraWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);

            yield return Wait(0.3f);

            var radiusMeters = CameraSettings.Defaults.CollisionRadiusMeters;
            Assert.That(CameraOf().position.z,
                Is.EqualTo(WallNearFaceZMeters + radiusMeters).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Camera_WallRemoved_RelaxesBackToNormalDistance()
        {
            AddFloor();
            var wall = AddCameraWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            var camera = CameraOf();
            yield return Wait(0.3f);
            Assert.That(camera.position.z, Is.GreaterThan(WallNearFaceZMeters),
                "precondition: pulled in");

            Object.Destroy(wall.gameObject);
            yield return null; // Destroy takes effect at the end of the frame
            yield return Wait(2f);

            var restMeters = Vector3.Distance(CameraStart, StartPivot);
            var pivot = CurrentPivot;
            Assert.That(Vector3.Distance(camera.position, pivot),
                Is.EqualTo(restMeters).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Camera_OrbitedInOpenSpace_SitsAtFullOrbitOffset()
        {
            AddFloor();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return null;
            SetField(_movement, "_yawDegrees", 40f);
            SetField(_movement, "_pitchDegrees", 10f);

            yield return null;

            var restOffset = CameraStart - StartPivot;
            var orbit = Quaternion.AngleAxis(40f, Vector3.up) *
                Quaternion.AngleAxis(10f, Vector3.right); // start camera faces +z, so right is +x
            var pivot = CurrentPivot;
            Assert.That(Vector3.Distance(CameraOf().position, pivot + orbit * restOffset),
                Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Camera_WallOnExcludedLayer_DoesNotPullCameraIn()
        {
            const int ignoredLayer = 31;
            AddFloor();
            AddCameraWall().gameObject.layer = ignoredLayer;
            AddCheryll(Vector3.up * CameraSpawnHeightMeters,
                cameraConfig: CameraConfig(0.2f, ~(1 << ignoredLayer)));

            yield return Wait(0.3f);

            Assert.That(CameraOf().position.z, Is.EqualTo(CameraStart.z).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Camera_LargerCollisionRadius_StopsFurtherFromWall()
        {
            const float radiusMeters = 0.5f; // under the pivot height, clear of the floor
            AddFloor();
            AddCameraWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters,
                cameraConfig: CameraConfig(radiusMeters, -1));

            yield return Wait(0.3f);

            Assert.That(CameraOf().position.z,
                Is.EqualTo(WallNearFaceZMeters + radiusMeters).Within(0.05f));
        }

        // Wall face at z = -0.5, so the camera ray is pulled inside the wall-lift threshold.
        private GameObject AddSqueezingWall() =>
            AddBlockingBox(new Vector3(0f, 1.5f, -0.75f), new Vector3(10f, 3f, 0.5f));

        private static Vector3 FirstPersonCameraPosition => Vector3.up *
            (CameraSpawnHeightMeters + CameraSettings.Defaults.EyeHeightMeters) +
            Vector3.forward * CameraSettings.Defaults.FirstPersonForwardMeters;

        private const float BlendWaitSeconds = 0.6f;

        [UnityTest]
        public IEnumerator Camera_NonBlockingObjectBehind_DoesNotMoveCamera()
        {
            AddFloor();
            AddBox(new Vector3(0f, 1.5f, -2.25f), new Vector3(10f, 3f, 0.5f)); // Default layer
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);

            yield return Wait(0.3f);

            Assert.That(CameraOf().position.z, Is.EqualTo(CameraStart.z).Within(0.01f));
            Assert.That(_movement.IsFirstPerson, Is.False);
        }

        [UnityTest]
        public IEnumerator Camera_Squeezed_LiftsAboveCheryllInThirdPerson()
        {
            AddFloor();
            // The wall is close enough that a fixed rear offset would block an upward lift.
            AddBlockingBox(new Vector3(0f, 1.5f, -0.48f), new Vector3(10f, 3f, 0.5f));
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);

            yield return Wait(BlendWaitSeconds);

            Assert.That(_movement.IsFirstPerson, Is.False);
            Assert.That(CameraOf().position.y, Is.GreaterThan(CurrentPivot.y + 0.5f));
            Assert.That(CameraOf().position.z, Is.GreaterThanOrEqualTo(-0.035f));
            Assert.That(Vector3.Dot(CameraOf().forward, Vector3.down), Is.GreaterThan(0.65f));
        }

        [UnityTest]
        public IEnumerator Camera_SteppingAwayFromWall_ReturnsToNormalOrbitHeight()
        {
            AddFloor();
            AddSqueezingWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return Wait(BlendWaitSeconds);
            Assert.That(CameraOf().position.y, Is.GreaterThan(CurrentPivot.y + 0.5f));

            _controller.Move(Vector3.forward * 3f);
            yield return Wait(BlendWaitSeconds);

            Assert.That(_movement.IsFirstPerson, Is.False);
            Assert.That(CameraOf().position.y - CurrentPivot.y,
                Is.EqualTo(CameraStart.y - StartPivot.y).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator Camera_VKey_CutsToFirstPersonWithTheHeadAndHairVisible()
        {
            AddFloor();
            var cheryll = AddVrmCheryll();
            yield return Wait(0.2f);
            var head = _animator.GetBoneTransform(HumanBodyBones.Head);
            var headScale = head.localScale;
            var face = cheryll.Find("Face").GetComponent<Renderer>();
            var hair = cheryll.Find("Hair").GetComponent<Renderer>();
            var faceMaterial = face.sharedMaterials[0];
            var hairMaterial = hair.sharedMaterials[0];
            var start = CameraOf().position;

            yield return Tap(Key.V);

            Assert.That(_movement.IsFirstPerson, Is.True);
            Assert.That(Vector3.Distance(CameraOf().position, FirstPersonCameraPosition),
                Is.LessThan(0.05f));
            Assert.That(head.localScale, Is.EqualTo(headScale));
            Assert.That(face.enabled && hair.enabled, Is.True);
            Assert.That(face.sharedMaterials[0], Is.SameAs(faceMaterial));
            Assert.That(hair.sharedMaterials[0], Is.SameAs(hairMaterial));

            yield return Tap(Key.V);

            Assert.That(_movement.IsFirstPerson, Is.False);
            Assert.That(Vector3.Distance(CameraOf().position, start), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator Camera_WallLift_StopsBelowTheCeiling()
        {
            AddFloor();
            AddSqueezingWall();
            const float ceilingBottomMeters = 2.45f;
            AddBlockingBox(new Vector3(0f, ceilingBottomMeters + 0.1f, 0f),
                new Vector3(10f, 0.2f, 10f));
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);

            yield return Wait(BlendWaitSeconds);

            Assert.That(_movement.IsFirstPerson, Is.False);
            Assert.That(CameraOf().position.y, Is.GreaterThan(CurrentPivot.y + 0.3f));
            Assert.That(CameraOf().position.y, Is.LessThanOrEqualTo(
                ceilingBottomMeters - CameraSettings.Defaults.CollisionRadiusMeters + 0.01f));
        }

        [UnityTest]
        public IEnumerator Camera_VKey_CanEnterAndLeaveFirstPersonBesideAWall()
        {
            AddFloor();
            AddSqueezingWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return Wait(BlendWaitSeconds);
            Assert.That(_movement.IsFirstPerson, Is.False);

            yield return Tap(Key.V);
            Assert.That(_movement.IsFirstPerson, Is.True);
            Assert.That(Vector3.Distance(CameraOf().position, FirstPersonCameraPosition),
                Is.LessThan(0.05f));

            yield return Tap(Key.V);
            Assert.That(_movement.IsFirstPerson, Is.False);
            Assert.That(CameraOf().position.y, Is.GreaterThan(CurrentPivot.y + 0.5f));
        }

        [UnityTest]
        public IEnumerator Camera_WallRemoved_LowersWithoutSnapping()
        {
            AddFloor();
            var wall = AddSqueezingWall();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return Wait(BlendWaitSeconds);
            var camera = CameraOf();
            var previous = camera.position;
            Assert.That(previous.y, Is.GreaterThan(CurrentPivot.y + 0.5f));
            Object.Destroy(wall);

            var worstStepMeters = 0f;
            for (var elapsed = 0f; elapsed < 1.5f; elapsed += Time.deltaTime)
            {
                yield return null;
                worstStepMeters = Mathf.Max(worstStepMeters,
                    Vector3.Distance(previous, camera.position));
                previous = camera.position;
            }

            Assert.That(worstStepMeters, Is.LessThan(0.5f), "largest one-frame jump, m");
            Assert.That(Vector3.Distance(camera.position, CameraStart), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator Camera_ManualFirstPerson_IsNotPushedByWalls()
        {
            AddFloor();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return Tap(Key.V);
            yield return Wait(BlendWaitSeconds);

            AddSqueezingWall();
            yield return Wait(BlendWaitSeconds);
            Assert.That(Vector3.Distance(CameraOf().position, FirstPersonCameraPosition),
                Is.LessThan(0.05f));

            Hold(Key.W);
            yield return Wait(1.5f);
            Hold();
            Assert.That(_movement.IsFirstPerson, Is.True, "still the viewer's choice");
        }

        [UnityTest]
        public IEnumerator Camera_FirstPerson_LimitsDownwardLook()
        {
            AddFloor();
            AddCheryll(Vector3.up * CameraSpawnHeightMeters);
            yield return Tap(Key.V);
            SetField(_movement, "_pitchDegrees", CameraSettings.Defaults.MaxPitchDegrees);

            yield return Wait(BlendWaitSeconds);

            Assert.That(CameraOf().eulerAngles.x,
                Is.EqualTo(CameraSettings.Defaults.FirstPersonMaxLookDownDegrees).Within(0.5f));
        }

        private Transform AddVrmCheryll()
        {
#if UNITY_EDITOR
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Avatar/Cheryll_3.vrm");
            Assert.That(avatar, Is.Not.Null);
            return AddCheryll(Vector3.up * CameraSpawnHeightMeters, avatar: avatar);
#else
            return AddCheryll(Vector3.up * CameraSpawnHeightMeters);
#endif
        }

        private Transform AddCheryll(Vector3 position,
            RuntimeAnimatorController controller = null, StaffCameraConfig cameraConfig = null,
            GameObject avatar = null, bool hasJumpSpringLimits = false)
        {
            var go = avatar != null ? Object.Instantiate(avatar) : new GameObject("Cheryll");
            go.SetActive(false); // Awake must see the injected references
            go.transform.SetParent(_root.transform);
            go.transform.position = position;
            _animator = avatar != null ? go.GetComponent<Animator>() : go.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller != null ? controller :
                LoadController();
            var camera = new GameObject("Camera").transform;
            camera.SetParent(_root.transform);
            camera.position = CameraStart;
            _movement = go.AddComponent<StaffConsoleMovement>();
            SetField(_movement, "_animator", _animator);
            SetField(_movement, "_staffCamera", camera);
            SetField(_movement, "_isFocusRequired", false); // the Editor may not be focused
            if (cameraConfig != null)
                SetField(_movement, "_cameraConfig", cameraConfig);
            if (hasJumpSpringLimits)
            {
                var springLimits = go.AddComponent<StaffConsoleJumpSkirt>();
                SetField(springLimits, "_avatar", go.GetComponent("Vrm10Instance"));
                SetField(springLimits, "_movement", _movement);
            }
            go.SetActive(true);
            _controller = go.GetComponent<CharacterController>();
            return go.transform;
        }

        private static RuntimeAnimatorController LoadController() =>
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
#else
            null;
#endif

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static T GetField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);

        private GameObject AddBox(Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(_root.transform);
            box.transform.SetPositionAndRotation(center, Quaternion.identity);
            box.transform.localScale = size;
            return box;
        }

        private void AddFloor() =>
            AddBox(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f)); // top at y = 0

        private void AddStep(float heightMeters) =>
            AddBox(new Vector3(0f, heightMeters / 2f, 3f), new Vector3(10f, heightMeters, 4f));

        private void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        // Queued without InputSystem.Update so the next frame's own update sees a fresh press.
        private IEnumerator Tap(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

#if UNITY_EDITOR
        // The real controller's Jump -> Idle wiring, in memory: leaves Jump when IsGrounded is
        // true, with no exit time, so nothing but landing ends Jump.
        private static RuntimeAnimatorController GroundedController()
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IdleLock", AnimatorControllerParameterType.Bool);
            controller.AddParameter("WalkHorizontal", AnimatorControllerParameterType.Float);
            controller.AddParameter("WalkForward", AnimatorControllerParameterType.Float);
            controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle");
            var jump = machine.AddState("Jump");
            jump.tag = "Jump";
            var clip = new AnimationClip { name = "Jumping0" };
            clip.SetCurve("NoSuchBone", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1.2f, 0f));
            jump.motion = clip;
            var landed = jump.AddTransition(idle);
            landed.hasExitTime = false;
            landed.duration = 0f;
            landed.AddCondition(
                UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsGrounded");
            return controller;
        }
#else
        private static RuntimeAnimatorController GroundedController() => null;
#endif

        private static IEnumerator Wait(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
                yield return null;
        }
    }
}
