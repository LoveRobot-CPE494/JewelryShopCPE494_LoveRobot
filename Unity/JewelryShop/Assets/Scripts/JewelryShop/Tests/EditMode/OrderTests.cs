using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class OrderTests
    {
        private static readonly GemKey RedRound = new GemKey(GemCut.Round, GemColor.Red);
        private static readonly GemKey BlueEmerald = new GemKey(GemCut.Emerald, GemColor.Blue);

        [Test]
        public void Add_NewGem_IsAddedAndListed()
        {
            var order = new Order();
            Assert.That(order.Add(RedRound, 100, 5), Is.EqualTo(Order.AddResult.Added));
            Assert.That(order.Lines.Count, Is.EqualTo(1));
            Assert.That(order.Lines[0].Key, Is.EqualTo(RedRound));
        }

        [Test]
        public void Add_Repeat_KeepsOneLinePerGem()
        {
            var order = new Order();
            order.Add(RedRound, 100, 5);
            order.Add(RedRound, 100, 5);
            Assert.That(order.Lines.Count, Is.EqualTo(2));
        }

        [Test]
        public void Add_AtDefaultMaximum_IsFullAndUnchanged()
        {
            var order = new Order();
            for (int i = 0; i < Order.DefaultMaxGems; i++)
                order.Add(RedRound, 1, 1000);
            Assert.That(order.Add(RedRound, 1, 1000), Is.EqualTo(Order.AddResult.Full));
            Assert.That(order.Lines.Count, Is.EqualTo(Order.DefaultMaxGems));
            Assert.That(order.TotalBaht, Is.EqualTo(30));
        }

        [Test]
        public void Add_AtChangedMaximum_IsFull()
        {
            var order = new Order(2);
            order.Add(RedRound, 1, 5);
            order.Add(BlueEmerald, 1, 5);
            Assert.That(order.Add(RedRound, 1, 5), Is.EqualTo(Order.AddResult.Full));
        }

        [Test]
        public void Add_AtStockCount_IsSoldOutAndUnchanged()
        {
            var order = new Order();
            order.Add(RedRound, 10, 2);
            order.Add(RedRound, 10, 2);
            Assert.That(order.Add(RedRound, 10, 2), Is.EqualTo(Order.AddResult.SoldOut));
            Assert.That(order.Lines.Count, Is.EqualTo(2));
            Assert.That(order.TotalBaht, Is.EqualTo(20));
        }

        [Test]
        public void Add_StockCountZero_IsSoldOut()
        {
            Assert.That(new Order().Add(RedRound, 10, 0), Is.EqualTo(Order.AddResult.SoldOut));
        }

        [Test]
        public void Add_SoldOutForOneKey_StillAllowsAnother()
        {
            var order = new Order();
            order.Add(RedRound, 10, 1);
            Assert.That(order.Add(BlueEmerald, 20, 1), Is.EqualTo(Order.AddResult.Added));
        }

        [Test]
        public void TotalBaht_SumsEveryLine()
        {
            var order = new Order();
            order.Add(RedRound, 100, 5);
            order.Add(BlueEmerald, 250, 5);
            order.Add(RedRound, 100, 5);
            Assert.That(order.TotalBaht, Is.EqualTo(450));
        }

        [Test]
        public void Changed_RaisedOnEverySuccessfulAdd()
        {
            var order = new Order();
            int raised = 0;
            order.Changed += () => raised++;
            order.Add(RedRound, 1, 5);
            order.Add(RedRound, 1, 5);
            Assert.That(raised, Is.EqualTo(2));
        }

        [Test]
        public void RemoveAt_ValidIndex_RemovesThatGemAndItsPrice()
        {
            var order = new Order();
            order.Add(RedRound, 100, 5);
            order.Add(BlueEmerald, 250, 5);
            Assert.That(order.RemoveAt(0), Is.True);
            Assert.That(order.Lines.Count, Is.EqualTo(1));
            Assert.That(order.Lines[0].Key, Is.EqualTo(BlueEmerald));
            Assert.That(order.TotalBaht, Is.EqualTo(250));
        }

        [Test]
        public void RemoveAt_OneOfTwoRepeats_KeepsTheOther()
        {
            var order = new Order();
            order.Add(RedRound, 100, 5);
            order.Add(RedRound, 100, 5);
            order.RemoveAt(1);
            Assert.That(order.Lines.Count, Is.EqualTo(1));
            Assert.That(order.Lines[0].Key, Is.EqualTo(RedRound));
            Assert.That(order.TotalBaht, Is.EqualTo(100));
        }

        [Test]
        public void RemoveAt_RaisesChanged()
        {
            var order = new Order();
            order.Add(RedRound, 1, 5);
            int raised = 0;
            order.Changed += () => raised++;
            order.RemoveAt(0);
            Assert.That(raised, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void RemoveAt_OutOfRange_IsNoOpWithoutEvent(int index)
        {
            var order = new Order();
            order.Add(RedRound, 1, 5);
            int raised = 0;
            order.Changed += () => raised++;
            Assert.That(order.RemoveAt(index), Is.False);
            Assert.That(order.Lines.Count, Is.EqualTo(1));
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void Changed_RaisedOnAddOnly()
        {
            var order = new Order(1);
            int raised = 0;
            order.Changed += () => raised++;
            order.Add(RedRound, 1, 5);
            order.Add(RedRound, 1, 5); // Full
            order.Add(BlueEmerald, 1, 0); // Sold out
            Assert.That(raised, Is.EqualTo(1));
        }
    }
}
