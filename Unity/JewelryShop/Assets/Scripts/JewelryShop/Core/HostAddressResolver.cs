namespace JewelryShop.Core
{
    /// <summary>
    /// Picks the one Host Address the Quest connects to (ADR-0023): the discovered address when
    /// Automatic is on and one was found, else the typed address. Only IPv4 literals pass.
    /// </summary>
    public static class HostAddressResolver
    {
        /// <summary>True for dotted-quad IPv4: four parts, each 0-255, no leading zeros.</summary>
        public static bool IsValidIPv4(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('.');
            if (parts.Length != 4) return false;
            foreach (var part in parts)
            {
                // A leading zero is read as octal by some parsers, so it is refused.
                if (part.Length == 0 || part.Length > 3) return false;
                if (part.Length > 1 && part[0] == '0') return false;
                var value = 0;
                foreach (var c in part)
                {
                    if (c < '0' || c > '9') return false; // also rejects spaces, signs, hex, IPv6
                    value = value * 10 + (c - '0');
                }
                if (value > 255) return false;
            }
            return true;
        }

        /// <summary>
        /// A host address worth announcing: valid IPv4 that is not 0.0.0.0, broadcast or loopback
        /// (127.x), since none of those lets a headset reach the PC.
        /// </summary>
        public static bool IsUsableAddress(string address)
        {
            if (!IsValidIPv4(address)) return false;
            return address != "0.0.0.0" && address != "255.255.255.255"
                && !address.StartsWith("127.", System.StringComparison.Ordinal);
        }

        /// <summary>The address to connect to, or null when nothing valid is available.</summary>
        public static string Resolve(string typed, bool automatic, string discovered)
        {
            if (automatic && IsValidIPv4(discovered)) return discovered;
            return IsValidIPv4(typed) ? typed : null;
        }
    }
}
