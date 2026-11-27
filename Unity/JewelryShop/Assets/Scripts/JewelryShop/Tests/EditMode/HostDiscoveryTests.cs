using JewelryShop.Core;
using JewelryShop.Networking;
using NUnit.Framework;
using UnityEngine;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class HostDiscoveryTests
    {
        private string _key, _robotKey, _autoKey;
        private HostAddressConfig _config;
        private HostDiscovery _discovery;

        [SetUp]
        public void SetUp()
        {
            _key = HostAddressStore.Key;
            _robotKey = HostAddressStore.RobotKey;
            _autoKey = HostAddressStore.AutomaticKey;
            HostAddressStore.Key = "JewelryShop.Tests.DiscHost";
            HostAddressStore.RobotKey = "JewelryShop.Tests.DiscRobot";
            HostAddressStore.AutomaticKey = "JewelryShop.Tests.DiscAuto";
            PlayerPrefs.DeleteKey(HostAddressStore.Key);
            PlayerPrefs.DeleteKey(HostAddressStore.RobotKey);
            PlayerPrefs.DeleteKey(HostAddressStore.AutomaticKey);
            HostAddressStore.TrySetTyped("10.0.0.5");
            HostAddressStore.TrySetTypedRobot("10.0.0.6");
            _config = ScriptableObject.CreateInstance<HostAddressConfig>();
            _discovery = new HostDiscovery(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _discovery.Dispose();
            Object.DestroyImmediate(_config);
            PlayerPrefs.DeleteKey(HostAddressStore.Key);
            PlayerPrefs.DeleteKey(HostAddressStore.RobotKey);
            PlayerPrefs.DeleteKey(HostAddressStore.AutomaticKey);
            HostAddressStore.Key = _key;
            HostAddressStore.RobotKey = _robotKey;
            HostAddressStore.AutomaticKey = _autoKey;
        }

        private static byte[] Beacon(BeaconRole role, string address) =>
            HostBeaconCodec.Encode(role, address, 7777);

        [Test]
        public void Automatic_DefaultsOn() =>
            Assert.That(HostAddressStore.Automatic, Is.True);

        [Test]
        public void SetAutomatic_Saves_AndRaisesChanged()
        {
            var raised = 0;
            void OnChanged() => raised++;
            HostAddressStore.AutomaticChanged += OnChanged;
            try
            {
                HostAddressStore.SetAutomatic(false);
                Assert.That(HostAddressStore.Automatic, Is.False);
                Assert.That(raised, Is.EqualTo(1));
            }
            finally { HostAddressStore.AutomaticChanged -= OnChanged; }
        }

        [Test]
        public void Refresh_FreshStaffBeacon_HostDiscovered()
        {
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 100);
            _discovery.Refresh(102.9);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.9"));
            Assert.That(_discovery.HostState, Is.EqualTo(AddressState.Discovered));
        }

        [Test]
        public void Refresh_StaleBeacon_HostNotFoundTypedStays()
        {
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 100);
            _discovery.Refresh(103.1);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.5"));
            Assert.That(_discovery.HostState, Is.EqualTo(AddressState.HostNotFound));
        }

        [Test]
        public void Refresh_NoBeacon_HostNotFoundTypedStays()
        {
            _discovery.Refresh(50);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.5"));
            Assert.That(_discovery.HostState, Is.EqualTo(AddressState.HostNotFound));
        }

        [Test]
        public void Refresh_AutomaticOff_IgnoresBeacon()
        {
            HostAddressStore.SetAutomatic(false);
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 100);
            _discovery.Refresh(100);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.5"));
            Assert.That(_discovery.HostState, Is.EqualTo(AddressState.Manual));
        }

        [Test]
        public void Feed_Malformed_NeverChangesTheAddress()
        {
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 100);
            var bad = Beacon(BeaconRole.Staff, "10.0.0.77");
            bad[0] = (byte)'X';
            Assert.That(_discovery.Feed(bad, 101), Is.False);
            Assert.That(_discovery.Feed(new byte[] { 1, 2, 3 }, 101), Is.False);
            Assert.That(_discovery.Feed(null, 101), Is.False);
            _discovery.Refresh(101);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.9"));
        }

        [Test]
        public void Refresh_RobotBeacon_DoesNotChangeHostAddress()
        {
            _discovery.Feed(Beacon(BeaconRole.Robot, "10.0.0.8"), 100);
            _discovery.Refresh(100);
            Assert.That(_discovery.RobotAddress, Is.EqualTo("10.0.0.8"));
            Assert.That(_discovery.RobotState, Is.EqualTo(AddressState.Discovered));
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.5"));
            Assert.That(_discovery.HostState, Is.EqualTo(AddressState.HostNotFound));
        }

        [Test]
        public void Refresh_StaffBeacon_DoesNotChangeRobotAddress()
        {
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 100);
            _discovery.Refresh(100);
            Assert.That(_discovery.HostAddress, Is.EqualTo("10.0.0.9"));
            Assert.That(_discovery.RobotAddress, Is.EqualTo("10.0.0.6"));
            Assert.That(_discovery.RobotState, Is.EqualTo(AddressState.HostNotFound));
        }

        [Test]
        public void Refresh_AddressChange_RaisesChangedOnceAndNotWhenSame()
        {
            var raised = 0;
            _discovery.Changed += () => raised++;
            _discovery.Refresh(100); // first resolve: typed + HostNotFound
            _discovery.Refresh(100);
            Assert.That(raised, Is.EqualTo(1));
            _discovery.Feed(Beacon(BeaconRole.Staff, "10.0.0.9"), 101);
            _discovery.Refresh(101);
            Assert.That(raised, Is.EqualTo(2));
        }

        [TestCase(2.9, AddressState.Discovered)]
        [TestCase(3.0, AddressState.Discovered)]
        [TestCase(3.1, AddressState.HostNotFound)]
        public void Resolver_Threshold(double age, AddressState expected)
        {
            HostAddressResolver.Resolve("10.0.0.5", true, "10.0.0.9", age, out var state);
            Assert.That(state, Is.EqualTo(expected));
        }
    }
}
