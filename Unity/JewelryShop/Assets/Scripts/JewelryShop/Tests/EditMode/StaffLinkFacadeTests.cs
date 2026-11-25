using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class StaffLinkFacadeTests
    {
        private const double RetrySeconds = 2;
        private const double AttemptTimeoutSeconds = 4;

        private sealed class FakeLink : IStaffLink
        {
            public bool IsConnected { get; set; }
            public bool IsConnecting { get; set; }
            public int Connects;
            public int Disconnects;
            public string ServerAddress;
            public void SetServerAddress(string address) => ServerAddress = address;
            public void Connect() => Connects++;
            public void Disconnect() { Disconnects++; IsConnected = false; IsConnecting = false; }
            public event System.Action<StaffCallMessage> MessageReceived;
            public readonly List<StaffCallMessage> Sent = new List<StaffCallMessage>();
            public void Send(StaffCallMessage message) => Sent.Add(message);
            public void Receive(StaffCallMessage message) => MessageReceived?.Invoke(message);
        }

        [Test]
        public void Reconnect_SetsAddressBetweenDisconnectAndNextAttempt()
        {
            var link = new FakeLink { IsConnected = true };
            var facade = Wanted(link);
            facade.Tick(0);

            facade.Reconnect("10.0.0.7");

            Assert.That(link.Disconnects, Is.EqualTo(1));
            Assert.That(link.ServerAddress, Is.EqualTo("10.0.0.7"));
            var connectsBefore = link.Connects;
            facade.Tick(10);
            Assert.That(link.Connects, Is.EqualTo(connectsBefore + 1));
        }

        [Test]
        public void SendAndReceive_PassThroughTheLink()
        {
            var link = new FakeLink();
            var facade = Wanted(link);
            var got = new List<StaffCallMessage>();
            facade.MessageReceived += got.Add;

            facade.Send(StaffCallMessage.Call);
            link.Receive(StaffCallMessage.Answer);

            Assert.That(link.Sent, Is.EqualTo(new[] { StaffCallMessage.Call }));
            Assert.That(got, Is.EqualTo(new[] { StaffCallMessage.Answer }));
        }

        private static StaffLinkFacade Wanted(FakeLink link)
        {
            var facade = new StaffLinkFacade(link, RetrySeconds, AttemptTimeoutSeconds);
            facade.Connect();
            return facade;
        }

        [Test]
        public void Tick_LinkComesUpAndDrops_RaisesEachChangeOnce()
        {
            var link = new FakeLink();
            var facade = Wanted(link);
            var seen = new List<bool>();
            facade.ConnectionChanged += seen.Add;

            link.IsConnected = true;
            facade.Tick(0);
            facade.Tick(1);
            link.IsConnected = false;
            facade.Tick(2);

            Assert.That(seen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void Tick_NotConnected_RetriesOncePerInterval()
        {
            var link = new FakeLink();
            var facade = Wanted(link);

            facade.Tick(0);
            facade.Tick(1.9);
            Assert.That(link.Connects, Is.EqualTo(1));
            facade.Tick(2.0);
            Assert.That(link.Connects, Is.EqualTo(2));
        }

        [Test]
        public void Tick_Connected_DoesNotRetry()
        {
            var link = new FakeLink { IsConnected = true };
            var facade = Wanted(link);

            facade.Tick(0);
            facade.Tick(10);

            Assert.That(link.Connects, Is.EqualTo(0));
        }

        [Test]
        public void Tick_PeerRestarts_RetriesThenReconnects()
        {
            var link = new FakeLink { IsConnected = true };
            var facade = Wanted(link);
            var seen = new List<bool>();
            facade.ConnectionChanged += seen.Add;
            facade.Tick(0);

            link.IsConnected = false;     // peer stopped
            facade.Tick(1);
            Assert.That(link.Connects, Is.EqualTo(1), "first attempt right after the drop");
            facade.Tick(2.9);
            Assert.That(link.Connects, Is.EqualTo(1), "not before the interval");
            facade.Tick(3);
            Assert.That(link.Connects, Is.EqualTo(2));
            link.IsConnected = true;      // peer back
            facade.Tick(4);
            facade.Tick(10);

            Assert.That(link.Connects, Is.EqualTo(2));
            Assert.That(facade.IsConnected, Is.True);
            Assert.That(seen, Is.EqualTo(new[] { true, false, true }));
        }

        [Test]
        public void Tick_AttemptInFlight_IsNotRestarted()
        {
            var link = new FakeLink();
            var facade = Wanted(link);
            facade.Tick(0);
            link.IsConnecting = true; // handshake takes ~3 s, longer than the retry interval

            facade.Tick(2);
            facade.Tick(3);

            Assert.That(link.Connects, Is.EqualTo(1));
            Assert.That(link.Disconnects, Is.EqualTo(0));
        }

        [Test]
        public void Tick_AttemptStuckPastTimeout_AbortsThenRetries()
        {
            var link = new FakeLink();
            var facade = Wanted(link);
            facade.Tick(0);
            link.IsConnecting = true;

            facade.Tick(4);
            Assert.That(link.Disconnects, Is.EqualTo(1));
            facade.Tick(6);

            Assert.That(link.Connects, Is.EqualTo(2));
        }

        [Test]
        public void Tick_AttemptFails_RetriesAfterInterval()
        {
            var link = new FakeLink();
            var facade = Wanted(link);
            facade.Tick(0);
            link.IsConnecting = true;
            facade.Tick(1);
            link.IsConnecting = false; // attempt ended without connecting

            facade.Tick(2);

            Assert.That(link.Connects, Is.EqualTo(2));
        }

        [Test]
        public void Disconnect_StopsRetryingAndRaisesDisconnected()
        {
            var link = new FakeLink { IsConnected = true };
            var facade = Wanted(link);
            facade.Tick(0);
            var last = true;
            facade.ConnectionChanged += c => last = c;

            facade.Disconnect();
            facade.Tick(100);

            Assert.That(last, Is.False);
            Assert.That(link.Connects, Is.EqualTo(0));
        }
    }
}
