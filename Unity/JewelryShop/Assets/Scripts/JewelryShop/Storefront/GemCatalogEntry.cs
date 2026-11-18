using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Storefront
{
    /// <summary>The catalog facts of one Cut + Color; Gem Models and Gem Details
    /// (ADR-0013) read it.</summary>
    [CreateAssetMenu(menuName = "JewelryShop/Gem Catalog Entry")]
    public class GemCatalogEntry : ScriptableObject
    {
        [SerializeField] private string _gemName;
        [SerializeField] private GemCut _cut;
        [SerializeField] private GemColor _color;
        [SerializeField] private float _carat;
        [SerializeField] private string _clarity;
        [SerializeField] private string _origin;
        [SerializeField] private int _priceBaht;
        [SerializeField] private int _startingStockCount = 5;

        /// <summary>Display name only; code never identifies a Gem by it.</summary>
        public string GemName => _gemName;

        /// <summary>Cut + Color: the Gem's identity.</summary>
        public GemKey Key => new GemKey(_cut, _color);

        /// <summary>Weight in carats.</summary>
        public float Carat => _carat;

        /// <summary>Clarity grade as shown to the Customer, e.g. "VS1".</summary>
        public string Clarity => _clarity;

        /// <summary>Where the gem comes from.</summary>
        public string Origin => _origin;

        /// <summary>Price of one Gem in baht.</summary>
        public int PriceBaht => _priceBaht;

        /// <summary>Stock Count at app start (ADR-0011).</summary>
        public int StartingStockCount => _startingStockCount;
    }
}
