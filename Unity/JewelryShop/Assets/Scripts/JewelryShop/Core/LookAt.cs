using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>Remembered Look-At flag; PlayerPrefs-backed in the game, a fake in tests.</summary>
    public interface ILookAtStore
    {
        /// <summary>The stored flag, or null when nothing is stored.</summary>
        bool? Get();
        /// <summary>Remembers the flag.</summary>
        void Set(bool on);
    }

    /// <summary>Staff Avatar Look-At tuning (ADR-0020).</summary>
    [Serializable]
    public struct LookAtSettings
    {
        public bool DefaultOn;
        public float YawLimitDegrees;
        public float PitchLimitDegrees;
        public float SmoothingSeconds;
        /// <summary>The camera counts as in front within this yaw of the body's forward.</summary>
        public float FrontHalfAngleDegrees;
        /// <summary>Extra degrees past the half-angle before the camera mode switches.</summary>
        public float FrontHysteresisDegrees;
        /// <summary>Time the body takes to catch up with a target past the head's yaw limit.</summary>
        public float BodyTurnSeconds;

        public static LookAtSettings Defaults => new LookAtSettings
        {
            DefaultOn = false,
            YawLimitDegrees = 45f,
            PitchLimitDegrees = 25f,
            SmoothingSeconds = 0.15f,
            FrontHalfAngleDegrees = 90f,
            FrontHysteresisDegrees = 10f,
            BodyTurnSeconds = 0.25f,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public LookAtSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var half = ConfigRule.Clamp(nameof(FrontHalfAngleDegrees),
                FrontHalfAngleDegrees, 0f, 180f, d.FrontHalfAngleDegrees, warnings);
            var hysteresis = ConfigRule.Clamp(nameof(FrontHysteresisDegrees),
                FrontHysteresisDegrees, 0f, 180f, d.FrontHysteresisDegrees, warnings);
            if (hysteresis > half)
            {
                warnings.Add($"FrontHysteresisDegrees {hysteresis} is above " +
                    $"FrontHalfAngleDegrees {half}; using {half}.");
                hysteresis = half;
            }

            return new LookAtSettings
            {
                FrontHalfAngleDegrees = half,
                FrontHysteresisDegrees = hysteresis,
                BodyTurnSeconds = ConfigRule.Clamp(nameof(BodyTurnSeconds),
                    BodyTurnSeconds, 0f, 5f, d.BodyTurnSeconds, warnings),
                DefaultOn = DefaultOn,
                YawLimitDegrees = ConfigRule.Clamp(nameof(YawLimitDegrees),
                    YawLimitDegrees, 0f, 180f, d.YawLimitDegrees, warnings),
                PitchLimitDegrees = ConfigRule.Clamp(nameof(PitchLimitDegrees),
                    PitchLimitDegrees, 0f, 90f, d.PitchLimitDegrees, warnings),
                SmoothingSeconds = ConfigRule.Clamp(nameof(SmoothingSeconds),
                    SmoothingSeconds, 0f, 5f, d.SmoothingSeconds, warnings),
            };
        }
    }

    /// <summary>Head pose to apply: eased angles and the 0..1 weight over the animation pose.</summary>
    public struct LookAtPose
    {
        public float Yaw;
        public float Pitch;
        public float Weight;
    }

    /// <summary>Look-At rules (ADR-0020): toggle, limits, easing, status text.</summary>
    public static class LookAt
    {
        // exp(-3) is about 5%: the ease is 95% done after the smoothing time.
        private const float SettleRate = 3f;

        /// <summary>A stored value always wins; with none, the config default applies.</summary>
        public static bool IsOn(ILookAtStore store, LookAtSettings settings) =>
            store.Get() ?? settings.DefaultOn;

        /// <summary>Flips the flag, remembers it, and returns the new value.</summary>
        public static bool Toggle(ILookAtStore store, LookAtSettings settings)
        {
            var on = !IsOn(store, settings);
            store.Set(on);
            return on;
        }

        /// <summary>Clamps to the limits; angles past 180 take the shortest signed way round.</summary>
        public static (float Yaw, float Pitch) Clamp(float yawDegrees, float pitchDegrees,
            LookAtSettings s)
        {
            return (Symmetric(Normalize(yawDegrees), s.YawLimitDegrees),
                Symmetric(pitchDegrees, s.PitchLimitDegrees));
        }

        /// <summary>Wraps to (-180, 180]; exactly -180 becomes +180, the right-hand side.</summary>
        public static float Normalize(float yawDegrees)
        {
            var yaw = yawDegrees % 360f;
            if (yaw > 180f) yaw -= 360f;
            else if (yaw <= -180f) yaw += 360f;
            return yaw;
        }

        /// <summary>Yaw the target sits past the head's limit (signed), 0 when inside it.</summary>
        public static float ExcessYaw(float yawDegrees, LookAtSettings s)
        {
            var yaw = Normalize(yawDegrees);
            return yaw - Symmetric(yaw, s.YawLimitDegrees);
        }

        /// <summary>The share of the excess yaw the body turns this frame.</summary>
        public static float BodyStep(float excessYaw, float deltaSeconds, LookAtSettings s) =>
            Ease(0f, excessYaw, deltaSeconds, s.BodyTurnSeconds);

        private static float Symmetric(float value, float limit) =>
            Math.Max(-limit, Math.Min(limit, value));

        /// <summary>Clamped yaw and pitch of a direction in the body's local space (z forward).</summary>
        public static (float Yaw, float Pitch) Angles(float x, float y, float z, LookAtSettings s)
        {
            var (yaw, pitch) = RawAngles(x, y, z);
            return Clamp(yaw, pitch, s);
        }

        /// <summary>Unclamped yaw and pitch of a body-local direction.</summary>
        public static (float Yaw, float Pitch) RawAngles(float x, float y, float z)
        {
            // x + 0f turns -0 into +0, so a target exactly behind is +180, not -180.
            var yaw = Math.Atan2(x + 0f, z) * 180.0 / Math.PI;
            var pitch = Math.Atan2(y, Math.Sqrt(x * x + z * z)) * 180.0 / Math.PI;
            return ((float)yaw, (float)pitch);
        }

        /// <summary>Exponential ease; about 95% of the way after <paramref name="seconds"/>.</summary>
        public static float Ease(float current, float target, float deltaSeconds, float seconds)
        {
            if (seconds <= 0f) return target;
            return current + (target - current) *
                (1f - (float)Math.Exp(-SettleRate * deltaSeconds / seconds));
        }

        /// <summary>The panel status line.</summary>
        public static string StatusText(bool isOn) =>
            isOn ? "Look-At: ON · Target: camera" : "Look-At: OFF";
    }

    /// <summary>
    /// Picks what the head aims at for the Camera target (ADR-0020): the camera's position while
    /// it is in front, the direction it faces while it is behind; the switch has hysteresis.
    /// </summary>
    public sealed class CameraAim
    {
        /// <summary>Whether the camera is currently treated as in front of the body.</summary>
        public bool InFront { get; private set; }

        /// <summary>
        /// Unclamped yaw and pitch to aim at. Directions are body-local (z forward): to the
        /// camera from the head, then the way the camera faces.
        /// </summary>
        public (float Yaw, float Pitch) Aim(float toX, float toY, float toZ,
            float faceX, float faceY, float faceZ, LookAtSettings s)
        {
            var toYaw = Math.Abs(LookAt.RawAngles(toX, toY, toZ).Yaw);
            if (InFront && toYaw > s.FrontHalfAngleDegrees + s.FrontHysteresisDegrees)
                InFront = false;
            else if (!InFront && toYaw < s.FrontHalfAngleDegrees - s.FrontHysteresisDegrees)
                InFront = true;
            return InFront ? LookAt.RawAngles(toX, toY, toZ) : LookAt.RawAngles(faceX, faceY, faceZ);
        }
    }

    /// <summary>Eases the head angles and the weight over the animation pose.</summary>
    public sealed class LookAtHead
    {
        // Below this weight an OFF head counts as back on the animation pose.
        private const float OffWeightCutoff = 0.0005f;

        private float _yaw;
        private float _pitch;
        private float _weight;

        /// <summary>One frame: angles follow the target only while ON; the weight fades in or out.</summary>
        public LookAtPose Tick(float deltaSeconds, bool isOn, float targetYaw, float targetPitch,
            LookAtSettings s)
        {
            // While fully off the angles track the target, so switching on only eases the weight.
            if (_weight <= 0f && isOn)
            {
                _yaw = targetYaw;
                _pitch = targetPitch;
            }
            else if (isOn)
            {
                _yaw = LookAt.Ease(_yaw, targetYaw, deltaSeconds, s.SmoothingSeconds);
                _pitch = LookAt.Ease(_pitch, targetPitch, deltaSeconds, s.SmoothingSeconds);
            }

            _weight = LookAt.Ease(_weight, isOn ? 1f : 0f, deltaSeconds, s.SmoothingSeconds);
            if (!isOn && _weight < OffWeightCutoff) _weight = 0f;
            return new LookAtPose { Yaw = _yaw, Pitch = _pitch, Weight = _weight };
        }
    }
}
