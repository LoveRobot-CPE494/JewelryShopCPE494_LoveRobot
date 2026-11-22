using UnityEngine;

namespace JewelryShop.Storefront
{
    /// <summary>Marks a token in the Order Tray and records which Order line it stands for (#79).</summary>
    public class OrderToken : MonoBehaviour
    {
        /// <summary>Index of this token's Gem in Order.Lines.</summary>
        public int Index { get; set; }
    }
}
