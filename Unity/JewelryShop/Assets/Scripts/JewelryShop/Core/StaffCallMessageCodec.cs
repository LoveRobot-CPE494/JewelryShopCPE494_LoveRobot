using System;

namespace JewelryShop.Core
{
    /// <summary>The one-byte wire form of a <see cref="StaffCallMessage"/>.</summary>
    public static class StaffCallMessageCodec
    {
        /// <summary>The byte to put on the wire.</summary>
        public static byte Encode(StaffCallMessage message) => (byte)message;

        /// <summary>
        /// False for a byte this build does not know (e.g. a newer peer); the caller ignores it.
        /// </summary>
        public static bool TryDecode(byte value, out StaffCallMessage message)
        {
            message = (StaffCallMessage)value;
            return Enum.IsDefined(typeof(StaffCallMessage), message);
        }
    }
}
