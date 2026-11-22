using JewelryShop.Core;
using Unity.Netcode;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// The Customer's pose on the link. The Host spawns it owned by the Quest. On the Quest it
    /// copies the scene's pose mailbox into a NetworkVariable (only when it changed); on the
    /// Staff Console it copies the variable into the mailbox that the Customer Avatar reads.
    /// Carries no visuals, so the Quest shows nothing.
    /// </summary>
    public sealed class CustomerPoseNetwork : NetworkBehaviour
    {
        // A struct of floats is memcpy-safe. HasPose is false until the Quest publishes, and
        // goes false again when its source stops.
        internal struct Wire : INetworkSerializeByMemcpy
        {
            public CustomerPose Pose;
            public bool HasPose;
        }

        private readonly NetworkVariable<Wire> _wire = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private CustomerPoseMailbox _mailbox;

        /// <summary>The host hands over the mailbox before it spawns this object.</summary>
        internal void Bind(CustomerPoseMailbox mailbox) => _mailbox = mailbox;

        /// <summary>Gets the pose mailbox (the Quest finds it) and, on the host, starts copying the
        /// Quest's pose into it.</summary>
        public override void OnNetworkSpawn()
        {
            if (_mailbox == null)
            {
                // The Quest's copy is made by Netcode, so it cannot be bound: one lookup per spawn.
                var link = FindAnyObjectByType<StaffLinkBehaviour>();
                if (link == null)
                {
                    Debug.LogWarning("CustomerPoseNetwork: no StaffLinkBehaviour in the scene.", this);
                    return;
                }
                _mailbox = link.CustomerPose;
            }
            if (IsServer) _wire.OnValueChanged += OnWireChanged;
        }

        /// <summary>Stops copying and clears the mailbox so no stale pose is left behind.</summary>
        public override void OnNetworkDespawn()
        {
            _wire.OnValueChanged -= OnWireChanged;
            if (IsServer) _mailbox?.Clear();
        }

        private void OnWireChanged(Wire _, Wire wire)
        {
            if (wire.HasPose) _mailbox.Publish(wire.Pose);
            else _mailbox.Clear();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || IsServer || _mailbox == null) return;
            var current = _wire.Value;
            if (!_mailbox.IsAvailable)
            {
                if (current.HasPose) _wire.Value = default;
                return;
            }
            var pose = _mailbox.Latest;
            if (!current.HasPose || pose.HasChanged(current.Pose))
                _wire.Value = new Wire { Pose = pose, HasPose = true };
        }
    }

    /// <summary>
    /// Host side: spawns the Customer's pose object, owned by the connected Quest, and removes it
    /// when the Quest leaves. Call <see cref="Tick"/> every frame so a client that is not yet in
    /// the manager's list when the link reports connected still gets one.
    /// </summary>
    internal sealed class NetcodeCustomerPoseSpawner
    {
        private readonly NetworkManager _manager;
        private readonly GameObject _prefab;
        private NetworkObject _spawned;
        private readonly CustomerPoseMailbox _mailbox;
        private bool _isWanted;

        public NetcodeCustomerPoseSpawner(
            NetworkManager manager, GameObject prefab, CustomerPoseMailbox mailbox)
        {
            _manager = manager;
            _prefab = prefab;
            _mailbox = mailbox;
        }

        public void SetConnected(bool isConnected)
        {
            _isWanted = isConnected;
            if (!isConnected) Despawn();
        }

        public void Tick()
        {
            if (!_isWanted || _spawned != null) return;
            foreach (var clientId in _manager.ConnectedClientsIds)
            {
                if (clientId == _manager.LocalClientId) continue;
                var instance = Object.Instantiate(_prefab);
                instance.GetComponent<CustomerPoseNetwork>().Bind(_mailbox);
                _spawned = instance.GetComponent<NetworkObject>();
                _spawned.SpawnWithOwnership(clientId, true);
                return;
            }
        }

        private void Despawn()
        {
            if (_spawned == null) return;
            // Already gone when the host shut down; Despawn on a stopped manager would throw.
            if (_spawned.IsSpawned) _spawned.Despawn(true);
            else Object.Destroy(_spawned.gameObject);
            _spawned = null;
        }
    }
}
