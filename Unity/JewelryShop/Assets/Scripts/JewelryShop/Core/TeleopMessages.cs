using System.Globalization;

namespace JewelryShop.Core
{
    /// <summary>
    /// The Teleop UDP message text (ADR-0017). Byte-identical to what the Teleop Bridge parses;
    /// pinned by tests. shortcut: hand-built strings, typed messages per ADR-0008 later.
    /// </summary>
    public static class TeleopMessages
    {
        /// <summary>Let go: the arm stops following (Grip released).</summary>
        public const string Release = "{\"type\":\"release\"}";

        /// <summary>Emergency stop (B); latched until the next engage.</summary>
        public const string Stop = "{\"type\":\"stop\"}";

        /// <summary>Go to the Home pose (A).</summary>
        public const string Home = "{\"type\":\"home\"}";

        /// <summary>Grip pressed: this controller position now maps to the arm's position.
        /// </summary>
        public static string Engage(float x, float y, float z) =>
            $"{{\"type\":\"engage\",\"x\":{F(x)},\"y\":{F(y)},\"z\":{F(z)}}}";

        /// <summary>Suction on or off (Trigger).</summary>
        public static string Grip(bool on) => $"{{\"type\":\"grip\",\"value\":{(on ? 1 : 0)}}}";

        /// <summary>Controller position while engaged, in headset tracking space (metres).
        /// </summary>
        public static string Pose(float x, float y, float z) =>
            $"{{\"type\":\"pose\",\"frame\":\"headset\",\"x\":{F(x)},\"y\":{F(y)},\"z\":{F(z)}}}";

        private static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// "Has sent recently": UDP has no acknowledgement, so this only says a packet left.
    /// </summary>
    public sealed class TeleopSendTracker
    {
        /// <summary>A send counts as recent for this long (seconds).</summary>
        public const double RecentWindowSeconds = 1.0;

        private bool _any;

        /// <summary>Clock time of the last send; meaningless before the first.</summary>
        public double LastSentTime { get; private set; }

        /// <summary>Records a send at the given clock time.</summary>
        public void MarkSent(double now)
        {
            _any = true;
            LastSentTime = now;
        }

        /// <summary>True when a packet left within <see cref="RecentWindowSeconds"/>.</summary>
        public bool HasSentRecently(double now) =>
            _any && now - LastSentTime <= RecentWindowSeconds;
    }

    /// <summary>
    /// Where Teleop sends: follows the resolved Robot Address and says when to reconnect.
    /// A change while engaged releases the old target first, so the arm is never left
    /// following a headset that has moved on. A null address is a change too (stop sending).
    /// </summary>
    public sealed class TeleopTarget
    {
        private bool _chosen;

        /// <summary>The current target, or null when there is no valid Robot Address.</summary>
        public string Address { get; private set; }

        /// <summary>
        /// True when <paramref name="resolved"/> differs from the current target (or on the
        /// first call): the caller reconnects, or stops sending when Address is null.
        /// <paramref name="releaseOld"/>: send Release to the old target before closing it.
        /// </summary>
        public bool TryRetarget(string resolved, bool engaged, out bool releaseOld)
        {
            releaseOld = false;
            if (_chosen && resolved == Address) return false;
            releaseOld = engaged && Address != null;
            _chosen = true;
            Address = resolved;
            return true;
        }
    }
}
