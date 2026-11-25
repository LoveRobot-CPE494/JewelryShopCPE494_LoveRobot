using System;
using System.Globalization;

namespace JewelryShop.Core
{
    /// <summary>The catalog facts the Gem Details Panel shows (ADR-0013), copied from a
    /// Gem Catalog Entry so Core stays free of Unity types.</summary>
    public readonly struct GemDetails
    {
        /// <summary>Copies one Gem Catalog Entry's facts.</summary>
        public GemDetails(
            string name, GemKey key, float carat, string clarity, string origin, int priceBaht)
        {
            Name = name; Key = key; Carat = carat; Clarity = clarity; Origin = origin;
            PriceBaht = priceBaht;
        }

        /// <summary>Display name, the panel heading.</summary>
        public string Name { get; }

        /// <summary>Cut + Color: the Gem's identity.</summary>
        public GemKey Key { get; }

        /// <summary>Weight in carats.</summary>
        public float Carat { get; }

        /// <summary>Clarity grade, e.g. "VS1".</summary>
        public string Clarity { get; }

        /// <summary>Where the gem comes from.</summary>
        public string Origin { get; }

        /// <summary>Price of one Gem in baht.</summary>
        public int PriceBaht { get; }

        /// <summary>Panel text: the name as heading, the facts, the Price last.</summary>
        public string[] Lines() => new[]
        {
            Name,
            "Cut: " + Key.Cut,
            "Color: " + Key.Color,
            "Carat: " + Carat.ToString("0.##", CultureInfo.InvariantCulture),
            "Clarity: " + Clarity,
            "Origin: " + Origin,
            OrderPanelText.FormatBaht(PriceBaht),
        };
    }

    /// <summary>When the Gem Details Panel is open: A toggles it while a Gem Model is held;
    /// letting go closes it. <see cref="Opened"/> is the hook for the opening effects.</summary>
    public sealed class GemDetailsFlow
    {
        private GemDetails? _held;

        /// <summary>Raised each time the panel opens, with the entry it shows.</summary>
        public event Action<GemDetails> Opened;

        /// <summary>Whether the Gem Details Panel is showing.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The entry on show; meaningful while <see cref="IsOpen"/>.</summary>
        public GemDetails Shown { get; private set; }

        /// <summary>A Gem Model was grabbed; the panel starts closed for it.</summary>
        public void Grabbed(GemDetails details)
        {
            _held = details;
            IsOpen = false;
        }

        /// <summary>The Gem Model was let go; the panel closes.</summary>
        public void Released()
        {
            _held = null;
            IsOpen = false;
        }

        /// <summary>A toggles the panel while a Gem Model is held; otherwise does nothing.</summary>
        public void APressed()
        {
            if (IsOpen) { IsOpen = false; return; }
            if (_held == null) return;
            Shown = _held.Value;
            IsOpen = true;
            Opened?.Invoke(Shown);
        }
    }
}
