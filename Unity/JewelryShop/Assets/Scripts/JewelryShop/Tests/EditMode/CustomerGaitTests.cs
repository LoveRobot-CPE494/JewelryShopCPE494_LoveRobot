using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class CustomerGaitTests
    {
        private const float Dt = 0.02f;

        private static CustomerPoseSmoother.Result At(float x, float z, float yaw = 0f, bool isSnap = false) =>
            new CustomerPoseSmoother.Result { X = x, Z = z, YawDegrees = yaw, IsSnap = isSnap };

        private static CustomerGait.Output Walk(CustomerGait gait, float speed, float yaw, float dirX, float dirZ, int steps)
        {
            var output = default(CustomerGait.Output);
            for (var i = 1; i <= steps; i++)
                output = gait.Step(At(dirX * speed * Dt * i, dirZ * speed * Dt * i, yaw), Dt);
            return output;
        }

        [Test]
        public void Step_StandingStill_IsNotMoving()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = gait.Step(At(0f, 0f), Dt);
            Assert.That(output.IsMoving, Is.False);
            Assert.That(output.Forward, Is.EqualTo(0f));
        }

        [Test]
        public void Step_WalkingForward_IsMovingWithForwardBlend()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = Walk(gait, 1f, 0f, 0f, 1f, 40);
            Assert.That(output.IsMoving, Is.True);
            Assert.That(output.Forward, Is.EqualTo(1f).Within(0.05f));
            Assert.That(output.Right, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Step_WalkingSidewaysToTheRight_BlendsRight()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = Walk(gait, 1f, 0f, 1f, 0f, 40); // yaw 0 faces +Z, so +X is her right
            Assert.That(output.Right, Is.EqualTo(1f).Within(0.05f));
            Assert.That(output.Forward, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Step_SlowDrift_StaysInDeadZone()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = Walk(gait, 0.05f, 0f, 0f, 1f, 100);
            Assert.That(output.IsMoving, Is.False);
        }

        [Test]
        public void Step_Teleport_ReportsNoWalk()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            Walk(gait, 1f, 0f, 0f, 1f, 40);
            var output = gait.Step(At(10f, 10f, 0f, true), Dt);
            Assert.That(output.IsMoving, Is.False);
            Assert.That(output.Forward, Is.EqualTo(0f));
            Assert.That(output.Right, Is.EqualTo(0f));
        }

        [Test]
        public void Step_AfterTeleport_DoesNotCountTheJumpAsSpeed()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            gait.Step(At(10f, 10f, 0f, true), Dt);
            var output = gait.Step(At(10f, 10f), Dt);
            Assert.That(output.IsMoving, Is.False);
        }

        [Test]
        public void Reset_NextStepIsFirstSample_NoWalk()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            Walk(gait, 1f, 0f, 0f, 1f, 40);
            gait.Reset();
            Assert.That(gait.Step(At(5f, 5f), Dt).IsMoving, Is.False);
        }

        [Test]
        public void Step_ZeroDelta_DoesNotDivideByZero()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = gait.Step(At(1f, 0f), 0f);
            Assert.That(float.IsNaN(output.Forward), Is.False);
        }

        [Test]
        public void Step_Yaw90MovingPlusX_IsForward()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f, 90f), Dt);
            var output = Walk(gait, 1f, 90f, 1f, 0f, 40);
            Assert.That(output.Forward, Is.EqualTo(1f).Within(0.05f));
            Assert.That(output.Right, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Step_Yaw90MovingPlusZ_IsLeft()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f, 90f), Dt);
            var output = Walk(gait, 1f, 90f, 0f, 1f, 40);
            Assert.That(output.Right, Is.EqualTo(-1f).Within(0.05f));
            Assert.That(output.Forward, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Step_TwoMetersPerSecond_ClampsBlendToOne()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = Walk(gait, 2f, 0f, 0f, 1f, 60);
            Assert.That(output.Forward, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Step_Diagonal_BlendsBothAndStaysUnitLength()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            var output = Walk(gait, 2f, 0f, 0.7071f, 0.7071f, 60);
            Assert.That(output.Forward, Is.EqualTo(0.7071f).Within(0.02f));
            Assert.That(output.Right, Is.EqualTo(0.7071f).Within(0.02f));
        }

        [Test]
        public void Step_WalkThenStop_KeepsWalkingUntilSpeedDecaysBelowStop()
        {
            var gait = new CustomerGait(1f);
            gait.Step(At(0f, 0f), Dt);
            Walk(gait, 1f, 0f, 0f, 1f, 40);
            var z = 0.8f;
            var first = gait.Step(At(0f, z), Dt); // one frame after stopping: still decaying
            Assert.That(first.IsMoving, Is.True);
            var steps = 0;
            var output = first;
            while (output.IsMoving && steps++ < 500) output = gait.Step(At(0f, z), Dt);
            Assert.That(output.IsMoving, Is.False);
            Assert.That(steps, Is.GreaterThan(1).And.LessThan(500));
        }
    }
}
