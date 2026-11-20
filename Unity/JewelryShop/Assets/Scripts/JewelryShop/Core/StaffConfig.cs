using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>Clamping shared by the Staff Config settings.</summary>
    internal static class ConfigRule
    {
        internal static float Clamp(string name, float value, float min, float max,
            float fallback, List<string> warnings)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                warnings.Add($"{name} is not a number; using {fallback}.");
                return fallback;
            }

            var clamped = Math.Min(max, Math.Max(min, value));
            if (clamped != value)
                warnings.Add($"{name} {value} is outside {min}..{max}; using {clamped}.");
            return clamped;
        }
    }

    /// <summary>Staff Avatar movement tuning. Defaults equal the pre-config behaviour.</summary>
    [Serializable]
    public struct MovementSettings
    {
        public float WalkSpeedMetersPerSecond;
        public float AccelerationMetersPerSecondSquared;
        public float TurnDegreesPerSecond;
        public float EdgeMarginMeters;
        // Physics-only fields below are validated now and read by later tickets.
        public float GravityMetersPerSecondSquared;
        public float JumpSpeedMetersPerSecond;
        public float AirControl;
        public float StepHeightMeters;
        public float SlopeLimitDegrees;
        public float CapsuleRadiusMeters;
        public float CapsuleHeightMeters;

        public static MovementSettings Defaults => new MovementSettings
        {
            WalkSpeedMetersPerSecond = 1f,
            AccelerationMetersPerSecondSquared = 8f,
            TurnDegreesPerSecond = 180f,
            EdgeMarginMeters = 0.25f,
            GravityMetersPerSecondSquared = 9.81f,
            JumpSpeedMetersPerSecond = 4f,
            AirControl = 0.3f,
            StepHeightMeters = 0.3f,
            SlopeLimitDegrees = 45f,
            CapsuleRadiusMeters = 0.3f,
            CapsuleHeightMeters = 1.7f,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public MovementSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            return new MovementSettings
            {
                WalkSpeedMetersPerSecond = ConfigRule.Clamp(nameof(WalkSpeedMetersPerSecond),
                    WalkSpeedMetersPerSecond, 0f, 10f, d.WalkSpeedMetersPerSecond, warnings),
                AccelerationMetersPerSecondSquared = ConfigRule.Clamp(
                    nameof(AccelerationMetersPerSecondSquared),
                    AccelerationMetersPerSecondSquared, 0.1f, 100f,
                    d.AccelerationMetersPerSecondSquared, warnings),
                TurnDegreesPerSecond = ConfigRule.Clamp(nameof(TurnDegreesPerSecond),
                    TurnDegreesPerSecond, 1f, 1440f, d.TurnDegreesPerSecond, warnings),
                EdgeMarginMeters = ConfigRule.Clamp(nameof(EdgeMarginMeters),
                    EdgeMarginMeters, 0f, 5f, d.EdgeMarginMeters, warnings),
                GravityMetersPerSecondSquared = ConfigRule.Clamp(
                    nameof(GravityMetersPerSecondSquared),
                    GravityMetersPerSecondSquared, 0f, 50f,
                    d.GravityMetersPerSecondSquared, warnings),
                JumpSpeedMetersPerSecond = ConfigRule.Clamp(nameof(JumpSpeedMetersPerSecond),
                    JumpSpeedMetersPerSecond, 0f, 20f, d.JumpSpeedMetersPerSecond, warnings),
                AirControl = ConfigRule.Clamp(nameof(AirControl),
                    AirControl, 0f, 1f, d.AirControl, warnings),
                StepHeightMeters = ConfigRule.Clamp(nameof(StepHeightMeters),
                    StepHeightMeters, 0f, 1f, d.StepHeightMeters, warnings),
                SlopeLimitDegrees = ConfigRule.Clamp(nameof(SlopeLimitDegrees),
                    SlopeLimitDegrees, 0f, 89f, d.SlopeLimitDegrees, warnings),
                CapsuleRadiusMeters = ConfigRule.Clamp(nameof(CapsuleRadiusMeters),
                    CapsuleRadiusMeters, 0.05f, 2f, d.CapsuleRadiusMeters, warnings),
                CapsuleHeightMeters = ConfigRule.Clamp(nameof(CapsuleHeightMeters),
                    CapsuleHeightMeters, 0.1f, 4f, d.CapsuleHeightMeters, warnings),
            };
        }
    }

    /// <summary>Staff Console camera tuning. Defaults equal the pre-config behaviour.</summary>
    [Serializable]
    public struct CameraSettings
    {
        public float PivotHeightMeters;
        public float LookDegreesPerPixel;
        public float MinPitchDegrees;
        public float MaxPitchDegrees;
        // Collision fields are validated now and read by a later ticket.
        public float CollisionRadiusMeters;
        public int CollisionLayerMask;

        public static CameraSettings Defaults => new CameraSettings
        {
            PivotHeightMeters = 0.9f,
            LookDegreesPerPixel = 0.15f,
            MinPitchDegrees = -20f,
            MaxPitchDegrees = 25f,
            CollisionRadiusMeters = 0.2f,
            CollisionLayerMask = -1,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public CameraSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var min = ConfigRule.Clamp(nameof(MinPitchDegrees),
                MinPitchDegrees, -89f, 89f, d.MinPitchDegrees, warnings);
            var max = ConfigRule.Clamp(nameof(MaxPitchDegrees),
                MaxPitchDegrees, -89f, 89f, d.MaxPitchDegrees, warnings);
            if (min > max)
            {
                warnings.Add($"MinPitchDegrees {min} is above MaxPitchDegrees {max}; " +
                    "using the default pitch limits.");
                min = d.MinPitchDegrees;
                max = d.MaxPitchDegrees;
            }

            return new CameraSettings
            {
                PivotHeightMeters = ConfigRule.Clamp(nameof(PivotHeightMeters),
                    PivotHeightMeters, 0f, 3f, d.PivotHeightMeters, warnings),
                LookDegreesPerPixel = ConfigRule.Clamp(nameof(LookDegreesPerPixel),
                    LookDegreesPerPixel, 0f, 5f, d.LookDegreesPerPixel, warnings),
                MinPitchDegrees = min,
                MaxPitchDegrees = max,
                CollisionRadiusMeters = ConfigRule.Clamp(nameof(CollisionRadiusMeters),
                    CollisionRadiusMeters, 0f, 2f, d.CollisionRadiusMeters, warnings),
                CollisionLayerMask = CollisionLayerMask,
            };
        }
    }
}
