namespace JewelryShop.Core
{
    /// <summary>Which end of the Staff Call this session runs on.</summary>
    public enum StaffCallRole { Customer, Staff }

    /// <summary>
    /// Customer: Idle, Calling, StaffComing, InCall. Staff: Idle, Incoming, Serving.
    /// </summary>
    public enum StaffCallState { Idle, Calling, StaffComing, InCall, Incoming, Serving }

    /// <summary>
    /// What crosses the link. Customer sends Call and Cancel; Staff sends Answer and Arrived;
    /// either sends HangUp.
    /// </summary>
    public enum StaffCallMessage : byte { Call, Cancel, Answer, HangUp, Arrived }

    /// <summary>
    /// Spawns and removes Cheryll (the Staff Avatar). Host side only: scene synchronization is off,
    /// so she must be spawned from a networked prefab at runtime, never placed in a scene.
    /// </summary>
    public interface ICheryllSpawner
    {
        /// <summary>Make Cheryll appear at the Staff Door.</summary>
        void Spawn();

        /// <summary>Remove Cheryll; safe when she is not there.</summary>
        void Despawn();
    }

    /// <summary>Safe fallback when no prefab is wired: Cheryll simply never appears.</summary>
    public sealed class NullCheryllSpawner : ICheryllSpawner
    {
        /// <inheritdoc />
        public void Spawn() { }

        /// <inheritdoc />
        public void Despawn() { }
    }
}
