using System.Linq;
using UniGLTF.SpringBoneJobs;
using UniGLTF.SpringBoneJobs.Blittables;
using UnityEngine;
using UniVRM10;

namespace JewelryShop.StaffConsole
{
    /// <summary>Keeps Cheryll's skirt draped while her thighs move.</summary>
    [DefaultExecutionOrder(10990)] // Apply the pose before Vrm10Instance solves the springs.
    public sealed class StaffConsoleJumpSkirt : MonoBehaviour
    {
        private const float JumpSwingDegrees = 6f;
        private const float JumpFlareDegrees = 8f;
        private const float JumpFlareSpeedDegreesPerSecond = 80f;
        private const float BowUprightWeight = 0.8f;

        [SerializeField] private Vrm10Instance _avatar;
        [SerializeField] private StaffConsoleMovement _movement;

        private Transform _hips;
        private Animator _animator;
        private (Transform Bone, Pose ParentPose, Pose HipsPose)[] _anchors;
        private VRM10SpringBoneJoint[] _joints;
        private IVrm10SpringBoneRuntime _springs;
        private bool _isLimited;
        private float _jumpFlareDegrees;

        private void Awake()
        {
            _hips = _avatar != null ? _avatar.Humanoid.Hips : null;
            _animator = GetComponent<Animator>();
            if (_hips == null || _movement == null || _animator == null)
            {
                Debug.LogWarning("StaffConsoleJumpSkirt needs a VRM avatar and movement.", this);
                enabled = false;
                return;
            }

            var chains = _avatar.SpringBone.Springs
                .Where(chain => chain.Name == "Skirt" && chain.Joints.Count > 0 &&
                    chain.Joints[0] != null && chain.Joints[0].transform.parent != null)
                .ToArray();
            _anchors = chains.Select(chain => chain.Joints[0].transform.parent)
                .Distinct()
                .Select(bone => (bone, new Pose(bone.localPosition, bone.localRotation),
                    new Pose(_hips.InverseTransformPoint(bone.position),
                        Quaternion.Inverse(_hips.rotation) * bone.rotation)))
                .ToArray();
            _joints = chains.SelectMany(chain => chain.Joints)
                .Where(joint => joint != null)
                .Distinct()
                .ToArray();

            if (_anchors.Length == 0)
            {
                Debug.LogWarning("StaffConsoleJumpSkirt could not find skirt springs.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            var isJumping = _movement.IsJumping;
            _jumpFlareDegrees = Mathf.MoveTowards(_jumpFlareDegrees,
                isJumping ? JumpFlareDegrees : 0f,
                JumpFlareSpeedDegreesPerSecond * Time.deltaTime);
            if (!isJumping)
            {
                RestoreJumpLimits();
            }
            else if (!_isLimited)
            {
                _springs = _avatar.Runtime.SpringBone;
                foreach (var joint in _joints)
                {
                    var normal = joint.Blittable;
                    var limited = new BlittableJointMutable(
                        normal.stiffnessForce, normal.gravityPower, normal.gravityDir,
                        normal.dragForce, normal.radius, (float)AnglelimitTypes.Cone,
                        JumpSwingDegrees * Mathf.Deg2Rad, 0f, normal.anglelimitOffset);
                    _springs.SetJointLevel(joint.transform, limited);
                }

                _isLimited = true;
            }

            // The skirt roots sit under the thighs; keep leg motion from folding the fabric.
            var hipsRotation = _hips.rotation;
            var bowing = _animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Bow") ||
                _animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Bow");
            if (bowing)
            {
                var upright = Quaternion.Euler(0f, _hips.eulerAngles.y, 0f);
                hipsRotation = Quaternion.Slerp(hipsRotation, upright, BowUprightWeight);
            }

            var correction = hipsRotation * Quaternion.Inverse(_hips.rotation);
            foreach (var (bone, _, pose) in _anchors)
            {
                var position = _hips.TransformPoint(pose.position);
                var rotation = _hips.rotation * pose.rotation;
                position = _hips.position + correction * (position - _hips.position);
                rotation = correction * rotation;

                if (_jumpFlareDegrees > 0f)
                {
                    var radial = new Vector3(pose.position.x, 0f, pose.position.z);
                    if (radial.sqrMagnitude > 0.0001f)
                    {
                        var outward = hipsRotation * radial.normalized;
                        var direction = Vector3.down + outward *
                            Mathf.Tan(_jumpFlareDegrees * Mathf.Deg2Rad);
                        rotation = Quaternion.FromToRotation(Vector3.down, direction) * rotation;
                    }
                }

                bone.SetPositionAndRotation(position, rotation);
            }
        }

        private void OnDisable()
        {
            RestoreJumpLimits();
            if (_anchors == null)
                return;

            foreach (var (bone, pose, _) in _anchors)
                bone.SetLocalPositionAndRotation(pose.position, pose.rotation);
        }

        private void RestoreJumpLimits()
        {
            if (!_isLimited)
                return;

            _isLimited = false;
            foreach (var joint in _joints)
                _springs.SetJointLevel(joint.transform, joint.Blittable);
        }
    }
}
