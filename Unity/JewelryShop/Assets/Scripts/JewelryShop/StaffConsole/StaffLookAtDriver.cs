using JewelryShop.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UniVRM10;

namespace JewelryShop.StaffConsole
{
    /// <summary>
    /// Key 8 toggles Camera Look-At, F toggles Focus, and R resets heading (ADR-0020).
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
        private const float FocusRayMeters = 20f;
        private static readonly RaycastHit[] s_focusHits = new RaycastHit[16];
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
        [Tooltip("Optional. Focus (F) looks at this when no Customer is in range.")]
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

        [Tooltip("Cheryll's VRM; its look-at turns the eyes. Empty: found on this object.")]
        [SerializeField] private Vrm10Instance _avatar;

        private readonly ILookAtStore _store = new PlayerPrefsStore();
        private readonly LookAtHead _head = new LookAtHead();
        private readonly CameraAim _cameraAim = new CameraAim();
        private readonly LookAtFocus _focus = new LookAtFocus();
        private readonly GestureLook _gesture = new GestureLook();
        private Animator _animator;
        private StaffConsoleMovement _movement;
        private LookAtSettings _settings;
        private Transform _headBone;
        private Text _status;
        private bool _isOn;
        private bool _hasTurnAnimation;
        private Transform _selectedTarget;
        private Vector3 _selectedPoint;

        private Transform InterestTarget => _interestTarget != null ? _interestTarget :
            _selectedTarget;
        private bool HasFocus => _movement != null ? _movement.HasFocus : Application.isFocused;

        private void Reset()
        {
            _avatar = GetComponent<Vrm10Instance>();
        }

        private void Awake()
        {
            if (_config == null)
                Debug.LogWarning("No Look-At Config assigned; using defaults.", this);
            _settings = _config != null ? _config.Settings : LookAtSettings.Defaults;

            if (_staffCamera == null && Camera.main != null)
                _staffCamera = Camera.main.transform;
            _animator = GetComponent<Animator>();
            _movement = GetComponent<StaffConsoleMovement>();
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (_staffCamera == null || _headBone == null)
            {
                Debug.LogWarning("StaffLookAtDriver needs a Staff Camera and a humanoid head bone; " +
                    "doing nothing.", this);
                enabled = false;
                return;
            }

            if (_avatar == null) _avatar = GetComponent<Vrm10Instance>();
            if (_avatar == null)
                Debug.LogWarning("No Vrm10Instance; the head follows but the eyes stay put.", this);
            else // we feed the eyes yaw and pitch ourselves
                _avatar.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.YawPitchValue;

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
            if (keyboard == null || !HasFocus) return;
            var isLookTogglePressed = keyboard.digit8Key.wasPressedThisFrame;
            var isFocusTogglePressed = keyboard.fKey.wasPressedThisFrame;
            if (isLookTogglePressed)
                _isOn = LookAt.Toggle(_store, _settings);

            var hasCustomer = _customerAvatar != null;
            var distance = 0f;
            if (hasCustomer)
            {
                var offset = _customerAvatar.position - transform.position;
                distance = new Vector2(offset.x, offset.z).magnitude;
            }
            var previousTarget = CurrentTarget();
            _focus.Tick(isFocusTogglePressed, hasCustomer, distance, _settings);
            if (isFocusTogglePressed)
            {
                _selectedTarget = null;
                if (_focus.IsOn && _interestTarget == null)
                    SelectInterestPoint();
            }
            if (isLookTogglePressed || isFocusTogglePressed || previousTarget != CurrentTarget() ||
                (Mouse.current != null && Mouse.current.rightButton.isPressed))
                _gesture.ResumeBodyFollow();
            if (keyboard.rKey.wasPressedThisFrame) _gesture.RequestReset();
            ShowStatus();
        }

