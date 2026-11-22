using System;
using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class StaffCallSessionTests
    {
        private sealed class FakeLink : IStaffLink
        {
            public bool IsConnected { get; set; } = true;
            public bool IsConnecting => false;
            public readonly List<StaffCallMessage> Sent = new List<StaffCallMessage>();
            public event Action<StaffCallMessage> MessageReceived;
            public void Connect() { }
            public void Disconnect() { IsConnected = false; }
            public void Send(StaffCallMessage message) => Sent.Add(message);
            public void Receive(StaffCallMessage message) => MessageReceived?.Invoke(message);
        }

        private sealed class FakeSpawner : ICheryllSpawner
        {
            public int Spawns, Despawns;
            public void Spawn() => Spawns++;
            public void Despawn() => Despawns++;
        }

        // One end of the call: its link, facade, clock, spawner and session.
        private sealed class End
        {
            public readonly FakeLink Link = new FakeLink();
            public readonly StaffLinkFacade Facade;
            public readonly FakeSpawner Spawner = new FakeSpawner();
            public readonly StaffCallSession Session;
            public double Now;
            public int Unavailables;

            public End(StaffCallRole role, bool connected = true)
            {
                Link.IsConnected = connected;
                Facade = new StaffLinkFacade(Link, 2, 4);
                Facade.Tick(0); // takes the link state
                Session = new StaffCallSession(role, Facade, () => Now, Spawner);
                Session.Unavailable += () => Unavailables++;
            }

            public void DropLink()
            {
                Link.IsConnected = false;
                Facade.Tick(Now);
            }
        }

        private static End Customer(bool connected = true) =>
            new End(StaffCallRole.Customer, connected);
        private static End Staff() => new End(StaffCallRole.Staff);

        // Deliver what `from` has sent since sentCount to `to`, as the wire would.
        private static void Flush(End from, End to, ref int sentCount)
        {
            for (; sentCount < from.Link.Sent.Count; sentCount++)
                to.Link.Receive(from.Link.Sent[sentCount]);
        }

        [Test]
        public void SuccessfulCall_CustomerAndStaffWalkEveryState()
        {
            var customer = Customer();
            var staff = Staff();
            int c = 0, s = 0;

            customer.Session.Call();
            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Calling));
            Flush(customer, staff, ref c);
            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Incoming));

            staff.Session.Answer();
            Flush(staff, customer, ref s);
            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Serving));
            Assert.That(staff.Spawner.Spawns, Is.EqualTo(1));
            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.StaffComing));
            Assert.That(customer.Session.IsCheryllVisible, Is.True);

            staff.Session.Arrive();
            Flush(staff, customer, ref s);
            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.InCall));
            Assert.That(customer.Session.IsCheryllVisible, Is.True);

            customer.Session.HangUp();
            Flush(customer, staff, ref c);
            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Session.IsCheryllVisible, Is.False);
            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(staff.Spawner.Despawns, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_FromCalling_ReturnsBothToIdle()
        {
            var customer = Customer();
            var staff = Staff();
            int c = 0;
            customer.Session.Call();
            Flush(customer, staff, ref c);

            customer.Session.Cancel();
            Flush(customer, staff, ref c);

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Unavailables, Is.EqualTo(0));
        }

        [Test]
        public void Call_NoStaffConsole_IsUnavailableAndStaysIdle()
        {
            var customer = Customer(connected: false);

            customer.Session.Call();

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Unavailables, Is.EqualTo(1));
            Assert.That(customer.Link.Sent, Is.Empty);
        }

        [Test]
        public void Tick_NoAnswerBefore60Seconds_KeepsCalling()
        {
            var customer = Customer();
            customer.Session.Call();

            customer.Now = 59.9;
            customer.Session.Tick();

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Calling));
        }

        // The Customer's Cancel (or timeout) crossed the Staff's Answer on the wire.
        [Test]
        public void Cancel_ArrivesAfterStaffAnswered_StaffGoesIdleAndCheryllDespawns()
        {
            var staff = Staff();
            staff.Link.Receive(StaffCallMessage.Call);
            staff.Session.Answer();

            staff.Link.Receive(StaffCallMessage.Cancel);

            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(staff.Spawner.Despawns, Is.EqualTo(1));
        }

        [Test]
        public void Answer_ArrivesAfterCustomerCancelled_CustomerStaysIdle()
        {
            var customer = Customer();
            customer.Session.Call();
            customer.Session.Cancel();

            customer.Link.Receive(StaffCallMessage.Answer);

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Session.IsCheryllVisible, Is.False);
        }

        [Test]
        public void Tick_ExactlyAt60Seconds_TimesOutUnavailableAndCancelsTheCall()
        {
            var customer = Customer();
            var staff = Staff();
            int c = 0;
            customer.Session.Call();
            Flush(customer, staff, ref c);

            customer.Now = 60;
            customer.Session.Tick();
            Flush(customer, staff, ref c);

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Unavailables, Is.EqualTo(1));
            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
        }

        [Test]
        public void HangUp_FromStaff_ReturnsCustomerToIdleAndCheryllDisappears()
        {
            var customer = Customer();
            var staff = Staff();
            int c = 0, s = 0;
            customer.Session.Call();
            Flush(customer, staff, ref c);
            staff.Session.Answer();
            Flush(staff, customer, ref s);

            staff.Session.HangUp();
            Flush(staff, customer, ref s);

            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(staff.Spawner.Despawns, Is.EqualTo(1));
            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Session.IsCheryllVisible, Is.False);
        }

        [Test]
        public void LinkLost_CustomerMidCall_IdleAtOnceWithUnavailable()
        {
            var customer = Customer();
            customer.Session.Call();
            customer.Link.Receive(StaffCallMessage.Answer);

            customer.DropLink();

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Session.IsCheryllVisible, Is.False);
            Assert.That(customer.Unavailables, Is.EqualTo(1));
        }

        [Test]
        public void LinkLost_StaffServing_DespawnsCheryll()
        {
            var staff = Staff();
            staff.Link.Receive(StaffCallMessage.Call);
            staff.Session.Answer();

            staff.DropLink();

            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(staff.Spawner.Despawns, Is.EqualTo(1));
        }

        [Test]
        public void LinkLost_WhileCalling_IsUnavailable()
        {
            var customer = Customer();
            customer.Session.Call();

            customer.DropLink();

            Assert.That(customer.Session.State, Is.EqualTo(StaffCallState.Idle));
            Assert.That(customer.Unavailables, Is.EqualTo(1));
        }

        [Test]
        public void Call_WhileActive_IsIgnored()
        {
            var customer = Customer();
            customer.Session.Call();

            customer.Session.Call();

            Assert.That(customer.Link.Sent, Is.EqualTo(new[] { StaffCallMessage.Call }));
        }

        [Test]
        public void IncomingCall_WhileStaffBusy_IsIgnored()
        {
            var staff = Staff();
            staff.Link.Receive(StaffCallMessage.Call);
            staff.Session.Answer();

            staff.Link.Receive(StaffCallMessage.Call);

            Assert.That(staff.Session.State, Is.EqualTo(StaffCallState.Serving));
        }
    }
}
