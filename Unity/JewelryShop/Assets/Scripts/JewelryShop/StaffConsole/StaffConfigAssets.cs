using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only movement tuning for the Staff Avatar (ADR-0018).</summary>
    [CreateAssetMenu(fileName = "StaffMovementConfig",
        menuName = "JewelryShop/Staff Console/Movement Config")]
    public sealed class StaffMovementConfig : ScriptableObject
    {
        [SerializeField] private MovementSettings _settings = MovementSettings.Defaults;

        /// <summary>The settings, clamped; invalid values log a warning.</summary>
        public MovementSettings Settings
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
