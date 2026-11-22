using System;

namespace JewelryShop.Core
{
    /// <summary>
    /// The Staff Call state machine for one end of the link. Customer: Idle, Calling, StaffComing,
    /// InCall. Staff: Idle, Incoming, Serving. The link goes through the Facade (ADR-0008) and the
    /// clock is injected, so tests drive it with a fake link and a fake clock.
    /// Call <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class StaffCallSession : IDisposable
    {
        /// <summary>How long a Customer waits for an answer before "not available".</summary>
        public const double AnswerTimeoutSeconds = 60;

        private readonly StaffCallRole _role;
        private readonly StaffLinkFacade _link;
        private readonly Func<double> _clock;
        private readonly ICheryllSpawner _cheryll;
        private double _deadlineSeconds;

        /// <param name="cheryll">Staff side only; null falls back to a no-op.</param>
        public StaffCallSession(
            StaffCallRole role, StaffLinkFacade link, Func<double> clock,
            ICheryllSpawner cheryll = null)
        {
            _role = role;
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _cheryll = cheryll ?? new NullCheryllSpawner();
            _link.MessageReceived += OnMessage;
            _link.ConnectionChanged += OnConnectionChanged;
        }

        /// <summary>Where this end of the call is right now.</summary>
        public StaffCallState State { get; private set; } = StaffCallState.Idle;

        /// <summary>
        /// Cheryll is serving this Customer (Staff coming until the call ends). She is visible
        /// from the link connecting, so this does not gate her renderer.
        /// </summary>
        public bool IsCheryllVisible =>
            State == StaffCallState.StaffComing || State == StaffCallState.InCall;

        /// <summary>Raised with the new state after every change.</summary>
        public event Action<StaffCallState> StateChanged;

        /// <summary>
        /// Customer side: no Staff Console, no answer in 60 s, or the link dropped mid-call.
        /// </summary>
        public event Action Unavailable;

        // Answered and not yet hung up: Cheryll is serving (or on her way) while this holds.
        private bool IsCallActive => State == StaffCallState.StaffComing
            || State == StaffCallState.InCall || State == StaffCallState.Serving;

        /// <summary>Stop listening to the link.</summary>
        public void Dispose()
        {
            _link.MessageReceived -= OnMessage;
            _link.ConnectionChanged -= OnConnectionChanged;
        }

        /// <summary>Customer: press Staff Call. Ignored unless Idle.</summary>
        public void Call()
        {
            if (_role != StaffCallRole.Customer || State != StaffCallState.Idle) return;
            if (!_link.IsConnected) { Unavailable?.Invoke(); return; }
            _link.Send(StaffCallMessage.Call);
            _deadlineSeconds = _clock() + AnswerTimeoutSeconds;
            SetState(StaffCallState.Calling);
        }

        /// <summary>Customer: cancel while Calling.</summary>
        public void Cancel()
        {
            if (_role != StaffCallRole.Customer || State != StaffCallState.Calling) return;
            _link.Send(StaffCallMessage.Cancel);
            SetState(StaffCallState.Idle);
        }

        /// <summary>Staff: answer the incoming call; Cheryll warps to the Staff Door.</summary>
        public void Answer()
        {
            if (_role != StaffCallRole.Staff || State != StaffCallState.Incoming) return;
            _link.Send(StaffCallMessage.Answer);
            _cheryll.WarpToDoor();
            SetState(StaffCallState.Serving);
        }

        /// <summary>
        /// Staff: Cheryll reached the Customer; the Customer moves to In call. Walking (#68) calls this.
        /// </summary>
        public void Arrive()
        {
            if (_role != StaffCallRole.Staff || State != StaffCallState.Serving) return;
            _link.Send(StaffCallMessage.Arrived);
        }

        /// <summary>Either side: end the call and Cheryll jumps back to the Staff Room.</summary>
        public void HangUp()
        {
            if (!IsCallActive) return;
            _link.Send(StaffCallMessage.HangUp);
            End();
        }

        /// <summary>Call every frame; times out an unanswered Staff Call.</summary>
        public void Tick()
        {
            if (State != StaffCallState.Calling || _clock() < _deadlineSeconds) return;
            _link.Send(StaffCallMessage.Cancel);
            SetState(StaffCallState.Idle);
            Unavailable?.Invoke();
        }

        private void OnMessage(StaffCallMessage message)
        {
            switch (message)
            {
                case StaffCallMessage.Call
                    when _role == StaffCallRole.Staff && State == StaffCallState.Idle:
                    SetState(StaffCallState.Incoming);
                    break;
                // Serving too: the Customer's Cancel can cross our Answer on the wire.
                case StaffCallMessage.Cancel when _role == StaffCallRole.Staff
                    && (State == StaffCallState.Incoming || State == StaffCallState.Serving):
                    End();
                    break;
                case StaffCallMessage.Answer when State == StaffCallState.Calling:
                    SetState(StaffCallState.StaffComing);
                    break;
                case StaffCallMessage.Arrived when State == StaffCallState.StaffComing:
                    SetState(StaffCallState.InCall);
                    break;
                case StaffCallMessage.HangUp when IsCallActive:
                    End();
                    break;
            }
        }

        private void OnConnectionChanged(bool isConnected)
        {
            if (isConnected || State == StaffCallState.Idle) return;
            var wasCustomer = _role == StaffCallRole.Customer;
            End();
            if (wasCustomer) Unavailable?.Invoke();
        }

        private void End()
        {
            if (_role == StaffCallRole.Staff) _cheryll.WarpToRoom();
            SetState(StaffCallState.Idle);
        }

        private void SetState(StaffCallState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
