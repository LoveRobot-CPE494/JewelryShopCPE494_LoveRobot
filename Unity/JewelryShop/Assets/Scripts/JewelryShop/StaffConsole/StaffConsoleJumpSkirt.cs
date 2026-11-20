using System.Linq;
using UniGLTF.SpringBoneJobs;
using UniGLTF.SpringBoneJobs.Blittables;
using UnityEngine;
using UniVRM10;

namespace JewelryShop.StaffConsole
{
    /// <summary>Keeps Cheryll's skirt draped while her thighs bend during the jump.</summary>
    [DefaultExecutionOrder(10990)] // Apply the pose before Vrm10Instance solves the springs.
    public sealed class StaffConsoleJumpSkirt : MonoBehaviour
    {
        private const float JumpSwingDegrees = 6f;

        [SerializeField] private Vrm10Instance _avatar;
        [SerializeField] private StaffConsoleMovement _movement;

        private Transform _hips;
        private (Transform Bone, Pose ParentPose, Pose HipsPose)[] _anchors;
        private VRM10SpringBoneJoint[] _joints;
        private IVrm10SpringBoneRuntime _springs;
        private bool _isLimited;

        private void Awake()
        {
            _hips = _avatar != null ? _avatar.Humanoid.Hips : null;
            if (_hips == null || _movement == null)
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
            if (!_movement.IsJumping)
            {
                RestoreSkirt();
                return;
            }

            if (!_isLimited)
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

            // The skirt roots sit under the thighs. Follow the hips while airborne.
            foreach (var (bone, _, pose) in _anchors)
            {
                bone.SetPositionAndRotation(
                    _hips.TransformPoint(pose.position), _hips.rotation * pose.rotation);
            }
        }

        private void OnDisable()
        {
            RestoreSkirt();
        }

        private void RestoreSkirt()
        {
            if (!_isLimited)
                return;

            _isLimited = false;
            foreach (var (bone, pose, _) in _anchors)
                bone.SetLocalPositionAndRotation(pose.position, pose.rotation);

            foreach (var joint in _joints)
                _springs.SetJointLevel(joint.transform, joint.Blittable);
        }
    }
}
