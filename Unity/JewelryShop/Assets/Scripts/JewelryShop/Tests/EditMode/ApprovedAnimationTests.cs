using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class ApprovedAnimationTests
    {
        [Test]
        public void Progress_ClampsToZeroAndOne()
        {
            Assert.That(ApprovedAnimation.Progress(-1f, 0.4f), Is.EqualTo(0f));
            Assert.That(ApprovedAnimation.Progress(0.2f, 0.4f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ApprovedAnimation.Progress(9f, 0.4f), Is.EqualTo(1f));
        }

        [Test]
        public void Progress_ZeroDuration_IsDone()
        {
            Assert.That(ApprovedAnimation.Progress(0f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void Scale_StartsAtZero_EndsAtOne_Overshoots()
        {
            Assert.That(ApprovedAnimation.Scale(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ApprovedAnimation.Scale(1f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ApprovedAnimation.Scale(0.7f), Is.GreaterThan(1f));
        }
    }
}
