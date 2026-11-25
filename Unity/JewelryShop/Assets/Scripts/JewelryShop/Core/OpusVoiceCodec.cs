using Concentus;
using Concentus.Enums;

namespace JewelryShop.Core
{
    /// <summary>
    /// Mono 16 kHz Opus (Concentus), 20 ms frames, tuned for speech. One instance per direction
    /// of a call: it keeps encoder and decoder state between frames.
    /// </summary>
    public sealed class OpusVoiceCodec
    {
        /// <summary>Samples per second; Opus supports 8, 12, 16, 24 and 48 kHz.</summary>
        public const int SampleRate = 16000;

        /// <summary>Samples in one 20 ms frame.</summary>
        public const int FrameSamples = 320;

        /// <summary>Largest packet, in bytes; well under the unreliable-message MTU.</summary>
        public const int MaxPacketBytes = 400;

        private readonly IOpusEncoder _encoder =
            OpusCodecFactory.CreateEncoder(SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        private readonly IOpusDecoder _decoder = OpusCodecFactory.CreateDecoder(SampleRate, 1);

        /// <summary>Create the encoder and decoder.</summary>
        public OpusVoiceCodec()
        {
            _encoder.Bitrate = 24000; // bits per second: clear speech, about 60 bytes per frame
        }

        /// <summary>Encode one <see cref="FrameSamples"/> frame; returns the packet length.</summary>
        public int Encode(float[] frame, byte[] packet) =>
            _encoder.Encode(frame, FrameSamples, packet, packet.Length);

        /// <summary>Decode one packet into <paramref name="frame"/>; returns the sample count.</summary>
        public int Decode(byte[] packet, int length, float[] frame) =>
            _decoder.Decode(new System.ReadOnlySpan<byte>(packet, 0, length), frame, FrameSamples, false);
    }
}
