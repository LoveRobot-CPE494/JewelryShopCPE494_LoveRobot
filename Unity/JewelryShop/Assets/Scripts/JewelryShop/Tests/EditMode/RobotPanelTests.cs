using System.Collections.Generic;
using System.IO;
using System.Text;
using JewelryShop.Core;
using NUnit.Framework;
using UnityEngine;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class RobotPanelTests
    {
        // Messages are compared as parsed JSON (key order and number formatting do not matter).
        private static Dictionary<string, object> Contract() =>
            (Dictionary<string, object>)Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "Dobot", "contract", "teleop_examples.json"))));

        private static object Parse(string json)
        {
            Assert.That(JsonLite.TryParse(json, out var v), Is.True, json);
            return v;
        }

        private static object Example(string section, string name)
        {
            foreach (Dictionary<string, object> e in (List<object>)Contract()[section])
                if ((string)e["name"] == name) return e["message"];
            Assert.Fail("no contract example " + name);
            return null;
        }

        private static void AssertSameJson(object expected, byte[] actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(Canon(Parse(Encoding.UTF8.GetString(actual))), Is.EqualTo(Canon(expected)));
        }

        // Key-sorted text so two parsed trees compare by value.
        private static string Canon(object o)
        {
            if (o is Dictionary<string, object> d)
            {
                var keys = new List<string>(d.Keys);
                keys.Sort(string.CompareOrdinal);
                var parts = new List<string>();
                foreach (var k in keys) parts.Add(k + "=" + Canon(d[k]));
                return "{" + string.Join(",", parts) + "}";
            }
            if (o is List<object> l)
            {
                var parts = new List<string>();
                foreach (var i in l) parts.Add(Canon(i));
                return "[" + string.Join(",", parts) + "]";
            }
            return o == null ? "null" : o.GetType().Name + ":" + o;
        }

        private static byte[] StatusBytes(string name) =>
            Encoding.UTF8.GetBytes(ToJson(Example("status", name)));

        // Re-serialises a contract status so the model reads the contract content as bytes.
        private static string ToJson(object o)
        {
            if (o is Dictionary<string, object> d)
            {
                var parts = new List<string>();
                foreach (var kv in d) parts.Add("\"" + kv.Key + "\":" + ToJson(kv.Value));
                return "{" + string.Join(",", parts) + "}";
            }
            if (o is string s) return "\"" + s + "\"";
            if (o is bool b) return b ? "true" : "false";
            return System.Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static RobotPanel PanelWith(string statusName, double at = 0)
        {
            var panel = new RobotPanel();
            Assert.That(panel.Apply(StatusBytes(statusName), at), Is.True);
            return panel;
        }

        // shortcut: these two use inline JSON; switch to the contract examples (preset_1 and the
        // status preset_running) once #219 merges.
        private const string StatusWithPresets =
            "{\"type\":\"status\",\"mode\":\"enabled\",\"error_id\":0,\"x\":300.0,\"y\":0.0,\"z\":50.0," +
            "\"r\":0.0,\"suction\":false,\"stop_latched\":false,\"last_command\":\"\",\"result\":\"\"," +
            "\"reason\":\"\",\"presets\":[{\"id\":1,\"name\":\"Square\",\"valid\":true,\"reason\":\"\"}," +
            "{\"id\":2,\"name\":\"Bad\",\"valid\":false,\"reason\":\"z too low\"}]," +
            "\"running_preset\":RUNNING}";

        private static byte[] WithPresets(string running) =>
            Encoding.UTF8.GetBytes(StatusWithPresets.Replace("RUNNING", running));

        // ---- commands ----

        [TestCase("enable", "Enable")]
        [TestCase("disable", "Disable")]
        [TestCase("clear_error", "ClearError")]
        [TestCase("resume", "Resume")]
        [TestCase("stop", "Stop")]
        [TestCase("home", "Home")]
        public void Command_MatchesContractExample(string example, string method)
        {
            var bytes = (byte[])typeof(RobotPanelCommands).GetMethod(method).Invoke(null, null);
            AssertSameJson(Example("commands", example), bytes);
        }

        [Test]
        public void Suction_MatchesContractExamples()
        {
            AssertSameJson(Example("commands", "grip_on"), RobotPanelCommands.Suction(true));
            AssertSameJson(Example("commands", "grip_off"), RobotPanelCommands.Suction(false));
        }

        [Test]
        public void Speed_MatchesContractExample_AndOnlyHeadsetChoices()
        {
            AssertSameJson(Example("commands", "speed"), RobotPanelCommands.Speed(30));
            Assert.That(RobotPanelCommands.Speed(10), Is.Not.Null);
            Assert.That(RobotPanelCommands.Speed(50), Is.Not.Null);
            Assert.That(RobotPanelCommands.Speed(100), Is.Null);
            Assert.That(RobotPanelCommands.Speed(0), Is.Null);
        }

        [Test]
        public void Jog_MatchesContractExamples()
        {
            AssertSameJson(Example("commands", "jog_x_plus_10"), RobotPanelCommands.Jog("x", 10));
            AssertSameJson(Example("commands", "jog_z_minus_1"), RobotPanelCommands.Jog("z", -1));
            AssertSameJson(Example("commands", "jog_y_plus_50"), RobotPanelCommands.Jog("y", 50));
        }

        [TestCase("x", 5)]
        [TestCase("x", 0)]
        [TestCase("x", 100)]
        [TestCase("x", int.MinValue)]
        [TestCase("r", 10)]
        [TestCase("X", 10)]
        [TestCase(null, 10)]
        public void Jog_BadAxisOrStep_IsRejected(string axis, int step)
        {
            Assert.That(RobotPanelCommands.Jog(axis, step), Is.Null);
        }

        [Test]
        public void MoveTo_MatchesContractExample()
        {
            AssertSameJson(Example("commands", "move_to"),
                RobotPanelCommands.MoveTo("300", "0", "50.0", "0"));
        }

        [TestCase("abc", "0", "0", "0")]
        [TestCase("1", "", "0", "0")]
        [TestCase("1", "0", null, "0")]
        [TestCase("1", "0", "0", "NaN")]
        [TestCase("Infinity", "0", "0", "0")]
        public void MoveTo_NotANumber_IsRejected(string x, string y, string z, string r)
        {
            Assert.That(RobotPanelCommands.MoveTo(x, y, z, r), Is.Null);
        }

        [Test]
        public void Preset_BuildsMessage_ForIds1To9Only()
        {
            AssertSameJson(Parse("{\"type\": \"preset\", \"id\": 1}"), RobotPanelCommands.Preset(1));
            Assert.That(RobotPanelCommands.Preset(9), Is.Not.Null);
            Assert.That(RobotPanelCommands.Preset(0), Is.Null);
            Assert.That(RobotPanelCommands.Preset(10), Is.Null);
        }

        // ---- status decode ----

        [Test]
        public void Apply_ContractStatus_DecodesIntoView()
        {
            var view = PanelWith("home_refused_after_stop").View(0);

            Assert.That(view.Mode, Is.EqualTo("enabled"));
            Assert.That(view.StopLatched, Is.True);
            Assert.That(view.X, Is.EqualTo(300.0));
            Assert.That(view.Z, Is.EqualTo(50.0));
            Assert.That(view.LastCommand, Is.EqualTo("home"));
            Assert.That(view.LastResult, Is.EqualTo("refused"));
            Assert.That(view.LastReason, Is.EqualTo("stop latched"));
            Assert.That(PanelWith("arm_error").View(0).ErrorId, Is.EqualTo(22));
            Assert.That(PanelWith("jog_done").View(0).Suction, Is.True);
        }

        [Test]
        public void Apply_StatusWithoutPresets_HasEmptyListAndNoneRunning()
        {
            var view = PanelWith("idle").View(0);
            Assert.That(view.Presets, Is.Empty);
            Assert.That(view.Running, Is.Null);
        }

        [Test]
        public void Apply_StatusWithPresets_ExposesListAndRunning()
        {
            var panel = new RobotPanel();
            Assert.That(panel.Apply(WithPresets("{\"id\":1,\"step\":2,\"of\":5}"), 0), Is.True);
            var view = panel.View(0);

            Assert.That(view.Presets.Count, Is.EqualTo(2));
            Assert.That(view.Presets[1].Name, Is.EqualTo("Bad"));
            Assert.That(view.Presets[1].Valid, Is.False);
            Assert.That(view.Presets[1].Reason, Is.EqualTo("z too low"));
            Assert.That(view.Running.Id, Is.EqualTo(1));
            Assert.That(view.Running.Step, Is.EqualTo(2));
            Assert.That(view.Running.Of, Is.EqualTo(5));

            Assert.That(panel.Apply(WithPresets("null"), 1), Is.True);
            Assert.That(panel.View(1).Running, Is.Null);
        }

        [Test]
        public void Apply_BadPackets_AreRejectedAndViewUnchanged()
        {
            var panel = PanelWith("arm_error");
            var bad = new List<byte[]>
            {
                null, new byte[0], Encoding.UTF8.GetBytes("not json"),
                Encoding.UTF8.GetBytes("[1,2]"), Encoding.UTF8.GetBytes("{}"),
                Encoding.UTF8.GetBytes("{\"type\":\"pose\",\"x\":1}"),
                Encoding.UTF8.GetBytes("{\"type\":\"status\",\"mode\":\"exploded\",\"error_id\":0," +
                    "\"x\":0,\"y\":0,\"z\":0,\"r\":0,\"suction\":false,\"stop_latched\":false," +
                    "\"last_command\":\"\",\"result\":\"\",\"reason\":\"\"}"),
                new byte[] { 0xFF, 0xFE, 0x7B },
                new byte[RobotPanel.MaxPacketBytes + 1],
                WithPresets("{\"id\":1}"),
                WithPresets("7"),
                WithPresets("{\"id\":1,\"step\":0,\"of\":5}"),
                WithPresets("{\"id\":1,\"step\":6,\"of\":5}"),
                WithPresets("{\"id\":42,\"step\":1,\"of\":5}"),
                Encoding.UTF8.GetBytes(StatusWithPresets.Replace("RUNNING", "null")
                    .Replace("\"id\":2", "\"id\":42")),
            };
            // Oversized but otherwise valid.
            var padded = new StringBuilder(StatusWithPresets.Replace("RUNNING", "null"));
            padded.Append(' ', RobotPanel.MaxPacketBytes);
            bad.Add(Encoding.UTF8.GetBytes(padded.ToString()));

            foreach (var packet in bad)
                Assert.That(panel.Apply(packet, 1), Is.False);
            var view = panel.View(1);
            Assert.That(view.Mode, Is.EqualTo("error"));
            Assert.That(view.ErrorId, Is.EqualTo(22));
        }

        [Test]
        public void Apply_RandomBytes_NeverThrows()
        {
            var panel = new RobotPanel();
            var rng = new System.Random(7);
            for (var n = 0; n < 500; n++)
            {
                var junk = new byte[rng.Next(0, 300)];
                rng.NextBytes(junk);
                Assert.DoesNotThrow(() => panel.Apply(junk, 0));
            }
        }

        [Test]
        public void Apply_DeeplyNestedJson_NeverThrows()
        {
            var panel = new RobotPanel();
            var deep = Encoding.UTF8.GetBytes(new string('[', 3000) + new string(']', 3000));
            Assert.DoesNotThrow(() => Assert.That(panel.Apply(deep, 0), Is.False));
        }

        // ---- link ----

        [Test]
        public void Link_AliveWithin2SecondsOfLastGoodStatus_ThenNot()
        {
            var panel = PanelWith("idle", 10.0);

            Assert.That(panel.View(10.0).LinkAlive, Is.True);
            Assert.That(panel.View(12.0).LinkAlive, Is.True);
            Assert.That(panel.View(12.01).LinkAlive, Is.False);
            panel.Apply(new byte[] { 1, 2, 3 }, 12.5); // junk does not refresh the link
            Assert.That(panel.View(12.5).LinkAlive, Is.False);
            panel.Apply(StatusBytes("idle"), 13.0);
            Assert.That(panel.View(14.0).LinkAlive, Is.True);
        }

        [Test]
        public void Link_NoStatusYet_IsNotAlive()
        {
            Assert.That(new RobotPanel().View(0).LinkAlive, Is.False);
        }

        // ---- enabled buttons ----

        [Test]
        public void IsEnabled_Idle_AllowsMotionAndEverythingElse()
        {
            var panel = PanelWith("idle");
            foreach (PanelButton b in System.Enum.GetValues(typeof(PanelButton)))
                if (b != PanelButton.Preset) Assert.That(panel.IsEnabled(b, 0), Is.True, b.ToString());
        }

        [Test]
        public void IsEnabled_StopLatched_DisablesMotionKeepsResume()
        {
            var panel = PanelWith("home_refused_after_stop");
            Assert.That(panel.IsEnabled(PanelButton.Home, 0), Is.False);
            Assert.That(panel.IsEnabled(PanelButton.Jog, 0), Is.False);
            Assert.That(panel.IsEnabled(PanelButton.MoveTo, 0), Is.False);
            Assert.That(panel.IsEnabled(PanelButton.Resume, 0), Is.True);
            Assert.That(panel.IsEnabled(PanelButton.Stop, 0), Is.True);
        }

        [Test]
        public void IsEnabled_Disabled_AllowsOnlyEnableAndStop()
        {
            var panel = new RobotPanel();
            panel.Apply(Encoding.UTF8.GetBytes(StatusWithPresets.Replace("RUNNING", "null")
                .Replace("\"enabled\"", "\"disabled\"")), 0);
            foreach (PanelButton b in System.Enum.GetValues(typeof(PanelButton)))
                Assert.That(panel.IsEnabled(b, 0, 1),
                    Is.EqualTo(b == PanelButton.Enable || b == PanelButton.Stop), b.ToString());
        }

        [Test]
        public void IsEnabled_ErrorMode_DisablesMotionKeepsClearError()
        {
            var panel = PanelWith("arm_error");
            Assert.That(panel.IsEnabled(PanelButton.Home, 0), Is.False);
            Assert.That(panel.IsEnabled(PanelButton.ClearError, 0), Is.True);
        }

        [Test]
        public void IsEnabled_LinkDown_DisablesMotionButNotEnableOrStop()
        {
            var panel = PanelWith("idle", 0);
            Assert.That(panel.IsEnabled(PanelButton.Home, 5), Is.False);
            Assert.That(panel.IsEnabled(PanelButton.Enable, 5), Is.True);
            Assert.That(panel.IsEnabled(PanelButton.Stop, 5), Is.True);
            Assert.That(new RobotPanel().IsEnabled(PanelButton.Enable, 0), Is.True);
        }

        [Test]
        public void IsEnabled_Preset_FollowsValidityAndRunning()
        {
            var panel = new RobotPanel();
            panel.Apply(WithPresets("null"), 0);
            Assert.That(panel.IsEnabled(PanelButton.Preset, 0, 1), Is.True);
            Assert.That(panel.IsEnabled(PanelButton.Preset, 0, 2), Is.False, "not valid");
            Assert.That(panel.IsEnabled(PanelButton.Preset, 0, 3), Is.False, "not listed");

            panel.Apply(WithPresets("{\"id\":1,\"step\":1,\"of\":3}"), 0);
            Assert.That(panel.IsEnabled(PanelButton.Preset, 0, 1), Is.False, "one is running");
        }

        [Test]
        public void IsEnabled_PresetWhileStopLatched_IsDisabled()
        {
            var panel = new RobotPanel();
            panel.Apply(Encoding.UTF8.GetBytes(StatusWithPresets.Replace("RUNNING", "null")
                .Replace("\"stop_latched\":false", "\"stop_latched\":true")), 0);
            Assert.That(panel.IsEnabled(PanelButton.Preset, 0, 1), Is.False);
        }
    }
}
