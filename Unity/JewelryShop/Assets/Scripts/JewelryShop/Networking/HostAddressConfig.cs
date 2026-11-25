using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>Data-only Host Address default and ports (ADR-0018, ADR-0023).</summary>
    [CreateAssetMenu(fileName = "HostAddressConfig",
        menuName = "JewelryShop/Networking/Host Address Config")]
    public sealed class HostAddressConfig : ScriptableObject
    {
        [Tooltip("Used until the Customer types one. A placeholder: "
            + "set the Staff Console PC's LAN IP.")]
        [SerializeField] private string _defaultHostAddress = "192.168.1.10";
        // shortcut: ports unread, StaffLinkBehaviour._port drives; wire in Host Discovery (#206).
        [SerializeField] private ushort _staffLinkPort = 7777;
        [SerializeField] private ushort _discoveryPort = 7778;

        public string DefaultHostAddress => _defaultHostAddress;
        public ushort StaffLinkPort => _staffLinkPort;
        public ushort DiscoveryPort => _discoveryPort;
    }
}
