using JewelryShop.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace JewelryShop.Storefront
{
    /// <summary>A Gem Model in the Display Case; flies home to it once released (#77).</summary>
    public class GemModel : MonoBehaviour
    {
        [SerializeField] private GemCatalogEntry _entry;
        [Tooltip("Seconds after release before the Gem Model flies home.")]
        [SerializeField] private float _returnDelaySeconds = 3f;
        [Tooltip("Seconds the fly home takes.")]
        [SerializeField] private float _flightDurationSeconds = 0.5f;

        private XRGrabInteractable _grab;
        private Rigidbody _body;
        private GemReturnTimer _timer;
        private Pose _home;
        private Pose _flightStart;
        private bool _isBodyKinematic;
        private bool _isGravityOn;
        private bool _isFlying;
        private bool _isBodyRestorePending;

        /// <summary>The catalog entry wired in the Inspector; null until wired.</summary>
        public GemCatalogEntry Entry => _entry;

        /// <summary>Cut + Color via the catalog entry; false (and a warning) while the
        /// entry is unwired.</summary>
        public bool TryGetKey(out GemKey key)
        {
            key = default;
            if (_entry == null)
            {
                Debug.LogWarning("GemModel: no catalog entry wired.", this);
                return false;
            }
            key = _entry.Key;
            return true;
        }

        /// <summary>Fly home now, with no delay (the Order Tray uses this).</summary>
        public void ReturnHomeNow()
        {
            if (_timer == null)
            {
                Debug.LogWarning("GemModel: ReturnHomeNow before Awake; ignored.", this);
                return;
            }
            _timer.ReturnNow();
            BeginFlight();
        }

        private void Awake()
        {
            _timer = new GemReturnTimer(_returnDelaySeconds, _flightDurationSeconds);
            _home = new Pose(transform.position, transform.rotation);
            _body = GetComponent<Rigidbody>();
            if (_body != null)
            {
                _isBodyKinematic = _body.isKinematic;
                _isGravityOn = _body.useGravity;
            }
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                Debug.LogWarning("GemModel: no XRGrabInteractable; it will never return.", this);
        }

        private void OnEnable()
        {
            if (_grab == null) return;
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (_grab != null)
            {
                _grab.selectEntered.RemoveListener(OnGrabbed);
                _grab.selectExited.RemoveListener(OnReleased);
            }
            _timer.Grab();
            if (_isFlying) EndFlight();
        }

        // Leave the body to XRI: it has already set up the held Rigidbody.
        private void OnGrabbed(SelectEnterEventArgs _)
        {
            _timer.Grab();
            _isFlying = false;
        }

        private void OnReleased(SelectExitEventArgs _)
        {
            _timer.Release();
            _isBodyRestorePending = true; // XRI may restore our saved mid-flight state
        }

        private void Update()
        {
            if (_isBodyRestorePending)
            {
                _isBodyRestorePending = false;
                if (!_isFlying) ApplyRestingBodyState(); // keeps the throw velocity
            }
            if (_timer.Phase == GemReturnTimer.PhaseKind.Idle) return;
            bool hasArrived = _timer.Tick(Time.deltaTime);
            if (_timer.Phase == GemReturnTimer.PhaseKind.Flying || hasArrived)
                Fly(hasArrived ? 1f : _timer.FlightProgress, hasArrived);
        }

        private void Fly(float progress, bool hasArrived)
        {
            BeginFlight();
            transform.SetPositionAndRotation(
                Vector3.Lerp(_flightStart.position, _home.position, progress),
                Quaternion.Slerp(_flightStart.rotation, _home.rotation, progress));
            if (hasArrived) EndFlight();
        }

        private void BeginFlight()
        {
            if (_isFlying) return;
            _isFlying = true;
            _flightStart = new Pose(transform.position, transform.rotation);
            if (_body != null) _body.isKinematic = true;
        }

        /// <summary>Back to the at-rest Rigidbody state with no leftover motion.</summary>
        private void EndFlight()
        {
            _isFlying = false;
            ApplyRestingBodyState();
            if (_body != null && !_isBodyKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyRestingBodyState()
        {
            if (_body == null) return;
            _body.isKinematic = _isBodyKinematic;
            _body.useGravity = _isGravityOn;
        }
    }
}
