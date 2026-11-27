using System;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Resolves the Host Address (Staff PC) and Robot Address (Robot PC) the Quest connects to
    /// (ADR-0023, ADR-0024, ADR-0027). Typed address and Automatic flag come from
    /// <see cref="HostAddressStore"/>; beacons come from the listener. Plain C#: each owner
    /// (the client StaffLinkBehaviour, MG400Teleop) holds its own and calls <see cref="Tick"/>
    /// from Update. The listener binds with ReuseAddress, so two owners can share the port.
    /// </summary>
    public sealed class HostDiscovery : IDisposable
    {
        private const double RetryBindSeconds = 5.0;

        private readonly HostAddressConfig _config;
        private readonly HostBeaconListener _listener;
        private readonly BeaconBoard _board = new BeaconBoard();
        private double _nextBindAtSeconds;

        /// <summary>Discovery on the config's port; a null config leaves it typed-only.</summary>
        public HostDiscovery(HostAddressConfig config)
        {
            _config = config;
            _listener = new HostBeaconListener(config != null ? config.DiscoveryPort : (ushort)0);
        }

        /// <summary>Resolved Staff PC address (Staff Link), or null when none is valid.</summary>
        public string HostAddress { get; private set; }

        /// <summary>Resolved Robot PC address (Teleop), or null when none is valid.</summary>
        public string RobotAddress { get; private set; }

        /// <summary>Where <see cref="HostAddress"/> came from.</summary>
        public AddressState HostState { get; private set; }

        /// <summary>Where <see cref="RobotAddress"/> came from.</summary>
        public AddressState RobotState { get; private set; }

        /// <summary>Raised when the Host or Robot address or its state changes; read the
        /// properties inside the handler.</summary>
        public event Action Changed;

        /// <summary>Feeds a received datagram. A malformed one is ignored.</summary>
        public bool Feed(byte[] datagram, double nowSeconds) =>
            _board.TryAccept(datagram, nowSeconds);

        /// <summary>Polls the socket (while Automatic is on), then re-resolves. Never throws.
        /// </summary>
        public void Tick(double nowSeconds)
        {
            if (HostAddressStore.Automatic)
            {
                if (!_listener.IsActive && nowSeconds >= _nextBindAtSeconds)
                {
                    _nextBindAtSeconds = nowSeconds + RetryBindSeconds;
                    _listener.Start();
                }
                _listener.Poll(_board, nowSeconds);
            }
            else if (_listener.IsActive) _listener.Stop();
            Refresh(nowSeconds);
        }

        /// <summary>Re-resolves both addresses now and raises Changed on any difference.</summary>
        public void Refresh(double nowSeconds)
        {
            var automatic = HostAddressStore.Automatic;
            var host = Resolve(BeaconRole.Staff, HostAddressStore.Typed(_config), automatic,
                nowSeconds, out var hostState);
            var robot = Resolve(BeaconRole.Robot, HostAddressStore.TypedRobot(_config), automatic,
                nowSeconds, out var robotState);
            if (host == HostAddress && robot == RobotAddress
                && hostState == HostState && robotState == RobotState) return;
            HostAddress = host;
            RobotAddress = robot;
            HostState = hostState;
            RobotState = robotState;
            Debug.Log($"HostDiscovery: Host {host ?? "none"} ({hostState}), "
                + $"Robot {robot ?? "none"} ({robotState}).");
            Changed?.Invoke();
        }

        private string Resolve(BeaconRole role, string typed, bool automatic, double now,
            out AddressState state)
        {
            _board.TryGet(role, now, out var discovered, out var age);
            return HostAddressResolver.Resolve(typed, automatic, discovered, age, out state);
        }

        /// <summary>Closes the listener and releases the multicast lock.</summary>
        public void Dispose() => _listener.Dispose();
    }
}
