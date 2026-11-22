using System;
using System.Collections.Generic;
using System.Text;

namespace JewelryShop.Core
{
    /// <summary>The Receipt slip's text (#91): one line per Gem, repeats included, then the total.
    /// Built from a snapshot of the Order's lines taken at approval.</summary>
    public static class ReceiptText
    {
        /// <summary>"Name  ฿price" per Gem, then "Total ฿sum"; an unknown name shows its Cut + Color.</summary>
        public static string Build(IReadOnlyList<OrderLine> lines, Func<GemKey, string> nameOf)
        {
            var sb = new StringBuilder();
            int total = 0;
            foreach (var line in lines)
            {
                var name = nameOf?.Invoke(line.Key);
                sb.Append(string.IsNullOrEmpty(name) ? line.Key.ToString() : name)
                  .Append("  ").Append(OrderPanelText.FormatBaht(line.PriceBaht)).Append('\n');
                total += line.PriceBaht;
            }
            return sb.Append("Total ").Append(OrderPanelText.FormatBaht(total)).ToString();
        }
    }
}
