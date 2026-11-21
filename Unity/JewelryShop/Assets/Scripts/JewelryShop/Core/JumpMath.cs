using System;

namespace JewelryShop.Core
{
    /// <summary>Jump arc maths: a parabola peaking at the height halfway through.</summary>
    public static class JumpMath
    {
        /// <summary>Upward speed at take-off that peaks at the given height.</summary>
        public static float TakeoffSpeed(float heightMeters, float gravityMetersPerSecondSquared) =>
            (float)Math.Sqrt(2f * heightMeters * gravityMetersPerSecondSquared);

        /// <summary>
        /// Share of the walk acceleration that applies: all on the ground,
        /// <paramref name="airControl"/> in the air. It scales acceleration only;
        /// there is no separate air speed cap.
        /// </summary>
        public static float ControlScale(bool isGrounded, float airControl) =>
            isGrounded ? 1f : Math.Max(0f, Math.Min(1f, airControl));
    }
}
