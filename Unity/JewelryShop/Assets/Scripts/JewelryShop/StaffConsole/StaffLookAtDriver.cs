using JewelryShop.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JewelryShop.StaffConsole
{
    /// <summary>
    /// Key 8 toggles Look-At and F toggles Focus on the Customer Avatar: Cheryll's head
    /// follows the target (Customer, Interest Target or the Staff Console camera) within the
    /// config limits and the body turns the excess (ADR-0020). Head and body only; the
    /// rules live in <see cref="LookAt"/>.
    /// </summary>
    // Run after StaffConsoleMovement.LateUpdate moves the camera, so we read this frame's
    // camera direction, not last frame's (a one-frame lag shakes the head while walking).
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public sealed class StaffLookAtDriver : MonoBehaviour
    {
        private const string GestureTag = "Gesture";
        private const string JumpTag = "Jump";
        private const float TurnBlendSeconds = 0.08f;
        private const float MinimumTurnDegreesPerSecond = 6f;
        private const float FullTurnDegreesPerSecond = 60f;
        private const float MaxTurnStepWeight = 0.65f;
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_isGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int s_turnAmount = Animator.StringToHash("LookAtTurnAmount");
        private static readonly int s_turnInPlace =
            Animator.StringToHash("Base Layer.Turn In Place");
        private const string StatusName = "Look-At Status";
        private const string PrefsKey = "LookAt.On";

        [SerializeField] private StaffLookAtConfig _config;
        [SerializeField] private Transform _staffCamera;
        [Tooltip("Optional. Focus (F) looks at it while it is in range.")]
        [SerializeField] private Transform _customerAvatar;
        [Tooltip("Optional. Looked at when Focus has no Customer; for testing or a display.")]
        [SerializeField] private Transform _interestTarget;

        private sealed class PlayerPrefsStore : ILookAtStore
        {
            public bool? Get() => PlayerPrefs.HasKey(PrefsKey) ? PlayerPrefs.GetInt(PrefsKey) == 1 : null;
            public void Set(bool on)
            {
                PlayerPrefs.SetInt(PrefsKey, on ? 1 : 0);
                PlayerPrefs.Save(); // survive an Editor crash or a killed build
            }
        }

        private readonly ILookAtStore _store = new PlayerPrefsStore();
        private readonly LookAtHead _head = new LookAtHead();
        private readonly CameraAim _cameraAim = new CameraAim();
        private readonly LookAtFocus _focus = new LookAtFocus();
        private Animator _animator;
        private LookAtSettings _settings;
        private Transform _headBone;
        private Text _status;
        private bool _isOn;
        private bool _hasTurnAnimation;

        private void Awake()
        {
            if (_config == null)
                Debug.LogWarning("No Look-At Config assigned; using defaults.", this);
            _settings = _config != null ? _config.Settings : LookAtSettings.Defaults;

            if (_staffCamera == null && Camera.main != null)
                _staffCamera = Camera.main.transform;
            _animator = GetComponent<Animator>();
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (_staffCamera == null || _headBone == null)
            {
                Debug.LogWarning("StaffLookAtDriver needs a Staff Camera and a humanoid head bone; " +
                    "doing nothing.", this);
                enabled = false;
                return;
            }

            var statusObject = GameObject.Find(StatusName);
            _status = statusObject != null ? statusObject.GetComponent<Text>() : null;
            if (_status == null)
                Debug.LogWarning($"No Text named '{StatusName}'; Look-At still works.", this);
            _hasTurnAnimation = _animator.HasState(0, s_turnInPlace);
            _isOn = LookAt.IsOn(_store, _settings);
            ShowStatus();
        }

        private void OnDisable()
        {
            if (_hasTurnAnimation && _animator != null)
                _animator.SetFloat(s_turnAmount, 0f);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit8Key.wasPressedThisFrame)
                _isOn = LookAt.Toggle(_store, _settings);

            var hasCustomer = _customerAvatar != null;
            var distance = 0f;
            if (hasCustomer)
            {
                var offset = _customerAvatar.position - transform.position;
                distance = new Vector2(offset.x, offset.z).magnitude;
            }
            _focus.Tick(_isOn, keyboard.fKey.wasPressedThisFrame, hasCustomer, distance, _settings);
            ShowStatus();
        }

        // After the Animator has posed the head: turn the body past the limit, then the head.
        private void LateUpdate()
        {
            float rawYaw, rawPitch;
            var target = CurrentTarget();
            var aimed = target == LookAtTarget.Customer ? _customerAvatar : _interestTarget;
            // A target destroyed since Update falls back to the camera for this frame.
            if (target == LookAtTarget.Camera || aimed == null)
            {
                var toCamera = _staffCamera.position - _headBone.position;
                var to = transform.InverseTransformDirection(toCamera);
                var face = transform.InverseTransformDirection(_staffCamera.forward);
                (rawYaw, rawPitch) = _cameraAim.Aim(to.x, to.y, to.z, face.x, face.y, face.z,
                    _settings);
            }
            else
            {
                var to = transform.InverseTransformDirection(aimed.position - _headBone.position);
                (rawYaw, rawPitch) = LookAt.RawAngles(to.x, to.y, to.z);
            }

            // Body Follow yields the heading and pose to locomotion, jumps and gestures.
            var elapsedSeconds = Time.deltaTime;
            var stepDegrees = 0f;
            if (_isOn && _animator.GetBool(s_isGrounded) && !_animator.GetBool(s_isMoving) &&
                !IsPlayingTag(GestureTag) && !IsPlayingTag(JumpTag))
            {
                stepDegrees = LookAt.BodyStep(LookAt.ExcessYaw(rawYaw, _settings),
                    elapsedSeconds, _settings);
                transform.Rotate(0f, stepDegrees, 0f, Space.World);
                rawYaw -= stepDegrees;
            }
            AnimateTurn(elapsedSeconds > 0f ? stepDegrees / elapsedSeconds : 0f);

            var (yaw, pitch) = LookAt.Clamp(rawYaw, rawPitch, _settings);
            var pose = _head.Tick(elapsedSeconds, _isOn, yaw, pitch, _settings);
            if (pose.Weight <= 0f) return;

            var offset = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(-pose.Pitch, pose.Yaw, 0f), pose.Weight);
            var body = transform.rotation;
            _headBone.rotation = body * offset * Quaternion.Inverse(body) * _headBone.rotation;
        }

        private void AnimateTurn(float degreesPerSecond)
        {
            if (!_hasTurnAnimation) return;

            var amount = Mathf.Abs(degreesPerSecond) < MinimumTurnDegreesPerSecond ? 0f :
                Mathf.Clamp(degreesPerSecond / FullTurnDegreesPerSecond, -1f, 1f) *
                MaxTurnStepWeight;
            _animator.SetFloat(s_turnAmount, amount);
            var state = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) :
                _animator.GetCurrentAnimatorStateInfo(0);
            if (amount != 0f && state.fullPathHash != s_turnInPlace)
                _animator.CrossFadeInFixedTime(s_turnInPlace, TurnBlendSeconds, 0);
        }

        private bool IsPlayingTag(string tag) =>
            _animator.GetCurrentAnimatorStateInfo(0).IsTag(tag) ||
            _animator.GetNextAnimatorStateInfo(0).IsTag(tag);

        private LookAtTarget CurrentTarget() =>
            LookAt.PickTarget(_focus.InRange, _interestTarget != null);

        private void ShowStatus()
        {
            if (_status == null) return;
            _status.text = LookAt.StatusText(_isOn, _focus, _customerAvatar != null,
                CurrentTarget(), _interestTarget != null ? _interestTarget.name : "");
        }
    }
}
