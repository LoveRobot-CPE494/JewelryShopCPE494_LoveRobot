using System.Linq;
using NUnit.Framework;
using JewelryShop.Core;
using JewelryShop.Storefront;
using UnityEditor;

namespace JewelryShop.Tests
{
    // Gem catalog (#76): the 9 entries exist, are distinct by Cut + Color, and match #74's table.
    [TestFixture]
    public class GemCatalogTests
    {
        private const string Folder = "Assets/Data/GemCatalog";
        private const int CatalogSize = 9;

        private static GemCatalogEntry[] Entries() =>
            AssetDatabase.FindAssets("t:GemCatalogEntry", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GemCatalogEntry>)
                .ToArray();

        [Test]
        public void Catalog_Loaded_HasNineEntriesDistinctByCutAndColor()
        {
            var keys = Entries().Select(e => e.Key).ToArray();
            Assert.That(keys.Length, Is.EqualTo(CatalogSize));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(CatalogSize), "duplicate Cut + Color");
        }

        [TestCase(GemCut.Round, GemColor.Red, "Ruby", 1.02f, "VS", "Mogok, Myanmar", 45900)]
        [TestCase(GemCut.Round, GemColor.Blue, "Blue Sapphire", 1.15f, "VVS", "Sri Lanka", 38500)]
        [TestCase(GemCut.Round, GemColor.Clear, "Diamond", 0.50f, "VS1", "Botswana", 52000)]
        [TestCase(GemCut.Round, GemColor.Pink, "Pink Sapphire", 0.90f, "VS", "Madagascar", 21900)]
        [TestCase(GemCut.Round, GemColor.Purple, "Amethyst", 2.10f, "Eye-clean", "Brazil", 3900)]
        [TestCase(GemCut.Emerald, GemColor.Green, "Emerald", 1.30f, "Eye-clean", "Colombia", 49000)]
        [TestCase(GemCut.Emerald, GemColor.Yellow, "Yellow Sapphire", 1.50f, "VS",
            "Chanthaburi, Thailand", 27500)]
        [TestCase(GemCut.Emerald, GemColor.Blue, "London Blue Topaz", 3.00f, "VVS", "Brazil", 4500)]
        [TestCase(GemCut.Emerald, GemColor.Orange, "Citrine", 2.40f, "Eye-clean", "Brazil", 2900)]
        public void Catalog_EntryForCutAndColor_MatchesTable(GemCut cut, GemColor color, string name,
            float carat, string clarity, string origin, int priceBaht)
        {
            var e = Entries().SingleOrDefault(x => x.Key.Equals(new GemKey(cut, color)));
            Assert.That(e, Is.Not.Null, $"{color} {cut}");
            Assert.That(e.GemName, Is.EqualTo(name));
            Assert.That(e.Carat, Is.EqualTo(carat).Within(0.001f));
            Assert.That(e.Clarity, Is.EqualTo(clarity));
            Assert.That(e.Origin, Is.EqualTo(origin));
            Assert.That(e.PriceBaht, Is.EqualTo(priceBaht));
            Assert.That(e.StartingStockCount, Is.EqualTo(5));
        }
    }
}
