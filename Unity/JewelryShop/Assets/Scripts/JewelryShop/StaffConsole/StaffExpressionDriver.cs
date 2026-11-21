using JewelryShop.Core;
using UnityEngine;
using UniVRM10;

namespace JewelryShop.StaffConsole
{
    /// <summary>
    /// Writes the Expression Blender's weights to Cheryll's VRM face (ADR-0019). Drives only
    /// happy, relaxed, angry, sad and blink; aa, ee and surprised belong to other systems.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class StaffExpressionDriver : MonoBehaviour
    {
        private const string GestureTag = "Gesture";

        [SerializeField] private Vrm10Instance _avatar;
        [SerializeField] private StaffExpressionConfig _config;

        private Animator _animator;
        private StaffConsoleMovement _movement;
        private ExpressionBlender _blender;

        private void Reset()
        {
            _avatar = GetComponent<Vrm10Instance>();
        }

        private void Awake()
        {
            if (_avatar == null)
            {
                Debug.LogWarning("StaffExpressionDriver has no Vrm10Instance; doing nothing.",
                    this);
                enabled = false;
                return;
            }

            if (_config == null)
                Debug.LogWarning("No Expression Config assigned; using defaults.", this);
            _blender = new ExpressionBlender(_config != null ? _config.Settings :
                ExpressionSettings.Defaults);
            _animator = GetComponent<Animator>();
            _movement = GetComponent<StaffConsoleMovement>();
        }

        private void OnEnable()
        {
            if (_movement != null)
                _movement.GestureStarted += OnGestureStarted;
        }

        private void OnDisable()
        {
            if (_movement != null)
                _movement.GestureStarted -= OnGestureStarted;
        }

        private void OnGestureStarted(string stateName) => _blender.StartGesture(stateName);

        private void Update()
        {
            var isGesture = _animator.GetCurrentAnimatorStateInfo(0).IsTag(GestureTag) ||
                _animator.GetNextAnimatorStateInfo(0).IsTag(GestureTag);
            var w = _blender.Tick(Time.deltaTime, isGesture);
            var expression = _avatar.Runtime.Expression;
            expression.SetWeight(ExpressionKey.Happy, w.Happy);
            expression.SetWeight(ExpressionKey.Relaxed, w.Relaxed);
            expression.SetWeight(ExpressionKey.Angry, w.Angry);
            expression.SetWeight(ExpressionKey.Sad, w.Sad);
            expression.SetWeight(ExpressionKey.Blink, w.Blink);
        }
    }
}
