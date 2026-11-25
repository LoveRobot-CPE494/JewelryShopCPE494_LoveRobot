using JewelryShop.Core;
using JewelryShop.Networking;
using NUnit.Framework;
using UnityEngine;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class HostAddressTests
    {
        private string _realKey;

        [SetUp]
        public void SetUp()
        {
            _realKey = HostAddressStore.Key;
            HostAddressStore.Key = "JewelryShop.Tests.HostAddress";
            PlayerPrefs.DeleteKey(HostAddressStore.Key);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(HostAddressStore.Key);
            HostAddressStore.Key = _realKey;
        }

        [TestCase("192.168.1.10", true)]
        [TestCase("0.0.0.0", true)]
        [TestCase("255.255.255.255", true)]
        [TestCase("1.0.10.200", true)]
        [TestCase("01.2.3.4", false)]
        [TestCase("001.002.003.004", false)]
        [TestCase("1.2.3.04", false)]
        [TestCase("256.1.1.1", false)]
        [TestCase("1.2.3", false)]
        [TestCase("1.2.3.4.5", false)]
        [TestCase("1.2..4", false)]
        [TestCase("1.2.3.4 ", false)]
        [TestCase("-1.2.3.4", false)]
        [TestCase("a.b.c.d", false)]
        [TestCase("::1", false)]
        [TestCase("localhost", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValidIPv4_ClassifiesInput(string text, bool expected) =>
            Assert.That(HostAddressResolver.IsValidIPv4(text), Is.EqualTo(expected));

        [Test]
        public void Resolve_AutomaticOff_ReturnsTypedEvenWhenDiscovered() =>
            Assert.That(HostAddressResolver.Resolve("10.0.0.5", false, "10.0.0.9"),
                Is.EqualTo("10.0.0.5"));

        [Test]
        public void Resolve_AutomaticOnWithDiscovered_ReturnsDiscovered() =>
            Assert.That(HostAddressResolver.Resolve("10.0.0.5", true, "10.0.0.9"),
                Is.EqualTo("10.0.0.9"));

        [Test]
        public void Resolve_AutomaticOnNothingDiscovered_FallsBackToTyped() =>
            Assert.That(HostAddressResolver.Resolve("10.0.0.5", true, null),
                Is.EqualTo("10.0.0.5"));

        [Test]
        public void Resolve_InvalidTyped_ReturnsNull() =>
            Assert.That(HostAddressResolver.Resolve("not an ip", false, null), Is.Null);

        [Test]
        public void Typed_NothingSaved_UsesConfigDefault()
        {
            var config = ScriptableObject.CreateInstance<HostAddressConfig>();
            Assert.That(HostAddressStore.Typed(config), Is.EqualTo(config.DefaultHostAddress));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Typed_SavedValueInvalid_FallsBackToConfigDefault()
        {
            var config = ScriptableObject.CreateInstance<HostAddressConfig>();
            PlayerPrefs.SetString(HostAddressStore.Key, "not an ip");
            Assert.That(HostAddressStore.Typed(config), Is.EqualTo(config.DefaultHostAddress));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Typed_NullConfigWithSavedValid_ReturnsSaved()
        {
            PlayerPrefs.SetString(HostAddressStore.Key, "10.1.2.3");
            Assert.That(HostAddressStore.Typed(null), Is.EqualTo("10.1.2.3"));
        }

        [Test]
        public void TrySetTyped_Valid_SavesRestoresAndRaisesChanged()
        {
            var raised = 0;
            void OnChanged() => raised++;
            HostAddressStore.Changed += OnChanged;
            try
            {
                Assert.That(HostAddressStore.TrySetTyped("172.16.0.2"), Is.True);
                Assert.That(HostAddressStore.Typed(null), Is.EqualTo("172.16.0.2"));
                Assert.That(raised, Is.EqualTo(1));
            }
            finally { HostAddressStore.Changed -= OnChanged; }
        }

        [Test]
        public void TrySetTyped_Invalid_SavesNothingAndRaisesNothing()
        {
            var raised = 0;
            void OnChanged() => raised++;
            HostAddressStore.Changed += OnChanged;
            try
            {
                Assert.That(HostAddressStore.TrySetTyped("999.1.1.1"), Is.False);
                Assert.That(HostAddressStore.Typed(null), Is.Null);
                Assert.That(raised, Is.EqualTo(0));
            }
            finally { HostAddressStore.Changed -= OnChanged; }
        }
    }
}
