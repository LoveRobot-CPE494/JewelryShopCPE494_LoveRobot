using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace JewelryShop.Storefront
{
    /// <summary>Lifts the camera to the XR Origin's Camera Y Offset when the XR Interaction Simulator is on.
    /// With no headset the Origin's tracking mode stays Unknown, so XR Origin never applies that offset and the
    /// camera sits at the floor. With a real headset the Simulator is off and the mode is known, so this does
    /// nothing and the headset's own height is used.</summary>
    public class SimulatorEyeHeight : MonoBehaviour
    {
        [SerializeField] private XROrigin _origin;
        [SerializeField] private XRInteractionSimulator _simulator;

        /// <summary>Only the Simulator without a known tracking mode needs the lift.</summary>
        public static bool NeedsLift(bool simulatorActive, TrackingOriginModeFlags mode) =>
            simulatorActive && mode == TrackingOriginModeFlags.Unknown;

        private void Start()
        {
            if (_origin == null || _simulator == null || _origin.CameraFloorOffsetObject == null)
            {
                Debug.LogWarning("SimulatorEyeHeight: Origin or Simulator not wired; nothing to lift.", this);
                return;
            }

            if (!NeedsLift(_simulator.gameObject.activeInHierarchy, _origin.CurrentTrackingOriginMode)) return;

            var offset = _origin.CameraFloorOffsetObject.transform;
            offset.localPosition = new Vector3(offset.localPosition.x, _origin.CameraYOffset, offset.localPosition.z);
        }
    }
}
