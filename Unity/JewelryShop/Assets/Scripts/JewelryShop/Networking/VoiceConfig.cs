using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Data-only live voice tuning (ADR-0018). Read once when the component starts. Turn
    /// Is Half Duplex Enabled on only if echo of Cheryll's voice is heard on a real Quest.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceConfig", menuName = "JewelryShop/Staff Call/Voice Config")]
    public sealed class VoiceConfig : ScriptableObject
    {
        [SerializeField] private VoiceSettings _settings = VoiceSettings.Defaults;

        /// <summary>The settings, validated; invalid values log a warning.</summary>
        public VoiceSettings Settings
        {
            get
            {
                var warnings = new List<string>();
                var valid = _settings.Validated(warnings);
                foreach (var warning in warnings) Debug.LogWarning($"{name}: {warning}", this);
                return valid;
            }
        }
    }
}
