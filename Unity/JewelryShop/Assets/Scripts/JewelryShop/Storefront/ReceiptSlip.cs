using TMPro;
using UnityEngine;

namespace JewelryShop.Storefront
{
    /// <summary>The Receipt slip (#91): a flat panel with text that slides out of the Card Terminal
    /// on each Approved Checkout, then hides after a few seconds or when the next Checkout begins.
    /// CheckoutBehaviour drives it. The panel's placement in the scene is its hidden spot (inside the
    /// terminal); it slides by <c>_slideOffset</c> (local) from there.</summary>
    public class ReceiptSlip : MonoBehaviour
    {
        [Tooltip("The flat slip; its scene position is where it starts, hidden in the Card Terminal.")]
        [SerializeField] private Transform _panel;
        [SerializeField] private TMP_Text _text;
        [Tooltip("Local-space move from the hidden spot to the shown spot.")]
        [SerializeField] private Vector3 _slideOffset = new Vector3(0f, -0.12f, 0f);
        [SerializeField] private float _slideSeconds = 0.5f;
        [SerializeField] private float _visibleSeconds = 5f;

        private Vector3 _hiddenLocalPos;
        private float _elapsed;
        private bool _showing;

        private void Awake()
        {
            if (_panel == null || _text == null)
            {
                Debug.LogWarning("ReceiptSlip: panel or text is unwired; no Receipt will show.", this);
                return;
            }
            _hiddenLocalPos = _panel.localPosition;
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Slides the slip out showing <paramref name="receipt"/>.</summary>
        public void Show(string receipt)
        {
            if (_panel == null || _text == null) return;
            _text.text = receipt;
            _elapsed = 0f;
            _showing = true;
            _panel.localPosition = _hiddenLocalPos;
            _panel.gameObject.SetActive(true);
        }

        /// <summary>Puts the slip away at once.</summary>
        public void Hide()
        {
            _showing = false;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_showing) return;
            _elapsed += Time.deltaTime;
            var t = _slideSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _slideSeconds);
            _panel.localPosition = _hiddenLocalPos + _slideOffset * (1f - (1f - t) * (1f - t));
            if (_elapsed >= _visibleSeconds) Hide();
        }
    }
}
