using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>One Gem in an Order, priced when added.</summary>
    public readonly struct OrderLine
    {
        /// <summary>A line for one Gem of this Cut + Color at this price.</summary>
        public OrderLine(GemKey key, int priceBaht) { Key = key; PriceBaht = priceBaht; }

        /// <summary>Cut + Color of the Gem.</summary>
        public GemKey Key { get; }

        /// <summary>Price of this one Gem in baht.</summary>
        public int PriceBaht { get; }
    }

    /// <summary>The Gems a Customer pays for in one payment: ordered, repeats allowed.</summary>
    public class Order
    {
        /// <summary>The default most Gems one Order may hold (5 for now, easy to hit while testing).</summary>
        public const int DefaultMaxGems = 5;

        /// <summary>What Add did.</summary>
        public enum AddResult { Added, Full, SoldOut }

        private readonly List<OrderLine> _lines = new List<OrderLine>();
        private readonly int _maxGems;

        /// <summary>An empty Order that holds at most <paramref name="maxGems"/> Gems.</summary>
        public Order(int maxGems = DefaultMaxGems) { _maxGems = maxGems; }

        /// <summary>The Gems added so far, in the order added.</summary>
        public IReadOnlyList<OrderLine> Lines => _lines;

        /// <summary>Sum of every line's price, in baht.</summary>
        public int TotalBaht { get; private set; }

        /// <summary>Raised after every successful Add or RemoveAt (Observer, ADR-0008).</summary>
        public event Action Changed;

        /// <summary>Adds one Gem unless the Order is Full or this Cut + Color already
        /// fills its Stock Count (ADR-0011); a refusal changes nothing.</summary>
        public AddResult Add(GemKey key, int priceBaht, int stockCount)
        {
            if (_lines.Count >= _maxGems) return AddResult.Full;
            int sameKeyCount = 0;
            foreach (var line in _lines)
                if (line.Key.Equals(key)) sameKeyCount++;
            if (sameKeyCount >= stockCount) return AddResult.SoldOut;

            _lines.Add(new OrderLine(key, priceBaht));
            TotalBaht += priceBaht;
            Changed?.Invoke();
            return AddResult.Added;
        }

        /// <summary>Removes the Gem at <paramref name="index"/>; false and no event when out of range.</summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _lines.Count) return false;
            TotalBaht -= _lines[index].PriceBaht;
            _lines.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }
    }
}
