using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class ReceiptTextTests
    {
        private static readonly GemKey RedRound = new GemKey(GemCut.Round, GemColor.Red);
        private static readonly GemKey BlueEmerald = new GemKey(GemCut.Emerald, GemColor.Blue);

        private static string NameOf(GemKey k) => k.Equals(RedRound) ? "Ruby" : null;

        [Test]
        public void Build_OneLinePerGemIncludingRepeats_ThenTotal()
        {
            var lines = new List<OrderLine>
            {
                new OrderLine(RedRound, 1200), new OrderLine(RedRound, 1200), new OrderLine(BlueEmerald, 45900),
            };
            Assert.That(ReceiptText.Build(lines, NameOf),
                Is.EqualTo("Ruby  ฿1,200\nRuby  ฿1,200\nBlue Emerald  ฿45,900\nTotal ฿48,300"));
        }

        [Test]
        public void Build_NullNameLookup_FallsBackToCutAndColor() =>
            Assert.That(ReceiptText.Build(new[] { new OrderLine(BlueEmerald, 10) }, null),
                Is.EqualTo("Blue Emerald  ฿10\nTotal ฿10"));
    }
}
