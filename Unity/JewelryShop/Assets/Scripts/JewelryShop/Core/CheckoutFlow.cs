using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>Where the Checkout stands.</summary>
    public enum CheckoutState { Idle, AwaitingTap, Processing, Approved, Declined }

    /// <summary>The Card Terminal's state machine: Idle, AwaitingTap, Processing, Approved, Idle; a Declined reply
    /// returns to AwaitingTap. A tap counts only in AwaitingTap (in Idle it shows a hint), so an
    /// Order is charged at most once. The caller feeds taps and time deltas.</summary>
    public class CheckoutFlow
    {
        public const string IdleText = "Welcome";
        public const string ProcessingText = "Processing…";
        public const string ApprovedText = "Approved ✓";
        public const string EmptyOrderHintText = "Add a Gem first";
        public const string DeclinedText = "Declined — terminal offline, try again";

        private readonly Order _order;
        private readonly IOrderSubmitter _submitter;
        private readonly Action _checkoutSucceeded;
        private readonly float _minProcessingSeconds;
        private readonly float _approvedSeconds;
        private readonly float _noticeSeconds;
        private float _elapsedSeconds;
        private float _hintLeft;
        private SubmitReply? _reply;

        /// <summary>Checkout for <paramref name="order"/>; <paramref name="checkoutSucceeded"/> runs
        /// once on Approved and must empty the Order and lower the Stock Count.</summary>
        public CheckoutFlow(Order order, IOrderSubmitter submitter, Action checkoutSucceeded,
            float minProcessingSeconds = 1.5f, float approvedSeconds = 2f, float noticeSeconds = 2f)
        {
            _noticeSeconds = noticeSeconds;
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
            CheckoutState.Declined => DeclinedText,
            _ => _hintLeft > 0f ? EmptyOrderHintText : IdleText,
        };

        /// <summary>Raised whenever State or ScreenText may have changed.</summary>
        public event Action Changed;

        /// <summary>Raised once per Checkout, on entering Approved after the Order is emptied; never on Declined.
        /// Carries the Gems as they were at approval.</summary>
        public event Action<IReadOnlyList<OrderLine>> Approved;

        /// <summary>The Payment Card entered the tap zone while held.</summary>
        public void Tap()
        {
            if (State == CheckoutState.Idle)
            {
                _hintLeft = _noticeSeconds;
                Changed?.Invoke();
                return;
            }
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
                    var sold = new List<OrderLine>(_order.Lines); // the Order empties in _checkoutSucceeded
                    Enter(CheckoutState.Approved);
                    _checkoutSucceeded();
                    Approved?.Invoke(sold);
                }
                else Enter(CheckoutState.Declined);
            }
            else if (State == CheckoutState.Approved)
            {
                _elapsedSeconds += deltaSeconds;
                if (_elapsedSeconds >= _approvedSeconds) Sync();
            }
            else if (State == CheckoutState.Declined)
            {
                _elapsedSeconds += deltaSeconds;
                if (_elapsedSeconds >= _noticeSeconds) Sync();
            }
            else if (_hintLeft > 0f)
            {
                _hintLeft -= deltaSeconds;
                if (_hintLeft <= 0f) Changed?.Invoke();
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
            _hintLeft = 0f;
            Changed?.Invoke();
        }
    }
}
