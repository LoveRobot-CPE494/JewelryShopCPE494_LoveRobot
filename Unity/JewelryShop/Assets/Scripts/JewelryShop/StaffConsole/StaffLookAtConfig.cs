using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only Look-At tuning for the Staff Avatar's head (ADR-0018, ADR-0020).</summary>
    [CreateAssetMenu(fileName = "StaffLookAtConfig",
        menuName = "JewelryShop/Staff Console/Look-At Config")]
    public sealed class StaffLookAtConfig : ScriptableObject
    {
        [SerializeField] private LookAtSettings _settings = LookAtSettings.Defaults;

        /// <summary>The settings, clamped; invalid values log a warning.</summary>
        public LookAtSettings Settings
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
