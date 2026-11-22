using System;
using System.Collections.Generic;
using System.Globalization;

namespace JewelryShop.Core
{
    /// <summary>One row of the Order panel: a Gem's name and its Price text.</summary>
    public readonly struct OrderPanelRow
    {
        public OrderPanelRow(string name, string price) { Name = name; Price = price; }

        /// <summary>The Gem's display name.</summary>
        public string Name { get; }

        /// <summary>The Gem's Price, baht format.</summary>
        public string Price { get; }
    }

    /// <summary>Turns an Order into the text the floating Order panel shows (#80). One row
    /// per Gem; repeats are separate rows. Past <see cref="MaxRows"/> Gems the last row
    /// reads "+N more" so the panel never outgrows its box.</summary>
    public static class OrderPanelText
    {
        /// <summary>Shown while the Order has no Gems.</summary>
        public const string EmptyHint = "Drop a Gem here";

        /// <summary>Most rows the panel shows, including a "+N more" row.</summary>
        public const int MaxRows = 8;

        /// <summary>Baht with a symbol and thousands separators, e.g. ฿45,900.</summary>
        public static string FormatBaht(int baht) =>
            "฿" + baht.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>One row per Order line, in order added, at most <see cref="MaxRows"/>;
        /// a Gem whose name is unknown shows its Cut + Color.</summary>
        public static List<OrderPanelRow> Rows(Order order, Func<GemKey, string> nameOf)
        {
            int count = order.Lines.Count;
            int shown = count > MaxRows ? MaxRows - 1 : count;
            var rows = new List<OrderPanelRow>(MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var line = order.Lines[i];
                var name = nameOf?.Invoke(line.Key);
                rows.Add(new OrderPanelRow(string.IsNullOrEmpty(name) ? line.Key.ToString() : name,
                    FormatBaht(line.PriceBaht)));
            }
            if (shown < count) rows.Add(new OrderPanelRow($"+{count - shown} more", ""));
            return rows;
        }

        /// <summary>The rows' names, one per line.</summary>
        public static string NameColumn(IEnumerable<OrderPanelRow> rows) =>
            string.Join("\n", System.Linq.Enumerable.Select(rows, r => r.Name));

        /// <summary>The rows' prices, one per line, so each lines up with its name.</summary>
        public static string PriceColumn(IEnumerable<OrderPanelRow> rows) =>
            string.Join("\n", System.Linq.Enumerable.Select(rows, r => r.Price));

        /// <summary>The total line, e.g. "Total ฿45,900".</summary>
        public static string TotalText(Order order) => "Total " + FormatBaht(order.TotalBaht);
    }
}
