namespace JewelryShop.Core
{
    /// <summary>
    /// The talk / not-talk toggle (not push-to-talk): unmuted by default, flipped by a key, and
    /// back to unmuted whenever a call ends. Composes with the half-duplex gate: either muting
    /// means nothing is sent.
    /// </summary>
    public sealed class MicrophoneMute
    {
        /// <summary>True while the user has muted the microphone.</summary>
        public bool IsMuted { get; private set; }

        /// <summary>Flip between talking and muted; returns the new IsMuted.</summary>
        public bool Toggle()
        {
            IsMuted = !IsMuted;
            return IsMuted;
        }

        /// <summary>Back to unmuted (call ended).</summary>
        public void Reset() => IsMuted = false;

        /// <summary>A frame is sent only when neither the user nor half-duplex muted it.</summary>
        public bool CanSend(bool isHalfDuplexMuted) => !IsMuted && !isHalfDuplexMuted;

        /// <summary>The loudness the local face and meters should read: 0 while muted.</summary>
        public float LocalLoudness(float microphoneLoudness) => IsMuted ? 0f : microphoneLoudness;
    }
}
