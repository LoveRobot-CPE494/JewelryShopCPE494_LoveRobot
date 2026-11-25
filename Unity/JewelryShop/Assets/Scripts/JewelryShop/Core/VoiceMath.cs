using System;

namespace JewelryShop.Core
{
    /// <summary>Loudness and gating rules for live voice.</summary>
    public static class VoiceMath
    {
        /// <summary>Root-mean-square loudness of the first <paramref name="count"/> samples.</summary>
        public static float RootMeanSquare(float[] samples, int count)
        {
            if (count <= 0) return 0f;
            double sum = 0;
            for (var i = 0; i < count; i++) sum += samples[i] * samples[i];
            return (float)Math.Sqrt(sum / count);
        }

        /// <summary>Mouth opening 0..1: 0 at or below silence, 1 at or above full, linear between.</summary>
        public static float MouthOpen(float loudness, float silenceLoudness, float fullLoudness)
        {
            if (fullLoudness <= silenceLoudness) return loudness > silenceLoudness ? 1f : 0f;
            return Math.Min(1f, Math.Max(0f,
                (loudness - silenceLoudness) / (fullLoudness - silenceLoudness)));
        }

        /// <summary>
        /// Voice flows only while In call. The Staff has no In call state (#68 will send Arrived
        /// later), so Serving counts for her. Her first packets, sent before the Customer's
        /// Arrived, reach a Customer who is not InCall and are dropped there, which is harmless:
        /// nobody has arrived to hear them.
        /// </summary>
        public static bool IsLive(StaffCallRole role, StaffCallState state) =>
            role == StaffCallRole.Customer ? state == StaffCallState.InCall
                : state == StaffCallState.Serving;
    }
}
