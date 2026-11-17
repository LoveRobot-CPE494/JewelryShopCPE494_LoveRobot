using JewelryShop.Storefront;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class GemModelKeyRotatorTests
    {
        private GameObject _go;
        private GemModelKeyRotator _rotator;
        private Transform _attach;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("rotator");
            _rotator = _go.AddComponent<GemModelKeyRotator>();
            _attach = new GameObject("attach").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_attach.gameObject);
        }

        private static void AssertAngle(Quaternion a, Quaternion b) => Assert.That(Quaternion.Angle(a, b), Is.LessThan(0.01f));

        [Test]
        public void Step_AltHeldWhileHolding_MouseXYawsAndFreezesAim()
        {
            var frozen = _rotator.Step(_attach, true, true, new Vector2(10, 0), 0);
            Assert.That(frozen, Is.True);
            AssertAngle(_attach.localRotation, Quaternion.Euler(0, -3f, 0));
        }

        [Test]
        public void Step_AltHeldWhileHolding_MouseYPitches()
        {
            _rotator.Step(_attach, true, true, new Vector2(0, 10), 0);
            AssertAngle(_attach.localRotation, Quaternion.Euler(3f, 0, 0));
        }

        [Test]
        public void Step_AltHeldWhileHolding_ScrollRolls()
        {
            _rotator.Step(_attach, true, true, Vector2.zero, 120);
            AssertAngle(_attach.localRotation, Quaternion.Euler(0, 0, 30f));
        }

        [Test]
        public void Step_AltUp_NoRotationNoFreeze()
        {
            var frozen = _rotator.Step(_attach, true, false, new Vector2(50, 50), 120);
            Assert.That(frozen, Is.False);
            AssertAngle(_attach.localRotation, Quaternion.identity);
        }

        [Test]
        public void Step_NothingHeld_NoRotationNoFreeze()
        {
            var frozen = _rotator.Step(_attach, false, true, new Vector2(50, 50), 120);
            Assert.That(frozen, Is.False);
            AssertAngle(_attach.localRotation, Quaternion.identity);
        }

        [Test]
        public void Step_AltReleasedWhileHolding_KeepsRotation()
        {
            _rotator.Step(_attach, true, true, new Vector2(10, 0), 0);
            var turned = _attach.localRotation;
            _rotator.Step(_attach, true, false, Vector2.zero, 0);
            AssertAngle(_attach.localRotation, turned);
        }

        [Test]
        public void Step_GemModelReleased_ResetsAttachRotation()
        {
            _attach.localRotation = Quaternion.Euler(0, 20, 0);
            _rotator.Step(_attach, true, false, Vector2.zero, 0);
            _rotator.Step(_attach, true, true, new Vector2(10, 10), 120);
            _rotator.Step(_attach, false, false, Vector2.zero, 0);
            AssertAngle(_attach.localRotation, Quaternion.Euler(0, 20, 0));
        }

        [Test]
        public void IsSimulatorActive_NoSimulatorOrNotEditor_IsFalse()
        {
            Assert.That(GemModelKeyRotator.IsSimulatorActive(true, null), Is.False);
            var sim = _go.AddComponent<XRInteractionSimulator>();
            Assert.That(GemModelKeyRotator.IsSimulatorActive(false, sim), Is.False);
        }

        [Test]
        public void IsSimulatorActive_EnabledSimulatorInEditor_IsTrue()
        {
            var sim = _go.AddComponent<XRInteractionSimulator>();
            Assert.That(GemModelKeyRotator.IsSimulatorActive(true, sim), Is.True);
            sim.enabled = false;
            Assert.That(GemModelKeyRotator.IsSimulatorActive(true, sim), Is.False);
        }
    }
}
