using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class HostBeaconTests
    {
        private static byte[] StaffWire() =>
            HostBeaconCodec.Encode(BeaconRole.Staff, "192.168.1.42", 7777);

        private static bool Decodes(byte[] data) =>
            HostBeaconCodec.TryDecode(data, out _, out _, out _);

        [TestCase(BeaconRole.Staff, "192.168.1.42", (ushort)7777)]
        [TestCase(BeaconRole.Robot, "10.0.0.1", (ushort)1)]
        [TestCase(BeaconRole.Robot, "172.16.255.254", (ushort)65535)]
        public void Encode_ThenTryDecode_RoundTrips(BeaconRole role, string address, ushort port)
        {
            var wire = HostBeaconCodec.Encode(role, address, port);

            var ok = HostBeaconCodec.TryDecode(wire, out var backRole, out var backAddress,
                out var backPort);

            Assert.That(ok, Is.True);
            Assert.That(wire.Length, Is.EqualTo(11));
            Assert.That(backRole, Is.EqualTo(role));
            Assert.That(backAddress, Is.EqualTo(address));
            Assert.That(backPort, Is.EqualTo(port));
        }

        [Test]
        public void Encode_MatchesDocumentedExampleBytes()
        {
            Assert.That(StaffWire(), Is.EqualTo(
                new byte[] { 0x4A, 0x53, 0x48, 0x42, 0x01, 0xC0, 0xA8, 0x01, 0x2A, 0x1E, 0x61 }));
            Assert.That(HostBeaconCodec.Encode(BeaconRole.Robot, "192.168.1.42", 7777), Is.EqualTo(
                new byte[] { 0x4A, 0x53, 0x52, 0x42, 0x01, 0xC0, 0xA8, 0x01, 0x2A, 0x1E, 0x61 }));
        }

        [Test]
        public void TryDecode_NullOrWrongSize_IsRejected()
        {
            var size = HostBeaconCodec.Length;
            Assert.That(Decodes(null), Is.False);
            Assert.That(Decodes(new byte[0]), Is.False);
            Assert.That(Decodes(new byte[size - 1]), Is.False);
            Assert.That(Decodes(new byte[size + 1]), Is.False);
        }

        [Test]
        public void TryDecode_UnknownRole_IsRejected()
        {
            var wire = StaffWire();
            wire[2] = (byte)'X'; // "JSXB"
            Assert.That(Decodes(wire), Is.False);
        }

        [Test]
        public void TryDecode_WrongMagic_IsRejected()
        {
            var wire = StaffWire();
            wire[0] = (byte)'X';
            Assert.That(Decodes(wire), Is.False);
        }

        [TestCase((byte)0)]
        [TestCase((byte)2)]
        [TestCase((byte)255)]
        public void TryDecode_UnknownVersion_IsRejected(byte version)
        {
            var wire = StaffWire();
            wire[4] = version;
            Assert.That(Decodes(wire), Is.False);
        }

        [TestCase((byte)0, (byte)0, (byte)0, (byte)0)]
        [TestCase((byte)255, (byte)255, (byte)255, (byte)255)]
        [TestCase((byte)127, (byte)0, (byte)0, (byte)1)]
        public void TryDecode_UnusableAddress_IsRejected(byte a, byte b, byte c, byte d)
        {
            var wire = StaffWire();
            wire[5] = a; wire[6] = b; wire[7] = c; wire[8] = d;
            Assert.That(Decodes(wire), Is.False);
        }

        [Test]
        public void TryDecode_PortZero_IsRejected()
        {
            var wire = StaffWire();
            wire[9] = 0; wire[10] = 0;
            Assert.That(Decodes(wire), Is.False);
        }

        [Test]
        public void TryDecode_StrayBytes_NeverThrows()
        {
            var rng = new System.Random(1);
            for (var n = 0; n < 500; n++)
            {
                var junk = new byte[rng.Next(0, 20)];
                rng.NextBytes(junk);
                Assert.DoesNotThrow(() => HostBeaconCodec.TryDecode(junk, out _, out _, out _));
            }
        }

        [Test]
        public void Encode_UnusableInput_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode(BeaconRole.Staff, "0.0.0.0", 7777));
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode(BeaconRole.Robot, "not an ip", 7777));
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode(BeaconRole.Staff, "192.168.1.42", 0));
        }
    }
}
