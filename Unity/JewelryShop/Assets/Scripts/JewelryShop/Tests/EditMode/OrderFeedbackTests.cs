using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class OrderFeedbackTests
    {
        [Test]
        public void MessageFor_Added_IsNull() =>
            Assert.That(OrderFeedback.MessageFor(Order.AddResult.Added), Is.Null);

        [Test]
        public void MessageFor_Full_IsOrderIsFull() =>
            Assert.That(OrderFeedback.MessageFor(Order.AddResult.Full), Is.EqualTo("Order is full"));

        [Test]
        public void MessageFor_SoldOut_IsSoldOut() =>
            Assert.That(OrderFeedback.MessageFor(Order.AddResult.SoldOut), Is.EqualTo("Sold out"));

        [TestCase(0, true)]
        [TestCase(-1, true)]
        [TestCase(1, false)]
        [TestCase(5, false)]
        public void ShowsSoldOutTag_OnlyWhenStockCountIsZero(int stock, bool expected) =>
            Assert.That(OrderFeedback.ShowsSoldOutTag(stock), Is.EqualTo(expected));
    }
}
