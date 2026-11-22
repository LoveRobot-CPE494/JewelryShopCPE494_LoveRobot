using JewelryShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace JewelryShop.Storefront
{
    /// <summary>Card Terminal feedback (brief 4a.ii): a spinner while Processing, Approved ✓ popping in,
    /// and a beep plus haptic pulse on each Approved Checkout. CheckoutBehaviour drives it; every
    /// unassigned part is skipped with one warning.</summary>
    public class CheckoutFeedback : MonoBehaviour
    {
        [Tooltip("Shown and spun only while Processing.")]
        [SerializeField] private Transform _spinner;
        [SerializeField] private float _spinDegreesPerSecond = 360f;
        [Tooltip("Scaled in on Approved ✓ (usually the screen text's Transform).")]
        [SerializeField] private Transform _approvedMark;
        [Tooltip("Faded in on Approved ✓ (usually the screen text).")]
        [SerializeField] private TMP_Text _approvedText;
        [SerializeField] private float _approvedAnimSeconds = 0.4f;
        [SerializeField] private AudioSource _audio;
        [Tooltip("Empty = a generated 880 Hz beep.")]
        [SerializeField] private AudioClip _beep;
        [SerializeField] private HapticImpulsePlayer _haptics;
        [SerializeField, Range(0f, 1f)] private float _hapticAmplitude = 0.5f;
        [SerializeField] private float _hapticSeconds = 0.15f;

        private CheckoutState? _state; // null until the first StateChanged, so Idle still hides the spinner
        private float _approvedElapsed;
        private bool _warned;
        private AudioClip _generatedBeep;

        /// <summary>The Checkout entered <paramref name="state"/> (or changed text within it).</summary>
        public void StateChanged(CheckoutState state)
        {
            if (state == _state) return;
            _state = state;
            _approvedElapsed = 0f;
            if (_spinner != null) _spinner.gameObject.SetActive(state == CheckoutState.Processing);
            if (state != CheckoutState.Approved) ApplyApproved(1f, 1f);
            else
            {
                var p = ApprovedAnimation.Progress(0f, _approvedAnimSeconds); // 0, or 1 when the duration is 0
                ApplyApproved(ApprovedAnimation.Scale(p), p);
            }
        }

        /// <summary>One Approved Checkout: beep and haptic pulse.</summary>
        public void PlayApproved()
        {
            if (_audio == null) Warn("audio source");
            else _audio.PlayOneShot(_beep != null ? _beep : _generatedBeep ??= MakeBeep());
            if (_haptics == null) Warn("haptic player");
            else _haptics.SendHapticImpulse(_hapticAmplitude, _hapticSeconds);
        }

        private void Update()
        {
            if (_state == CheckoutState.Processing && _spinner != null)
                _spinner.Rotate(0f, 0f, -_spinDegreesPerSecond * Time.deltaTime);
            else if (_state == CheckoutState.Approved && _approvedElapsed < _approvedAnimSeconds)
            {
                _approvedElapsed += Time.deltaTime;
                var p = ApprovedAnimation.Progress(_approvedElapsed, _approvedAnimSeconds);
                ApplyApproved(ApprovedAnimation.Scale(p), p);
            }
        }

        private void ApplyApproved(float scale, float alpha)
        {
            if (_approvedMark != null) _approvedMark.localScale = Vector3.one * scale;
            if (_approvedText != null) _approvedText.alpha = alpha;
        }

        private void Warn(string what)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"CheckoutFeedback: {what} is unwired; that part of the Approved feedback is skipped.", this);
        }

        private static AudioClip MakeBeep()
        {
            const int rate = 44100, length = rate / 7;
            var data = new float[length];
            for (var i = 0; i < length; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * 0.3f * (1f - (float)i / length);
            var clip = AudioClip.Create("GeneratedBeep", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
