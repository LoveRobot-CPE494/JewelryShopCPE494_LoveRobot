using System.Linq;
using JewelryShop.Core;
using UniGLTF.SpringBoneJobs;
using UniGLTF.SpringBoneJobs.Blittables;
using UnityEngine;
using UniVRM10;

namespace JewelryShop.StaffConsole
{
    /// <summary>Keeps Cheryll's skirt draped through strides, jumps and bows.</summary>
    [DefaultExecutionOrder(10990)] // Apply the pose before Vrm10Instance solves the springs.
    public sealed class StaffConsoleJumpSkirt : MonoBehaviour
    {
        private const float BustJumpSwingDegrees = 2f;
        private const float BustJumpMinimumDrag = 0.8f;

        [SerializeField] private Vrm10Instance _avatar;
        [SerializeField] private StaffConsoleMovement _movement;
        [SerializeField] private StaffSkirtConfig _skirtConfig;

        private Transform _hips;
        private Transform _leftKnee;
        private Transform _rightKnee;
        private Animator _animator;
        private (Transform Bone, Pose ParentPose, Pose HipsPose)[] _anchors;
        private VRM10SpringBoneJoint[] _skirtJoints;
        private VRM10SpringBoneJoint[] _bustJoints;
        private IVrm10SpringBoneRuntime _springs;
        private SkirtSettings _skirt;
        private bool _isLimited;
        private float _jumpFlareDegrees;
        private float _runClearanceMeters;

        private void Awake()
        {
            if (_skirtConfig == null)
                Debug.LogWarning("No Skirt Config assigned; using defaults.", this);
            _skirt = _skirtConfig != null ? _skirtConfig.Settings : SkirtSettings.Defaults;
            _hips = _avatar != null ? _avatar.Humanoid.Hips : null;
            _animator = GetComponent<Animator>();
            if (_hips == null || _movement == null || _animator == null)
            {
                Debug.LogWarning("StaffConsoleJumpSkirt needs a VRM avatar and movement.", this);
                enabled = false;
                return;
            }

            _leftKnee = _avatar.Humanoid.LeftLowerLeg;
            _rightKnee = _avatar.Humanoid.RightLowerLeg;
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
            _skirtJoints = chains.SelectMany(chain => chain.Joints)
                .Where(joint => joint != null)
                .Distinct()
                .ToArray();
            _bustJoints = _avatar.SpringBone.Springs
                .Where(chain => chain.Name == "Bust")
                .SelectMany(chain => chain.Joints)
                .Where(joint => joint != null)
                .Distinct()
                .ToArray();

            if (_anchors.Length == 0)
            {
                Debug.LogWarning("StaffConsoleJumpSkirt could not find skirt springs.", this);
                enabled = _bustJoints.Length > 0;
            }
        }

        private void LateUpdate()
        {
            var isJumping = _movement.IsJumping;
            var flareStepDegrees = _skirt.FlareSpeedDegreesPerSecond * Time.deltaTime;
            _jumpFlareDegrees = Mathf.MoveTowards(_jumpFlareDegrees,
                isJumping ? _skirt.FlareDegrees : 0f, flareStepDegrees);
            var runWeight = _movement.RunBlend;
            var kneeLeadMeters = _leftKnee != null && _rightKnee != null ? Mathf.Max(
                Vector3.Dot(_leftKnee.position - _hips.position, transform.forward),
                Vector3.Dot(_rightKnee.position - _hips.position, transform.forward)) : 0f;
            _runClearanceMeters = SkirtSettings.ReleaseRunClearance(_runClearanceMeters,
                _skirt.RunKneeClearance(kneeLeadMeters) * runWeight, runWeight, Time.deltaTime);
            if (!isJumping)
            {
                RestoreJumpLimits();
            }
            else
            {
                if (!_isLimited)
                {
                    _springs = _avatar.Runtime.SpringBone;
                    foreach (var joint in _skirtJoints)
                        LimitJumpSwing(joint, _skirt.SwingDegrees, minimumDrag: 0f);
                    _isLimited = true;
                }

                // The spring buffer may be initialized after the first jump frame.
                foreach (var joint in _bustJoints)
                    LimitJumpSwing(joint, BustJumpSwingDegrees, BustJumpMinimumDrag);
            }

            // The skirt roots sit under the thighs; keep leg motion from folding the fabric.
            var hipsRotation = _hips.rotation;
            var bowing = _animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Bow") ||
                _animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Bow");
            if (bowing)
            {
                var upright = Quaternion.Euler(0f, _hips.eulerAngles.y, 0f);
                hipsRotation = Quaternion.Slerp(hipsRotation, upright, _skirt.BowUprightWeight);
            }

            var correction = hipsRotation * Quaternion.Inverse(_hips.rotation);
            foreach (var (bone, _, pose) in _anchors)
            {
                var position = _hips.TransformPoint(pose.position);
                var rotation = _hips.rotation * pose.rotation;
                position = _hips.position + correction * (position - _hips.position);
                rotation = correction * rotation;

                if (_jumpFlareDegrees > 0f || runWeight > 0f || _runClearanceMeters > 0f)
                {
                    var radial = new Vector3(pose.position.x, 0f, pose.position.z);
                    if (radial.sqrMagnitude > 0.0001f)
                    {
                        var outward = hipsRotation * radial.normalized;
                        // The knee needs clearance mainly in front of the running avatar.
                        var frontWeight = Mathf.Clamp01(Vector3.Dot(transform.forward, outward));
                        var clearanceWeight = Mathf.Clamp01(frontWeight * 1.25f);
                        position += transform.forward * (_runClearanceMeters * clearanceWeight);
                        var flareDegrees = Mathf.Max(_jumpFlareDegrees,
                            _skirt.RunAnchorFlare(frontWeight) * runWeight);
                        var direction = Vector3.down + outward *
                            Mathf.Tan(flareDegrees * Mathf.Deg2Rad);
                        rotation = Quaternion.FromToRotation(Vector3.down, direction) * rotation;
                    }
                }

                bone.SetPositionAndRotation(position, rotation);
            }
        }

        private void LimitJumpSwing(VRM10SpringBoneJoint joint, float swingDegrees, float minimumDrag)
        {
            var normal = joint.Blittable;
            var limited = new BlittableJointMutable(
                normal.stiffnessForce, normal.gravityPower, normal.gravityDir,
                Mathf.Max(normal.dragForce, minimumDrag), normal.radius,
                (float)AnglelimitTypes.Cone, swingDegrees * Mathf.Deg2Rad, 0f,
                normal.anglelimitOffset);
            _springs.SetJointLevel(joint.transform, limited);
        }

        private void OnDisable()
        {
            _runClearanceMeters = 0f;
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
            foreach (var joint in _skirtJoints)
                _springs.SetJointLevel(joint.transform, joint.Blittable);
            foreach (var joint in _bustJoints)
                _springs.SetJointLevel(joint.transform, joint.Blittable);
        }
    }
}
