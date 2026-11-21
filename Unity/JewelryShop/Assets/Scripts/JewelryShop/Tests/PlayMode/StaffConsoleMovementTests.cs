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
        private CharacterController _controller;
        private Animator _animator;
        private StaffConsoleMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MovementTestRoot");
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            Hold(); // release held keys before the device goes
            InputSystem.RemoveDevice(_keyboard);
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
            AddBox(new Vector3(0f, 1.5f, -2.25f), new Vector3(10f, 3f, 0.5f)).transform;

        private static Vector3 StartPivot =>
            Vector3.up * (CameraSpawnHeightMeters + CameraSettings.Defaults.PivotHeightMeters);

        private Vector3 CurrentPivot => _movement.transform.position +
            Vector3.up * CameraSettings.Defaults.PivotHeightMeters;

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

        private Transform AddCheryll(Vector3 position,
            RuntimeAnimatorController controller = null, StaffCameraConfig cameraConfig = null)
        {
            var go = new GameObject("Cheryll");
            go.SetActive(false); // Awake must see the injected references
            go.transform.SetParent(_root.transform);
            go.transform.position = position;
            _animator = go.AddComponent<Animator>();
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
