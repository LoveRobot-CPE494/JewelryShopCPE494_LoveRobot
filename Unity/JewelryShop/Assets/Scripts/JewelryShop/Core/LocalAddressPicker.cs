using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>One IPv4 address of one network card, as the OS reports it.</summary>
    public readonly struct AddressCandidate
    {
        /// <summary>Describes one address of one network card.</summary>
        public AddressCandidate(string address, bool isUp, bool hasGateway)
        {
            Address = address;
            IsUp = isUp;
            HasGateway = hasGateway;
        }

        /// <summary>Dotted-quad IPv4 text.</summary>
        public string Address { get; }

        /// <summary>True when the network card is operational.</summary>
        public bool IsUp { get; }

        /// <summary>True when the card has an IPv4 default gateway.</summary>
        public bool HasGateway { get; }
    }

    /// <summary>Chooses the local IPv4 address the Staff Console announces (ADR-0023).</summary>
    public static class LocalAddressPicker
    {
        /// <summary>
        /// The first operational candidate with a usable address and an IPv4 default gateway
        /// (this skips most virtual adapters), or null when there is none.
        /// </summary>
        public static string Pick(IEnumerable<AddressCandidate> candidates)
        {
            if (candidates == null) return null;
            foreach (var candidate in candidates)
                if (candidate.IsUp && candidate.HasGateway
                    && HostAddressResolver.IsUsableAddress(candidate.Address))
                    return candidate.Address;
            return null;
        }
    }
}
