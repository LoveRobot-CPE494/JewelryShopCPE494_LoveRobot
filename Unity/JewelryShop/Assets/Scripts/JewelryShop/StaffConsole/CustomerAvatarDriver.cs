using System;
using JewelryShop.Core;
using JewelryShop.Networking;
using UnityEngine;

namespace JewelryShop.StaffConsole
{
    /// <summary>
    /// The Customer Avatar (the Alice prefab) on the Staff Console. While the Customer is connected
    /// it stands where the Customer stands, faces where they face (snapping on a teleport), reaches
    /// the right controller with Humanoid IK and shows the held Gem Model. The left arm just plays
    /// the idle clip. Needs a Humanoid Animator on this object whose controller has IK Pass ticked
    /// on the layer.
    /// Staff Console scene ONLY: never put this in the Quest scene or a prefab the Quest loads
    /// (nothing enforces it; the Cheryll prefab is shared with the Quest, so an asmdef platform
    /// exclusion is not safe).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CustomerAvatarDriver : MonoBehaviour
    {
        private const float IkBlendSeconds = 0.25f;

        [Serializable]
        private struct HeldGem
        {
            [Tooltip("Cut of the Gem this prefab shows.")]
            public GemCut CutKind;
            [Tooltip("Color of the Gem this prefab shows.")]
            public GemColor ColorKind;
            [Tooltip("A visual-only copy of the Gem Model (no Rigidbody or grab component).")]
            public GameObject Prefab;
        }

        [Tooltip("The scene's StaffLinkBehaviour, which owns the pose mailbox.")]
        [SerializeField] private StaffLinkBehaviour _link;
        [Tooltip("World Y of the floor in meters; the feet go here.")]
        [SerializeField] private float _floorYMeters;
        [Tooltip("One entry per Gem the Customer can hold.")]
        [SerializeField] private HeldGem[] _heldGems = Array.Empty<HeldGem>();

        private readonly CustomerPoseSmoother _smoother = new CustomerPoseSmoother();
        private Animator _animator;
        private Renderer[] _renderers;
        private Transform _hand;
        private GameObject _heldInstance;
        private byte _heldCode = GemKeyCode.None;
        private Vector3 _handPosition;
        private Quaternion _handRotation = Quaternion.identity;
        private float _ikWeight;
        private bool _isVisible = true;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _hand = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (_hand == null)
                Debug.LogWarning("CustomerAvatarDriver: no Humanoid right hand; the arm will not reach.", this);
            if (_link == null)
                Debug.LogWarning("CustomerAvatarDriver: link not wired; the Customer Avatar stays hidden.", this);
            SetVisible(false);
        }

        private void Update()
        {
            var mailbox = _link != null ? _link.CustomerPose : null;
            var isAvailable = mailbox != null && mailbox.IsAvailable;
            SetVisible(isAvailable);
            var targetWeight = isAvailable && _hand != null ? 1f : 0f;
            _ikWeight = Mathf.MoveTowards(_ikWeight, targetWeight, Time.deltaTime / IkBlendSeconds);
            if (!isAvailable)
            {
                _smoother.Reset();
                return;
            }

            var pose = mailbox.Latest;
            var body = _smoother.Step(pose.HeadX, pose.HeadY, pose.HeadZ, pose.HeadYawDegrees, Time.deltaTime);
            transform.SetPositionAndRotation(
                new Vector3(body.X, _floorYMeters, body.Z), Quaternion.Euler(0f, body.YawDegrees, 0f));
            _handPosition = new Vector3(pose.HandX, pose.HandY, pose.HandZ);
            _handRotation = new Quaternion(pose.HandRotX, pose.HandRotY, pose.HandRotZ, pose.HandRotW);
            ShowHeldGem(pose.GemCode);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _ikWeight);
            if (_ikWeight <= 0f) return;
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _handPosition);
            _animator.SetIKRotation(AvatarIKGoal.RightHand, _handRotation);
        }

        private void SetVisible(bool isVisible)
        {
            if (isVisible == _isVisible) return;
            _isVisible = isVisible;
            foreach (var renderer in _renderers)
                if (renderer != null) renderer.enabled = isVisible;
            if (!isVisible) ShowHeldGem(GemKeyCode.None);
        }

        private void ShowHeldGem(byte code)
        {
            if (code == _heldCode) return;
            _heldCode = code;
            if (_heldInstance != null) Destroy(_heldInstance);
            _heldInstance = null;
            if (_hand == null || !GemKeyCode.TryDecode(code, out var key)) return;
            foreach (var gem in _heldGems)
            {
                if (gem.CutKind != key.Cut || gem.ColorKind != key.Color || gem.Prefab == null) continue;
                _heldInstance = Instantiate(gem.Prefab, _hand);
                _heldInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }
            Debug.LogWarning($"CustomerAvatarDriver: no Gem prefab wired for {key}.", this);
        }
    }
}
