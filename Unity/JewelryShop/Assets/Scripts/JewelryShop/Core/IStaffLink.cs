using System;

namespace JewelryShop.Core
{
    /// <summary>The wire to the other end of Staff Call. The Netcode-backed one lives in JewelryShop.Networking.</summary>
    public interface IStaffLink
    {
        /// <summary>Client: connected to the host. Host: a client is attached (hosting alone is not "connected").</summary>
        bool IsConnected { get; }

        /// <summary>A connection attempt is in flight (started, neither connected nor failed yet).</summary>
        bool IsConnecting { get; }

        /// <summary>Start one connection attempt. Safe to call repeatedly.</summary>
        void Connect();

        /// <summary>Drop the link and abort any attempt in flight.</summary>
        void Disconnect();

        /// <summary>Send one message to the other end; dropped when not connected.</summary>
        void Send(StaffCallMessage message);

        /// <summary>Raised when the other end sends a message.</summary>
        event Action<StaffCallMessage> MessageReceived;
    }
}
