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
        [Tooltip("Robot PC (Teleop and Robot Panel). A placeholder: set the Robot PC's LAN IP.")]
        [SerializeField] private string _defaultRobotAddress = "192.168.1.11";
        [SerializeField] private ushort _teleopPort = 9000;
        [SerializeField] private ushort _robotStatusPort = 9001;
        // shortcut: Staff Link port unread, StaffLinkBehaviour._port drives it; upgrade by
        // reading it there. The discovery port is read by the Host Beacon.
        [SerializeField] private ushort _staffLinkPort = 7777;
        [SerializeField] private ushort _discoveryPort = 7778;

        public string DefaultHostAddress => _defaultHostAddress;
        public string DefaultRobotAddress => _defaultRobotAddress;
        public ushort TeleopPort => _teleopPort;
        public ushort RobotStatusPort => _robotStatusPort;
        public ushort StaffLinkPort => _staffLinkPort;
        public ushort DiscoveryPort => _discoveryPort;
    }
}
