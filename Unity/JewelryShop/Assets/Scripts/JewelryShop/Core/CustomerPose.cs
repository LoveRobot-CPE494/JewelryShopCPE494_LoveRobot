using System;

namespace JewelryShop.Core
{
    /// <summary>What the Quest tells the Staff Console about the Customer, in world space.</summary>
    public struct CustomerPose
    {
        /// <summary>Head position in meters.</summary>
        public float HeadX, HeadY, HeadZ;
        /// <summary>Head yaw (turn about the up axis) in degrees, 0 to 360.</summary>
        public float HeadYawDegrees;
        /// <summary>Right controller position in meters.</summary>
        public float HandX, HandY, HandZ;
        /// <summary>Right controller rotation as a unit quaternion.</summary>
        public float HandRotX, HandRotY, HandRotZ, HandRotW;
        /// <summary>The Gem Model in the right hand, as <see cref="GemKeyCode"/>; None when empty.</summary>
        public byte GemCode;

        /// <summary>Position changes smaller than this (meters) are headset jitter, not sent.</summary>
        public const float PositionEpsilonMeters = 0.005f;
        /// <summary>Yaw changes smaller than this (degrees) are not sent.</summary>
        public const float YawEpsilonDegrees = 0.5f;
        /// <summary>Quaternion component changes smaller than this are not sent.</summary>
        public const float RotationEpsilon = 0.002f;

        /// <summary>True when something moved beyond the jitter epsilons (or the held Gem changed);
        /// gates network writes.</summary>
        public bool HasChanged(in CustomerPose other)
        {
            return GemCode != other.GemCode
                || Far(HeadX, other.HeadX, PositionEpsilonMeters)
                || Far(HeadY, other.HeadY, PositionEpsilonMeters)
                || Far(HeadZ, other.HeadZ, PositionEpsilonMeters)
                || Math.Abs(CustomerPoseSmoother.ShortestDeltaDegrees(HeadYawDegrees, other.HeadYawDegrees))
                    > YawEpsilonDegrees
                || Far(HandX, other.HandX, PositionEpsilonMeters)
                || Far(HandY, other.HandY, PositionEpsilonMeters)
                || Far(HandZ, other.HandZ, PositionEpsilonMeters)
                || Far(HandRotX, other.HandRotX, RotationEpsilon)
                || Far(HandRotY, other.HandRotY, RotationEpsilon)
                || Far(HandRotZ, other.HandRotZ, RotationEpsilon)
                || Far(HandRotW, other.HandRotW, RotationEpsilon);
        }

        private static bool Far(float a, float b, float epsilon) => Math.Abs(a - b) > epsilon;
    }

    /// <summary>
    /// A one-slot mailbox for the Customer's latest pose. The scene object that knows it writes
    /// it; the one that needs it reads it. The StaffLinkBehaviour owns it and hands it out, and
    /// Networking moves it across the link, so only Networking sees Netcode (ADR-0008/0010).
    /// </summary>
    public sealed class CustomerPoseMailbox
    {
        /// <summary>True while a Customer is attached and has published a pose.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>The last published pose; meaningful only while <see cref="IsAvailable"/>.</summary>
        public CustomerPose Latest { get; private set; }

        /// <summary>Store a new pose and mark the mailbox available.</summary>
        public void Publish(in CustomerPose pose)
        {
            Latest = pose;
            IsAvailable = true;
        }

        /// <summary>Drop the pose (disconnect, or the source stopped); readers see unavailable.</summary>
        public void Clear() => IsAvailable = false;
    }

    /// <summary>One byte on the wire for the held Gem: Cut * 8 + Color, 255 for none.</summary>
    public static class GemKeyCode
    {
        /// <summary>The code for "no Gem in hand".</summary>
        public const byte None = 255;

        /// <summary>The wire code of a Gem.</summary>
        public static byte Encode(GemKey key) => (byte)((int)key.Cut * 8 + (int)key.Color);

        /// <summary>The Gem for a wire code; false for <see cref="None"/> or an unknown code.</summary>
        public static bool TryDecode(byte code, out GemKey key)
        {
            var cut = code / 8;
            var color = code % 8;
            var isValid = code != None && Enum.IsDefined(typeof(GemCut), cut)
                && Enum.IsDefined(typeof(GemColor), color);
            key = isValid ? new GemKey((GemCut)cut, (GemColor)color) : default;
            return isValid;
        }
    }

    /// <summary>
    /// Smooths the Customer's head position and yaw, and snaps (no glide) when it jumps farther
    /// than the threshold: the link has no teleport event, so a jump is the signal.
    /// </summary>
    public sealed class CustomerPoseSmoother
    {
        /// <summary>A move larger than this between two updates counts as a teleport.</summary>
        public const float TeleportSnapMeters = 1f;
        private const float FollowRatePerSecond = 15f;

        /// <summary>The smoothed body placement.</summary>
        public struct Result
        {
            /// <summary>Position in meters.</summary>
            public float X, Y, Z;
            /// <summary>Yaw in degrees.</summary>
            public float YawDegrees;
            /// <summary>True when this step snapped (first sample or teleport) instead of gliding.</summary>
            public bool IsSnap;
        }

