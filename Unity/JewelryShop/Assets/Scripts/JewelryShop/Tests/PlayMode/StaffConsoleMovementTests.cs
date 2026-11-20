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

        private Transform AddCheryll(Vector3 position)
        {
            var go = new GameObject("Cheryll");
            go.SetActive(false); // Awake must see the injected references
            go.transform.SetParent(_root.transform);
            go.transform.position = position;
            _animator = go.AddComponent<Animator>();
            _animator.runtimeAnimatorController =
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
#else
                null;
#endif
            var camera = new GameObject("Camera").transform;
            camera.SetParent(_root.transform);
            camera.position = new Vector3(0f, 2f, -3f);
            _movement = go.AddComponent<StaffConsoleMovement>();
            SetField(_movement, "_animator", _animator);
            SetField(_movement, "_staffCamera", camera);
            SetField(_movement, "_isFocusRequired", false); // the Editor may not be focused
            go.SetActive(true);
            _controller = go.GetComponent<CharacterController>();
            return go.transform;
        }

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

        private static IEnumerator Wait(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
                yield return null;
        }
    }
}
