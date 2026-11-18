using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Storefront
{
    /// <summary>A Gem Model in the Display Case; its catalog entry says which Gem it is.</summary>
    public class GemModel : MonoBehaviour
    {
        [SerializeField] private GemCatalogEntry _entry;

        /// <summary>The catalog entry wired in the Inspector; null until wired.</summary>
        public GemCatalogEntry Entry => _entry;

        /// <summary>Cut + Color via the catalog entry; false (and a warning) while the
        /// entry is unwired.</summary>
        public bool TryGetKey(out GemKey key)
        {
            key = default;
            if (_entry == null)
            {
                Debug.LogWarning("GemModel: no catalog entry wired.", this);
                return false;
            }
            key = _entry.Key;
            return true;
        }
    }
}
