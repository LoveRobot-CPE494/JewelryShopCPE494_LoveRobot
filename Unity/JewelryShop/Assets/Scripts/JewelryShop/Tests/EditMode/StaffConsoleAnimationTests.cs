using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class StaffConsoleAnimationTests
    {
        private const string AnimationFolder = "Assets/Avatar/Animations/Cheryll/";

        /// <summary>Each direction uses its own looping Humanoid walk.</summary>
        [TestCase("WALK00_F", 0f, 1f)]
        [TestCase("WALK00_B", 0f, -1f)]
        [TestCase("WALK00_L", -1f, 0f)]
        [TestCase("WALK00_R", 1f, 0f)]
        public void PreviewController_Walk_BlendsDirectionalHumanoidLoops(
            string name, float horizontal, float forward)
        {
            var controller = LoadController();
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Avatar/VRoid_AIM/Locomotion/{name}.anim");
            var walkState = controller.layers[0].stateMachine.states.Single(
                child => child.state.name == "Walk").state;

            Assert.That(walk, Is.Not.Null);
            Assert.That(walkState.motion, Is.TypeOf<BlendTree>());
            var tree = (BlendTree)walkState.motion;
            var direction = new Vector2(horizontal, forward);
            Assert.That(tree.children.Single(child => child.position == direction).motion,
                Is.EqualTo(walk));
            Assert.That(walk.isLooping, Is.True);
            Assert.That(walk.humanMotion, Is.True);
        }

        /// <summary>The preview jump plays once and returns to the main standby.</summary>
        [Test]
        public void PreviewController_Jump_PlaysJumping0OnceAndReturnsToIdle()
        {
            var machine = LoadController().layers[0].stateMachine;
            var state = machine.states.SingleOrDefault(child => child.state.name == "Jump").state;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "Locomotion/Jumping0.anim");

            Assert.That(state, Is.Not.Null);
            Assert.That(clip, Is.Not.Null);
            Assert.That(state.motion, Is.EqualTo(clip));
            Assert.That(state.tag, Is.EqualTo("Jump"));
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isLooping, Is.False);

            var exit = state.transitions.Single();
            Assert.That(exit.destinationState, Is.EqualTo(machine.defaultState));
            Assert.That(exit.hasExitTime, Is.True);
            Assert.That(exit.exitTime, Is.EqualTo(1f));
            Assert.That(exit.conditions, Is.Empty);
        }

        /// <summary>The default Idle state holds the main standby pose.</summary>
        [Test]
        public void PreviewController_Idle_LoopsMainStandbyClip()
        {
            var idle = LoadController().layers[0].stateMachine.defaultState;
            var clip = LoadStandbyClip("WAIT00");

            Assert.That(idle.motion, Is.EqualTo(clip));
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isLooping, Is.True);
        }

        /// <summary>Each standby variation plays once and can be interrupted by walking.</summary>
        [TestCase("Standby01", "Animation2/Standby01")]
        [TestCase("Standby02", "Animation2/Standby02")]
        [TestCase("Standby03", "WAIT01")]
        [TestCase("Standby04", "WAIT02")]
        [TestCase("Standby05", "Animation2/Standby05")]
        public void PreviewController_Standby_PlaysOnceAndReturnsToIdle(
            string stateName, string clipName)
        {
            var machine = LoadController().layers[0].stateMachine;
            var state = machine.states.SingleOrDefault(
                child => child.state.name == stateName).state;
            var walk = machine.states.Single(child => child.state.name == "Walk").state;
            var clip = LoadStandbyClip(clipName);

            Assert.That(state, Is.Not.Null);
            Assert.That(state.motion, Is.EqualTo(clip));
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isLooping, Is.False);
            AssertTransition(state, walk, AnimatorConditionMode.If);

            var exit = state.transitions.Single(
                item => item.destinationState == machine.defaultState);
            Assert.That(exit.hasExitTime, Is.True);
            Assert.That(exit.exitTime, Is.EqualTo(1f));
            Assert.That(exit.conditions, Is.Empty);
        }

        /// <summary>Pressing and releasing movement can switch clips mid-loop.</summary>
        [Test]
        public void PreviewController_IsMoving_SwitchesBetweenIdleAndWalkWithoutExitTime()
        {
            var controller = LoadController();
            var stateMachine = controller.layers[0].stateMachine;
            var idle = stateMachine.defaultState;
            var walk = stateMachine.states.Single(child => child.state.name == "Walk").state;

            Assert.That(idle.name, Is.EqualTo("Idle"));
            AssertTransition(idle, walk, AnimatorConditionMode.If);
            AssertTransition(walk, idle, AnimatorConditionMode.IfNot);
        }

        /// <summary>The preview plays each gesture once and returns to Idle.</summary>
        [TestCase("Wave", "Wave.anim")]
        [TestCase("Bow", "Bow.anim")]
        [TestCase("Point", "Point.fbx")]
        [TestCase("Nod", "Nod.fbx")]
        [TestCase("Embarrassed", "Embarrassed.anim")]
        public void PreviewController_Gesture_PlaysImportedClipOnceAndReturnsToIdle(
            string name, string fileName)
        {
            var stateMachine = LoadController().layers[0].stateMachine;
            var state = stateMachine.states.SingleOrDefault(
                child => child.state.name == name).state;
            Assert.That(state, Is.Not.Null, $"Missing gesture state {name}");

            var clip = LoadClip(AnimationFolder + "Gestures/" + fileName);
            Assert.That(state.motion, Is.EqualTo(clip));
            Assert.That(state.tag, Is.EqualTo("Gesture"));
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isLooping, Is.False);
            var exit = state.transitions.Single();
            Assert.That(exit.destinationState, Is.EqualTo(stateMachine.defaultState));
            Assert.That(exit.hasExitTime, Is.True);
            Assert.That(exit.exitTime, Is.EqualTo(1f));
        }

        private static void AssertTransition(
            AnimatorState source, AnimatorState destination, AnimatorConditionMode expectedMode)
        {
            var transition = source.transitions.Single(
                item => item.destinationState == destination);
            var condition = transition.conditions.Single();
            Assert.That(transition.hasExitTime, Is.False);
            Assert.That(condition.parameter, Is.EqualTo("IsMoving"));
            Assert.That(condition.mode, Is.EqualTo(expectedMode));
        }

        private static AnimatorController LoadController()
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AnimationFolder + "CheryllPreview.controller");
        }

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(clip => !clip.name.StartsWith("__preview__"));
        }

        private static AnimationClip LoadStandbyClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + $"Locomotion/Standby/{name}.anim");
            Assert.That(clip, Is.Not.Null);
            return clip;
        }
    }
}
