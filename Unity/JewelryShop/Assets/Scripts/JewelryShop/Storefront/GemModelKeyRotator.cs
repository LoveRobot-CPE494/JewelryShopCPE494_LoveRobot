using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>Simulator only: Left Alt + mouse/scroll turns the Gem Model the right Controller
    /// holds (#59) and the Controller visual with it (#61).</summary>
    [DefaultExecutionOrder(-29992)] // before XRInteractionSimulator (-29991) so the aim freeze lands the same frame
    public class GemModelKeyRotator : MonoBehaviour
    {
        [SerializeField] private XRInteractionSimulator _simulator;
        [SerializeField] private NearFarInteractor _interactor;
        [Tooltip("Right Controller Visual: turns with the Gem Model. Empty = Gem Model only.")]
        [SerializeField] private Transform _controllerVisual;
        [SerializeField] private float _degreesPerPixel = 0.3f;
        [SerializeField] private float _degreesPerScrollUnit = 0.25f;

        private bool _wasHolding;
        private bool _savedPointAndClick;
        private bool _frozen;
        private Quaternion _baseRotation;
        private Quaternion _visualBaseRotation;

        /// <summary>True only in the Editor with an enabled XR Interaction Simulator; never on a Quest or in a build.</summary>
        public static bool IsSimulatorActive(bool isEditor, XRInteractionSimulator simulator) =>
            isEditor && simulator != null && simulator.isActiveAndEnabled;

        /// <summary>Mouse delta and scroll to a rotation in the attach point's parent frame.</summary>
        public static Quaternion Rotate(Quaternion current, Vector2 mouseDelta, float scroll,
            float degreesPerPixel, float degreesPerScrollUnit) =>
            Quaternion.Euler(mouseDelta.y * degreesPerPixel, -mouseDelta.x * degreesPerPixel,
                scroll * degreesPerScrollUnit) * current;

        /// <summary>Applies one frame of input to the attach point and the optional Controller
        /// visual; returns true while the aim must stay frozen.</summary>
        public bool Step(Transform attach, bool holding, bool altHeld, Vector2 mouseDelta, float scroll,
            Transform visual = null)
        {
            if (holding && !_wasHolding)
            {
                _baseRotation = attach.localRotation;
                if (visual != null) _visualBaseRotation = visual.localRotation;
            }
            else if (!holding && _wasHolding) ReleaseHold(attach, visual);
            _wasHolding = holding;

            if (!holding || !altHeld) return false;
            Turn(attach, mouseDelta, scroll);
            if (visual != null) Turn(visual, mouseDelta, scroll);
            return true;
        }

        /// <summary>Puts the attach point and visual back to their grab-time rotations, so the
        /// Controller model never stays turned once the hold ends.</summary>
        public void ReleaseHold(Transform attach, Transform visual)
        {
            if (!_wasHolding) return;
            if (attach != null) attach.localRotation = _baseRotation;
            if (visual != null) visual.localRotation = _visualBaseRotation;
            _wasHolding = false;
        }

        private void Turn(Transform t, Vector2 mouseDelta, float scroll) =>
            t.localRotation = Rotate(t.localRotation, mouseDelta, scroll, _degreesPerPixel, _degreesPerScrollUnit);

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
                mouse.delta.ReadValue(), mouse.scroll.ReadValue().y, _controllerVisual);

            // Point And Click re-aims every frame; switching it off holds the aim. The grab state is untouched.
            if (freeze && !_frozen) { _savedPointAndClick = _simulator.usePointAndClick; _simulator.usePointAndClick = false; }
            else if (!freeze && _frozen) _simulator.usePointAndClick = _savedPointAndClick;
            _frozen = freeze;
        }

        private void OnDisable()
        {
            if (_frozen && _simulator != null) _simulator.usePointAndClick = _savedPointAndClick;
            _frozen = false;
            ReleaseHold(_interactor != null ? _interactor.attachTransform : null, _controllerVisual);
        }
    }
}
