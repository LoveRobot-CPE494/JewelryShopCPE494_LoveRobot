using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only gesture-to-Expression table and timing (ADR-0018, ADR-0019).</summary>
    [CreateAssetMenu(fileName = "StaffExpressionConfig",
        menuName = "JewelryShop/Staff Console/Expression Config")]
    public sealed class StaffExpressionConfig : ScriptableObject
    {
        [SerializeField] private ExpressionSettings _settings = ExpressionSettings.Defaults;

        /// <summary>The settings, validated; invalid values log a warning.</summary>
        public ExpressionSettings Settings
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
