using System;

namespace JewelryShop.Core
{
    /// <summary>
    /// The one place Staff Call connection state lives (ADR-0008 Facade): raises a
    /// connected/disconnected event and, while not connected, re-attempts every retry interval so a
    /// restarted peer reconnects. An attempt in flight is left alone until it ends, or aborted once
    /// it outlives the attempt timeout.
    /// </summary>
    public sealed class StaffLinkFacade
    {
        private readonly IStaffLink _link;
        private readonly double _retrySeconds;
        private readonly double _attemptTimeoutSeconds;
        private bool _isWanted;
        private bool _isConnected;
        private double _lastAttemptSeconds = double.NegativeInfinity;

        /// <summary>Wraps a link; retry and attempt-timeout are in seconds.</summary>
        public StaffLinkFacade(IStaffLink link, double retrySeconds, double attemptTimeoutSeconds)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _retrySeconds = retrySeconds;
            _attemptTimeoutSeconds = attemptTimeoutSeconds;
            _link.MessageReceived += message => MessageReceived?.Invoke(message);
        }

        /// <summary>Raised when the other end sends a Staff Call message.</summary>
        public event Action<StaffCallMessage> MessageReceived;

        /// <summary>Send a Staff Call message; the link drops it when not connected.</summary>
        public void Send(StaffCallMessage message) => _link.Send(message);

        /// <summary>Whether the link is connected right now (as of the last tick).</summary>
        public bool IsConnected => _isConnected;

        /// <summary>Raised with the new state whenever it changes.</summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>Start connecting, and keep retrying until connected or disconnected.</summary>
        public void Connect()
        {
            _isWanted = true;
            _lastAttemptSeconds = double.NegativeInfinity;
        }

        /// <summary>Drop the link and stop retrying.</summary>
        public void Disconnect()
        {
            _isWanted = false;
            _link.Disconnect();
            Poll();
        }

        /// <summary>Drop the link, point it at a new host address, and connect again.</summary>
        public void Reconnect(string serverAddress)
        {
            Disconnect();
            _link.SetServerAddress(serverAddress);
            Connect();
        }

        /// <summary>Call every frame with a monotonic clock in seconds.</summary>
        public void Tick(double nowSeconds)
        {
            Poll();
            if (!_isWanted || _isConnected) return;
            var sinceAttemptSeconds = nowSeconds - _lastAttemptSeconds;
            if (_link.IsConnecting)
            {
                // Never tear down a handshake that is still running; only abort a stuck one.
                if (sinceAttemptSeconds >= _attemptTimeoutSeconds) _link.Disconnect();
                return;
            }
            if (sinceAttemptSeconds < _retrySeconds) return;
            _lastAttemptSeconds = nowSeconds;
            _link.Connect();
        }

        private void Poll()
        {
            var isConnected = _link.IsConnected;
            if (isConnected == _isConnected) return;
            _isConnected = isConnected;
            ConnectionChanged?.Invoke(isConnected);
        }
    }
}
