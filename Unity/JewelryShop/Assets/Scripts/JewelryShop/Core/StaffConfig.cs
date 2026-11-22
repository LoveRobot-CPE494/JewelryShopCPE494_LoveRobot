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

        /// <summary>Like Clamp, but an out-of-range value falls back to the default.</summary>
        internal static float DefaultIfOutside(string name, float value, float min, float max,
            float fallback, List<string> warnings)
        {
            if (value >= min && value <= max)
                return value;
            warnings.Add($"{name} {value} is outside {min}..{max}; using {fallback}.");
            return fallback;
        }
    }

    /// <summary>Staff Avatar movement tuning. Defaults equal the pre-config behaviour.</summary>
    [Serializable]
    public struct MovementSettings
    {
        public float WalkSpeedMetersPerSecond;
        /// <summary>Target ground speed while Shift and movement are held.</summary>
        public float RunSpeedMetersPerSecond;
        /// <summary>Time for the ground gait to blend all the way from walk to run.</summary>
        public float RunBlendSeconds;
        /// <summary>Running clip playback multiplier at full run speed.</summary>
        public float RunPlaybackRate;
        public float AccelerationMetersPerSecondSquared;
        public float TurnDegreesPerSecond;
        public float GravityMetersPerSecondSquared;
        // The jump uses the same gravity as falling off a ledge.
        public float JumpHeightMeters;
        public float JumpTakeoffDelaySeconds;
        /// <summary>Share of walk acceleration kept in the air (0..1); no air speed cap.</summary>
        public float AirControl;
        public float StepHeightMeters;
        public float SlopeLimitDegrees;
        public float CapsuleRadiusMeters;
        public float CapsuleHeightMeters;
        public float KillPlaneYMeters;

        public static MovementSettings Defaults => new MovementSettings
        {
            WalkSpeedMetersPerSecond = 1f,
            RunSpeedMetersPerSecond = 2.4f,
            RunBlendSeconds = 0.16f,
            RunPlaybackRate = 1f,
            AccelerationMetersPerSecondSquared = 8f,
            TurnDegreesPerSecond = 540f,
            GravityMetersPerSecondSquared = 9.81f,
            JumpHeightMeters = 0.5f,
            JumpTakeoffDelaySeconds = 0.14f,
            AirControl = 0.7f,
            StepHeightMeters = 0.3f,
            SlopeLimitDegrees = 45f,
            CapsuleRadiusMeters = 0.3f,
            CapsuleHeightMeters = 1.7f,
            KillPlaneYMeters = -10f,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public MovementSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var walkSpeedMetersPerSecond = ConfigRule.Clamp(nameof(WalkSpeedMetersPerSecond),
                WalkSpeedMetersPerSecond, 0f, 10f, d.WalkSpeedMetersPerSecond, warnings);
            var minimumRunSpeedMetersPerSecond = walkSpeedMetersPerSecond + 0.1f;
            var defaultRunSpeedMetersPerSecond = Math.Max(d.RunSpeedMetersPerSecond,
                minimumRunSpeedMetersPerSecond);
            return new MovementSettings
            {
                WalkSpeedMetersPerSecond = walkSpeedMetersPerSecond,
                RunSpeedMetersPerSecond = ClampRunSetting(nameof(RunSpeedMetersPerSecond),
                    RunSpeedMetersPerSecond, minimumRunSpeedMetersPerSecond, 15f,
                    defaultRunSpeedMetersPerSecond, warnings),
                RunBlendSeconds = ClampRunSetting(nameof(RunBlendSeconds), RunBlendSeconds,
                    0.01f, 2f, d.RunBlendSeconds, warnings),
                RunPlaybackRate = ClampRunSetting(nameof(RunPlaybackRate), RunPlaybackRate,
                    0.1f, 3f, d.RunPlaybackRate, warnings),
                AccelerationMetersPerSecondSquared = ConfigRule.Clamp(
                    nameof(AccelerationMetersPerSecondSquared),
                    AccelerationMetersPerSecondSquared, 0.1f, 100f,
                    d.AccelerationMetersPerSecondSquared, warnings),
                TurnDegreesPerSecond = ConfigRule.Clamp(nameof(TurnDegreesPerSecond),
                    TurnDegreesPerSecond, 1f, 1440f, d.TurnDegreesPerSecond, warnings),
                GravityMetersPerSecondSquared = ConfigRule.Clamp(
                    nameof(GravityMetersPerSecondSquared),
                    GravityMetersPerSecondSquared, 0f, 50f,
                    d.GravityMetersPerSecondSquared, warnings),
                JumpHeightMeters = ConfigRule.Clamp(nameof(JumpHeightMeters),
                    JumpHeightMeters, 0f, 3f, d.JumpHeightMeters, warnings),
                JumpTakeoffDelaySeconds = ConfigRule.Clamp(nameof(JumpTakeoffDelaySeconds),
                    JumpTakeoffDelaySeconds, 0f, 2f, d.JumpTakeoffDelaySeconds, warnings),
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
                KillPlaneYMeters = ConfigRule.Clamp(nameof(KillPlaneYMeters),
                    KillPlaneYMeters, -1000f, 1000f, d.KillPlaneYMeters, warnings),
            };
        }

        /// <summary>Run weight and playback target for the actual ground speed.</summary>
        public (float Weight, float PlaybackRate) RunGait(float groundSpeedMetersPerSecond)
        {
            var runFraction = (groundSpeedMetersPerSecond - WalkSpeedMetersPerSecond) /
                (RunSpeedMetersPerSecond - WalkSpeedMetersPerSecond);
            var playbackFraction = groundSpeedMetersPerSecond / RunSpeedMetersPerSecond;
            return (Math.Min(1f, Math.Max(0f, runFraction)),
                Math.Min(1f, Math.Max(0f, playbackFraction)) * RunPlaybackRate);
        }

        private static float ClampRunSetting(string name, float value, float min, float max,
            float fallback, List<string> warnings)
        {
            // An older serialized asset has zero in each missing run field.
            return value == 0f ? ConfigRule.DefaultIfOutside(name, value, min, max, fallback,
                warnings) : ConfigRule.Clamp(name, value, min, max, fallback, warnings);
        }
    }

    /// <summary>Staff Console camera tuning. Defaults equal the pre-config behaviour.</summary>
    [Serializable]
    public struct CameraSettings
    {
        /// <summary>Physics layer of walls and ceilings, the only camera blockers.</summary>
        public const int CameraBlockingLayer = 6;

        // No [FormerlySerializedAs("PivotHeightMeters")]: Core has no UnityEngine, and the old
        // value (0.9, mid-body) must not become the eye height; 0 reads as unset and defaults.
        /// <summary>Camera height above the feet: third-person pivot, first-person eye.</summary>
        public float EyeHeightMeters;
        public float LookDegreesPerPixel;
        public float MinPitchDegrees;
        public float MaxPitchDegrees;
        public float CollisionRadiusMeters;
        public int CollisionLayerMask;
        /// <summary>How fast the camera eases back out after a wall stops blocking it.</summary>
        public float CollisionRelaxMetersPerSecond;
        /// <summary>Clearance at which a wall fully raises the third-person camera.</summary>
        public float FirstPersonThresholdMeters;
        /// <summary>First-person camera shift forward of the eye.</summary>
        public float FirstPersonForwardMeters;
        /// <summary>Furthest first-person look-down, so the neck opening stays hidden.</summary>
        public float FirstPersonMaxLookDownDegrees;

        public static CameraSettings Defaults => new CameraSettings
        {
            EyeHeightMeters = 1.6f,
            LookDegreesPerPixel = 0.15f,
            MinPitchDegrees = -20f,
            MaxPitchDegrees = 25f,
            CollisionRadiusMeters = 0.2f,
            CollisionLayerMask = 1 << CameraBlockingLayer,
            CollisionRelaxMetersPerSecond = 6f,
            FirstPersonThresholdMeters = 0.6f,
            FirstPersonForwardMeters = 0.1f,
            FirstPersonMaxLookDownDegrees = 20f,
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
                // 0 is also what an asset saved before the field existed reads: warn and default.
                EyeHeightMeters = ConfigRule.DefaultIfOutside(nameof(EyeHeightMeters),
                    EyeHeightMeters, 0.1f, 3f, d.EyeHeightMeters, warnings),
                LookDegreesPerPixel = ConfigRule.Clamp(nameof(LookDegreesPerPixel),
                    LookDegreesPerPixel, 0f, 5f, d.LookDegreesPerPixel, warnings),
                MinPitchDegrees = min,
                MaxPitchDegrees = max,
                CollisionRadiusMeters = ConfigRule.Clamp(nameof(CollisionRadiusMeters),
                    CollisionRadiusMeters, 0f, 2f, d.CollisionRadiusMeters, warnings),
                CollisionLayerMask = CollisionLayerMask,
                // 0 is what a config saved before this field existed reads back: "unset".
                CollisionRelaxMetersPerSecond = CollisionRelaxMetersPerSecond == 0f ?
                    d.CollisionRelaxMetersPerSecond : ConfigRule.Clamp(
                        nameof(CollisionRelaxMetersPerSecond), CollisionRelaxMetersPerSecond,
                        0.1f, 100f, d.CollisionRelaxMetersPerSecond, warnings),
                FirstPersonThresholdMeters = ConfigRule.DefaultIfOutside(
                    nameof(FirstPersonThresholdMeters), FirstPersonThresholdMeters,
                    0.05f, 5f, d.FirstPersonThresholdMeters, warnings),
                FirstPersonForwardMeters = ConfigRule.Clamp(nameof(FirstPersonForwardMeters),
                    FirstPersonForwardMeters, 0f, 0.5f, d.FirstPersonForwardMeters, warnings),
                FirstPersonMaxLookDownDegrees = ConfigRule.Clamp(
                    nameof(FirstPersonMaxLookDownDegrees), FirstPersonMaxLookDownDegrees,
                    0f, 89f, d.FirstPersonMaxLookDownDegrees, warnings),
            };
        }
    }

    /// <summary>One gesture: a number-row key digit (1-9) and the Animator state it plays.</summary>
    [Serializable]
    public struct GestureEntry
    {
        public int Digit;
        public string StateName;
    }

    /// <summary>Key-to-gesture map and the crossfade shared by gestures and the jump.</summary>
    [Serializable]
    public struct GestureMapSettings
    {
        public GestureEntry[] Entries;
        public float BlendSeconds;

        public static GestureMapSettings Defaults => new GestureMapSettings
        {
            Entries = new[]
            {
                new GestureEntry { Digit = 1, StateName = "Base Layer.Wave" },
                new GestureEntry { Digit = 2, StateName = "Base Layer.Bow" },
                new GestureEntry { Digit = 3, StateName = "Base Layer.Point" },
                new GestureEntry { Digit = 4, StateName = "Base Layer.Nod" },
                new GestureEntry { Digit = 5, StateName = "Base Layer.Shake" },
                new GestureEntry { Digit = 6, StateName = "Base Layer.Embarrassed" },
            },
            BlendSeconds = 0.08f,
        };

        /// <summary>A clean copy: bad or duplicate entries dropped, an empty map reset to defaults.</summary>
        public GestureMapSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var kept = new List<GestureEntry>();
            foreach (var entry in Entries ?? Array.Empty<GestureEntry>())
            {
                if (entry.Digit < 1 || entry.Digit > 9)
                    warnings.Add($"Gesture key digit {entry.Digit} is outside 1..9; entry dropped.");
                else if (string.IsNullOrWhiteSpace(entry.StateName))
                    warnings.Add($"Gesture key {entry.Digit} has an empty state name; entry dropped.");
                else if (kept.Exists(k => k.Digit == entry.Digit))
                    warnings.Add($"Gesture key {entry.Digit} is mapped twice; later entry dropped.");
                else
                    kept.Add(entry);
            }

            if (kept.Count == 0)
            {
                warnings.Add("Gesture map has no valid entries; using the default map.");
                kept.AddRange(d.Entries);
            }

            return new GestureMapSettings
            {
                Entries = kept.ToArray(),
                BlendSeconds = ConfigRule.Clamp(nameof(BlendSeconds),
                    BlendSeconds, 0f, 1f, d.BlendSeconds, warnings),
            };
        }
    }

    /// <summary>Timing for the optional Standby variations.</summary>
    [Serializable]
    public struct IdleSettings
    {
        public float MinIntervalSeconds;
        public float MaxIntervalSeconds;
        public float BlendSeconds;

        public static IdleSettings Defaults => new IdleSettings
        {
            MinIntervalSeconds = 5f,
            MaxIntervalSeconds = 9f,
            BlendSeconds = 0.3f,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public IdleSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var min = ConfigRule.Clamp(nameof(MinIntervalSeconds),
                MinIntervalSeconds, 0f, 600f, d.MinIntervalSeconds, warnings);
            var max = ConfigRule.Clamp(nameof(MaxIntervalSeconds),
                MaxIntervalSeconds, 0f, 600f, d.MaxIntervalSeconds, warnings);
            if (min > max)
            {
                warnings.Add($"MinIntervalSeconds {min} is above MaxIntervalSeconds {max}; " +
                    "using the default interval.");
                min = d.MinIntervalSeconds;
                max = d.MaxIntervalSeconds;
            }

            return new IdleSettings
            {
                MinIntervalSeconds = min,
                MaxIntervalSeconds = max,
                BlendSeconds = ConfigRule.Clamp(nameof(BlendSeconds),
                    BlendSeconds, 0f, 5f, d.BlendSeconds, warnings),
            };
        }
    }

    /// <summary>Skirt sway and flare tuning. Defaults equal the pre-config behaviour.</summary>
    [Serializable]
    public struct SkirtSettings
    {
        private const float ClearanceStartMeters = 0.12f;
        private const float FullClearanceMeters = 0.28f;
        private const float RunClearanceReleaseMetersPerSecond = 0.35f;
        private const float GroundClearanceReleaseMetersPerSecond = 0.9f;

        public float SwingDegrees;
        public float FlareDegrees;
        public float FlareSpeedDegreesPerSecond;
        public float BowUprightWeight;
        /// <summary>Maximum outward angle added to the skirt anchors at full run blend.</summary>
        public float RunFlareDegrees;
        /// <summary>Forward clearance for the skirt anchors when a running knee lifts.</summary>
        public float RunFrontClearanceMeters;

        public static SkirtSettings Defaults => new SkirtSettings
        {
            SwingDegrees = 6f,
            FlareDegrees = 8f,
            FlareSpeedDegreesPerSecond = 80f,
            BowUprightWeight = 0.8f,
            RunFlareDegrees = 22f,
            RunFrontClearanceMeters = 0.2f,
        };

        /// <summary>Forward clearance for the current running knee lead.</summary>
        public float RunKneeClearance(float kneeLeadMeters)
        {
            var weight = (kneeLeadMeters - ClearanceStartMeters) /
                (FullClearanceMeters - ClearanceStartMeters);
            return RunFrontClearanceMeters * Math.Min(1f, Math.Max(0f, weight));
        }

        /// <summary>Clears a rising knee immediately and eases the skirt back between strides.</summary>
        public static float ReleaseRunClearance(float currentMeters, float requiredMeters,
            float runWeight, float elapsedSeconds)
        {
            var releaseMetersPerSecond = GroundClearanceReleaseMetersPerSecond +
                (RunClearanceReleaseMetersPerSecond - GroundClearanceReleaseMetersPerSecond) *
                runWeight;
            return Math.Max(requiredMeters, currentMeters - releaseMetersPerSecond * elapsedSeconds);
        }

        /// <summary>Run flare eases outward from the side and rear toward the front.</summary>
        public float RunAnchorFlare(float frontWeight) =>
            RunFlareDegrees * (0.3f + 0.7f * frontWeight);

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public SkirtSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            return new SkirtSettings
            {
                SwingDegrees = ConfigRule.Clamp(nameof(SwingDegrees),
                    SwingDegrees, 0f, 90f, d.SwingDegrees, warnings),
                FlareDegrees = ConfigRule.Clamp(nameof(FlareDegrees),
                    FlareDegrees, 0f, 60f, d.FlareDegrees, warnings),
                FlareSpeedDegreesPerSecond = ConfigRule.Clamp(
                    nameof(FlareSpeedDegreesPerSecond),
                    FlareSpeedDegreesPerSecond, 1f, 1000f, d.FlareSpeedDegreesPerSecond, warnings),
                BowUprightWeight = ConfigRule.Clamp(nameof(BowUprightWeight),
                    BowUprightWeight, 0f, 1f, d.BowUprightWeight, warnings),
                RunFlareDegrees = ConfigRule.Clamp(nameof(RunFlareDegrees),
                    RunFlareDegrees, 0f, 30f, d.RunFlareDegrees, warnings),
                RunFrontClearanceMeters = ConfigRule.Clamp(nameof(RunFrontClearanceMeters),
                    RunFrontClearanceMeters, 0f, 0.2f, d.RunFrontClearanceMeters, warnings),
            };
        }
    }
}
