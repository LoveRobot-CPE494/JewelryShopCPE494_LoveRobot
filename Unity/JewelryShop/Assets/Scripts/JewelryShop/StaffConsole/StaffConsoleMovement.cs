using System.Linq;
using JewelryShop.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JewelryShop.StaffConsole
{
    /// <summary>Drives the Staff Avatar and third-person camera in the offline preview.</summary>
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public sealed class StaffConsoleMovement : MonoBehaviour
    {
        // Keeps the controller pressed on the ground so isGrounded stays true on flats and ramps.
        private const float GroundStickMetersPerSecond = 2f;
        private const float MinimumMovementMeters = 0.0001f;
        private const string GestureTag = "Gesture";
        private const string JumpTag = "Jump";
        // Foot contact frames in Jumping0 (normalized clip time).
        private const float JumpTakeoffPose = 0.36f;
        private const float JumpLandingPose = 0.75f;
        private const string IdleLockStatusName = "Idle Lock Status";
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_idleLock = Animator.StringToHash("IdleLock");
        private static readonly int s_walkHorizontal = Animator.StringToHash("WalkHorizontal");
        private static readonly int s_walkForward = Animator.StringToHash("WalkForward");
        private static readonly int s_jump = Animator.StringToHash("Base Layer.Jump");
        private static readonly int s_isGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int s_jumpPose = Animator.StringToHash("JumpPose");

        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _staffCamera;
        [SerializeField] private StaffMovementConfig _movementConfig;
        [SerializeField] private StaffCameraConfig _cameraConfig;
        [SerializeField] private StaffGestureMapConfig _gestureMapConfig;

        private CharacterController _controller;
        private Vector3 _startPositionMeters;
        private Quaternion _startRotation;
        private float _verticalMetersPerSecond;
        private float _jumpTakeoffMetersPerSecond;
        private bool _isTakingOff; // Space pressed, still waiting out the take-off delay
        private float _takeoffRemainingSeconds;
        private bool _isInJumpArc;
        private bool _hasMoved; // isGrounded is meaningless before the first controller Move
        private bool _hasGroundedParameter;
        private bool _hasJumpPoseParameter;
        // 16 is far more than a camera ray crosses; on overflow Unity keeps an arbitrary subset,
        // so the nearest wall could be missed for that frame.
        private static readonly RaycastHit[] s_cameraHits = new RaycastHit[16];
        private Vector3 _initialCameraOffsetMeters;
        private float _cameraDistanceMeters;
        private Quaternion _initialCameraRotation;
        private float _initialCameraYawDegrees;
        private float _yawDegrees;
        private float _pitchDegrees;
        private Vector3 _walkVelocityMetersPerSecond;
        // PlayMode tests turn it off so an unfocused Editor still walks.
        private bool _isFocusRequired = true;
        private Text _idleLockStatus;
        private MovementSettings _movement;
        private CameraSettings _camera;
        private float _blendSeconds;
        private (Key Key, int State)[] _gestures;

        /// <summary>Whether the avatar is in or blending with the jump animation.</summary>
        public bool IsJumping => _animator != null && IsPlayingTag(JumpTag);

        // Space, gestures and Idle Lock need real ground, with no grace after leaving an edge.
        private bool CanUseGroundKeys => !_isInJumpArc && !_isTakingOff &&
            (!_hasMoved || _controller.isGrounded);

        private Vector3 CameraPivot => transform.position + Vector3.up * _camera.PivotHeightMeters;

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (_movementConfig == null)
                Debug.LogWarning("No Movement Config assigned; using defaults.", this);
            _movement = _movementConfig != null ? _movementConfig.Settings :
                MovementSettings.Defaults;
            if (_cameraConfig == null)
                Debug.LogWarning("No Camera Config assigned; using defaults.", this);
            _camera = _cameraConfig != null ? _cameraConfig.Settings : CameraSettings.Defaults;
            if (_gestureMapConfig == null)
                Debug.LogWarning("No Gesture Map Config assigned; using defaults.", this);
            var gestureMap = _gestureMapConfig != null ? _gestureMapConfig.Settings :
                GestureMapSettings.Defaults;
            _blendSeconds = gestureMap.BlendSeconds;
            _gestures = gestureMap.Entries
                .Select(e => ((Key)((int)Key.Digit1 + e.Digit - 1),
                    Animator.StringToHash(e.StateName)))
                .ToArray();

            // RequireComponent does not retrofit scene objects saved before it existed.
            if (!TryGetComponent(out _controller))
            {
                Debug.LogWarning("No CharacterController on Cheryll; adding one at runtime. " +
                    "Add it in the Editor and save the scene.", this);
                _controller = gameObject.AddComponent<CharacterController>();
            }

            if (_staffCamera == null || _animator == null ||
                _animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("StaffConsoleMovement needs Staff Camera and Animator.", this);
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
            SetUpJump();
            _controller.radius = _movement.CapsuleRadiusMeters;
            _controller.height = _movement.CapsuleHeightMeters;
            _controller.center = Vector3.up * (_movement.CapsuleHeightMeters / 2f);
            _controller.stepOffset = _movement.StepHeightMeters;
            _controller.slopeLimit = _movement.SlopeLimitDegrees;
            _startPositionMeters = transform.position;
            _startRotation = transform.rotation;
            _initialCameraOffsetMeters = _staffCamera.position - CameraPivot;
            _cameraDistanceMeters = _initialCameraOffsetMeters.magnitude;
            _initialCameraRotation = _staffCamera.rotation;
            _initialCameraYawDegrees = _initialCameraRotation.eulerAngles.y;
        }

        private void SetUpJump()
        {
            _jumpTakeoffMetersPerSecond =
                JumpMath.TakeoffSpeed(_movement.JumpHeightMeters,
                    _movement.GravityMetersPerSecondSquared);

            _hasGroundedParameter = _animator.parameters.Any(p =>
                p.nameHash == s_isGrounded && p.type == AnimatorControllerParameterType.Bool);
            _hasJumpPoseParameter = _animator.parameters.Any(p =>
                p.nameHash == s_jumpPose && p.type == AnimatorControllerParameterType.Float);
            if (!_hasGroundedParameter)
                Debug.LogWarning("Animator has no bool 'IsGrounded'; the Jump state can only " +
                    "end when its clip ends.", this);
        }

        private void Update()
        {
            if (_isFocusRequired && !Application.isFocused)
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
                var lookDegrees = mouse.delta.ReadValue() * _camera.LookDegreesPerPixel;
                _yawDegrees = Mathf.Repeat(_yawDegrees + lookDegrees.x, 360f);
                _pitchDegrees = Mathf.Clamp(_pitchDegrees - lookDegrees.y,
                    _camera.MinPitchDegrees, _camera.MaxPitchDegrees);
            }

            if (keyboard != null)
            {
                var onGround = CanUseGroundKeys;
                if (onGround && keyboard.digit7Key.wasPressedThisFrame)
                    SetIdleLock(!_animator.GetBool(s_idleLock));

                var canMove = true;
                if (!isJumping && onGround && _jumpTakeoffMetersPerSecond > 0f &&
                    keyboard.spaceKey.wasPressedThisFrame)
                {
                    if (_hasJumpPoseParameter)
                        _animator.SetFloat(s_jumpPose, 0f);
                    _animator.CrossFadeInFixedTime(s_jump, _blendSeconds, 0);
                    _isTakingOff = true;
                    _takeoffRemainingSeconds = _movement.JumpTakeoffDelaySeconds;
                    isJumping = true;
                }
                else if (!isJumping && ((onGround && TryPlayGesture(keyboard)) ||
                    IsPlayingTag(GestureTag)))
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
            foreach (var (key, state) in _gestures)
            {
                if (!keyboard[key].wasPressedThisFrame)
                    continue;

                _animator.CrossFadeInFixedTime(state, _blendSeconds, 0);
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
            var heading = Quaternion.Euler(0f, _initialCameraYawDegrees + _yawDegrees, 0f);
            var direction = heading * new Vector3(input.x, 0f, input.y);
            var targetVelocityMetersPerSecond = direction * _movement.WalkSpeedMetersPerSecond;
            var isGrounded = _controller.isGrounded && !_isInJumpArc;
            _walkVelocityMetersPerSecond = Vector3.MoveTowards(_walkVelocityMetersPerSecond,
                targetVelocityMetersPerSecond,
                _movement.AccelerationMetersPerSecondSquared *
                JumpMath.ControlScale(isGrounded, _movement.AirControl) * elapsedSeconds);
            if (isGrounded)
            {
                _verticalMetersPerSecond = -GroundStickMetersPerSecond;
            }

            var gravitySeconds = isGrounded ? 0f : elapsedSeconds;
            if (_isTakingOff)
            {
                _takeoffRemainingSeconds -= elapsedSeconds;
                if (!isGrounded)
                    _isTakingOff = false; // walked off an edge before take-off
                else if (_takeoffRemainingSeconds <= 0f)
                {
                    _verticalMetersPerSecond = _jumpTakeoffMetersPerSecond;
                    gravitySeconds = Mathf.Min(elapsedSeconds, -_takeoffRemainingSeconds);
                    _isInJumpArc = true;
                    _isTakingOff = false;
                }
            }

            var previousMeters = transform.position;
            var gravityDelta = _movement.GravityMetersPerSecondSquared * gravitySeconds;
            var verticalDistance = _isInJumpArc || !isGrounded
                ? (_verticalMetersPerSecond - 0.5f * gravityDelta) * gravitySeconds
                : _verticalMetersPerSecond * elapsedSeconds;
            _verticalMetersPerSecond -= gravityDelta;
            var contacts = _controller.Move(_walkVelocityMetersPerSecond * elapsedSeconds +
                Vector3.up * verticalDistance);
            if ((contacts & CollisionFlags.Above) != 0)
                _verticalMetersPerSecond = Mathf.Min(0f, _verticalMetersPerSecond);
            // Landing is detected here, in the Move that lands, so the Animator bool does not lag.
            _hasMoved = true;
            var landed = _controller.isGrounded && (!_isInJumpArc || verticalDistance < 0f);
            if (_hasJumpPoseParameter && _isTakingOff)
            {
                _animator.SetFloat(s_jumpPose, JumpTakeoffPose *
                    (1f - _takeoffRemainingSeconds / _movement.JumpTakeoffDelaySeconds));
            }
            else if (_hasJumpPoseParameter && _isInJumpArc)
            {
                var progress = landed ? 1f : Mathf.Clamp01(
                    (_jumpTakeoffMetersPerSecond - _verticalMetersPerSecond) /
                    (2f * _jumpTakeoffMetersPerSecond));
                _animator.SetFloat(s_jumpPose, Mathf.Lerp(JumpTakeoffPose, JumpLandingPose,
                    progress));
            }
            if (landed)
                _isInJumpArc = false;

            if (transform.position.y < _movement.KillPlaneYMeters)
            {
                Respawn();
                return;
            }

            // Walls and steps shape the animation: only what actually moved counts, sideways.
            var displacementMeters = transform.position - previousMeters;
            displacementMeters.y = 0f;
            var isMoving =
                displacementMeters.sqrMagnitude > MinimumMovementMeters * MinimumMovementMeters;
            var walkBlend = Vector2.zero;
            if (isMoving)
            {
                var cameraDisplacementMeters = Quaternion.Inverse(heading) * displacementMeters;
                var fullSpeedDistanceMeters = Mathf.Max(MinimumMovementMeters,
                    _movement.WalkSpeedMetersPerSecond * elapsedSeconds);
                var cameraDirection = new Vector2(cameraDisplacementMeters.x,
                    cameraDisplacementMeters.z) / fullSpeedDistanceMeters;
                var turnDegrees = Mathf.Atan2(cameraDirection.x, cameraDirection.y) *
                    Mathf.Rad2Deg;
                var targetRotation = heading * Quaternion.Euler(0f, turnDegrees, 0f);
                var rotation = isJumping ? transform.rotation : Quaternion.RotateTowards(
                    transform.rotation, targetRotation,
                    _movement.TurnDegreesPerSecond * elapsedSeconds);
                transform.rotation = rotation;

                var localDisplacementMeters = Quaternion.Inverse(rotation) * displacementMeters;
                var localDirection = new Vector2(localDisplacementMeters.x,
                    localDisplacementMeters.z) / fullSpeedDistanceMeters;
                walkBlend = Vector2.ClampMagnitude(localDirection, 1f);
            }

            _animator.SetFloat(s_walkHorizontal, walkBlend.x);
            _animator.SetFloat(s_walkForward, walkBlend.y);
            _animator.SetBool(s_isMoving, isMoving);
            // False from the Space press until she lands, so Jump -> Idle fires only on landing.
            if (_hasGroundedParameter)
                _animator.SetBool(s_isGrounded,
                    _controller.isGrounded && !_isInJumpArc && !_isTakingOff);
        }

        private void Respawn()
        {
            Debug.LogWarning("Cheryll fell below the kill plane " +
                $"({_movement.KillPlaneYMeters} m); respawning at her start position. " +
                "Is there a floor collider under her?", this);
            _controller.enabled = false; // a CharacterController overrides direct teleports
            transform.SetPositionAndRotation(_startPositionMeters, _startRotation);
            _controller.enabled = true;
            _walkVelocityMetersPerSecond = Vector3.zero;
            _verticalMetersPerSecond = 0f;
            _isInJumpArc = false;
            _isTakingOff = false;
            _cameraDistanceMeters = _initialCameraOffsetMeters.magnitude;
            // Back to the start camera heading, and no stale walk blend while a key is still held.
            _yawDegrees = 0f;
            _pitchDegrees = 0f;
            _animator.SetFloat(s_walkHorizontal, 0f);
            _animator.SetFloat(s_walkForward, 0f);
            _animator.SetBool(s_isMoving, false);
        }

        private void LateUpdate()
        {
            var yaw = Quaternion.AngleAxis(_yawDegrees, Vector3.up);
            var cameraRight = Quaternion.Euler(0f, _initialCameraYawDegrees, 0f) * Vector3.right;
            var orbit = yaw * Quaternion.AngleAxis(_pitchDegrees, cameraRight);
            var pivot = CameraPivot;
            var offset = orbit * _initialCameraOffsetMeters;
            var rotation = orbit * _initialCameraRotation;
            var restMeters = offset.magnitude;
            if (restMeters < Mathf.Epsilon)
            {
                // Camera rests on the pivot: no direction to cast along.
                _staffCamera.SetPositionAndRotation(pivot, rotation);
                return;
            }

            var direction = offset / restMeters;
            var clearMeters = ClearDistanceMeters(pivot, direction, restMeters);
            // Pull in at once so no frame clips the wall; ease back out when the way is clear.
            _cameraDistanceMeters = clearMeters < _cameraDistanceMeters ? clearMeters :
                Mathf.MoveTowards(_cameraDistanceMeters, clearMeters,
                    _camera.CollisionRelaxMetersPerSecond * Time.deltaTime);
            _staffCamera.SetPositionAndRotation(
                pivot + direction * _cameraDistanceMeters, rotation);
        }

        // Cheryll's own collider sits around the pivot, so her hits must not count.
        private float ClearDistanceMeters(Vector3 pivot, Vector3 direction, float maxMeters)
        {
            var count = Physics.SphereCastNonAlloc(pivot, _camera.CollisionRadiusMeters, direction,
                s_cameraHits, maxMeters, _camera.CollisionLayerMask,
                QueryTriggerInteraction.Ignore);
            var nearest = maxMeters;
            for (var i = 0; i < count; i++)
                if (!s_cameraHits[i].transform.IsChildOf(transform))
                    nearest = Mathf.Min(nearest, s_cameraHits[i].distance);
            return nearest;
        }

        private void OnDisable()
        {
            _walkVelocityMetersPerSecond = Vector3.zero;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool(s_isMoving, false);
        }
    }
}
