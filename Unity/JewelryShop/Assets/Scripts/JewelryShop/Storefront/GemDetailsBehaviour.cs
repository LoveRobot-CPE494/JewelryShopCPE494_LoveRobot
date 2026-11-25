using JewelryShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>Press A on the right Controller while holding a Gem Model to open the Gem
    /// Details Panel beside it (#98, ADR-0013). Glue between the XR interactor, the A action
    /// and <see cref="GemDetailsFlow"/>; the panel floats to the Customer's left of the Gem,
    /// faces the head and follows with damped smoothing.</summary>
    public class GemDetailsBehaviour : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractor _interactor;
        [Tooltip("Bind to <XRController>{RightHand}/primaryButton.")]
        [SerializeField] private InputActionReference _aButton;
        [Tooltip("World-space panel root, not this GameObject; toggled active. "
            + "Its TMP font asset must contain the baht sign (NotoSansThai SDF).")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _text;
        [Tooltip("Customer head; empty = Camera.main.")]
        [SerializeField] private Transform _head;
        [Tooltip("Metres to the Customer's left of the Gem.")]
        [SerializeField] private float _sideOffsetM = 0.15f;
        [Tooltip("Follow speed; higher is snappier.")]
        [SerializeField] private float _smoothing = 12f;

        private readonly GemDetailsFlow _flow = new GemDetailsFlow();
        private GemModel _gem;
        private bool _isPanelShown;

        /// <summary>The rules; subscribe to its Opened event (004-02, 004-03).</summary>
        public GemDetailsFlow Flow => _flow;

        private bool IsWired =>
            _interactor != null && _aButton != null && _aButton.action != null
            && _panel != null && _text != null;

        private void OnEnable()
        {
            if (!IsWired)
            {
                Debug.LogWarning(
                    "GemDetailsBehaviour: interactor, A action, panel or text is unwired; "
                    + "panel stays closed.", this);
                return;
            }
            _interactor.selectEntered.AddListener(OnGrabbed);
            _interactor.selectExited.AddListener(OnReleased);
            // The action is shared with the rig's input asset, so it is left enabled on disable.
            if (!_aButton.action.enabled) _aButton.action.Enable();
            _aButton.action.performed += OnA;
            _panel.SetActive(false);
            if (_interactor.hasSelection) Hold(_interactor.interactablesSelected[0]);
        }

        private void OnDisable()
        {
            if (!IsWired) return;
            _interactor.selectEntered.RemoveListener(OnGrabbed);
            _interactor.selectExited.RemoveListener(OnReleased);
            _aButton.action.performed -= OnA;
            LetGo();
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Hold(args.interactableObject);

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactableObject.transform.GetComponentInParent<GemModel>() == _gem) LetGo();
        }

        private void Hold(IXRSelectInteractable interactable)
        {
            var gem = interactable.transform.GetComponentInParent<GemModel>();
            if (gem == null) return;
            var entry = gem.Entry;
            if (entry == null)
            {
                Debug.LogWarning("GemDetailsBehaviour: held Gem Model has no catalog entry.", gem);
                return;
            }
            _gem = gem;
            _flow.Grabbed(new GemDetails(
                entry.GemName, entry.Key, entry.Carat, entry.Clarity, entry.Origin, entry.PriceBaht));
        }

        private void LetGo()
        {
            _gem = null;
            _flow.Released();
        }

        private void OnA(InputAction.CallbackContext _) => _flow.APressed();

        private void LateUpdate()
        {
            if (!IsWired) return;
            bool isOpen = _flow.IsOpen && _gem != null;
            var panelTransform = _panel.transform;
            if (isOpen && !_isPanelShown)
            {
                _text.text = string.Join("\n", _flow.Shown.Lines());
                _panel.SetActive(true);
                panelTransform.position = Target(); // snap, no fly-in
            }
            else if (!isOpen && _isPanelShown) _panel.SetActive(false);
            _isPanelShown = isOpen;
            if (!isOpen) return;

            float blend = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
            panelTransform.position = Vector3.Lerp(panelTransform.position, Target(), blend);
            var head = Head();
            var toPanel = head != null ? panelTransform.position - head.position : Vector3.zero;
            if (toPanel != Vector3.zero) panelTransform.rotation = Quaternion.LookRotation(toPanel);
        }

        private Transform Head() =>
            _head != null ? _head : (Camera.main != null ? Camera.main.transform : null);

        private Vector3 Target()
        {
            var head = Head();
            var left = head != null
                ? -Vector3.ProjectOnPlane(head.right, Vector3.up).normalized
                : Vector3.left;
            return _gem.transform.position + left * _sideOffsetM;
        }
    }
}
