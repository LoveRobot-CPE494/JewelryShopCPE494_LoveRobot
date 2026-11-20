using System.Linq;
using JewelryShop.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class IdleLockTests
    {
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        public void AllowsStandby_OnlyWhenStandingAndUnlocked(
            bool isMoving, bool isIdleLockOn, bool expected)
        {
            Assert.That(IdleLock.AllowsStandby(isMoving, isIdleLockOn), Is.EqualTo(expected));
        }

        [Test]
        public void StatusText_FollowsTheToggle()
        {
            Assert.That(IdleLock.StatusText(true), Is.EqualTo("Idle Lock: ON"));
            Assert.That(IdleLock.StatusText(false), Is.EqualTo("Idle Lock: OFF"));
        }

        [Test]
        public void PreviewController_IdleLock_IsBoolDefaultingOff()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Avatar/Animations/Cheryll/CheryllPreview.controller");
            var parameter = controller.parameters.Single(item => item.name == "IdleLock");

            Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(parameter.defaultBool, Is.False);
        }
    }
}
