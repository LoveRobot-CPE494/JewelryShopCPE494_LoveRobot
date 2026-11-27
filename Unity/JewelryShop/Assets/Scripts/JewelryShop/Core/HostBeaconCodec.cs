using System;

namespace JewelryShop.Core
{
    /// <summary>Which PC a beacon announces (ADR-0023, ADR-0027).</summary>
    public enum BeaconRole
    {
        /// <summary>The Staff Console PC (Staff Link port).</summary>
        Staff,
        /// <summary>The Robot PC (Teleop port).</summary>
        Robot,
    }

    /// <summary>
    /// Wire form of the beacon a PC broadcasts (ADR-0023), 11 bytes:
    /// magic (4), version 1 (1), IPv4 in network order (4), port big-endian (2).
    /// The role is the magic: "JSHB" = staff (unchanged from before roles existed), "JSRB" = robot.
    /// Any other magic is an unknown role and is rejected.
    /// Examples, for 192.168.1.42 port 7777 (0x1E61):
    ///   staff  4A 53 48 42 01 C0 A8 01 2A 1E 61
    ///   robot  4A 53 52 42 01 C0 A8 01 2A 1E 61
    /// Decode is the trust boundary: any UDP sender can reach it, so it never throws.
    /// </summary>
    public static class HostBeaconCodec
    {
        /// <summary>Size of a beacon on the wire, in bytes.</summary>
        public const int Length = 11;

        /// <summary>The only wire version this build speaks.</summary>
        public const byte Version = 1;

        private static readonly byte[] StaffMagic = { (byte)'J', (byte)'S', (byte)'H', (byte)'B' };
        private static readonly byte[] RobotMagic = { (byte)'J', (byte)'S', (byte)'R', (byte)'B' };

        /// <summary>
        /// The 11 beacon bytes. Throws for an unusable address or port 0 (a caller bug).
        /// </summary>
        public static byte[] Encode(BeaconRole role, string address, ushort port)
        {
            if (!HostAddressResolver.IsUsableAddress(address) || port == 0)
                throw new ArgumentException($"Cannot beacon {address}:{port}.");
            var bytes = new byte[Length];
            Array.Copy(role == BeaconRole.Robot ? RobotMagic : StaffMagic, bytes, 4);
            bytes[4] = Version;
            var parts = address.Split('.');
            for (var i = 0; i < 4; i++) bytes[5 + i] = byte.Parse(parts[i]);
            bytes[9] = (byte)(port >> 8);
            bytes[10] = (byte)(port & 0xFF);
            return bytes;
        }

        /// <summary>False for anything that is not a well-formed, usable beacon.</summary>
        public static bool TryDecode(byte[] data, out BeaconRole role, out string address,
            out ushort port)
        {
            role = BeaconRole.Staff;
            address = null;
            port = 0;
            if (data == null || data.Length != Length) return false;
            BeaconRole candidateRole;
            if (StartsWith(data, StaffMagic)) candidateRole = BeaconRole.Staff;
            else if (StartsWith(data, RobotMagic)) candidateRole = BeaconRole.Robot;
            else return false;
            if (data[4] != Version) return false;
            var candidate = $"{data[5]}.{data[6]}.{data[7]}.{data[8]}";
            var candidatePort = (ushort)((data[9] << 8) | data[10]);
            if (!HostAddressResolver.IsUsableAddress(candidate) || candidatePort == 0) return false;
            role = candidateRole;
            address = candidate;
            port = candidatePort;
            return true;
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            for (var i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }
    }
}
