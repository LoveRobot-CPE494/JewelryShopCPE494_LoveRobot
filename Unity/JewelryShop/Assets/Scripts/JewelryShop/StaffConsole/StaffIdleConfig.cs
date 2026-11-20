using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only Idle fidget timing (ADR-0018).</summary>
    [CreateAssetMenu(fileName = "StaffIdleConfig",
        menuName = "JewelryShop/Staff Console/Idle Config")]
    public sealed class StaffIdleConfig : ScriptableObject
    {
        [SerializeField] private IdleSettings _settings = IdleSettings.Defaults;

        /// <summary>The settings, validated; invalid values log a warning.</summary>
        public IdleSettings Settings
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
