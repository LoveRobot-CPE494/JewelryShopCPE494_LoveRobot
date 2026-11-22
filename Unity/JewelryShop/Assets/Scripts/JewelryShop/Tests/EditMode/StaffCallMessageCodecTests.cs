using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class StaffCallMessageCodecTests
    {
        [TestCase(StaffCallMessage.Call)]
        [TestCase(StaffCallMessage.Cancel)]
        [TestCase(StaffCallMessage.Answer)]
        [TestCase(StaffCallMessage.HangUp)]
        [TestCase(StaffCallMessage.Arrived)]
        public void Encode_ThenTryDecode_RoundTrips(StaffCallMessage message)
        {
            var wire = StaffCallMessageCodec.Encode(message);

            var ok = StaffCallMessageCodec.TryDecode(wire, out var back);

            Assert.That(ok, Is.True);
            Assert.That(back, Is.EqualTo(message));
        }

        [Test]
        public void TryDecode_UnknownByte_IsRejected()
        {
            Assert.That(StaffCallMessageCodec.TryDecode(200, out _), Is.False);
        }
    }
}
