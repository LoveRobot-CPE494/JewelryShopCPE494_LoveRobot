using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>Live voice tuning (ADR-0018 rules in Core). Loudness is RMS, 0..1.</summary>
    [Serializable]
    public struct VoiceSettings
    {
        /// <summary>Quest: ask Android for its voice-call audio mode while In call.</summary>
        public bool IsCommunicationAudioModeRequested;

        /// <summary>Quest: mute the microphone while the Staff voice plays (off until echo is heard).</summary>
        public bool IsHalfDuplexEnabled;

        /// <summary>Staff voice loudness above which the microphone mutes.</summary>
        public float HalfDuplexMuteLoudness;

        /// <summary>Seconds the microphone stays muted after the Staff voice stops.</summary>
        public float HalfDuplexReleaseSeconds;

        /// <summary>Voice loudness at or below which the mouth is closed.</summary>
        public float MouthSilenceLoudness;

        /// <summary>Voice loudness at or above which the mouth is fully open.</summary>
        public float MouthFullLoudness;

        /// <summary>How fast the mouth moves, in full openings per second.</summary>
        public float MouthSpeedPerSecond;

        /// <summary>Audio queued before playback starts: bigger survives jitter, adds delay.</summary>
        public float JitterBufferSeconds;

        /// <summary>The shipped values; half-duplex stays off by default.</summary>
        public static VoiceSettings Defaults => new VoiceSettings
        {
            IsCommunicationAudioModeRequested = true,
            IsHalfDuplexEnabled = false,
            HalfDuplexMuteLoudness = 0.02f,
            HalfDuplexReleaseSeconds = 0.4f,
            MouthSilenceLoudness = 0.01f,
            MouthFullLoudness = 0.15f,
            MouthSpeedPerSecond = 15f,
            JitterBufferSeconds = 0.08f,
        };

        /// <summary>A clamped copy; each change is described in <paramref name="warnings"/>.</summary>
        public VoiceSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var silence = ConfigRule.Clamp(nameof(MouthSilenceLoudness), MouthSilenceLoudness,
                0f, 0.5f, d.MouthSilenceLoudness, warnings);
            // Full must sit above silence or the mouth would snap open.
            var full = ConfigRule.Clamp(nameof(MouthFullLoudness), MouthFullLoudness,
                silence + 0.01f, 1f, Math.Max(d.MouthFullLoudness, silence + 0.01f), warnings);
            return new VoiceSettings
            {
                IsCommunicationAudioModeRequested = IsCommunicationAudioModeRequested,
                IsHalfDuplexEnabled = IsHalfDuplexEnabled,
                HalfDuplexMuteLoudness = ConfigRule.Clamp(nameof(HalfDuplexMuteLoudness),
                    HalfDuplexMuteLoudness, 0.001f, 0.5f, d.HalfDuplexMuteLoudness, warnings),
                HalfDuplexReleaseSeconds = ConfigRule.Clamp(nameof(HalfDuplexReleaseSeconds),
                    HalfDuplexReleaseSeconds, 0f, 2f, d.HalfDuplexReleaseSeconds, warnings),
                MouthSilenceLoudness = silence,
                MouthFullLoudness = full,
                MouthSpeedPerSecond = ConfigRule.Clamp(nameof(MouthSpeedPerSecond),
                    MouthSpeedPerSecond, 1f, 60f, d.MouthSpeedPerSecond, warnings),
                JitterBufferSeconds = ConfigRule.Clamp(nameof(JitterBufferSeconds),
                    JitterBufferSeconds, 0.02f, 0.5f, d.JitterBufferSeconds, warnings),
            };
        }
    }
}
