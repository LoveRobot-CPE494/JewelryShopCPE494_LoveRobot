using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only skirt sway and flare tuning (ADR-0018).</summary>
    [CreateAssetMenu(fileName = "StaffSkirtConfig",
        menuName = "JewelryShop/Staff Console/Skirt Config")]
    public sealed class StaffSkirtConfig : ScriptableObject
    {
        [SerializeField] private SkirtSettings _settings = SkirtSettings.Defaults;

        /// <summary>The settings, validated; invalid values log a warning.</summary>
        public SkirtSettings Settings
        {
            get
            {
                var warnings = new List<string>();
                var valid = _settings.Validated(warnings);
                foreach (var warning in warnings)
                    Debug.LogWarning($"{name}: {warning}", this);
                return valid;
            }
        }
    }
}
