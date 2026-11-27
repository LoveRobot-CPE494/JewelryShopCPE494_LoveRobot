using System;
using System.Net;
using System.Net.Sockets;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Listens for Host Beacons on the discovery port (ADR-0023) and holds the Android multicast
    /// lock while it listens. Plain C#: the owner calls <see cref="Poll"/> from Update. Receive is
    /// polled with Available, so it never blocks the main thread; garbage is dropped by the board.
    /// </summary>
    public sealed class HostBeaconListener : IDisposable
    {
        private readonly ushort _port;
        private UdpClient _socket;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _multicastLock;
#endif

        // A flood must not stall a frame; the rest wait for the next Poll.
        private const int MaxDatagramsPerPoll = 32;

        /// <summary>A listener for the given discovery port (0: never binds).</summary>
        public HostBeaconListener(ushort discoveryPort) => _port = discoveryPort;

        /// <summary>True while the socket is bound.</summary>
        public bool IsActive => _socket != null;

        /// <summary>Binds the port, takes the multicast lock; false (logged) on failure.</summary>
        public bool Start()
        {
            if (_socket != null) return true;
            if (_port == 0) return false;
            try
            {
                _socket = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                _socket.Client.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _socket.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                AcquireMulticastLock();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HostBeaconListener: cannot listen on UDP {_port} "
                    + $"({e.GetType().Name}: {e.Message}). Host Discovery is off until it can.");
                Stop();
                return false;
            }
        }

        /// <summary>Closes the socket and releases the multicast lock.</summary>
        public void Stop()
        {
            _socket?.Close();
            _socket = null;
            ReleaseMulticastLock();
        }

        /// <summary>Feeds waiting datagrams (at most 32) to the board. Never throws.</summary>
        public void Poll(BeaconBoard board, double nowSeconds)
        {
            if (_socket == null) return;
            try
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                for (var i = 0; i < MaxDatagramsPerPoll && _socket.Available > 0; i++)
                    board.TryAccept(_socket.Receive(ref from), nowSeconds);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HostBeaconListener: receive failed ({e.GetType().Name}: "
                    + $"{e.Message}); listening restarts.");
                Stop();
            }
        }

        /// <summary>Same as <see cref="Stop"/>.</summary>
        public void Dispose() => Stop();

        private void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Without the lock the headset Wi-Fi drops broadcast and multicast to save power.
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
            {
                _multicastLock = wifi.Call<AndroidJavaObject>(
                    "createMulticastLock", "JewelryShopHostBeacon");
                _multicastLock.Call("setReferenceCounted", false);
                _multicastLock.Call("acquire");
            }
#endif
        }

        private void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_multicastLock == null) return;
            try { _multicastLock.Call("release"); }
            catch (Exception e)
            {
                Debug.LogWarning($"HostBeaconListener: lock release failed: {e.Message}");
            }
            _multicastLock.Dispose();
            _multicastLock = null;
#endif
        }
    }
}
