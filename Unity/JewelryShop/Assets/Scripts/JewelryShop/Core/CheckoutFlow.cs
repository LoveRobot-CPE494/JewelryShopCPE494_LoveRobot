using System;

namespace JewelryShop.Core
{
    /// <summary>Where the Checkout stands.</summary>
    public enum CheckoutState { Idle, AwaitingTap, Processing, Approved }

    /// <summary>The Card Terminal's state machine: Idle, AwaitingTap, Processing, Approved, Idle.
    /// A tap counts only in AwaitingTap, so an Order is charged at most once. The caller feeds
    /// taps and time deltas; Declined arrives with ticket 002-02.</summary>
    public class CheckoutFlow
    {
        public const string IdleText = "Welcome";
        public const string ProcessingText = "Processing…";
        public const string ApprovedText = "Approved ✓";

        private readonly Order _order;
        private readonly IOrderSubmitter _submitter;
        private readonly Action _checkoutSucceeded;
        private readonly float _minProcessingSeconds;
        private readonly float _approvedSeconds;
        private float _elapsedSeconds;
        private SubmitReply? _reply;

        /// <summary>Checkout for <paramref name="order"/>; <paramref name="checkoutSucceeded"/> runs
        /// once on Approved and must empty the Order and lower the Stock Count.</summary>
        public CheckoutFlow(Order order, IOrderSubmitter submitter, Action checkoutSucceeded,
            float minProcessingSeconds = 1.5f, float approvedSeconds = 2f)
        {
            _order = order;
            _submitter = submitter;
            _checkoutSucceeded = checkoutSucceeded;
            _minProcessingSeconds = minProcessingSeconds;
            _approvedSeconds = approvedSeconds;
            _order.Changed += OnOrderChanged;
            Sync();
        }

        public CheckoutState State { get; private set; }

        /// <summary>Stop listening to the Order.</summary>
        public void Detach() => _order.Changed -= OnOrderChanged;

        /// <summary>The Card Terminal's screen text for the current state.</summary>
        public string ScreenText => State switch
        {
            CheckoutState.AwaitingTap => $"Total {OrderPanelText.FormatBaht(_order.TotalBaht)} — Tap card",
            CheckoutState.Processing => ProcessingText,
            CheckoutState.Approved => ApprovedText,
            _ => IdleText,
        };

        /// <summary>Raised whenever State or ScreenText may have changed.</summary>
        public event Action Changed;

        /// <summary>The Payment Card entered the tap zone while held.</summary>
        public void Tap()
        {
            if (State != CheckoutState.AwaitingTap) return;
            Enter(CheckoutState.Processing);
            _submitter.Submit(_order, r => _reply = r);
        }

        /// <summary>Advance Processing and Approved by <paramref name="deltaSeconds"/>.</summary>
        public void Tick(float deltaSeconds)
        {
            if (State == CheckoutState.Processing)
            {
                _elapsedSeconds += deltaSeconds;
                if (_elapsedSeconds < _minProcessingSeconds || _reply == null) return;
                if (_reply == SubmitReply.Accepted)
                {
                    Enter(CheckoutState.Approved);
                    _checkoutSucceeded();
                }
                else Sync(); // shortcut: Declined screen comes with 002-02; back to the tap screen for now
            }
            else if (State == CheckoutState.Approved)
            {
                _elapsedSeconds += deltaSeconds;
                if (_elapsedSeconds >= _approvedSeconds) Sync();
            }
        }

        private void OnOrderChanged()
        {
            if (State == CheckoutState.Idle || State == CheckoutState.AwaitingTap) Sync();
        }

        // Idle or AwaitingTap, from whether the Order holds a Gem.
        private void Sync() => Enter(_order.Lines.Count > 0 ? CheckoutState.AwaitingTap : CheckoutState.Idle);

        private void Enter(CheckoutState state)
        {
            State = state;
            _elapsedSeconds = 0f;
            _reply = null;
            Changed?.Invoke();
        }
    }
}
