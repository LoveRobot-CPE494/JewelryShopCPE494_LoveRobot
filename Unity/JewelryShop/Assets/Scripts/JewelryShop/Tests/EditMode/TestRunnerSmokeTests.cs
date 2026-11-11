using NUnit.Framework;
using UnityEngine;

namespace JewelryShop.Tests
{
    // Proves the EditMode suite is wired: the test assembly compiles and runs inside the Editor.
    // Replace with real JewelryShop.Core rule tests as tickets land.
    [TestFixture]
    public class TestRunnerSmokeTests
    {
        [Test]
        public void EditModeSuite_RunsInEditor_IsEditor()
        {
            Assert.That(Application.isEditor, Is.True);
        }
    }
}
