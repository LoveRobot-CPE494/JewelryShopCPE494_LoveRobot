using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class LocalAddressPickerTests
    {
        [TestCase("192.168.1.42", true)]
        [TestCase("10.0.0.1", true)]
        [TestCase("0.0.0.0", false)]
        [TestCase("255.255.255.255", false)]
        [TestCase("127.0.0.1", false)]
        [TestCase("127.9.9.9", false)]
        [TestCase("999.1.1.1", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsUsableAddress_ClassifiesAddresses(string address, bool expected)
        {
            Assert.That(HostAddressResolver.IsUsableAddress(address), Is.EqualTo(expected));
        }

        [Test]
        public void Pick_TakesFirstUpUsableCandidateWithGateway()
        {
            var picked = LocalAddressPicker.Pick(new[]
            {
                new AddressCandidate("169.254.1.1", false, true),   // down
                new AddressCandidate("192.168.56.1", true, false),  // virtual: no gateway
                new AddressCandidate("127.0.0.1", true, true),      // loopback
                new AddressCandidate("192.168.1.42", true, true),
                new AddressCandidate("10.0.0.5", true, true),
            });
            Assert.That(picked, Is.EqualTo("192.168.1.42"));
        }

        [Test]
        public void Pick_NoCandidate_IsNull()
        {
            Assert.That(LocalAddressPicker.Pick(null), Is.Null);
            Assert.That(LocalAddressPicker.Pick(new AddressCandidate[0]), Is.Null);
        }
    }
}
