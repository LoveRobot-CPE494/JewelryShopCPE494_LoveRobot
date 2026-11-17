using System.Linq;
using NUnit.Framework;
using JewelryShop.Storefront;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace JewelryShop.Tests
{
    // Smoke test (#23): the Storefront is complete as a Customer sees it. Paths are the 005-03 names.
    [TestFixture]
    public class StorefrontSceneSmokeTests
    {
        const string ScenePath = "Assets/Scenes/Storefront.unity";
        const string Slots = "Storefront/DisplayCase/GemModelPositions";
        Scene _scene;

        [OneTimeSetUp]
        public void OpenScene() => _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        [OneTimeTearDown]
        public void CloseScene() => EditorSceneManager.CloseScene(_scene, true);

        // Hierarchy path from a scene root; never a bare name ("Greybox" repeats on purpose).
        Transform Find(string path)
        {
            var parts = path.Split('/');
            var t = _scene.GetRootGameObjects().FirstOrDefault(r => r.name == parts[0])?.transform;
            return t == null || parts.Length == 1 ? t : t.Find(string.Join("/", parts.Skip(1)));
        }

        Transform[] Gems()
        {
            var slots = Find(Slots);
            Assert.That(slots, Is.Not.Null, Slots);
            return slots.Cast<Transform>().SelectMany(s => s.Cast<Transform>()).ToArray();
        }

        [TestCase("Storefront/CustomerSpawnPoint")]
        [TestCase("Storefront/DisplayCase")]
        [TestCase(Slots)]
        [TestCase("Storefront/Checkout")]
        [TestCase("Storefront/Checkout/CheckoutCounter")]
        [TestCase("Storefront/Checkout/CardReader")]
        [TestCase("Storefront/StaffCallButton")]
        [TestCase("Storefront/StaffSpot")]
        public void Storefront_HasRequiredObject(string path) =>
            Assert.That(Find(path), Is.Not.Null, $"missing required object: {path}");

        [Test]
        public void Storefront_HasXrOriginAndNoCameraOutsideIt()
        {
            var origins = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).ToArray();
            Assert.That(origins.Length, Is.EqualTo(1), "expected exactly one XR Origin");
            var strays = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                .Where(c => !c.transform.IsChildOf(origins[0].transform)).Select(c => c.name);
            Assert.That(strays, Is.Empty, "Camera outside the XR Origin");
        }

        [Test]
        public void DisplayCase_HoldsExactlyNineGemModels()
        {
            var slots = Find(Slots);
            Assert.That(slots, Is.Not.Null, $"missing required object: {Slots}");
            Assert.That(slots.childCount, Is.EqualTo(9), "gem slots");
            foreach (Transform s in slots) Assert.That(s.childCount, Is.EqualTo(1), $"{s.name}: one Gem Model");
        }

        [Test]
        public void GemModels_AreGrabbableRigidbodies()
        {
            foreach (var g in Gems())
            {
                Assert.That(g.GetComponent<XRGrabInteractable>(), Is.Not.Null, $"{g.parent.name}: XRGrabInteractable");
                Assert.That(g.GetComponent<Rigidbody>(), Is.Not.Null, $"{g.parent.name}: Rigidbody");
            }
        }

        [Test]
        public void RightController_HasGemModelKeyRotator()
        {
            var rotator = _scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<GemModelKeyRotator>(true)).SingleOrDefault();
            Assert.That(rotator, Is.Not.Null, "GemModelKeyRotator on the right Controller");
            Assert.That(rotator.name, Is.EqualTo("Right Controller"));
        }

        [Test]
        public void Simulator_PointAndClickIsOn()
        {
            var sim = _scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<XRInteractionSimulator>(true)).Single();
            Assert.That(sim.usePointAndClick, Is.True, "Point And Click stays on (#59)");
        }
    }
}
