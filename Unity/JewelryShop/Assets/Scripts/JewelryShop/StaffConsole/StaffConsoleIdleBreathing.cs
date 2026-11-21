using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Layers subtle motion onto the Staff Avatar's standing Idle pose.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class StaffConsoleIdleBreathing : MonoBehaviour
    {
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_isGrounded = Animator.StringToHash("IsGrounded");
        [SerializeField, Range(0f, 3f)] private float _chestDegrees = 1.5f;
        [SerializeField, Min(0.5f)] private float _periodSeconds = 4f;
        [SerializeField, Min(0f)] private float _rampSeconds = 0.3f;
        private const float ArmSwayDegrees = 1f;
        private const float ElbowSwayDegrees = 1f;
        private const float WristSwayDegrees = 2.5f;
        private const float LeftArmPeriodSeconds = 6f;
        private const float RightArmPeriodSeconds = 6.8f;
        private const float ForwardSwayLagSeconds = 0.25f;
        private const float WristLagSeconds = 0.35f;

        private Animator _animator;
        private Transform _chest;
        private (Transform Upper, Transform Lower, Transform Hand) _leftArm;
        private (Transform Upper, Transform Lower, Transform Hand) _rightArm;
        private float _weight;
        private float _timeSeconds;
        private (int Destination, float Progress, float StartProgress,
            float StartWeight, float EndWeight)?
            _poseTransition;
        private readonly List<(Transform Bone, Quaternion LocalRotation)> _poseBeforeOffsets =
            new(7);

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator.avatar != null && _animator.avatar.isValid && _animator.isHuman)
            {
                _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
                _leftArm = (_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    _animator.GetBoneTransform(HumanBodyBones.LeftHand));
                _rightArm = (_animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    _animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    _animator.GetBoneTransform(HumanBodyBones.RightHand));
            }
            if (_chest != null)
                return;
            Debug.LogWarning("Idle breathing needs a humanoid Animator with a Chest bone; " +
                "disabled.", this);
            enabled = false;
        }

        // Clear the offsets before animation runs again, including frames culled by the Animator.
        private void Update() => RestorePose();

        private void OnDisable()
        {
            RestorePose();
            _weight = 0f;
            _timeSeconds = 0f;
            _poseTransition = null;
        }

        private void RestorePose()
        {
            foreach (var (bone, rotation) in _poseBeforeOffsets)
                if (bone != null)
                    bone.localRotation = rotation;
            _poseBeforeOffsets.Clear();
        }

        private void LateUpdate()
        {
            if (!_animator.isActiveAndEnabled || !_animator.isInitialized)
                return;

            _weight = BlendPoseWeight();
            if (_weight <= 0f)
            {
                _timeSeconds = 0f;
                return;
            }

            _timeSeconds += Time.deltaTime;
            var pitchDegrees = IdleBreathing.Wave(_timeSeconds, _periodSeconds) *
                _chestDegrees * _weight;
            var chestSway = Quaternion.AngleAxis(pitchDegrees, _chest.right);
            SetBoneRotation(_chest, chestSway * _chest.rotation);
            var chestCounterRotation = Quaternion.Inverse(chestSway);
            SwayArm(_leftArm, LeftArmPeriodSeconds, -1f, chestCounterRotation);
            SwayArm(_rightArm, RightArmPeriodSeconds, 1f, chestCounterRotation);
        }

        private float BlendPoseWeight()
        {
            var isGrounded = _animator.GetBool(s_isGrounded);
            var isMoving = _animator.GetBool(s_isMoving);
            if (_animator.IsInTransition(0))
            {
                var destination = _animator.GetNextAnimatorStateInfo(0).fullPathHash;
                var progress = Mathf.Clamp01(_animator.GetAnimatorTransitionInfo(0).normalizedTime);
                var isIdleDestination = destination == StaffConsoleIdleVariations.IdleState;
                var isBreathingAtDestination = IdleBreathing.ShouldBreathe(
                    isIdleDestination, isGrounded, isMoving);
                var endWeight = isBreathingAtDestination ? 1f : 0f;
                if (_poseTransition is not { } transition ||
                    transition.Destination != destination || progress < transition.Progress)
                {
                    // Start interruptions from the pose weight that was already visible.
                    transition = (destination, progress, 0f, _weight, endWeight);
                }
                else if (transition.EndWeight != endWeight)
                {
                    transition.StartWeight = _weight;
                    transition.StartProgress = transition.Progress;
                    transition.EndWeight = endWeight;
                }

                transition.Progress = progress;
                _poseTransition = transition;
                var blend = Mathf.InverseLerp(transition.StartProgress, 1f, progress);
                return progress >= 1f
                    ? endWeight
                    : Mathf.Lerp(transition.StartWeight, endWeight, blend);
            }

            var current = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var isInIdle = current == StaffConsoleIdleVariations.IdleState;
            var isBreathing = IdleBreathing.ShouldBreathe(isInIdle, isGrounded, isMoving);
            var targetWeight = isBreathing ? 1f : 0f;
            var previousTransition = _poseTransition;
            _poseTransition = null;
            if (previousTransition is { } completed && completed.Destination == current &&
                completed.EndWeight == targetWeight)
                return targetWeight;

            return IdleBreathing.NextWeight(_weight, isBreathing, Time.deltaTime, _rampSeconds);
        }

        private void SwayArm((Transform Upper, Transform Lower, Transform Hand) arm,
            float periodSeconds, float side, Quaternion chestCounterRotation)
        {
            if (arm.Upper == null || arm.Lower == null)
                return;

            var forward = transform.forward;
            var right = transform.right;
            var sidewaysDegrees = IdleBreathing.Wave(_timeSeconds, periodSeconds) *
                ArmSwayDegrees * _weight * side;
            var forwardTimeSeconds = Mathf.Max(0f, _timeSeconds - ForwardSwayLagSeconds);
            var forwardWave = IdleBreathing.Wave(forwardTimeSeconds, periodSeconds);
            var forwardDegrees = forwardWave * ArmSwayDegrees * _weight * side;
            // Keep the arm sway centered on the authored pose as the chest breathes.
            var armSway = Quaternion.AngleAxis(sidewaysDegrees, forward) *
                Quaternion.AngleAxis(forwardDegrees, right);
            SetBoneRotation(arm.Upper, armSway * chestCounterRotation * arm.Upper.rotation);

            var elbowDegrees = forwardWave * ElbowSwayDegrees * _weight;
            SetBoneRotation(arm.Lower, Quaternion.AngleAxis(elbowDegrees, right) *
                arm.Lower.rotation);

            if (arm.Hand == null)
                return;

            var wristTimeSeconds = Mathf.Max(0f, _timeSeconds - WristLagSeconds);
            var wristDegrees = IdleBreathing.Wave(wristTimeSeconds, periodSeconds) *
                WristSwayDegrees * _weight * side;
            var wristAxis = Vector3.ProjectOnPlane(forward, arm.Hand.position - arm.Lower.position);
            SetBoneRotation(arm.Hand, Quaternion.AngleAxis(wristDegrees, wristAxis) *
                arm.Hand.rotation);
        }

        private void SetBoneRotation(Transform bone, Quaternion rotation)
        {
            _poseBeforeOffsets.Add((bone, bone.localRotation));
            bone.rotation = rotation;
        }
    }
}
