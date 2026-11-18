using System;

namespace JewelryShop.Core
{
    /// <summary>The shape a Gem is cut to.</summary>
    public enum GemCut { Round, Emerald }

    /// <summary>The color of a Gem; matches the real Gem Pieces in the Gem Stock.</summary>
    public enum GemColor { Red, Blue, Clear, Pink, Purple, Green, Yellow, Orange }

    /// <summary>A Gem is identified by Cut + Color only, never by its name.</summary>
    public readonly struct GemKey : IEquatable<GemKey>
    {
        /// <summary>The Gem with this Cut and Color.</summary>
        public GemKey(GemCut cut, GemColor color) { Cut = cut; Color = color; }

        /// <summary>The Gem's Cut.</summary>
        public GemCut Cut { get; }

        /// <summary>The Gem's Color.</summary>
        public GemColor Color { get; }

        public bool Equals(GemKey other) => Cut == other.Cut && Color == other.Color;
        public override bool Equals(object obj) => obj is GemKey k && Equals(k);
        public override int GetHashCode() => ((int)Cut << 8) | (int)Color;
        public override string ToString() => $"{Color} {Cut}";
    }
}
