using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace JewelryShop.Storefront
{
    /// <summary>Turns the XR Interaction Simulator off at startup when a real headset is connected (#120,
    /// ADR-0015). The Simulator's device manager removes real HMDs in OnEnable, so this runs first (Awake)
    /// and deactivates the Simulator GameObject before any OnEnable. Checked once; a headset plugged in
    /// after Play is out of scope.</summary>
    [DefaultExecutionOrder(-30000)] // before SimulatedDeviceLifecycleManager (-29995) and XRInteractionSimulator (-29991)
    public class SimulatorAutoOff : MonoBehaviour
    {
        [SerializeField] private XRInteractionSimulator _simulator;

        /// <summary>A real headset is a non-simulated HMD device, or a running XR display (covers a Link HMD
        /// the Input System has not registered yet on the first frame).</summary>
        public static bool HasRealHeadset(IEnumerable<(bool IsHmd, bool IsSimulated)> devices, bool xrDisplayRunning) =>
            xrDisplayRunning || devices.Any(d => d.IsHmd && !d.IsSimulated);

        private void Awake()
        {
            if (_simulator == null)
            {
                Debug.LogWarning("SimulatorAutoOff: no Simulator wired; nothing to turn off.", this);
                return;
            }

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            var devices = InputSystem.devices.Select(d => (d is XRHMD, d is XRSimulatedHMD));
            if (!HasRealHeadset(devices, displays.Any(d => d.running))) return;

            Debug.Log("SimulatorAutoOff: real headset found, XR Interaction Simulator off.", this);
            _simulator.gameObject.SetActive(false);
        }
    }
}
