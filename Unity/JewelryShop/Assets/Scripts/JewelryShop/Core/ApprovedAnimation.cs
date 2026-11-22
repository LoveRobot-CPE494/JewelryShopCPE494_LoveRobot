using System;

namespace JewelryShop.Core
{
    /// <summary>Timing curves for the Approved ✓ pop-in (scale with a small overshoot, fade in).</summary>
    public static class ApprovedAnimation
    {
        /// <summary>0..1 progress of <paramref name="elapsed"/> through <paramref name="duration"/>.</summary>
        public static float Progress(float elapsed, float duration) =>
            duration <= 0f ? 1f : Math.Clamp(elapsed / duration, 0f, 1f);

        /// <summary>Ease-out-back: 0 at p=0, 1 at p=1, overshooting 1 in between.</summary>
        public static float Scale(float p)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var q = p - 1f;
            return 1f + c3 * q * q * q + c1 * q * q;
        }
    }
}
