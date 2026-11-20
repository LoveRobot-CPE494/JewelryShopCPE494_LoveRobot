using JewelryShop.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JewelryShop.StaffConsole
{
    /// <summary>Drives the Staff Avatar and third-person camera in the offline preview.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class StaffConsoleMovement : MonoBehaviour
    {
        private const float CameraPivotHeightMeters = 0.9f;
        private const float MinimumMovementMeters = 0.0001f;
        private const float PlatformEdgeMarginMeters = 0.25f;
        private const float WalkAccelerationMetersPerSecondSquared = 8f;
        private const float TurnDegreesPerSecond = 180f;
        private const float GestureBlendSeconds = 0.08f;
        private const string GestureTag = "Gesture";
        private const string JumpTag = "Jump";
        private const string IdleLockStatusName = "Idle Lock Status";
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_idleLock = Animator.StringToHash("IdleLock");
        private static readonly int s_walkHorizontal = Animator.StringToHash("WalkHorizontal");
        private static readonly int s_walkForward = Animator.StringToHash("WalkForward");
        private static readonly int s_jump = Animator.StringToHash("Base Layer.Jump");
        private static readonly (Key Key, int State)[] s_gestures =
        {
            (Key.Digit1, Animator.StringToHash("Base Layer.Wave")),
            (Key.Digit2, Animator.StringToHash("Base Layer.Bow")),
            (Key.Digit3, Animator.StringToHash("Base Layer.Point")),
            (Key.Digit4, Animator.StringToHash("Base Layer.Nod")),
            (Key.Digit5, Animator.StringToHash("Base Layer.Shake")),
            (Key.Digit6, Animator.StringToHash("Base Layer.Embarrassed")),
        };

        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _staffCamera;
        [SerializeField, FormerlySerializedAs("_previewPlatform")]
        private MeshRenderer _walkableFloorRenderer;
        [SerializeField, Min(0f)] private float _walkSpeedMetersPerSecond = 1f;
        [SerializeField, Min(0f)] private float _lookDegreesPerPixel = 0.15f;

        private Vector3 _platformCenterMeters;
        private float _platformRadiusMeters;
        private Vector3 _initialCameraOffsetMeters;
        private Quaternion _initialCameraRotation;
        private float _initialCameraYawDegrees;
        private float _yawDegrees;
        private float _pitchDegrees;
        private Vector3 _walkVelocityMetersPerSecond;
        private Text _idleLockStatus;

        /// <summary>Whether the avatar is in or blending with the jump animation.</summary>
        public bool IsJumping => _animator != null && IsPlayingTag(JumpTag);

        private Vector3 CameraPivot => transform.position + Vector3.up * CameraPivotHeightMeters;

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (_staffCamera == null || _walkableFloorRenderer == null || _animator == null ||
                _animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning(
                    "StaffConsoleMovement needs Staff Camera, Floor renderer, and Animator.",
                    this);
                enabled = false;
                return;
            }

            _animator.applyRootMotion = false;
            var statusObject = GameObject.Find(IdleLockStatusName);
            _idleLockStatus = statusObject != null ? statusObject.GetComponent<Text>() : null;
            if (_idleLockStatus == null)
                Debug.LogWarning(
                    $"No Text named '{IdleLockStatusName}'; Idle Lock still works.", this);
            SetIdleLock(false);
            var bounds = _walkableFloorRenderer.bounds;
            _platformCenterMeters = new Vector3(
                bounds.center.x, transform.position.y, bounds.center.z);
            var radiusMeters = Mathf.Min(bounds.extents.x, bounds.extents.z);
            _platformRadiusMeters = Mathf.Max(0f, radiusMeters - PlatformEdgeMarginMeters);
            _initialCameraOffsetMeters = _staffCamera.position - CameraPivot;
            _initialCameraRotation = _staffCamera.rotation;
            _initialCameraYawDegrees = _initialCameraRotation.eulerAngles.y;
        }

        private void Update()
        {
            if (!Application.isFocused)
            {
                _walkVelocityMetersPerSecond = Vector3.zero;
                Move(Vector2.zero, Time.deltaTime, IsJumping);
                return;
            }

            ApplyControls();
        }

        private void ApplyControls()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var movement = Vector2.zero;
            var isJumping = IsJumping;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var lookDegrees = mouse.delta.ReadValue() * _lookDegreesPerPixel;
                _yawDegrees = Mathf.Repeat(_yawDegrees + lookDegrees.x, 360f);
                _pitchDegrees = Mathf.Clamp(_pitchDegrees - lookDegrees.y, -20f, 25f);
            }

            if (keyboard != null)
            {
                if (keyboard.digit7Key.wasPressedThisFrame)
                    SetIdleLock(!_animator.GetBool(s_idleLock));

                var canMove = true;
                if (!isJumping && keyboard.spaceKey.wasPressedThisFrame)
                {
                    _animator.CrossFadeInFixedTime(s_jump, GestureBlendSeconds, 0);
                    isJumping = true;
                }
                else if (!isJumping && (TryPlayGesture(keyboard) || IsPlayingTag(GestureTag)))
                {
                    _walkVelocityMetersPerSecond = Vector3.zero;
                    canMove = false;
                }

                if (canMove)
                {
                    movement = new Vector2(
                        keyboard.dKey.ReadValue() - keyboard.aKey.ReadValue(),
                        keyboard.wKey.ReadValue() - keyboard.sKey.ReadValue());
                }
            }

            Move(movement, Time.deltaTime, isJumping);
        }

        private void SetIdleLock(bool isIdleLockOn)
        {
            _animator.SetBool(s_idleLock, isIdleLockOn);
            if (_idleLockStatus != null)
                _idleLockStatus.text = IdleLock.StatusText(isIdleLockOn);
        }

        private bool TryPlayGesture(Keyboard keyboard)
        {
            foreach (var (key, state) in s_gestures)
            {
                if (!keyboard[key].wasPressedThisFrame)
                    continue;

                _animator.CrossFadeInFixedTime(state, GestureBlendSeconds, 0);
                return true;
            }

            return false;
        }

        private bool IsPlayingTag(string tag)
        {
            return _animator.GetCurrentAnimatorStateInfo(0).IsTag(tag) ||
                _animator.GetNextAnimatorStateInfo(0).IsTag(tag);
        }

        private void Move(Vector2 movement, float elapsedSeconds, bool isJumping)
        {
            var input = Vector2.ClampMagnitude(movement, 1f);
            var isBacking = input.y < 0f;
            var heading = Quaternion.Euler(0f, _initialCameraYawDegrees + _yawDegrees, 0f);
            var direction = heading * new Vector3(input.x, 0f, input.y);
            var targetVelocityMetersPerSecond = direction * _walkSpeedMetersPerSecond;
            _walkVelocityMetersPerSecond = Vector3.MoveTowards(_walkVelocityMetersPerSecond,
                targetVelocityMetersPerSecond,
                WalkAccelerationMetersPerSecondSquared * elapsedSeconds);
            var previousMeters = transform.position;
            var nextMeters = previousMeters + _walkVelocityMetersPerSecond * elapsedSeconds;
            nextMeters = _platformCenterMeters +
                Vector3.ClampMagnitude(nextMeters - _platformCenterMeters, _platformRadiusMeters);

            var displacementMeters = nextMeters - previousMeters;
            var isMoving =
                displacementMeters.sqrMagnitude > MinimumMovementMeters * MinimumMovementMeters;
            var walkBlend = Vector2.zero;
            if (isMoving)
            {
                var cameraDisplacementMeters = Quaternion.Inverse(heading) * displacementMeters;
                var fullSpeedDistanceMeters = Mathf.Max(MinimumMovementMeters,
                    _walkSpeedMetersPerSecond * elapsedSeconds);
                var cameraDirection = new Vector2(cameraDisplacementMeters.x,
                    cameraDisplacementMeters.z) / fullSpeedDistanceMeters;
                var turnDegrees = Mathf.Atan2(cameraDirection.x,
                    Mathf.Abs(cameraDirection.y)) * Mathf.Rad2Deg;
                var targetRotation = heading * Quaternion.Euler(0f,
                    isBacking ? -turnDegrees : turnDegrees, 0f);
                var rotation = isJumping ? transform.rotation : Quaternion.RotateTowards(
                    transform.rotation, targetRotation, TurnDegreesPerSecond * elapsedSeconds);
                transform.SetPositionAndRotation(nextMeters, rotation);

                var localDisplacementMeters = Quaternion.Inverse(rotation) * displacementMeters;
                var localDirection = new Vector2(localDisplacementMeters.x,
                    localDisplacementMeters.z) / fullSpeedDistanceMeters;
                // Backpedaling uses the opposite side clip while turning away from that side.
                walkBlend = isBacking ? new Vector2(-cameraDirection.x, cameraDirection.y) :
                    localDirection;
                walkBlend = Vector2.ClampMagnitude(walkBlend, 1f);
            }

            _animator.SetFloat(s_walkHorizontal, walkBlend.x);
            _animator.SetFloat(s_walkForward, walkBlend.y);
            _animator.SetBool(s_isMoving, isMoving);
        }

        private void LateUpdate()
        {
            var yaw = Quaternion.AngleAxis(_yawDegrees, Vector3.up);
            var cameraRight = Quaternion.Euler(0f, _initialCameraYawDegrees, 0f) * Vector3.right;
            var orbit = yaw * Quaternion.AngleAxis(_pitchDegrees, cameraRight);
            _staffCamera.SetPositionAndRotation(
                CameraPivot + orbit * _initialCameraOffsetMeters, orbit * _initialCameraRotation);
        }

        private void OnDisable()
        {
            _walkVelocityMetersPerSecond = Vector3.zero;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool(s_isMoving, false);
        }
    }
}
