using System;
using System.Collections.Generic;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class VoiceTests
    {
        private static float[] Sine(int count, int offset)
        {
            var s = new float[count];
            for (var i = 0; i < count; i++)
                s[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 440 * (i + offset) / OpusVoiceCodec.SampleRate);
            return s;
        }

        [Test]
        public void Opus_EncodeDecode_StaysCloseToInput()
        {
            var codec = new OpusVoiceCodec();
            var input = new float[0];
            var output = new float[0];
            const int frames = 20;
            for (var f = 0; f < frames; f++)
            {
                var frame = Sine(OpusVoiceCodec.FrameSamples, f * OpusVoiceCodec.FrameSamples);
                var packet = new byte[OpusVoiceCodec.MaxPacketBytes];
                var length = codec.Encode(frame, packet);
                var decoded = new float[OpusVoiceCodec.FrameSamples];
                Assert.That(codec.Decode(packet, length, decoded), Is.EqualTo(OpusVoiceCodec.FrameSamples));
                input = Concat(input, frame);
                output = Concat(output, decoded);
            }
            // Opus delays the signal a little, so take the best match over a few hundred lags.
            var best = 0.0;
            for (var lag = 0; lag < 400; lag++) best = Math.Max(best, Correlation(input, output, lag));
            Assert.That(best, Is.GreaterThan(0.9));
            Assert.That(VoiceMath.RootMeanSquare(output, output.Length) / VoiceMath.RootMeanSquare(input, input.Length),
                Is.InRange(0.8, 1.2));
        }

        private static float[] Concat(float[] a, float[] b)
        {
            var r = new float[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        // Normalised correlation of input[i] with output[i + lag], skipping the codec warm-up.
        private static double Correlation(float[] input, float[] output, int lag)
        {
            double xy = 0, xx = 0, yy = 0;
            for (var i = 2000; i + lag < input.Length; i++)
            {
                xy += input[i] * output[i + lag];
                xx += input[i] * input[i];
                yy += output[i + lag] * output[i + lag];
            }
            return xy / Math.Sqrt(xx * yy);
        }

        [Test]
        public void MouthOpen_Silence_IsZero() =>
            Assert.That(VoiceMath.MouthOpen(0f, 0.01f, 0.2f), Is.EqualTo(0f));

        [Test]
        public void MouthOpen_BelowSilenceThreshold_IsZero() =>
            Assert.That(VoiceMath.MouthOpen(0.005f, 0.01f, 0.2f), Is.EqualTo(0f));

        [Test]
        public void MouthOpen_LoudSpeech_SaturatesAtOne() =>
            Assert.That(VoiceMath.MouthOpen(0.9f, 0.01f, 0.2f), Is.EqualTo(1f));

        [Test]
        public void MouthOpen_Between_RisesWithLoudness() =>
            Assert.That(VoiceMath.MouthOpen(0.105f, 0.01f, 0.2f), Is.EqualTo(0.5f).Within(0.001f));

        [Test]
        public void Rms_OfSilenceIsZeroAndOfConstantIsItsMagnitude()
        {
            Assert.That(VoiceMath.RootMeanSquare(new float[10], 10), Is.EqualTo(0f));
            Assert.That(VoiceMath.RootMeanSquare(new[] { 0.5f, -0.5f }, 2), Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void IsLive_StaffIsLiveWhenServingNotWhenIncoming()
        {
            Assert.That(VoiceMath.IsLive(StaffCallRole.Staff, StaffCallState.Serving), Is.True);
            Assert.That(VoiceMath.IsLive(StaffCallRole.Staff, StaffCallState.Incoming), Is.False);
        }

        [Test]
        public void VoiceSettings_Defaults_AreValidAndHalfDuplexIsOff()
        {
            var warnings = new List<string>();
            var valid = VoiceSettings.Defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);
            Assert.That(valid.IsHalfDuplexEnabled, Is.False);
        }

        [Test]
        public void VoiceSettings_FullBelowSilence_IsPushedAboveSilenceWithWarning()
        {
            var settings = VoiceSettings.Defaults;
            settings.MouthSilenceLoudness = 0.2f;
            settings.MouthFullLoudness = 0.1f;
            var warnings = new List<string>();
            var valid = settings.Validated(warnings);
            Assert.That(valid.MouthFullLoudness, Is.GreaterThan(valid.MouthSilenceLoudness));
            Assert.That(warnings, Is.Not.Empty);
        }

        [Test]
        public void IsLive_OnlyWhileInCall()
        {
            Assert.That(VoiceMath.IsLive(StaffCallRole.Customer, StaffCallState.InCall), Is.True);
            Assert.That(VoiceMath.IsLive(StaffCallRole.Staff, StaffCallState.Serving), Is.True);
            Assert.That(VoiceMath.IsLive(StaffCallRole.Customer, StaffCallState.StaffComing), Is.False);
            Assert.That(VoiceMath.IsLive(StaffCallRole.Customer, StaffCallState.Idle), Is.False);
            Assert.That(VoiceMath.IsLive(StaffCallRole.Staff, StaffCallState.Incoming), Is.False);
        }

        [Test]
        public void HalfDuplexGate_MutesAboveThresholdAndReleasesAfterHangover()
        {
            var gate = new HalfDuplexGate(0.05f, 0.3);
            Assert.That(gate.UpdateIsMuted(0f, 0.0), Is.False);
            Assert.That(gate.UpdateIsMuted(0.2f, 1.0), Is.True);
            Assert.That(gate.UpdateIsMuted(0f, 1.1), Is.True, "still inside the hangover");
            Assert.That(gate.UpdateIsMuted(0f, 1.31), Is.False, "released after the hangover");
        }

        [Test]
        public void PcmRingBuffer_PlaysSilenceUntilPrimedThenInOrder()
        {
            var ring = new PcmRingBuffer(100, 4);
            var dst = new float[2];
            ring.Write(new[] { 1f, 2f, 3f }, 3);
            ring.Read(dst);
            Assert.That(dst, Is.EqualTo(new[] { 0f, 0f }), "not primed yet");
            ring.Write(new[] { 4f }, 1);
            ring.Read(dst);
            Assert.That(dst, Is.EqualTo(new[] { 1f, 2f }));
            ring.Read(dst);
            Assert.That(dst, Is.EqualTo(new[] { 3f, 4f }));
            ring.Read(dst);
            Assert.That(dst, Is.EqualTo(new[] { 0f, 0f }), "underrun gives silence");
        }

        [Test]
        public void PcmRingBuffer_Overflow_DropsOldest()
        {
            var ring = new PcmRingBuffer(3, 1);
            ring.Write(new[] { 1f, 2f, 3f, 4f }, 4);
            var dst = new float[3];
            ring.Read(dst);
            Assert.That(dst, Is.EqualTo(new[] { 2f, 3f, 4f }));
        }

        [Test]
        public void MicrophoneMute_IsUnmutedByDefaultAndToggleFlips()
        {
            var mute = new MicrophoneMute();
            Assert.That(mute.IsMuted, Is.False);
            Assert.That(mute.Toggle(), Is.True);
            Assert.That(mute.IsMuted, Is.True);
            Assert.That(mute.Toggle(), Is.False);
        }

        [Test]
        public void MicrophoneMute_Muted_SendsNothingAndLocalLoudnessIsZero()
        {
            var mute = new MicrophoneMute();
            Assert.That(mute.CanSend(false), Is.True);
            Assert.That(mute.LocalLoudness(0.3f), Is.EqualTo(0.3f));
            mute.Toggle();
            Assert.That(mute.CanSend(false), Is.False);
            Assert.That(mute.LocalLoudness(0.3f), Is.EqualTo(0f));
        }

        [Test]
        public void MicrophoneMute_HalfDuplexAlsoMutes()
        {
            Assert.That(new MicrophoneMute().CanSend(true), Is.False);
        }

        [Test]
        public void MicrophoneMute_ResetReturnsToUnmuted()
        {
            var mute = new MicrophoneMute();
            mute.Toggle();
            mute.Reset();
            Assert.That(mute.IsMuted, Is.False);
        }

        [Test]
        public void NullVoiceChannel_DropsEverything()
        {
            var channel = new NullVoiceChannel();
            Assert.DoesNotThrow(() => channel.Send(new byte[3], 3));
        }
    }
}
