using System;
using JewelryShop.Core;
using Unity.Collections;
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
        private const string MessageName = "StaffCall";
        private readonly NetworkManager _networkManager;
        private readonly UnityTransport _transport;
        private StaffLinkSettings _settings;

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

        /// <inheritdoc />
        public void SetServerAddress(string address) => _settings.ServerAddress = address;

        /// <summary>Raised when the host cannot start (e.g. port in use).</summary>
        public event Action<string> Failed;

        public bool IsConnected => _settings.IsHost
            ? _networkManager.IsListening && _networkManager.ConnectedClientsIds.Count > 1
            : _networkManager.IsConnectedClient;

        public bool IsConnecting => !_settings.IsHost
            && _networkManager.IsListening
            && !_networkManager.IsConnectedClient
            && !_networkManager.ShutdownInProgress;

        /// <inheritdoc />
        public event Action<StaffCallMessage> MessageReceived;

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
                else RegisterMessageHandler();
                return;
            }
            _transport.SetConnectionData(_settings.ServerAddress, _settings.Port);
            if (_networkManager.StartClient()) RegisterMessageHandler();
        }

        public void Send(StaffCallMessage message)
        {
            // No peer: drop the message, never loop it back to ourselves.
            if (!IsConnected || !TryGetPeerId(out var target)) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteByteSafe(StaffCallMessageCodec.Encode(message));
            _networkManager.CustomMessagingManager.SendNamedMessage(MessageName, target, writer);
        }

        // The messaging manager is rebuilt on every start, so register after each one.
        private void RegisterMessageHandler() => _networkManager.CustomMessagingManager
            .RegisterNamedMessageHandler(MessageName, OnMessage);

        private void OnMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadByteSafe(out var value);
            // Trust boundary: ignore a value this build does not know.
            if (StaffCallMessageCodec.TryDecode(value, out var message))
                MessageReceived?.Invoke(message);
        }

        private bool TryGetPeerId(out ulong peerId)
        {
            if (!_settings.IsHost) { peerId = NetworkManager.ServerClientId; return true; }
            foreach (var id in _networkManager.ConnectedClientsIds)
            {
                if (id == _networkManager.LocalClientId) continue;
                peerId = id;
                return true;
            }
            peerId = default;
            return false;
        }

        public void Disconnect()
        {
            if (!_networkManager.IsListening) return;
            _networkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(MessageName);
            _networkManager.Shutdown();
        }
    }
}
