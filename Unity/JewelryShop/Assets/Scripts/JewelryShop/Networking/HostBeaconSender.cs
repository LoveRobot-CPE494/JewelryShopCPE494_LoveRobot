using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Broadcasts the Host Beacon about once a second (ADR-0023) and owns the UDP socket.
    /// Plain C#: the owner calls <see cref="Tick"/> from Update, <see cref="Dispose"/> on destroy.
    /// </summary>
    public sealed class HostBeaconSender : IDisposable
    {
        private const double SendIntervalSeconds = 1.0;
        // Enumerating network cards can hitch the main thread, so it runs on a slower timer.
        private const double PickIntervalSeconds = 5.0;

        private readonly ushort _staffLinkPort;
        private readonly ushort _discoveryPort;
        private readonly IPEndPoint _target;
        private UdpClient _socket;
        private byte[] _beacon;
        private double _nextPickAtSeconds;
        private double _nextSendAtSeconds;
        private bool _failureLogged;
        private bool _noAddressLogged;

        /// <summary>A discovery port of 0 keeps it quiet; it still looks up the address.</summary>
        public HostBeaconSender(ushort staffLinkPort, ushort discoveryPort)
        {
            _staffLinkPort = staffLinkPort;
            _discoveryPort = discoveryPort;
            _target = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
        }

        /// <summary>The local IPv4 being announced, or null while none is found.</summary>
        public string LocalAddress { get; private set; }

        /// <summary>Raised when <see cref="LocalAddress"/> changes (including to null).</summary>
        public event Action<string> LocalAddressChanged;

        /// <summary>Re-picks the address every 5 s, sends every 1 s. Never throws.</summary>
        public void Tick(double nowSeconds)
        {
            try
            {
                if (nowSeconds >= _nextPickAtSeconds)
                {
                    _nextPickAtSeconds = nowSeconds + PickIntervalSeconds;
                    Repick();
                }
                if (nowSeconds < _nextSendAtSeconds) return;
                _nextSendAtSeconds = nowSeconds + SendIntervalSeconds;
                if (_socket == null) return;
                _socket.Send(_beacon, _beacon.Length, _target);
                _failureLogged = false;
            }
            catch (Exception e)
            {
                if (_failureLogged) return;
                _failureLogged = true; // once, not every second
                Debug.LogWarning($"HostBeaconSender: beacon not sent ({e.GetType().Name}: "
                    + $"{e.Message}). Check the PC firewall allows UDP out on the discovery port.");
            }
        }

        private void Repick()
        {
            var address = LocalAddressPicker.Pick(ReadCandidates());
            if (address == null && !_noAddressLogged)
                Debug.LogWarning("HostBeaconSender: no local IPv4 address with a gateway; "
                    + "no Host Beacon until the PC joins a network.");
            _noAddressLogged = address == null;
            if (address != LocalAddress)
            {
                LocalAddress = address;
                LocalAddressChanged?.Invoke(address);
            }
            // Same address with a live socket: nothing to do. A bind that threw is retried here.
            else if (_socket != null || address == null || _discoveryPort == 0) return;

            _socket?.Close();
            _socket = null;
            if (address != null && _discoveryPort != 0)
            {
                _beacon = HostBeaconCodec.Encode(BeaconRole.Staff, address, _staffLinkPort);
                // Bound to the picked card so the broadcast leaves through that one.
                _socket = new UdpClient(new IPEndPoint(IPAddress.Parse(address), 0))
                {
                    EnableBroadcast = true,
                };
            }
        }

        private static IEnumerable<AddressCandidate> ReadCandidates()
        {
            foreach (var card in NetworkInterface.GetAllNetworkInterfaces())
            {
                var properties = card.GetIPProperties();
                var hasGateway = false;
                foreach (var gateway in properties.GatewayAddresses)
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork
                        && !gateway.Address.Equals(IPAddress.Any)) hasGateway = true;
                var isUp = card.OperationalStatus == OperationalStatus.Up;
                foreach (var unicast in properties.UnicastAddresses)
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        yield return new AddressCandidate(
                            unicast.Address.ToString(), isUp, hasGateway);
            }
        }

        /// <summary>Closes the socket.</summary>
        public void Dispose()
        {
            _socket?.Close();
            _socket = null;
        }
    }
}
