using System;
using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class CheckoutFlowTests
    {
        private static readonly GemKey RedRound = new GemKey(GemCut.Round, GemColor.Red);

        private class FakeSubmitter : IOrderSubmitter
        {
            public int Submitted;
            public Order LastOrder;
            public Action<SubmitReply> Reply;
            public void Submit(Order order, Action<SubmitReply> onReply)
            {
                Submitted++;
                LastOrder = order;
                Reply = onReply;
            }
        }

        private Order _order;
        private FakeSubmitter _submitter;
        private Dictionary<GemKey, int> _stock;
        private CheckoutFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _order = new Order();
            _submitter = new FakeSubmitter();
            _stock = new Dictionary<GemKey, int> { { RedRound, 3 } };
            _flow = new CheckoutFlow(_order, _submitter, () => _order.CheckoutSucceeded(_stock),
                minProcessingSeconds: 1.5f, approvedSeconds: 2f);
        }

        private void AddGem() => _order.Add(RedRound, 100, 3);

        private void TapToProcessing() { AddGem(); _flow.Tap(); }

        [Test]
        public void EmptyOrder_IsIdle()
        {
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Idle));
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.IdleText));
        }

        [Test]
        public void GemAdded_AwaitsTapWithTotal_AndTracksChanges()
        {
            AddGem();
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.AwaitingTap));
            Assert.That(_flow.ScreenText, Is.EqualTo("Total ฿100 — Tap card"));
            AddGem();
            Assert.That(_flow.ScreenText, Is.EqualTo("Total ฿200 — Tap card"));
        }

        [Test]
        public void LastGemRemoved_BackToIdle()
        {
            AddGem();
            _order.RemoveAt(0);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Idle));
        }

        [Test]
        public void Tap_WhileIdle_DoesNothing()
        {
            _flow.Tap();
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Idle));
            Assert.That(_submitter.Submitted, Is.EqualTo(0));
        }

        [Test]
        public void Tap_WhileAwaitingTap_SubmitsOnceAndProcesses()
        {
            TapToProcessing();
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Processing));
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.ProcessingText));
            Assert.That(_submitter.Submitted, Is.EqualTo(1));
            Assert.That(_submitter.LastOrder, Is.SameAs(_order));
        }

        [Test]
        public void SecondTap_InProcessingOrApproved_SubmitsNothing()
        {
            TapToProcessing();
            _flow.Tap();
            _submitter.Reply(SubmitReply.Accepted);
            _flow.Tick(1.5f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Approved));
            _flow.Tap();
            Assert.That(_submitter.Submitted, Is.EqualTo(1));
        }

        [Test]
        public void Accepted_ApprovesCheckoutSucceeded_ThenIdle()
        {
            TapToProcessing();
            _submitter.Reply(SubmitReply.Accepted);
            _flow.Tick(1.5f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Approved));
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.ApprovedText));
            Assert.That(_order.Lines.Count, Is.EqualTo(0));
            Assert.That(_stock[RedRound], Is.EqualTo(2));
            _flow.Tick(2f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Idle));
        }

        [Test]
        public void Processing_NeverEndsBeforeMinimum_EvenWithInstantReply()
        {
            TapToProcessing();
            _submitter.Reply(SubmitReply.Accepted);
            _flow.Tick(1.4f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Processing));
            _flow.Tick(0.1f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Approved));
        }

        [Test]
        public void Processing_WaitsForReply_PastMinimum()
        {
            TapToProcessing();
            _flow.Tick(10f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Processing));
        }

        [Test]
        public void Tap_WhileIdle_ShowsHintBriefly_SubmitsNothing()
        {
            _flow.Tap();
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.EmptyOrderHintText));
            Assert.That(_submitter.Submitted, Is.EqualTo(0));
            _flow.Tick(2f);
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.IdleText));
        }

        [Test]
        public void Declined_ShowsText_ThenAwaitingTap_OrderAndStockUnchanged()
        {
            TapToProcessing();
            _submitter.Reply(SubmitReply.Declined);
            _flow.Tick(1.5f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Declined));
            Assert.That(_flow.ScreenText, Is.EqualTo(CheckoutFlow.DeclinedText));
            _flow.Tick(2f);
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.AwaitingTap));
            Assert.That(_order.Lines.Count, Is.EqualTo(1));
            Assert.That(_stock[RedRound], Is.EqualTo(3));
        }

        [Test]
        public void RetryTap_AfterDecline_SubmitsAgain()
        {
            TapToProcessing();
            _submitter.Reply(SubmitReply.Declined);
            _flow.Tick(1.5f);
            _flow.Tick(2f);
            _flow.Tap();
            Assert.That(_flow.State, Is.EqualTo(CheckoutState.Processing));
            Assert.That(_submitter.Submitted, Is.EqualTo(2));
        }

        [Test]
        public void StandIn_DeclinesWhenSwitched()
        {
            var reply = (SubmitReply?)null;
            new LocalOrderSubmitter { Decline = true }.Submit(_order, r => reply = r);
            Assert.That(reply, Is.EqualTo(SubmitReply.Declined));
        }

        [Test]
        public void StandIn_AcceptsEveryOrder()
        {
            var reply = (SubmitReply?)null;
            new LocalOrderSubmitter().Submit(_order, r => reply = r);
            Assert.That(reply, Is.EqualTo(SubmitReply.Accepted));
        }
    }
}
