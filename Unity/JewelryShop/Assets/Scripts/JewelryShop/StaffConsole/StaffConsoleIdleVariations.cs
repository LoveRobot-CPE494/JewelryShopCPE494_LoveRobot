using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Schedules a single standby variation while the Staff Avatar waits.</summary>
    public sealed class StaffConsoleIdleVariations : StateMachineBehaviour
    {
        private const float MinimumIntervalSeconds = 6f;
        private const float MaximumIntervalSeconds = 12f;
        private const float BlendSeconds = 0.15f;
        private static readonly int s_isMoving = Animator.StringToHash("IsMoving");
        private static readonly int[] s_standbyStates =
        {
            Animator.StringToHash("Base Layer.Standby01"),
            Animator.StringToHash("Base Layer.Standby02"),
        };

        private float _secondsUntilChange;

        /// <summary>Starts a fresh countdown whenever the main Idle state begins.</summary>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            _secondsUntilChange = Random.Range(MinimumIntervalSeconds, MaximumIntervalSeconds);
        }

        /// <summary>Plays one standby variation after the main Idle state has waited.</summary>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (animator.IsInTransition(layerIndex) || animator.GetBool(s_isMoving))
                return;

            _secondsUntilChange -= Time.deltaTime;
            if (_secondsUntilChange > 0f)
                return;

            _secondsUntilChange = float.PositiveInfinity;
            var chosenState = s_standbyStates[Random.Range(0, s_standbyStates.Length)];
            animator.CrossFadeInFixedTime(chosenState, BlendSeconds, layerIndex);
        }
    }
}