        // After the Animator has posed the head: turn the body past the limit, then the head.
        private void LateUpdate()
        {
            var elapsedSeconds = Time.deltaTime;
            var isMoving = _animator.GetBool(s_isMoving) ||
                (_movement != null && _movement.HasMovementInput);
            var canTurn = HasFocus && !isMoving && _animator.GetBool(s_isGrounded) &&
                !IsPlayingTag(JumpTag);
            _gesture.Tick(IsPlayingTag(GestureTag));
            if (!canTurn && _gesture.Resetting)
            {
                _gesture.CancelReset();
                _gesture.ResumeBodyFollow();
            }
            if (isMoving) _gesture.ResumeBodyFollow();

            var target = CurrentTarget();
            var isAiming = _isOn || target != LookAtTarget.Camera;
            var (rawYaw, rawPitch) = Raw(target);
            var stepDegrees = 0f;
            if (canTurn && _gesture.Resetting)
            {
                var face = transform.InverseTransformDirection(_staffCamera.forward);
                var cameraYawDegrees = LookAt.RawAngles(face.x, 0f, face.z).Yaw;
                stepDegrees = _gesture.Step(cameraYawDegrees, elapsedSeconds, _settings);
            }
            else if (canTurn && isAiming && !_gesture.IsBodyFollowPaused && !_gesture.InGesture)
            {
                stepDegrees = LookAt.BodyStep(LookAt.ExcessYaw(rawYaw, _settings),
                    elapsedSeconds, _settings);
            }
            if (stepDegrees != 0f)
            {
                transform.Rotate(0f, stepDegrees, 0f, Space.World);
                (rawYaw, rawPitch) = Raw(target);
            }
            AnimateTurn(elapsedSeconds > 0f ? stepDegrees / elapsedSeconds : 0f);

            var (yaw, pitch) = LookAt.Clamp(rawYaw, rawPitch, _settings);
            var pose = _head.Tick(elapsedSeconds, isAiming, yaw, pitch, _settings,
                _gesture.InGesture);
            if (_avatar != null)
            {
                var (eyeYaw, eyePitch) = LookAt.EyeAngles(rawYaw, rawPitch, pose);
                _avatar.Runtime.LookAt.SetYawPitchManually(eyeYaw, eyePitch);
            }
            if (pose.Weight <= 0f) return;

            var offset = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(-pose.Pitch, pose.Yaw, 0f), pose.Weight);
            var body = transform.rotation;
            _headBone.rotation = body * offset * Quaternion.Inverse(body) * _headBone.rotation;
        }

        // Unclamped body-local yaw and pitch to a target; one destroyed since Update falls back
        // to the camera for this frame.
        private (float Yaw, float Pitch) Raw(LookAtTarget target)
        {
            var aimed = target == LookAtTarget.Customer ? _customerAvatar : InterestTarget;
            if (target == LookAtTarget.Camera || aimed == null)
            {
                var to = transform.InverseTransformDirection(
                    _staffCamera.position - _headBone.position);
                var face = transform.InverseTransformDirection(_staffCamera.forward);
                return _cameraAim.Aim(to.x, to.y, to.z, face.x, face.y, face.z, _settings);
            }

            var point = target == LookAtTarget.Interest && aimed == _selectedTarget
                ? aimed.TransformPoint(_selectedPoint) : aimed.position;
            var toAimed = transform.InverseTransformDirection(point - _headBone.position);
            return LookAt.RawAngles(toAimed.x, toAimed.y, toAimed.z);
        }

        private void SelectInterestPoint()
        {
            var count = Physics.RaycastNonAlloc(_staffCamera.position, _staffCamera.forward,
                s_focusHits, FocusRayMeters, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            var nearestMeters = FocusRayMeters;
            for (var i = 0; i < count; i++)
            {
                var hit = s_focusHits[i];
                if (hit.distance >= nearestMeters || hit.transform.IsChildOf(transform))
                    continue;

                nearestMeters = hit.distance;
                _selectedTarget = hit.transform;
                _selectedPoint = _selectedTarget.InverseTransformPoint(hit.point);
            }
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
            _selectedTarget != null ? LookAtTarget.Interest :
                LookAt.PickTarget(_focus.InRange, _focus.IsOn && InterestTarget != null);

        private void ShowStatus()
        {
            if (_status == null) return;
            _status.text = "      " + LookAt.StatusText(_isOn, _focus, _customerAvatar != null,
                CurrentTarget(), InterestTarget != null ? InterestTarget.name : "");
        }
    }
}
