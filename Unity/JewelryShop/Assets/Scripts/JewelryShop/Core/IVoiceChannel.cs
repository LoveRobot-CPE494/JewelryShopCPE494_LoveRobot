using System;

namespace JewelryShop.Core
{
    /// <summary>
    /// Unreliable, unordered voice packets to the other end of Staff Call. Separate from
    /// <see cref="IStaffLink"/> (reliable, 1 byte). The Netcode-backed one lives in JewelryShop.Networking.
    /// </summary>
    public interface IVoiceChannel
    {
        /// <summary>Send one Opus packet; dropped when not connected.</summary>
        void Send(byte[] packet, int length);

        /// <summary>Raised on the main thread with a packet (the buffer is reused: copy to keep).</summary>
        event Action<byte[], int> PacketReceived;
    }

    /// <summary>Safe fallback when no link is wired: voice goes nowhere.</summary>
    public sealed class NullVoiceChannel : IVoiceChannel
    {
        /// <inheritdoc />
        public void Send(byte[] packet, int length) { }

        /// <inheritdoc />
        public event Action<byte[], int> PacketReceived { add { } remove { } }
    }
}
