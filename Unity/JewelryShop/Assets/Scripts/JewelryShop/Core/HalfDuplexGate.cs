namespace JewelryShop.Core
{
    /// <summary>
    /// Automatic half-duplex (ADR-0010 voice link, tuned by ADR-0018 config): the microphone mutes
    /// while the other side's voice plays louder than the threshold, and stays muted for the
    /// release time after it stops.
    /// </summary>
    public sealed class HalfDuplexGate
    {
        private readonly float _muteLoudness;
        private readonly double _releaseSeconds;
        private double _lastLoudSeconds = double.NegativeInfinity;

        /// <summary>Mute above <paramref name="muteLoudness"/>; release after <paramref name="releaseSeconds"/>.</summary>
        public HalfDuplexGate(float muteLoudness, double releaseSeconds)
        {
            _muteLoudness = muteLoudness;
            _releaseSeconds = releaseSeconds;
        }

        /// <summary>Feed the playing voice's loudness; returns true while the microphone is muted.</summary>
        public bool UpdateIsMuted(float playbackLoudness, double nowSeconds)
        {
            if (playbackLoudness > _muteLoudness) _lastLoudSeconds = nowSeconds;
            return nowSeconds - _lastLoudSeconds < _releaseSeconds;
        }
    }
}
