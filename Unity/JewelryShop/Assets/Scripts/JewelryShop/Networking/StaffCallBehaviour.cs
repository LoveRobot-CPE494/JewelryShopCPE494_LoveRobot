using JewelryShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Scene host of the Staff Call session. Put one next to the StaffLinkBehaviour on each end;
    /// the role follows that link's role. Wire panel buttons to Call / Cancel / Answer / HangUp:
    /// the panels reach the session only through here, never Netcode.
    /// </summary>
    public sealed class StaffCallBehaviour : MonoBehaviour
    {
        [SerializeField] private StaffLinkBehaviour _link;
        [Tooltip("Optional; shows the Staff Call status line.")]
        [SerializeField] private TMP_Text _statusText;
        [Tooltip("Optional; same line for a legacy UI Text (the Staff Console's Incoming Call Prompt).")]
        [SerializeField] private Text _legacyStatusText;
        [Header("Optional buttons (enabled only in the state where they act)")]
        [SerializeField] private Button _callButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _answerButton;
        [SerializeField] private Button _hangUpButton;
        [Tooltip("Fires on every state change so a panel can show the right buttons.")]
        [SerializeField] private UnityEvent<StaffCallState> _onStateChanged;

        private StaffCallSession _session;

        /// <summary>The session, available from Start on (null before).</summary>
        public StaffCallSession Session => _session;

        private void Start()
        {
            if (_link == null)
            {
                Debug.LogError("StaffCallBehaviour: link not wired; Staff Call is disabled.", this);
                enabled = false;
                return;
            }
            if (_statusText == null)
                Debug.LogWarning(
                    "StaffCallBehaviour: status text not wired; state is only logged.", this);
            _session = new StaffCallSession(
                _link.IsHost ? StaffCallRole.Staff : StaffCallRole.Customer,
                _link.Facade, () => Time.realtimeSinceStartupAsDouble, _link.CheryllSpawner);
            _session.StateChanged += Show;
            _session.Unavailable += ShowUnavailable;
            Show(StaffCallState.Idle);
        }

        private void Update() => _session?.Tick();

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.StateChanged -= Show;
            _session.Unavailable -= ShowUnavailable;
            _session.Dispose();
        }

        /// <summary>Customer: press Staff Call.</summary>
        public void Call() => _session?.Call();
        /// <summary>Customer: cancel while calling.</summary>
        public void Cancel() => _session?.Cancel();
        /// <summary>Staff: answer the incoming Staff Call.</summary>
        public void Answer() => _session?.Answer();
        /// <summary>Either side: end the call.</summary>
        public void HangUp() => _session?.HangUp();

        private void Show(StaffCallState state)
        {
            SetText(state switch
            {
                StaffCallState.Calling => "Calling Staff...",
                StaffCallState.StaffComing => "Staff is coming",
                StaffCallState.Incoming => "Incoming Staff Call",
                StaffCallState.Serving => "Serving a Customer",
                _ => _link.IsHost ? "No Incoming Call" : "",
            });
            SetInteractable(_callButton, state == StaffCallState.Idle);
            SetInteractable(_cancelButton, state == StaffCallState.Calling);
            SetInteractable(_answerButton, state == StaffCallState.Incoming);
            SetInteractable(_hangUpButton, state is StaffCallState.StaffComing
                or StaffCallState.InCall or StaffCallState.Serving);
            _onStateChanged?.Invoke(state);
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        // Runs after Show(Idle), so the message stays on screen until the next state change.
        private void ShowUnavailable() => SetText("Staff is not available right now");

        private void SetText(string text)
        {
            if (_statusText != null) _statusText.text = text;
            if (_legacyStatusText != null) _legacyStatusText.text = text;
            if (text.Length > 0) Debug.Log($"Staff Call: {text}");
        }
    }
}