        private Result _current;
        private bool _hasValue;

        /// <summary>Forget the last placement; the next <see cref="Step"/> snaps.</summary>
        public void Reset() => _hasValue = false;

        /// <summary>Signed turn from <paramref name="from"/> to <paramref name="to"/> by the short
        /// way, in degrees (-180 to 180).</summary>
        public static float ShortestDeltaDegrees(float from, float to) =>
            ((to - from) % 360f + 540f) % 360f - 180f;

        /// <summary>Move toward the target by one frame; snaps on the first sample or a teleport.</summary>
        public Result Step(float x, float y, float z, float yawDegrees, float deltaSeconds)
        {
            var dx = x - _current.X;
            var dy = y - _current.Y;
            var dz = z - _current.Z;
            // Floor distance only: the avatar's feet ignore height.
            var isSnap = !_hasValue || dx * dx + dz * dz > TeleportSnapMeters * TeleportSnapMeters;
            if (isSnap)
                _current = new Result { X = x, Y = y, Z = z, YawDegrees = yawDegrees, IsSnap = true };
            else
            {
                _current.IsSnap = false;
                var t = 1f - (float)Math.Exp(-FollowRatePerSecond * Math.Max(0f, deltaSeconds));
                _current.X += dx * t;
                _current.Y += dy * t;
                _current.Z += dz * t;
                var yaw = _current.YawDegrees + ShortestDeltaDegrees(_current.YawDegrees, yawDegrees) * t;
                _current.YawDegrees = (yaw % 360f + 360f) % 360f;
            }
            _hasValue = true;
            return _current;
        }
    }

    /// <summary>
    /// Turns the smoothed body placement into the Animator's walk inputs: speed from the XZ
    /// movement, low-passed, with a dead zone. A snap (teleport) counts as no movement.
    /// </summary>
    public sealed class CustomerGait
    {
        /// <summary>Smoothed speed above this starts walking.</summary>
        public const float StartSpeedMetersPerSecond = 0.15f;
        /// <summary>Smoothed speed below this stops walking (lower than start: no flicker).</summary>
        public const float StopSpeedMetersPerSecond = 0.08f;
        /// <summary>Speed low-pass rate in 1/s (higher follows faster).</summary>
        private const float SmoothRatePerSecond = 8f;
        private readonly float _walkSpeedMetersPerSecond;

        /// <summary>The walk inputs for the Animator.</summary>
        public struct Output
        {
            /// <summary>True while walking.</summary>
            public bool IsMoving;
            /// <summary>Walk blend along the body's facing, -1 to 1.</summary>
            public float Forward;
            /// <summary>Walk blend to the body's right, -1 to 1.</summary>
            public float Right;
        }

        /// <param name="walkSpeedMetersPerSecond">Speed that maps to a full walk blend; pass the
        /// Staff Avatar's walk speed so one value tunes both.</param>
        public CustomerGait(float walkSpeedMetersPerSecond) =>
            _walkSpeedMetersPerSecond = Math.Max(0.01f, walkSpeedMetersPerSecond);

        private float _lastX, _lastZ, _velocityX, _velocityZ;
        private bool _hasValue, _isMoving;

        /// <summary>Forget the last placement; the next <see cref="Step"/> reports no walk.</summary>
        public void Reset()
        {
            _hasValue = false;
            _velocityX = _velocityZ = 0f;
            _isMoving = false;
        }

        /// <summary>One frame of the smoothed body placement.</summary>
        public Output Step(in CustomerPoseSmoother.Result body, float deltaSeconds)
        {
            if (!_hasValue || body.IsSnap)
            {
                _velocityX = _velocityZ = 0f;
                _isMoving = false;
            }
            else if (deltaSeconds > 0f)
            {
                var t = 1f - (float)Math.Exp(-SmoothRatePerSecond * deltaSeconds);
                _velocityX += ((body.X - _lastX) / deltaSeconds - _velocityX) * t;
                _velocityZ += ((body.Z - _lastZ) / deltaSeconds - _velocityZ) * t;
                var speed = (float)Math.Sqrt(_velocityX * _velocityX + _velocityZ * _velocityZ);
                _isMoving = speed > (_isMoving ? StopSpeedMetersPerSecond : StartSpeedMetersPerSecond);
            }
            _lastX = body.X;
            _lastZ = body.Z;
            _hasValue = true;
            if (!_isMoving) return default;

            var yaw = body.YawDegrees * (float)Math.PI / 180f;
            var sin = (float)Math.Sin(yaw);
            var cos = (float)Math.Cos(yaw);
            var forward = (_velocityX * sin + _velocityZ * cos) / _walkSpeedMetersPerSecond;
            var right = (_velocityX * cos - _velocityZ * sin) / _walkSpeedMetersPerSecond;
            var length = (float)Math.Sqrt(forward * forward + right * right);
            if (length > 1f) { forward /= length; right /= length; }
            return new Output { IsMoving = true, Forward = forward, Right = right };
        }
    }
}
