using Unity.Netcode;
using UnityEngine;
using UniVRM10;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Relays Cheryll's movement, animation and face from Staff Console to Storefront.
    /// </summary>
    [DefaultExecutionOrder(10980)] // After Staff Look-At and before the replica's VRM update.
    public sealed class CheryllNetworkAvatar : NetworkBehaviour
    {
        private const float StateBlendSeconds = 0.1f;

        internal struct FacePose : INetworkSerializeByMemcpy
        {
            public Quaternion HeadRotation;
            public float EyeYaw;
            public float EyePitch;
            public float Happy;
            public float Relaxed;
            public float Angry;
            public float Sad;
            public float Blink;
            public bool HasPose;
        }

        private readonly NetworkVariable<FacePose> _facePose = new();
        private int[] _lastStates;
        private Transform _source;
        private Animator _sourceAnimator;
        private Animator _animator;
        private Vrm10Instance _sourceAvatar;
        private Vrm10Instance _avatar;
        private Transform _sourceHead;
        private Transform _head;

        /// <summary>Host only: the Staff's own Cheryll that this copy follows.</summary>
        public void Follow(Transform source)
        {
            _source = source;
            _sourceAnimator = source != null ? source.GetComponent<Animator>() : null;
            _sourceAvatar = source != null ? source.GetComponent<Vrm10Instance>() : null;
            _sourceHead = _sourceAnimator != null && _sourceAnimator.isHuman
                ? _sourceAnimator.GetBoneTransform(HumanBodyBones.Head)
                : null;
        }

        public override void OnNetworkSpawn()
        {
            _animator = GetComponent<Animator>();
            if (!IsServer)
            {
                _avatar = GetComponent<Vrm10Instance>();
                _head = _animator != null && _animator.isHuman
                    ? _animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;
                if (_avatar != null)
                    _avatar.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.YawPitchValue;
                return;
            }

            // Keep animating while hidden, or the Quest would get a frozen pose.
            if (_animator != null) _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            CaptureFace();
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!IsServer)
            {
                ApplyFace(_facePose.Value);
                return;
            }

            if (_source == null) return;
            transform.SetPositionAndRotation(_source.position, _source.rotation);
            CaptureFace();
            if (_sourceAnimator == null || _animator == null) return;
            CopyParameters(_sourceAnimator, _animator);
            CopyStates(_sourceAnimator, _animator);
        }

        private void CaptureFace()
        {
            if (_sourceAvatar == null || _sourceHead == null) return;

            var runtime = _sourceAvatar.Runtime;
            var expression = runtime.Expression;
            var eyes = runtime.LookAt.LookAtInput.YawPitch ?? runtime.LookAt.EyeDirection;
            _facePose.Value = new FacePose
            {
                HasPose = true,
                HeadRotation = _sourceHead.localRotation,
                EyeYaw = eyes.Yaw,
                EyePitch = eyes.Pitch,
                Happy = expression.GetWeight(ExpressionKey.Happy),
                Relaxed = expression.GetWeight(ExpressionKey.Relaxed),
                Angry = expression.GetWeight(ExpressionKey.Angry),
                Sad = expression.GetWeight(ExpressionKey.Sad),
                Blink = expression.GetWeight(ExpressionKey.Blink),
            };
        }

        private void ApplyFace(FacePose pose)
        {
            if (_avatar == null || _head == null || !pose.HasPose) return;

            _head.localRotation = pose.HeadRotation;
            var runtime = _avatar.Runtime;
            runtime.LookAt.SetYawPitchManually(pose.EyeYaw, pose.EyePitch);
            var expression = runtime.Expression;
            expression.SetWeight(ExpressionKey.Happy, pose.Happy);
            expression.SetWeight(ExpressionKey.Relaxed, pose.Relaxed);
            expression.SetWeight(ExpressionKey.Angry, pose.Angry);
            expression.SetWeight(ExpressionKey.Sad, pose.Sad);
            expression.SetWeight(ExpressionKey.Blink, pose.Blink);
        }

        // Gestures are played with CrossFade, not with parameters, so follow the state itself.
        // A state that is blending in counts as the current one.
        private void CopyStates(Animator from, Animator to)
        {
            if (_lastStates == null || _lastStates.Length != from.layerCount)
            {
                _lastStates = new int[from.layerCount];
                for (var i = 0; i < _lastStates.Length; i++) _lastStates[i] = int.MinValue;
            }

            for (var layer = 0; layer < from.layerCount; layer++)
            {
                var info = from.IsInTransition(layer)
                    ? from.GetNextAnimatorStateInfo(layer)
                    : from.GetCurrentAnimatorStateInfo(layer);
                if (info.fullPathHash == _lastStates[layer]) continue;
                _lastStates[layer] = info.fullPathHash;
                to.CrossFadeInFixedTime(info.fullPathHash, StateBlendSeconds, layer,
                    info.normalizedTime * info.length);
            }
        }

        private static void CopyParameters(Animator from, Animator to)
        {
            foreach (var parameter in from.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        to.SetFloat(parameter.nameHash, from.GetFloat(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        to.SetInteger(parameter.nameHash, from.GetInteger(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        to.SetBool(parameter.nameHash, from.GetBool(parameter.nameHash));
                        break;
                }
            }
        }
    }
}
