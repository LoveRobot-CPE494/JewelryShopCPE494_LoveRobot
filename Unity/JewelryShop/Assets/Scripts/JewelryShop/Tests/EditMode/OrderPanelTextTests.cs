using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class OrderPanelTextTests
    {
        private static readonly GemKey RedRound = new GemKey(GemCut.Round, GemColor.Red);
        private static readonly GemKey BlueEmerald = new GemKey(GemCut.Emerald, GemColor.Blue);

        private static string NameOf(GemKey k) => k.Equals(RedRound) ? "Ruby" : null;

        [Test]
        public void FormatBaht_UsesSymbolAndThousandsSeparators()
        {
            Assert.That(OrderPanelText.FormatBaht(45900), Is.EqualTo("฿45,900"));
            Assert.That(OrderPanelText.FormatBaht(0), Is.EqualTo("฿0"));
        }

        [Test]
        public void Rows_EmptyOrder_HasNoRowsAndZeroTotal()
        {
            var order = new Order();
            Assert.That(OrderPanelText.Rows(order, NameOf), Is.Empty);
            Assert.That(OrderPanelText.TotalText(order), Is.EqualTo("Total ฿0"));
        }

        [Test]
        public void Rows_RepeatedGem_IsOneRowPerGem()
        {
            var order = new Order();
            order.Add(RedRound, 1200, 5);
            order.Add(RedRound, 1200, 5);
            var rows = OrderPanelText.Rows(order, NameOf);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[1].Name, Is.EqualTo("Ruby"));
            Assert.That(rows[1].Price, Is.EqualTo("฿1,200"));
            Assert.That(OrderPanelText.TotalText(order), Is.EqualTo("Total ฿2,400"));
        }

        [Test]
        public void Rows_UnknownName_FallsBackToCutAndColor()
        {
            var order = new Order();
            order.Add(BlueEmerald, 45900, 5);
            Assert.That(OrderPanelText.Rows(order, NameOf)[0].Name, Is.EqualTo("Blue Emerald"));
            Assert.That(OrderPanelText.Rows(order, null)[0].Name, Is.EqualTo("Blue Emerald"));
        }

        [Test]
        public void Rows_AfterRemoval_DropsThatRow()
        {
            var order = new Order();
            order.Add(RedRound, 1200, 5);
            order.Add(BlueEmerald, 45900, 5);
            order.RemoveAt(0);
            var rows = OrderPanelText.Rows(order, NameOf);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(OrderPanelText.TotalText(order), Is.EqualTo("Total ฿45,900"));
        }

        private static Order OrderOf(int gems)
        {
            var order = new Order(50);
            for (int i = 0; i < gems; i++) order.Add(RedRound, 100, 99);
            return order;
        }

        [Test]
        public void EmptyHint_IsDropAGemHere() =>
            Assert.That(OrderPanelText.EmptyHint, Is.EqualTo("Drop a Gem here"));

        [Test]
        public void Columns_EmptyRows_AreEmptyStrings()
        {
            var rows = OrderPanelText.Rows(new Order(), NameOf);
            Assert.That(OrderPanelText.NameColumn(rows), Is.Empty);
            Assert.That(OrderPanelText.PriceColumn(rows), Is.Empty);
        }

        [Test]
        public void Columns_JoinRowsWithNewlines()
        {
            var order = new Order();
            order.Add(RedRound, 1200, 5);
            order.Add(BlueEmerald, 45900, 5);
            var rows = OrderPanelText.Rows(order, NameOf);
            Assert.That(OrderPanelText.NameColumn(rows), Is.EqualTo("Ruby\nBlue Emerald"));
            Assert.That(OrderPanelText.PriceColumn(rows), Is.EqualTo("฿1,200\n฿45,900"));
        }

        [Test]
        public void Rows_ExactlyAtLimit_ShowsEveryGem()
        {
            var rows = OrderPanelText.Rows(OrderOf(OrderPanelText.MaxRows), NameOf);
            Assert.That(rows.Count, Is.EqualTo(OrderPanelText.MaxRows));
            Assert.That(rows[rows.Count - 1].Name, Is.EqualTo("Ruby"));
        }

        [Test]
        public void Rows_OneOverLimit_ReplacesLastRowWithMore()
        {
            var rows = OrderPanelText.Rows(OrderOf(OrderPanelText.MaxRows + 1), NameOf);
            Assert.That(rows.Count, Is.EqualTo(OrderPanelText.MaxRows));
            Assert.That(rows[rows.Count - 1].Name, Is.EqualTo("+2 more"));
        }

        [Test]
        public void Rows_FullOrderOf30_FitsAndKeepsTheTotal()
        {
            var order = OrderOf(Order.DefaultMaxGems);
            var rows = OrderPanelText.Rows(order, NameOf);
            Assert.That(rows.Count, Is.EqualTo(OrderPanelText.MaxRows));
            Assert.That(rows[rows.Count - 1].Name, Is.EqualTo("+23 more"));
            Assert.That(OrderPanelText.NameColumn(rows).Split('\n').Length, Is.EqualTo(OrderPanelText.MaxRows));
            Assert.That(OrderPanelText.PriceColumn(rows).Split('\n').Length, Is.EqualTo(OrderPanelText.MaxRows));
            Assert.That(OrderPanelText.TotalText(order), Is.EqualTo("Total ฿3,000"));
        }
    }
}
