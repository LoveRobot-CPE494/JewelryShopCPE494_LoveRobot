using JewelryShop.Core;
using JewelryShop.Networking;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>
    /// Quest scene: publishes the Customer's head, right controller and held Gem Model to the
    /// link's pose mailbox, so the Staff Console can show the Customer Avatar. Warns when unwired.
    /// </summary>
    public sealed class CustomerPoseSource : MonoBehaviour
    {
        [Tooltip("The scene's StaffLinkBehaviour, which owns the pose mailbox.")]
        [SerializeField] private StaffLinkBehaviour _link;
        [Tooltip("The headset camera. Falls back to Camera.main.")]
        [SerializeField] private Transform _head;
        [Tooltip("The right controller's transform (the hand the Customer Avatar reaches with).")]
        [SerializeField] private Transform _rightController;
        [Tooltip("The right hand's interactor; its selected Gem Model is shown in the Customer Avatar's hand.")]
        [SerializeField] private XRBaseInteractor _rightInteractor;

        private void Start()
        {
            if (_link == null)
                Debug.LogWarning("CustomerPoseSource: link not wired; the Staff Console sees no Customer Avatar.", this);
            if (_rightController == null)
                Debug.LogWarning("CustomerPoseSource: right controller not wired; her hand will not follow.", this);
        }

        private void Update()
        {
            if (_link == null) return;
            if (TryGetPose(out var pose)) _link.CustomerPose.Publish(pose);
            else _link.CustomerPose.Clear();
        }

        private void OnDisable()
        {
            if (_link != null) _link.CustomerPose.Clear();
        }

        private bool TryGetPose(out CustomerPose pose)
        {
            pose = default;
            var head = _head != null ? _head : (Camera.main != null ? Camera.main.transform : null);
            if (head == null || _rightController == null) return false;

            pose.HeadX = head.position.x;
            pose.HeadY = head.position.y;
            pose.HeadZ = head.position.z;
            pose.HeadYawDegrees = head.eulerAngles.y;
            var hand = _rightController;
            pose.HandX = hand.position.x;
            pose.HandY = hand.position.y;
            pose.HandZ = hand.position.z;
            pose.HandRotX = hand.rotation.x;
            pose.HandRotY = hand.rotation.y;
            pose.HandRotZ = hand.rotation.z;
            pose.HandRotW = hand.rotation.w;
            pose.GemCode = GemKeyCode.None;
            if (_rightInteractor != null && _rightInteractor.hasSelection
                && _rightInteractor.firstInteractableSelected is Component held
                && held.TryGetComponent(out GemModel gem) && gem.TryGetKey(out var key))
                pose.GemCode = GemKeyCode.Encode(key);
            return true;
        }
    }
}
