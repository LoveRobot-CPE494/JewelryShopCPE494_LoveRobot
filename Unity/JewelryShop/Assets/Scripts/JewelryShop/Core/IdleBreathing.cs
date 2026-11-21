using System;

namespace JewelryShop.Core
{
    /// <summary>Rules and math for subtle breathing layered on the static Idle clip.</summary>
    public static class IdleBreathing
    {
        /// <summary>Breathe while standing in Idle, including during Idle Lock.</summary>
        public static bool ShouldBreathe(bool isInIdleState, bool isGrounded, bool isMoving) =>
            isInIdleState && isGrounded && !isMoving;

        /// <summary>Moves the 0..1 weight toward its target; a ramp of 0 snaps.</summary>
        public static float NextWeight(float weight, bool isBreathing, float deltaSeconds,
            float rampSeconds)
        {
            var target = isBreathing ? 1f : 0f;
            if (rampSeconds <= 0f)
                return target;
            var step = deltaSeconds / rampSeconds;
            return weight < target
                ? Math.Min(target, weight + step)
                : Math.Max(target, weight - step);
        }

        /// <summary>A -1..1 sine over the period; 0 for a bad period.</summary>
        public static float Wave(float timeSeconds, float periodSeconds) =>
            periodSeconds <= 0f
                ? 0f
                : (float)Math.Sin(2.0 * Math.PI * timeSeconds / periodSeconds);
    }
}
