using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JewelryShop.Core
{
    /// <summary>The buttons of the Robot Panel (ADR-0024, ADR-0026).</summary>
    public enum PanelButton
    {
        Enable, Disable, ClearError, Resume, Stop, Home, SuctionOn, SuctionOff, Speed, Jog, MoveTo,
        Preset,
    }

    /// <summary>One Preset Action as the Teleop Bridge reports it.</summary>
    public sealed class PresetInfo
    {
        public int Id;
        public string Name;
        public bool Valid;
        public string Reason;
    }

    /// <summary>The Preset that is running now (step n of m).</summary>
    public sealed class RunningPreset
    {
        public int Id;
        public int Step;
        public int Of;
    }

    /// <summary>What the panel shows. Mode is "enabled", "disabled", "error" or "unknown".</summary>
    public sealed class RobotPanelView
    {
        public bool LinkAlive;
        public string Mode = "unknown";
        public int ErrorId;
        public double X, Y, Z, R;
        public bool Suction;
        public bool StopLatched;
        /// <summary>The last command, its result ("done" / "refused" / "") and the reason.</summary>
        public string LastCommand = "", LastResult = "", LastReason = "";
        public IReadOnlyList<PresetInfo> Presets = new PresetInfo[0];
        /// <summary>Null when no Preset runs.</summary>
        public RunningPreset Running;
    }

    /// <summary>
    /// Typed commands for UDP 9000, one JSON object per packet, the shapes in
    /// Dobot/contract/teleop_examples.json. The Teleop Bridge stays the safety point
    /// (clamp, Z_MIN_MM, Stop latch): this only refuses what can never be valid.
    /// </summary>
    public static class RobotPanelCommands
    {
        private static readonly int[] JogSteps = { 1, 10, 50 };
        private static readonly int[] SpeedChoices = { 10, 30, 50 }; // ADR-0026: no 100 %

        public const int MaxPresetId = 9;

        public static byte[] Enable() => Bytes("{\"type\": \"enable\"}");
        public static byte[] Disable() => Bytes("{\"type\": \"disable\"}");
        public static byte[] ClearError() => Bytes("{\"type\": \"clear_error\"}");
        public static byte[] Resume() => Bytes("{\"type\": \"resume\"}");
        public static byte[] Stop() => Bytes("{\"type\": \"stop\"}");
        public static byte[] Home() => Bytes("{\"type\": \"home\"}");

        public static byte[] Suction(bool on) =>
            Bytes("{\"type\": \"grip\", \"value\": " + (on ? 1 : 0) + "}");

        /// <summary>Speed percent: 10, 30 or 50. Null for anything else.</summary>
        public static byte[] Speed(int percent) =>
            Array.IndexOf(SpeedChoices, percent) < 0
                ? null
                : Bytes("{\"type\": \"speed\", \"value\": " + percent + "}");

        /// <summary>Axis x, y or z; step +/-1, 10 or 50 mm. Null for anything else.</summary>
        public static byte[] Jog(string axis, int stepMm)
        {
            if (axis != "x" && axis != "y" && axis != "z") return null;
            if (Array.IndexOf(JogSteps, stepMm) < 0 && Array.IndexOf(JogSteps, -stepMm) < 0)
                return null; // not Math.Abs: it throws on int.MinValue
            return Bytes("{\"type\": \"jog\", \"axis\": \"" + axis + "\", \"step\": " + stepMm + "}");
        }

        /// <summary>Typed X/Y/Z/R text. Null when any of them is not a finite number.</summary>
        public static byte[] MoveTo(string x, string y, string z, string r)
        {
            if (!TryNumber(x, out var xv) || !TryNumber(y, out var yv)
                || !TryNumber(z, out var zv) || !TryNumber(r, out var rv)) return null;
            return Bytes("{\"type\": \"move_to\", \"x\": " + Fmt(xv) + ", \"y\": " + Fmt(yv)
                + ", \"z\": " + Fmt(zv) + ", \"r\": " + Fmt(rv) + "}");
        }

        /// <summary>Run Preset 1-9. Null for any other id.</summary>
        public static byte[] Preset(int id) =>
            id < 1 || id > MaxPresetId ? null : Bytes("{\"type\": \"preset\", \"id\": " + id + "}");

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);

        private static string Fmt(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// The Robot Panel model: Robot Status bytes and the current time in, what to show and which
    /// buttons are enabled out. No sockets, no clock (the caller passes seconds), no scene.
    /// </summary>
    public sealed class RobotPanel
    {
        /// <summary>A status older than this means the link is down (ADR-0024).</summary>
        public const double LinkTimeoutSeconds = 2.0;

        /// <summary>Packets larger than this are dropped unread (a status with 9 Presets is about 1 KB).</summary>
        public const int MaxPacketBytes = 4096;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private RobotPanelView _last = new RobotPanelView();
        private double _lastGoodAt;
        private bool _hasStatus;

        /// <summary>
        /// Takes one Robot Status packet. False, and the view unchanged, for anything that is
        /// malformed, oversized, not a status or has an unknown mode. Never throws.
        /// </summary>
        public bool Apply(byte[] data, double nowSeconds)
        {
            if (data == null || data.Length == 0 || data.Length > MaxPacketBytes) return false;
            string text;
            try { text = StrictUtf8.GetString(data); }
            catch (ArgumentException) { return false; }
            if (!JsonLite.TryParse(text, out var root) || !(root is Dictionary<string, object> m))
                return false;
            if (!TryDecode(m, out var view)) return false;
            _last = view;
            _lastGoodAt = nowSeconds;
            _hasStatus = true;
            return true;
        }

        /// <summary>The view at this time. Before any status, Mode is "unknown" and the link is down.</summary>
        public RobotPanelView View(double nowSeconds)
        {
            var v = _last;
            // A copy, so LinkAlive follows the time without mutating the stored status.
            return new RobotPanelView
            {
                LinkAlive = _hasStatus && nowSeconds - _lastGoodAt <= LinkTimeoutSeconds,
                Mode = v.Mode, ErrorId = v.ErrorId, X = v.X, Y = v.Y, Z = v.Z, R = v.R,
                Suction = v.Suction, StopLatched = v.StopLatched, LastCommand = v.LastCommand,
                LastResult = v.LastResult, LastReason = v.LastReason, Presets = v.Presets,
                Running = v.Running,
            };
        }

        /// <summary>
        /// Whether a button is enabled. Stop is always enabled: it is the software stop and the
        /// Bridge accepts it in any state. With the link down only the non-motion buttons stay on,
        /// since the Bridge answers (and learns the Quest's address) only after a packet arrives.
        /// <paramref name="id"/> is used for Preset only.
        /// </summary>
        public bool IsEnabled(PanelButton button, double nowSeconds, int id = 0)
        {
            if (button == PanelButton.Stop) return true;
            var v = View(nowSeconds);
            if (v.LinkAlive && v.Mode == "disabled") return button == PanelButton.Enable;
            var isMotion = button == PanelButton.Home || button == PanelButton.Jog
                || button == PanelButton.MoveTo || button == PanelButton.Preset;
            if (isMotion && (!v.LinkAlive || v.Mode != "enabled" || v.StopLatched)) return false;
            if (button != PanelButton.Preset) return true;
            if (v.Running != null) return false;
            foreach (var p in v.Presets) if (p.Id == id) return p.Valid;
            return false;
        }

        private static bool TryDecode(Dictionary<string, object> m, out RobotPanelView v)
        {
            v = null;
            if (!Str(m, "type", out var type) || type != "status") return false;
            if (!Str(m, "mode", out var mode)
                || (mode != "enabled" && mode != "disabled" && mode != "error" && mode != "unknown"))
                return false;
            if (!Int(m, "error_id", out var errorId)
                || !Num(m, "x", out var x) || !Num(m, "y", out var y)
                || !Num(m, "z", out var z) || !Num(m, "r", out var r)
                || !Bool(m, "suction", out var suction) || !Bool(m, "stop_latched", out var latched)
                || !Str(m, "last_command", out var command) || !Str(m, "result", out var result)
                || !Str(m, "reason", out var reason)) return false;
            if (!TryPresets(m, out var presets) || !TryRunning(m, out var running)) return false;
            v = new RobotPanelView
            {
                Mode = mode, ErrorId = errorId, X = x, Y = y, Z = z, R = r, Suction = suction,
                StopLatched = latched, LastCommand = command, LastResult = result,
                LastReason = reason, Presets = presets, Running = running,
            };
            return true;
        }

        // presets and running_preset are optional: a status without them has none.
        private static bool TryPresets(Dictionary<string, object> m, out List<PresetInfo> list)
        {
            list = new List<PresetInfo>();
            if (!m.TryGetValue("presets", out var raw) || raw == null) return true;
            if (!(raw is List<object> items) || items.Count > RobotPanelCommands.MaxPresetId)
                return false;
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> p)
                    || !Int(p, "id", out var id) || id < 1 || id > RobotPanelCommands.MaxPresetId
                    || !Str(p, "name", out var name) || !Bool(p, "valid", out var valid)
                    || !Str(p, "reason", out var reason)) return false;
                list.Add(new PresetInfo { Id = id, Name = name, Valid = valid, Reason = reason });
            }
            return true;
        }

        private static bool TryRunning(Dictionary<string, object> m, out RunningPreset running)
        {
            running = null;
            if (!m.TryGetValue("running_preset", out var raw) || raw == null) return true;
            if (!(raw is Dictionary<string, object> p)
                || !Int(p, "id", out var id) || !Int(p, "step", out var step)
                || !Int(p, "of", out var of)
                || id < 1 || id > RobotPanelCommands.MaxPresetId || step < 1 || step > of)
                return false;
            running = new RunningPreset { Id = id, Step = step, Of = of };
            return true;
        }

        private static bool Str(Dictionary<string, object> m, string key, out string s)
        {
            s = null;
            if (!m.TryGetValue(key, out var o) || !(o is string str)) return false;
            s = str;
            return true;
        }

        private static bool Bool(Dictionary<string, object> m, string key, out bool b)
        {
            b = false;
            if (!m.TryGetValue(key, out var o) || !(o is bool v)) return false;
            b = v;
            return true;
        }

        private static bool Num(Dictionary<string, object> m, string key, out double d)
        {
            d = 0;
            if (!m.TryGetValue(key, out var o) || !(o is double v)) return false;
            d = v;
            return true;
        }

        private static bool Int(Dictionary<string, object> m, string key, out int i)
        {
            i = 0;
            if (!Num(m, key, out var d) || d != Math.Floor(d) || Math.Abs(d) > int.MaxValue)
                return false;
            i = (int)d;
            return true;
        }
    }
}
