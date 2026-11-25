using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class HostBeaconTests
    {
        [TestCase("192.168.1.42", (ushort)7777)]
        [TestCase("10.0.0.1", (ushort)1)]
        [TestCase("172.16.255.254", (ushort)65535)]
        public void Encode_ThenTryDecode_RoundTrips(string address, ushort port)
        {
            var wire = HostBeaconCodec.Encode(address, port);

            var ok = HostBeaconCodec.TryDecode(wire, out var backAddress, out var backPort);

            Assert.That(ok, Is.True);
            Assert.That(wire.Length, Is.EqualTo(11));
            Assert.That(backAddress, Is.EqualTo(address));
            Assert.That(backPort, Is.EqualTo(port));
        }

        [Test]
        public void TryDecode_NullOrWrongSize_IsRejected()
        {
            var size = HostBeaconCodec.Length;
            Assert.That(HostBeaconCodec.TryDecode(null, out _, out _), Is.False);
            Assert.That(HostBeaconCodec.TryDecode(new byte[0], out _, out _), Is.False);
            Assert.That(HostBeaconCodec.TryDecode(new byte[size - 1], out _, out _), Is.False);
            Assert.That(HostBeaconCodec.TryDecode(new byte[size + 1], out _, out _), Is.False);
        }

        [Test]
        public void TryDecode_WrongMagic_IsRejected()
        {
            var wire = HostBeaconCodec.Encode("192.168.1.42", 7777);
            wire[0] = (byte)'X';
            Assert.That(HostBeaconCodec.TryDecode(wire, out _, out _), Is.False);
        }

        [TestCase((byte)0)]
        [TestCase((byte)2)]
        [TestCase((byte)255)]
        public void TryDecode_UnknownVersion_IsRejected(byte version)
        {
            var wire = HostBeaconCodec.Encode("192.168.1.42", 7777);
            wire[4] = version;
            Assert.That(HostBeaconCodec.TryDecode(wire, out _, out _), Is.False);
        }

        [TestCase((byte)0, (byte)0, (byte)0, (byte)0)]
        [TestCase((byte)255, (byte)255, (byte)255, (byte)255)]
        [TestCase((byte)127, (byte)0, (byte)0, (byte)1)]
        public void TryDecode_UnusableAddress_IsRejected(byte a, byte b, byte c, byte d)
        {
            var wire = HostBeaconCodec.Encode("192.168.1.42", 7777);
            wire[5] = a; wire[6] = b; wire[7] = c; wire[8] = d;
            Assert.That(HostBeaconCodec.TryDecode(wire, out _, out _), Is.False);
        }

        [Test]
        public void TryDecode_PortZero_IsRejected()
        {
            var wire = HostBeaconCodec.Encode("192.168.1.42", 7777);
            wire[9] = 0; wire[10] = 0;
            Assert.That(HostBeaconCodec.TryDecode(wire, out _, out _), Is.False);
        }

        [Test]
        public void TryDecode_StrayBytes_NeverThrows()
        {
            var rng = new System.Random(1);
            for (var n = 0; n < 500; n++)
            {
                var junk = new byte[rng.Next(0, 20)];
                rng.NextBytes(junk);
                Assert.DoesNotThrow(() => HostBeaconCodec.TryDecode(junk, out _, out _));
            }
        }

        [Test]
        public void Encode_UnusableInput_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode("0.0.0.0", 7777));
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode("not an ip", 7777));
            Assert.Throws<System.ArgumentException>(
                () => HostBeaconCodec.Encode("192.168.1.42", 0));
        }
    }
}
