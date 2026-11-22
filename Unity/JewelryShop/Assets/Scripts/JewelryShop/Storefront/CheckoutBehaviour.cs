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
        [Tooltip("Least seconds Processing… shows, even when the reply is instant.")]
        [SerializeField] private float _minProcessingSeconds = 1.5f;
        [Tooltip("Seconds Approved ✓ shows before the screen goes idle.")]
        [SerializeField] private float _approvedSeconds = 2f;

        private CheckoutFlow _flow;

        private void OnEnable()
        {
            if (_tray == null || _tapZone == null || _screen == null)
            {
                Debug.LogWarning("CheckoutBehaviour: tray, tap zone or screen is unwired; Checkout is off.", this);
                return;
            }
            _flow = new CheckoutFlow(_tray.Order, new LocalOrderSubmitter(), _tray.CheckoutSucceeded,
                _minProcessingSeconds, _approvedSeconds);
            _flow.Changed += ShowScreen;
            _tapZone.Tapped += _flow.Tap;
            ShowScreen();
        }

        private void OnDisable()
        {
            if (_flow == null) return;
            _flow.Changed -= ShowScreen;
            _tapZone.Tapped -= _flow.Tap;
            _flow.Detach();
            _flow = null;
        }

        private void Update() => _flow?.Tick(Time.deltaTime);

        private void ShowScreen() => _screen.text = _flow.ScreenText;
    }
}
