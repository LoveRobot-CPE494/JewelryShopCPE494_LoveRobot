using System;
using JewelryShop.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace JewelryShop.Networking
{
    /// <summary>Everything the link needs from the Inspector, bundled.</summary>
    internal struct StaffLinkSettings
    {
        /// <summary>True on the Staff Console, false on the Quest.</summary>
        public bool IsHost;
        /// <summary>Client: the host's address.</summary>
        public string ServerAddress;
        /// <summary>Host: the address to listen on.</summary>
        public string ListenAddress;
        public ushort Port;
        public int DisconnectTimeoutMilliseconds;
        public int ConnectTimeoutMilliseconds;
        public int MaxConnectAttempts;
    }

    /// <summary>
    /// The only class that drives Netcode: the Staff Console hosts, the Quest connects as a client.
    /// The host counts as connected only while a client is attached, never merely for listening.
    /// </summary>
    internal sealed class NetcodeStaffLink : IStaffLink
    {
        private readonly NetworkManager _networkManager;
        private readonly UnityTransport _transport;
        private readonly StaffLinkSettings _settings;

        public NetcodeStaffLink(
            NetworkManager networkManager, UnityTransport transport, StaffLinkSettings settings)
        {
            _networkManager = networkManager;
            _transport = transport;
            _settings = settings;
            // Transport default is 30 s; "not connected within a few seconds" needs it short.
            _transport.DisconnectTimeoutMS = settings.DisconnectTimeoutMilliseconds;
            _transport.ConnectTimeoutMS = settings.ConnectTimeoutMilliseconds;
            _transport.MaxConnectAttempts = settings.MaxConnectAttempts;
        }

        /// <summary>Raised when the host cannot start (e.g. port in use).</summary>
        public event Action<string> Failed;

        public bool IsConnected => _settings.IsHost
            ? _networkManager.IsListening && _networkManager.ConnectedClientsIds.Count > 1
            : _networkManager.IsConnectedClient;

        public bool IsConnecting => !_settings.IsHost
            && _networkManager.IsListening
            && !_networkManager.IsConnectedClient
            && !_networkManager.ShutdownInProgress;

        public void Connect()
        {
            // Listening already means hosting, connected, or a client attempt still in flight.
            if (_networkManager.ShutdownInProgress || _networkManager.IsListening) return;
            if (_settings.IsHost)
            {
                _transport.SetConnectionData(
                    _settings.ListenAddress, _settings.Port, _settings.ListenAddress);
                if (!_networkManager.StartHost())
                    Failed?.Invoke($"Cannot host on port {_settings.Port} (port in use?)");
                return;
            }
            _transport.SetConnectionData(_settings.ServerAddress, _settings.Port);
            _networkManager.StartClient();
        }

        public void Disconnect()
        {
            if (_networkManager.IsListening) _networkManager.Shutdown();
        }
    }
}
