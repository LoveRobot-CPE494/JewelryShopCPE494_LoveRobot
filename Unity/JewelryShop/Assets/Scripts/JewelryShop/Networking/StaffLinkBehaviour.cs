using JewelryShop.Core;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Scene host of the Staff Call link. Put one on the Staff Console scene (Role = Host) and one
    /// on the Quest scene (Role = Client). It creates its own NetworkManager object at the scene
    /// root at runtime (Netcode forbids nesting it), so do NOT also place a NetworkManager in the
    /// scene: two managers fight over the singleton.
    /// Scene synchronization is switched off, so each end keeps its own scene. Host and client must
    /// run the same build: Netcode compares the NetworkConfig and refuses a mismatch. Do not add
    /// scene-placed NetworkObjects to this link.
    /// </summary>
    public sealed class StaffLinkBehaviour : MonoBehaviour
    {
        /// <summary>Which end of the link this scene is.</summary>
        public enum LinkRole { Host, Client }

        [SerializeField] private LinkRole _role = LinkRole.Client;
        [Tooltip("Client only: supplies the default Host Address (ADR-0023); "
            + "the typed one is saved on the headset.")]
        [SerializeField] private HostAddressConfig _hostAddressConfig;
        [SerializeField] private ushort _port = 7777;
        [Tooltip("Host only: the address to listen on (0.0.0.0 = every network card).")]
        [SerializeField] private string _hostListenAddress = "0.0.0.0";
        [Tooltip("Seconds of silence before the link counts as lost (transport default is 30).")]
        [SerializeField] private float _disconnectTimeoutSeconds = 3f;
        [Tooltip("One handshake attempt, in ms; it is tried Max Connect Attempts times.")]
        [SerializeField] private int _connectTimeoutMilliseconds = 1000;
        [SerializeField] private int _maxConnectAttempts = 3;
        [SerializeField] private float _reconnectIntervalSeconds = 2f;
        [Tooltip("Turn ON for the one-PC localhost test, so an unfocused Editor keeps running.")]
        [SerializeField] private bool _isRunningInBackground;
        [Tooltip("Optional; shows 'connected' / 'not connected'.")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private UnityEvent<bool> _onConnectionChanged;

        [Header("Cheryll (Staff Avatar)")]
        [Tooltip("Networked prefab (has a NetworkObject). Put it on BOTH ends: "
            + "the Quest must know it to show her.")]
        [SerializeField] private GameObject _cheryllPrefab;
        [Tooltip("Host only: where Answer warps her (the Staff Door).")]
        [SerializeField] private Transform _cheryllSpawnPoint;
        [Tooltip("Host only: her spot in the Staff Room; hang-up warps her back here.")]
        [SerializeField] private Transform _cheryllRoomPoint;
        [Tooltip("Host only: the Staff's own Cheryll. The copy spawns on link connect and "
            + "follows her; Answer and hang-up warp her.")]
        [SerializeField] private Transform _cheryllSource;

        [Header("Customer pose (Customer Avatar on the Staff Console)")]
        [Tooltip("Networked prefab with a NetworkObject + CustomerPoseNetwork and NO visuals. "
            + "Put it on BOTH ends. Host spawns it when the Quest connects.")]
        [SerializeField] private GameObject _customerPosePrefab;

        private StaffLinkFacade _facade;
        private NetcodeCustomerPoseSpawner _customerPoseSpawner;
        private GameObject _managerObject;
        private bool _hasHostAddress;
        private ICheryllSpawner _cheryllSpawner = new NullCheryllSpawner();

        /// <summary>The link's Facade: connect, disconnect and the connection event.</summary>
        public StaffLinkFacade Facade => _facade;

        /// <summary>The Customer's latest pose: the Quest scene writes it, the Staff Console reads it.</summary>
        public CustomerPoseMailbox CustomerPose { get; } = new CustomerPoseMailbox();

        /// <summary>True on the Staff Console.</summary>
        public bool IsHost => _role == LinkRole.Host;

        /// <summary>Spawns Cheryll on the host; a no-op when the prefab is not wired.</summary>
        public ICheryllSpawner CheryllSpawner => _cheryllSpawner;

        private void Awake()
        {
            if (_statusText == null)
                Debug.LogWarning("StaffLinkBehaviour: status text not wired; state is only logged.", this);
            if (_isRunningInBackground) Application.runInBackground = true;

            // Inactive while assembling so NetworkManager.Awake sees its transport.
            // Stays at the scene root: Netcode refuses a NetworkManager nested under a parent.
            _managerObject = new GameObject("StaffLinkNetworkManager");
            _managerObject.SetActive(false);
            var transport = _managerObject.AddComponent<UnityTransport>();
            var networkManager = _managerObject.AddComponent<NetworkManager>();
            // Scene sync off: otherwise a connecting client loads the host's scene over its own.
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
            };
            if (_cheryllPrefab == null)
                Debug.LogWarning(
                    "StaffLinkBehaviour: Cheryll prefab not wired; she will not appear.", this);
            else if (_cheryllPrefab.GetComponent<NetworkObject>() == null)
                Debug.LogError("StaffLinkBehaviour: Cheryll prefab has no NetworkObject.", this);
            else
            {
                networkManager.AddNetworkPrefab(_cheryllPrefab);
                if (_role == LinkRole.Host)
                {
                    if (_cheryllSpawnPoint == null || _cheryllRoomPoint == null
                        || _cheryllSource == null)
                        Debug.LogWarning("StaffLinkBehaviour: Cheryll door point, room point or "
                            + "source not wired; Answer or hang-up will not move her.", this);
                    _cheryllSpawner = new NetcodeCheryllSpawner(
                        _cheryllPrefab, _cheryllSpawnPoint, _cheryllRoomPoint, _cheryllSource);
                }
            }
            // Loud on both ends: a client without the prefab registered cannot take the Host's spawn.
            if (_customerPosePrefab == null)
                Debug.LogError(
                    "StaffLinkBehaviour: Customer pose prefab not wired; the Customer Avatar will not appear.", this);
            else if (_customerPosePrefab.GetComponent<NetworkObject>() == null
                || _customerPosePrefab.GetComponent<CustomerPoseNetwork>() == null)
                Debug.LogError(
                    "StaffLinkBehaviour: Customer pose prefab needs a NetworkObject and a CustomerPoseNetwork.", this);
            else
            {
                networkManager.AddNetworkPrefab(_customerPosePrefab);
                if (_role == LinkRole.Host)
                    _customerPoseSpawner = new NetcodeCustomerPoseSpawner(
                        networkManager, _customerPosePrefab, CustomerPose);
            }
            _managerObject.SetActive(true);

            var serverAddress = _role == LinkRole.Client ? ResolveHostAddress() : null;
            _hasHostAddress = serverAddress != null;
            var link = new NetcodeStaffLink(networkManager, transport, new StaffLinkSettings
            {
                IsHost = _role == LinkRole.Host,
                ServerAddress = serverAddress,
                ListenAddress = _hostListenAddress,
                Port = _port,
                DisconnectTimeoutMilliseconds = (int)(_disconnectTimeoutSeconds * 1000f),
                ConnectTimeoutMilliseconds = _connectTimeoutMilliseconds,
                MaxConnectAttempts = _maxConnectAttempts,
            });
            link.Failed += OnFailed;
            // An attempt that outlives its own handshake budget (plus a second of slack) is stuck.
            var attemptBudgetSeconds =
                _connectTimeoutMilliseconds * _maxConnectAttempts / 1000.0 + 1.0;
            _facade = new StaffLinkFacade(link, _reconnectIntervalSeconds, attemptBudgetSeconds);
            _facade.ConnectionChanged += Show;
            if (_customerPoseSpawner != null) _facade.ConnectionChanged += _customerPoseSpawner.SetConnected;
            if (_role == LinkRole.Host) _facade.ConnectionChanged += SetCheryllPresent;
            Show(false);
            if (_role == LinkRole.Client) HostAddressStore.Changed += OnHostAddressChanged;
        }

        private void Start()
        {
            if (_role == LinkRole.Host || _hasHostAddress) _facade.Connect();
        }

        // shortcut: typed address only (automatic=false, discovered=null); Host Discovery (#206).
        private string ResolveHostAddress()
        {
            var typed = HostAddressStore.Typed(_hostAddressConfig);
            var address = HostAddressResolver.Resolve(typed, false, null);
            if (address == null)
                Debug.LogWarning("StaffLinkBehaviour: no valid Host Address (wire a Host "
                    + "Address Config or type one); the Staff Link will not connect.", this);
            return address;
        }

        private void OnHostAddressChanged()
        {
            var address = ResolveHostAddress();
            if (address == null) return;
            _hasHostAddress = true;
            _facade.Reconnect(address);
        }

        private void Update()
        {
            _facade.Tick(Time.realtimeSinceStartupAsDouble);
            _customerPoseSpawner?.Tick();
        }

        private void OnDestroy()
        {
            HostAddressStore.Changed -= OnHostAddressChanged;
            if (_facade == null) return;
            _facade.ConnectionChanged -= Show;
            _facade.ConnectionChanged -= SetCheryllPresent;
            if (_customerPoseSpawner != null) _facade.ConnectionChanged -= _customerPoseSpawner.SetConnected;
            _facade.Disconnect();
            Destroy(_managerObject); // it is a scene root, so it would outlive this object
        }

        private void OnFailed(string message)
        {
            Debug.LogError($"Staff link ({_role}): {message}", this);
            if (_statusText != null) _statusText.text = message;
            _facade.Disconnect(); // retrying a port that is taken never helps
        }

        // Cheryll's copy exists exactly while a Customer is linked (ADR-0022).
        private void SetCheryllPresent(bool isConnected)
        {
            if (isConnected) _cheryllSpawner.Spawn();
            else _cheryllSpawner.Despawn();
        }

        private void Show(bool isConnected)
        {
            var text = isConnected ? "connected" : "not connected";
            if (_statusText != null) _statusText.text = text;
            Debug.Log($"Staff link ({_role}): {text}");
            _onConnectionChanged?.Invoke(isConnected);
        }
    }
}
