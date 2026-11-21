using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>Data-only camera tuning for the Staff Console (ADR-0018).</summary>
    [CreateAssetMenu(fileName = "StaffCameraConfig",
        menuName = "JewelryShop/Staff Console/Camera Config")]
    public sealed class StaffCameraConfig : ScriptableObject
    {
        [SerializeField] private CameraSettings _settings = CameraSettings.Defaults;

        /// <summary>The settings, clamped; invalid values log a warning.</summary>
        public CameraSettings Settings
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
