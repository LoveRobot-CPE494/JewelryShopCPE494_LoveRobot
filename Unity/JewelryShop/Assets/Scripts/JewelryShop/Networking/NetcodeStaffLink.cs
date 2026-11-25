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
    internal sealed class NetcodeStaffLink : IStaffLink, IVoiceChannel
    {
        private const string MessageName = "StaffCall";
        private const string VoiceMessageName = "StaffVoice";
        private byte[] _voiceBuffer = new byte[OpusVoiceCodec.MaxPacketBytes];
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

        /// <inheritdoc />
        public event Action<byte[], int> PacketReceived;

        /// <inheritdoc />
        public void Send(byte[] packet, int length)
        {
            if (!IsConnected || !TryGetPeerId(out var target)) return;
            using var writer = new FastBufferWriter(length + 2, Allocator.Temp);
            writer.WriteValueSafe((ushort)length);
            writer.WriteBytesSafe(packet, length);
            // Unreliable: a late voice packet is worthless, so never retransmit.
            _networkManager.CustomMessagingManager.SendNamedMessage(
                VoiceMessageName, target, writer, NetworkDelivery.Unreliable);
        }

        // The messaging manager is rebuilt on every start, so register after each one.
        private void RegisterMessageHandler()
        {
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                VoiceMessageName, OnVoice);
        }

        private void OnVoice(ulong senderId, FastBufferReader reader)
        {
            // Trust boundary: a truncated or hostile packet is dropped, never thrown on.
            try
            {
                const int headerBytes = sizeof(ushort);
                if (reader.Length - reader.Position < headerBytes) return;
                reader.ReadValueSafe(out ushort length);
                if (length == 0 || length > _voiceBuffer.Length
                    || reader.Length - reader.Position < length) return;
                reader.ReadBytesSafe(ref _voiceBuffer, length);
                PacketReceived?.Invoke(_voiceBuffer, length);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Staff voice: dropped a bad packet: {e.Message}");
            }
        }

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
            _networkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(VoiceMessageName);
            _networkManager.Shutdown();
        }
    }
}
