using System;
using JewelryShop.Core;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// The Customer's typed Host Address, saved on the headset; raises Changed when it is set.
    /// </summary>
    public static class HostAddressStore
    {
        /// <summary>PlayerPrefs key (tests swap it so they never touch the real address).</summary>
        public static string Key { get; set; } = "JewelryShop.HostAddress";

        /// <summary>PlayerPrefs key of the Robot Address.</summary>
        public static string RobotKey { get; set; } = "JewelryShop.RobotAddress";

        /// <summary>PlayerPrefs key of the Automatic IP in LAN flag.</summary>
        public static string AutomaticKey { get; set; } = "JewelryShop.AutomaticAddress";

        /// <summary>Raised after the Automatic flag was set.</summary>
        public static event Action AutomaticChanged;

        /// <summary>Automatic IP in LAN (ADR-0023); ON until the Customer turns it off.</summary>
        public static bool Automatic => PlayerPrefs.GetInt(AutomaticKey, 1) != 0;

        /// <summary>Saves the Automatic flag and raises AutomaticChanged.</summary>
        public static void SetAutomatic(bool on)
        {
            PlayerPrefs.SetInt(AutomaticKey, on ? 1 : 0);
            PlayerPrefs.Save();
            AutomaticChanged?.Invoke();
        }

        /// <summary>Raised after a new valid Host Address was saved.</summary>
        public static event Action Changed;

        /// <summary>Raised after a new valid Robot Address was saved.</summary>
        public static event Action RobotChanged;

        /// <summary>
        /// The saved address if it is valid IPv4, else the config default (null config: null).
        /// </summary>
        public static string Typed(HostAddressConfig config)
        {
            var saved = PlayerPrefs.GetString(Key, null);
            if (HostAddressResolver.IsValidIPv4(saved)) return saved;
            return config != null ? config.DefaultHostAddress : null;
        }

        /// <summary>Saves a valid IPv4 address and raises Changed; false if invalid.</summary>
        public static bool TrySetTyped(string address)
        {
            if (!HostAddressResolver.IsValidIPv4(address)) return false;
            PlayerPrefs.SetString(Key, address);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>The saved Robot Address if valid IPv4, else the config default.</summary>
        public static string TypedRobot(HostAddressConfig config)
        {
            var saved = PlayerPrefs.GetString(RobotKey, null);
            if (HostAddressResolver.IsValidIPv4(saved)) return saved;
            return config != null ? config.DefaultRobotAddress : null;
        }

        /// <summary>Saves a valid IPv4 Robot Address and raises RobotChanged; false if invalid.
        /// </summary>
        public static bool TrySetTypedRobot(string address)
        {
            if (!HostAddressResolver.IsValidIPv4(address)) return false;
            PlayerPrefs.SetString(RobotKey, address);
            PlayerPrefs.Save();
            RobotChanged?.Invoke();
            return true;
        }
    }
}
