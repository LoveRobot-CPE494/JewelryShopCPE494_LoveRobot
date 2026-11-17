using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>Simulator only: Left Alt + mouse/scroll turns the Gem Model the right Controller holds (#59).</summary>
    [DefaultExecutionOrder(-29992)] // before XRInteractionSimulator (-29991) so the aim freeze lands the same frame
    public class GemModelKeyRotator : MonoBehaviour
    {
        [SerializeField] private XRInteractionSimulator _simulator;
        [SerializeField] private NearFarInteractor _interactor;
        [SerializeField] private float _degreesPerPixel = 0.3f;
        [SerializeField] private float _degreesPerScrollUnit = 0.25f;

        private bool _wasHolding;
        private bool _savedPointAndClick;
        private bool _frozen;
        private Quaternion _baseRotation;

        /// <summary>True only in the Editor with an enabled XR Interaction Simulator; never on a Quest or in a build.</summary>
        public static bool IsSimulatorActive(bool isEditor, XRInteractionSimulator simulator) =>
            isEditor && simulator != null && simulator.isActiveAndEnabled;

        /// <summary>Mouse delta and scroll to a rotation in the attach point's parent frame.</summary>
        public static Quaternion Rotate(Quaternion current, Vector2 mouseDelta, float scroll,
            float degreesPerPixel, float degreesPerScrollUnit) =>
            Quaternion.Euler(mouseDelta.y * degreesPerPixel, -mouseDelta.x * degreesPerPixel,
                scroll * degreesPerScrollUnit) * current;

        /// <summary>Applies one frame of input to the attach point; returns true while the aim must stay frozen.</summary>
        public bool Step(Transform attach, bool holding, bool altHeld, Vector2 mouseDelta, float scroll)
        {
            if (holding && !_wasHolding) _baseRotation = attach.localRotation;
            else if (!holding && _wasHolding) attach.localRotation = _baseRotation;
            _wasHolding = holding;

            if (!holding || !altHeld) return false;
            attach.localRotation = Rotate(attach.localRotation, mouseDelta, scroll, _degreesPerPixel, _degreesPerScrollUnit);
            return true;
        }

        private void Awake()
        {
            if (_interactor == null) _interactor = GetComponentInChildren<NearFarInteractor>();
            if (_simulator == null) _simulator = FindAnyObjectByType<XRInteractionSimulator>();
            if (_interactor == null) Debug.LogWarning("GemModelKeyRotator: no NearFarInteractor under this Controller.", this);
        }

        private void Update()
        {
            // Deliberate exception to "input through XRI": the Simulator has no Alt or roll action, and this
            // path never runs on a Quest (see the guard below).
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (_interactor == null || _interactor.attachTransform == null || keyboard == null || mouse == null
                || !IsSimulatorActive(Application.isEditor, _simulator))
                return;

            var freeze = Step(_interactor.attachTransform, _interactor.hasSelection, keyboard.leftAltKey.isPressed,
                mouse.delta.ReadValue(), mouse.scroll.ReadValue().y);

            // Point And Click re-aims every frame; switching it off holds the aim. The grab state is untouched.
            if (freeze && !_frozen) { _savedPointAndClick = _simulator.usePointAndClick; _simulator.usePointAndClick = false; }
            else if (!freeze && _frozen) _simulator.usePointAndClick = _savedPointAndClick;
            _frozen = freeze;
        }

        private void OnDisable()
        {
            if (_frozen && _simulator != null) _simulator.usePointAndClick = _savedPointAndClick;
            _frozen = false;
        }
    }
}
