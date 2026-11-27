namespace JewelryShop.Core
{
    /// <summary>
    /// The latest valid beacon per role (ADR-0024). Feed it raw datagrams; ask it for a role's
    /// address and age. Pure: the caller supplies the clock.
    /// </summary>
    public sealed class BeaconBoard
    {
        private readonly string[] _address = new string[2];
        private readonly double[] _heardAt = new double[2];

        /// <summary>Stores the beacon if it decodes; false (no change) otherwise.</summary>
        public bool TryAccept(byte[] datagram, double nowSeconds)
        {
            if (!HostBeaconCodec.TryDecode(datagram, out var role, out var address, out _))
                return false;
            _address[(int)role] = address;
            _heardAt[(int)role] = nowSeconds;
            return true;
        }

        /// <summary>The role's latest beacon address and age; false if none heard.</summary>
        public bool TryGet(BeaconRole role, double nowSeconds, out string address,
            out double ageSeconds)
        {
            address = _address[(int)role];
            ageSeconds = nowSeconds - _heardAt[(int)role];
            return address != null;
        }
    }
}
