namespace JewelryShop.Core
{
    /// <summary>Rules for the Staff Console Idle Lock: no random Standby while it is ON.</summary>
    public static class IdleLock
    {
        /// <summary>Whether Idle may start a random Standby variation.</summary>
        public static bool AllowsStandby(bool isMoving, bool isIdleLockOn)
        {
            return !isMoving && !isIdleLockOn;
        }

        /// <summary>The panel status line.</summary>
        public static string StatusText(bool isIdleLockOn)
        {
            return isIdleLockOn ? "Idle Lock: ON" : "Idle Lock: OFF";
        }
    }
}
