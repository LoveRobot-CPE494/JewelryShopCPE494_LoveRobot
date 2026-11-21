using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Schedules a single standby variation while the Staff Avatar waits.</summary>
    public sealed class StaffConsoleIdleVariations : StateMachineBehaviour
    {
        internal static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_isGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int s_idleLock = Animator.StringToHash("IdleLock");
        // Start near the first visible motion in clips with a held opening pose.
        private static readonly (int State, float StartSeconds, float MinimumBlendSeconds)[]
            s_standbyVariations =
        {
            (Animator.StringToHash("Base Layer.Standby01"), 2.4f, 0.4f),
            (Animator.StringToHash("Base Layer.Standby02"), 0.8f, 0.4f),
            (Animator.StringToHash("Base Layer.Standby03"), 0f, 0f),
            (Animator.StringToHash("Base Layer.Standby04"), 0f, 0f),
            (Animator.StringToHash("Base Layer.Standby05"), 1.9f, 0.55f),
        };

        [SerializeField] private StaffIdleConfig _config;

        private IdleSettings _settings;
        private bool _isResolved;
        private float? _secondsUntilChange;

        /// <summary>Starts a fresh countdown whenever the main Idle state begins.</summary>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (!_isResolved)
            {
                if (_config == null)
                    Debug.LogWarning("No Idle Config assigned on the Idle state; using defaults.");
                _settings = _config != null ? _config.Settings : IdleSettings.Defaults;
                if (animator.isHuman && !animator.TryGetComponent<StaffConsoleIdleBreathing>(out _))
                    animator.gameObject.AddComponent<StaffConsoleIdleBreathing>();
                _isResolved = true;
            }

            _secondsUntilChange = null;
        }

        /// <summary>Plays one standby variation after the main Idle state has waited.</summary>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (!animator.GetBool(s_isGrounded) ||
                !IdleLock.AllowsStandby(animator.GetBool(s_isMoving), animator.GetBool(s_idleLock)))
            {
                _secondsUntilChange = null;
                return;
            }
            if (animator.IsInTransition(layerIndex))
                return;

            if (!_secondsUntilChange.HasValue)
            {
                _secondsUntilChange = Random.Range(
                    _settings.MinIntervalSeconds, _settings.MaxIntervalSeconds);
                return;
            }
            _secondsUntilChange -= Time.deltaTime;
            if (_secondsUntilChange > 0f)
                return;

            _secondsUntilChange = float.PositiveInfinity;
            var variation = s_standbyVariations[Random.Range(0, s_standbyVariations.Length)];
            var blendSeconds = Mathf.Max(_settings.BlendSeconds, variation.MinimumBlendSeconds);
            animator.CrossFadeInFixedTime(variation.State, blendSeconds, layerIndex,
                variation.StartSeconds);
        }

        internal void EndStandby(Animator animator)
        {
            var state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            if (System.Array.Exists(s_standbyVariations, item => item.State == state.fullPathHash))
                animator.CrossFadeInFixedTime(IdleState, _settings.BlendSeconds, 0);
        }
    }
}
