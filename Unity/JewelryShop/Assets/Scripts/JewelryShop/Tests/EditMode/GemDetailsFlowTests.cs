using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class GemDetailsFlowTests
    {
        private static GemDetails Ruby() =>
            new GemDetails(
                "Ruby", new GemKey(GemCut.Round, GemColor.Red), 1.2f, "VS1", "Myanmar", 45000);

        private static GemDetails Emerald() =>
            new GemDetails(
                "Emerald", new GemKey(GemCut.Emerald, GemColor.Green), 2f, "VVS2", "Colombia", 80000);

        private GemDetailsFlow _flow;
        private int _openedCount;
        private GemDetails _lastOpened;

        [SetUp]
        public void SetUp()
        {
            _flow = new GemDetailsFlow();
            _openedCount = 0;
            _flow.Opened += details => { _openedCount++; _lastOpened = details; };
        }

        [Test]
        public void APressed_NothingHeld_StaysClosedAndRaisesNothing()
        {
            _flow.APressed();
            Assert.IsFalse(_flow.IsOpen);
            Assert.AreEqual(0, _openedCount);
        }

        [Test]
        public void APressed_GemHeld_OpensForThatEntryOnce()
        {
            _flow.Grabbed(Ruby());
            _flow.APressed();
            Assert.IsTrue(_flow.IsOpen);
            Assert.AreEqual("Ruby", _flow.Shown.Name);
            Assert.AreEqual(1, _openedCount);
            Assert.AreEqual("Ruby", _lastOpened.Name);
        }

        [Test]
        public void APressed_WhenOpen_Closes()
        {
            _flow.Grabbed(Ruby());
            _flow.APressed();
            _flow.APressed();
            Assert.IsFalse(_flow.IsOpen);
            Assert.AreEqual(1, _openedCount);
        }

        [Test]
        public void Released_WhenOpen_Closes()
        {
            _flow.Grabbed(Ruby());
            _flow.APressed();
            _flow.Released();
            Assert.IsFalse(_flow.IsOpen);
        }

        [Test]
        public void APressed_AfterRelease_DoesNotOpen()
        {
            _flow.Grabbed(Ruby());
            _flow.Released();
            _flow.APressed();
            Assert.IsFalse(_flow.IsOpen);
        }

        [Test]
        public void Grabbed_SecondGem_StartsClosed()
        {
            _flow.Grabbed(Ruby());
            _flow.APressed();
            _flow.Grabbed(Emerald());
            Assert.IsFalse(_flow.IsOpen);
        }

        [Test]
        public void APressed_SecondGemHeld_ShowsThatEntryAndRaisesOpenedAgain()
        {
            _flow.Grabbed(Ruby());
            _flow.APressed();
            _flow.Released();
            _flow.Grabbed(Emerald());
            _flow.APressed();
            Assert.AreEqual("Emerald", _flow.Shown.Name);
            Assert.AreEqual(2, _openedCount);
            Assert.AreEqual("Emerald", _lastOpened.Name);
        }

        [Test]
        public void Lines_Always_HoldNameThenFactsThenPrice()
        {
            Assert.AreEqual(new[]
            {
                "Ruby", "Cut: Round", "Color: Red", "Carat: 1.2", "Clarity: VS1",
                "Origin: Myanmar", "฿45,000",
            }, Ruby().Lines());
        }
    }
}
