using System;

namespace JewelryShop.Core
{
    /// <summary>
    /// Wire form of the Host Beacon the Staff Console broadcasts (ADR-0023), 11 bytes:
    /// magic "JSHB" (4), version 1 (1), IPv4 in network order (4), Staff Link port big-endian (2).
    /// Decode is the trust boundary: any UDP sender can reach it, so it never throws.
    /// </summary>
    public static class HostBeaconCodec
    {
        /// <summary>Size of a beacon on the wire, in bytes.</summary>
        public const int Length = 11;

        /// <summary>The only wire version this build speaks.</summary>
        public const byte Version = 1;

        private static readonly byte[] Magic = { (byte)'J', (byte)'S', (byte)'H', (byte)'B' };

        /// <summary>
        /// The 11 beacon bytes. Throws for an unusable address or port 0 (a caller bug).
        /// </summary>
        public static byte[] Encode(string address, ushort port)
        {
            if (!HostAddressResolver.IsUsableAddress(address) || port == 0)
                throw new ArgumentException($"Cannot beacon {address}:{port}.");
            var bytes = new byte[Length];
            Array.Copy(Magic, bytes, Magic.Length);
            bytes[4] = Version;
            var parts = address.Split('.');
            for (var i = 0; i < 4; i++) bytes[5 + i] = byte.Parse(parts[i]);
            bytes[9] = (byte)(port >> 8);
            bytes[10] = (byte)(port & 0xFF);
            return bytes;
        }

        /// <summary>False for anything that is not a well-formed, usable beacon.</summary>
        public static bool TryDecode(byte[] data, out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (data == null || data.Length != Length) return false;
            for (var i = 0; i < Magic.Length; i++)
                if (data[i] != Magic[i]) return false;
            if (data[4] != Version) return false;
            var candidate = $"{data[5]}.{data[6]}.{data[7]}.{data[8]}";
            var candidatePort = (ushort)((data[9] << 8) | data[10]);
            if (!HostAddressResolver.IsUsableAddress(candidate) || candidatePort == 0) return false;
            address = candidate;
            port = candidatePort;
            return true;
        }
    }
}
