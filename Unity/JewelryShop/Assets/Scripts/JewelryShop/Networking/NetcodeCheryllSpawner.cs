using JewelryShop.Core;
using Unity.Netcode;
using UnityEngine;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Host side: spawns Cheryll's networked copy where the Staff's own Cheryll stands (the Staff
    /// Room) and despawns it. Answer warps the Staff's Cheryll to the Staff Door, hang-up back to
    /// the room spot; the copy follows her.
    /// The prefab must be registered on both ends (StaffLinkBehaviour does it).
    /// </summary>
    internal sealed class NetcodeCheryllSpawner : ICheryllSpawner
    {
        private readonly GameObject _prefab;
        private readonly Transform _doorPoint;
        private readonly Transform _roomPoint;
        private readonly Transform _source;
        private NetworkObject _spawned;

        public NetcodeCheryllSpawner(
            GameObject prefab, Transform doorPoint, Transform roomPoint, Transform source)
        {
            _prefab = prefab;
            _doorPoint = doorPoint;
            _roomPoint = roomPoint;
            _source = source;
        }

        public void Spawn()
        {
            if (_spawned != null) return;
            var pose = _source != null ? _source : _roomPoint;
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

        public void WarpToDoor() => Warp(_source, _doorPoint);

        public void WarpToRoom() => Warp(_source, _roomPoint);

        // A CharacterController overrides direct teleports, so switch it off for the jump.
        private static void Warp(Transform who, Transform to)
        {
            if (who == null || to == null) return;
            var controller = who.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            who.SetPositionAndRotation(to.position, to.rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
