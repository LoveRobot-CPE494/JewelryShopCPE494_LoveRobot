using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace JewelryShop.Tests
{
    // Guards the real-art Storefront (#22): gem look, grab behaviour and the 005-03 names.
    [TestFixture]
    public class StorefrontAssetsTests
    {
        const string ScenePath = "Assets/Scenes/Storefront.unity";
        static readonly string[] Colors = { "Red", "Blue", "Green", "Yellow" };
        static readonly string[] Cuts = { "Round", "Emerald" };
        static readonly string[] RequiredNames =
        {
            "Storefront", "DisplayCase", "GemModelPositions", "Checkout", "CheckoutCounter",
            "CardReader", "StaffCallButton", "StaffSpot", "CustomerSpawnPoint", "Floor", "Directional Light",
        };

        [TestCaseSource(nameof(Colors))]
        public void GemMaterial_IsUrpLitTransparentAndSmooth(string color)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Gems/Gem_{color}.mat");
            Assert.That(m, Is.Not.Null);
            Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(m.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(m.GetFloat("_Smoothness"), Is.GreaterThanOrEqualTo(0.9f));
        }

        [TestCaseSource(nameof(Cuts))]
        public void GemPrefab_KeepsGrabInteractableAndRigidbody(string cut)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/GemModel_{cut}.prefab");
            Assert.That(p, Is.Not.Null);
            Assert.That(p.GetComponent<XRGrabInteractable>(), Is.Not.Null);
            Assert.That(p.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(p.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(p.GetComponentInChildren<MeshFilter>().sharedMesh, Is.Not.Null);
        }

        [Test]
        public void Storefront_KeepsNamesAndNineMixedGems()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var all = new System.Collections.Generic.List<Transform>();
                foreach (var r in scene.GetRootGameObjects()) all.AddRange(r.GetComponentsInChildren<Transform>(true));
                var names = all.ConvertAll(t => t.name);
                foreach (var n in RequiredNames) Assert.That(names, Does.Contain(n), n);

                var cuts = new System.Collections.Generic.HashSet<string>();
                var colors = new System.Collections.Generic.HashSet<string>();
                for (int i = 1; i <= 9; i++)
                {
                    var slot = all.Find(t => t.name == $"GemModelPosition_0{i}");
                    Assert.That(slot, Is.Not.Null, $"slot {i}");
                    var gem = slot.Find("GemModel");
                    Assert.That(gem, Is.Not.Null, $"gem {i}");
                    Assert.That(gem.GetComponent<XRGrabInteractable>(), Is.Not.Null);
                    Assert.That(gem.GetComponent<Rigidbody>(), Is.Not.Null);
                    cuts.Add(PrefabUtility.GetCorrespondingObjectFromSource(gem.gameObject).name);
                    colors.Add(gem.GetComponentInChildren<MeshRenderer>().sharedMaterial.name);
                }
                Assert.That(cuts.Count, Is.EqualTo(2));
                Assert.That(colors.Count, Is.EqualTo(4));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
