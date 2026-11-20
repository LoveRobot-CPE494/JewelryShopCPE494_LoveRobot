using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Schedules a single standby variation while the Staff Avatar waits.</summary>
    public sealed class StaffConsoleIdleVariations : StateMachineBehaviour
    {
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int s_idleLock = Animator.StringToHash("IdleLock");
        private static readonly int[] s_standbyStates =
        {
            Animator.StringToHash("Base Layer.Standby01"),
            Animator.StringToHash("Base Layer.Standby02"),
            Animator.StringToHash("Base Layer.Standby03"),
            Animator.StringToHash("Base Layer.Standby04"),
            Animator.StringToHash("Base Layer.Standby05"),
        };

        [SerializeField] private StaffIdleConfig _config;

        private IdleSettings _settings;
        private bool _isResolved;
        private float _secondsUntilChange;

        /// <summary>Starts a fresh countdown whenever the main Idle state begins.</summary>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (!_isResolved)
            {
                if (_config == null)
                    Debug.LogWarning("No Idle Config assigned on the Idle state; using defaults.");
                _settings = _config != null ? _config.Settings : IdleSettings.Defaults;
                _isResolved = true;
            }

            _secondsUntilChange = Random.Range(
                _settings.MinIntervalSeconds, _settings.MaxIntervalSeconds);
        }

        /// <summary>Plays one standby variation after the main Idle state has waited.</summary>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (animator.IsInTransition(layerIndex) ||
                !IdleLock.AllowsStandby(animator.GetBool(s_isMoving), animator.GetBool(s_idleLock)))
                return;

            _secondsUntilChange -= Time.deltaTime;
            if (_secondsUntilChange > 0f)
                return;

            _secondsUntilChange = float.PositiveInfinity;
            var chosenState = s_standbyStates[Random.Range(0, s_standbyStates.Length)];
            animator.CrossFadeInFixedTime(chosenState, _settings.BlendSeconds, layerIndex);
        }
    }
}
