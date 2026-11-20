using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only key-to-gesture map (ADR-0018).</summary>
    [CreateAssetMenu(fileName = "StaffGestureMapConfig",
        menuName = "JewelryShop/Staff Console/Gesture Map Config")]
    public sealed class StaffGestureMapConfig : ScriptableObject
    {
        [SerializeField] private GestureMapSettings _settings = GestureMapSettings.Defaults;

        /// <summary>The settings, validated; invalid values log a warning.</summary>
        public GestureMapSettings Settings
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
