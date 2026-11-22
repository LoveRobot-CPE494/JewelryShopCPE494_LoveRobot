using JewelryShop.Core;
using Unity.Netcode;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Host side: spawns Cheryll from her networked prefab at the Staff Door and despawns her.
    /// The Staff's own Cheryll is warped to the door too, and the spawned copy follows her.
    /// The prefab must be registered on both ends (StaffLinkBehaviour does it).
    /// </summary>
    internal sealed class NetcodeCheryllSpawner : ICheryllSpawner
    {
        private readonly GameObject _prefab;
        private readonly Transform _spawnPoint;
        private readonly Transform _source;
        private NetworkObject _spawned;

        public NetcodeCheryllSpawner(GameObject prefab, Transform spawnPoint, Transform source)
        {
            _prefab = prefab;
            _spawnPoint = spawnPoint;
            _source = source;
        }

        public void Spawn()
        {
            if (_spawned != null) return;
            if (_spawnPoint != null && _source != null) Warp(_source, _spawnPoint);
            var pose = _source != null ? _source : _spawnPoint;
            var position = pose != null ? pose.position : Vector3.zero;
            var rotation = pose != null ? pose.rotation : Quaternion.identity;
            var instance = Object.Instantiate(_prefab, position, rotation);
            _spawned = instance.GetComponent<NetworkObject>();
            var avatar = instance.GetComponent<CheryllNetworkAvatar>();
            if (avatar != null) avatar.Follow(_source);
            _spawned.Spawn(true);
        }

        public void Despawn()
        {
            if (_spawned == null) return;
            // Already gone when the host shut down; Despawn on a stopped manager would throw.
            if (_spawned.IsSpawned) _spawned.Despawn(true);
            else Object.Destroy(_spawned.gameObject);
            _spawned = null;
        }

        // A CharacterController overrides direct teleports, so switch it off for the jump.
        private static void Warp(Transform who, Transform to)
        {
            var controller = who.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            who.SetPositionAndRotation(to.position, to.rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
