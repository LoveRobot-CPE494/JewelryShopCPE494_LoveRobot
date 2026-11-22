namespace JewelryShop.Core
{
    /// <summary>The words the Customer sees when the Order refuses a Gem, and when a Display
    /// Case position is out of stock (#81).</summary>
    public static class OrderFeedback
    {
        /// <summary>Shown when a drop is refused because the Order holds its most Gems.</summary>
        public const string FullMessage = "Order is full";

        /// <summary>Shown for a refused drop and on an out-of-stock Display Case position.</summary>
        public const string SoldOutMessage = "Sold out";

        /// <summary>The message for an Add result, or null when the Gem was added.</summary>
        public static string MessageFor(Order.AddResult result) => result switch
        {
            Order.AddResult.Full => FullMessage,
            Order.AddResult.SoldOut => SoldOutMessage,
            _ => null,
        };

        /// <summary>True when a Gem with this Stock Count shows the Sold out tag.</summary>
        public static bool ShowsSoldOutTag(int stockCount) => stockCount <= 0;
    }
}
