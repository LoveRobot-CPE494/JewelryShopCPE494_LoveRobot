using JewelryShop.Core;
using TMPro;
using UnityEngine;

namespace JewelryShop.Storefront
{
    /// <summary>Composition root of the Checkout (ADR-0008): builds the CheckoutFlow for the
    /// Order Tray's Order, wires the tap zone and the Card Terminal screen to it, and feeds
    /// it time. The stand-in submitter is the only one wired until the Hub link (#75).</summary>
    public class CheckoutBehaviour : MonoBehaviour
    {
        [SerializeField] private OrderTray _tray;
        [SerializeField] private CardTerminalZone _tapZone;
        [SerializeField] private TMP_Text _screen;
        [Tooltip("Optional: spinner, Approved animation, beep and haptic.")]
        [SerializeField] private CheckoutFeedback _feedback;
        [Tooltip("Least seconds Processing… shows, even when the reply is instant.")]
        [SerializeField] private float _minProcessingSeconds = 1.5f;
        [Tooltip("Seconds Approved ✓ shows before the screen goes idle.")]
        [SerializeField] private float _approvedSeconds = 2f;

        [Tooltip("Seconds the empty-Order hint and Declined show.")]
        [SerializeField] private float _noticeSeconds = 2f;
        [Tooltip("Stand-in submitter declines every Order (to try the Declined path). Changeable in play.")]
        [SerializeField] private bool _declineOrders;

        private CheckoutFlow _flow;
        private readonly LocalOrderSubmitter _submitter = new LocalOrderSubmitter();

        private void OnEnable()
        {
            if (_tray == null || _tapZone == null || _screen == null)
            {
                Debug.LogWarning("CheckoutBehaviour: tray, tap zone or screen is unwired; Checkout is off.", this);
                return;
            }
            _flow = new CheckoutFlow(_tray.Order, _submitter, _tray.CheckoutSucceeded,
                _minProcessingSeconds, _approvedSeconds, _noticeSeconds);
            _flow.Changed += ShowScreen;
            if (_feedback != null) _flow.Approved += _feedback.PlayApproved;
            _tapZone.Tapped += _flow.Tap;
            ShowScreen();
        }

        private void OnDisable()
        {
            if (_flow == null) return;
            _flow.Changed -= ShowScreen;
            if (_feedback != null) _flow.Approved -= _feedback.PlayApproved;
            _tapZone.Tapped -= _flow.Tap;
            _flow.Detach();
            _flow = null;
        }

        private void Update()
        {
            _submitter.Decline = _declineOrders;
            _flow?.Tick(Time.deltaTime);
        }

        private void ShowScreen()
        {
            _screen.text = _flow.ScreenText;
            if (_feedback != null) _feedback.StateChanged(_flow.State);
        }
    }
}
