// MG400Teleop.cs  —  Remote Maintenance (Teleop): Controller ขวาของ Quest 3 -> mg400_bridge.py (UDP)
// ใช้กับโปรเจกต์ Unity 6 + XR Interaction Toolkit 3 + OpenXR (ตาม ADR-0003)
//
// ปุ่ม (ตามตาราง 4.3 ในรายงาน):
//   Grip ค้าง    = แขนตาม Controller (Deadman) — ปล่อยแล้วแขนหยุดทันที
//                  ทุกครั้งที่กด Grip ใหม่ ตำแหน่ง Controller ตอนนั้น = ตำแหน่งแขนตอนนั้น (แขนไม่กระโดด)
//   Trigger ค้าง = หัวดูดทำงาน, ปล่อย = ปล่อยของ
//   A            = กลับ Home
//   B            = หยุดฉุกเฉิน (ต้องปล่อยแล้วกด Grip ใหม่ถึงบังคับต่อได้)
//   Thumbstick   = ขยับขึ้น-ลงทีละน้อย (ขณะกด Grip)
//
// ทดสอบใน Editor ด้วย XR Interaction Simulator (ไม่ใส่แว่น) ใช้คีย์บอร์ด:
//   G ค้าง = Grip, T ค้าง = Trigger, H = Home, Space = Stop, R / F = ขึ้น / ลง
//   (ตำแหน่งยังมาจาก Right Controller ที่ Simulator ขยับให้)

// shortcut: teammate's Teleop, ported with its controls unchanged (#210); the message text
// lives in Core/TeleopMessages (pinned by tests). Naming and typed messages (ADR-0008) are
// owed when it is brought up to standard (ADR-0017); the controls change in #237.

using System;
using System.Net.Sockets;
using System.Text;
using JewelryShop.Core;
using JewelryShop.Networking;
using UnityEngine;
using UnityEngine.XR;
// หมายเหตุ: ไม่ using UnityEngine.InputSystem เพราะมีชื่อ InputDevice / CommonUsages ซ้ำกับ UnityEngine.XR

namespace JewelryShop.Teleop
{
public class MG400Teleop : MonoBehaviour
{
    [Header("ปลายทาง: Robot PC ที่รัน mg400_bridge.py")]
    [Tooltip("Robot Address and Teleop port come from here (ADR-0023/0024); the address is the "
        + "discovered or typed Robot Address, never a field on this script.")]
    [SerializeField] HostAddressConfig hostAddressConfig;
    [Range(5, 60)] public float sendHz = 20f;

    [Header("Controller ขวา")]
    [Tooltip("ลาก XR Origin > Camera Offset > Right Controller มาใส่ (เว้นว่างได้ จะอ่านจากอุปกรณ์ตรง ๆ)")]
    public Transform rightController;

    [Header("Thumbstick ขึ้น-ลง")]
    [Tooltip("ความเร็วขึ้น-ลงเมื่อดัน Thumbstick สุด (เมตร/วินาที ของ Controller)")]
    public float thumbstickSpeed = 0.05f;

    [Header("แสดงสถานะ (ไม่ใส่ก็ได้)")]
    public TMPro.TMP_Text statusText;

    const int FallbackTeleopPort = 9000;

    UdpClient udp;
    HostDiscovery discovery;   // own one: the Teleop scene has no Staff Link
    string target;           // address udp is connected to, null when none
    bool targetChosen;
    readonly TeleopSendTracker sendTracker = new TeleopSendTracker();

    /// <summary>Clock for the "has sent" window (tests may swap it).</summary>
    public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

    /// <summary>A packet left in the last second. "Sent" only: UDP has no acknowledgement.
    /// </summary>
    public bool HasSentRecently => sendTracker.HasSentRecently(Clock());

    /// <summary>When the last packet left (Clock time); meaningless before the first send.
    /// </summary>
    public double LastSentTime => sendTracker.LastSentTime;

    InputDevice right;
    float nextSend;
    bool engaged, lastGrip, lastTrigger, lastA, lastB;
    float zOffset;
    string status = "พร้อม — กด Grip ค้างเพื่อบังคับแขน";

    void Start()
    {
        if (hostAddressConfig == null)
            Debug.LogWarning("[MG400] Host Address Config not wired; using Teleop port "
                + FallbackTeleopPort + " and the saved Robot Address only.", this);
        discovery = new HostDiscovery(hostAddressConfig);
        discovery.Refresh(Time.realtimeSinceStartupAsDouble);
        Retarget();
    }

    /// <summary>The one place Teleop learns where to send: the resolved Robot Address.</summary>
    // Discovered (Automatic on, fresh robot beacon) or typed; null when neither is valid.
    string ResolveRobotAddress() => discovery.RobotAddress;

    int TeleopPort => hostAddressConfig != null && hostAddressConfig.TeleopPort != 0
        ? hostAddressConfig.TeleopPort : FallbackTeleopPort;

    // Picks up a changed Robot Address (typed or discovered) without restarting the app.
    void Retarget()
    {
        var address = ResolveRobotAddress();
        if (targetChosen && address == target) return;
        targetChosen = true;

        if (udp != null)
        {
            if (engaged) Send(TeleopMessages.Release);   // ไม่ทิ้งแขนค้างไว้ที่ปลายทางเก่า
            udp.Close();
            udp = null;
        }
        engaged = false;
        target = address;

        if (address == null)
        {
            // Logged once per change to "no address", not every frame.
            Debug.LogWarning("[MG400] no valid Robot Address; Teleop is not sending.", this);
            return;
        }
        udp = new UdpClient();
        udp.Connect(address, TeleopPort);
        Debug.Log($"[MG400] ส่งไปที่ {address}:{TeleopPort}");
        Send(TeleopMessages.Release);   // บอก bridge ว่าเริ่มแบบยังไม่บังคับ
    }

    void Update()
    {
        discovery.Tick(Time.realtimeSinceStartupAsDouble);
        Retarget();
        if (!right.isValid)
            right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool grip = Button(CommonUsages.gripButton, KeyG());
        bool trigger = Button(CommonUsages.triggerButton, KeyT());
        bool a = Button(CommonUsages.primaryButton, KeyH());
        bool b = Button(CommonUsages.secondaryButton, KeySpace());
        float stickY = Stick();

        if (!TryGetPosition(out Vector3 pos))
        {
            SetStatus("ไม่เจอ Controller ขวา");
            return;
        }

        // ----- B: หยุดฉุกเฉิน (ส่ง 3 ครั้งกัน UDP หาย) -----
        if (b && !lastB)
        {
            for (int i = 0; i < 3; i++) Send(TeleopMessages.Stop);
            engaged = false;
            Haptic(1f, 0.3f);
            SetStatus("หยุดฉุกเฉิน — ปล่อย Grip แล้วกดใหม่เพื่อบังคับต่อ");
        }

        // ----- A: กลับ Home -----
        if (a && !lastA)
        {
            Send(TeleopMessages.Home);
            engaged = false;
            SetStatus("กลับ Home — กด Grip ใหม่เพื่อบังคับต่อ");
        }

        // ----- Grip: Deadman -----
        if (grip && !lastGrip)
        {
            zOffset = 0f;
            Send(TeleopMessages.Engage(pos.x, pos.y, pos.z));
            engaged = true;
            Haptic(0.4f, 0.1f);
            SetStatus("กำลังบังคับแขน");
        }
        else if (!grip && lastGrip && engaged)
        {
            Send(TeleopMessages.Release);
            engaged = false;
            SetStatus("ปล่อยแล้ว แขนหยุด");
        }

        // ----- Trigger: หัวดูด -----
        if (trigger != lastTrigger)
            Send(TeleopMessages.Grip(trigger));

        // ----- ส่งตำแหน่งขณะบังคับ -----
        if (engaged)
        {
            zOffset += stickY * thumbstickSpeed * Time.deltaTime;
            if (Time.time >= nextSend)
            {
                nextSend = Time.time + 1f / sendHz;
                Send(TeleopMessages.Pose(pos.x, pos.y + zOffset, pos.z));
            }
        }

        lastGrip = grip; lastTrigger = trigger; lastA = a; lastB = b;
        if (statusText) statusText.text = status;
    }

    // ตำแหน่งใน tracking space (หน่วยเมตร) — ใช้ localPosition เพื่อไม่ขึ้นกับว่า XR Origin วางตรงไหนในฉาก
    bool TryGetPosition(out Vector3 pos)
    {
        if (rightController)
        {
            pos = rightController.localPosition;
            return true;
        }
        pos = default;
        return right.isValid && right.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
    }

    bool Button(InputFeatureUsage<bool> usage, bool keyboard)
    {
        bool v = false;
        if (right.isValid) right.TryGetFeatureValue(usage, out v);
        return v || keyboard;
    }

    float Stick()
    {
        Vector2 v = Vector2.zero;
        if (right.isValid) right.TryGetFeatureValue(CommonUsages.primary2DAxis, out v);
        float y = Mathf.Abs(v.y) > 0.2f ? v.y : 0f;   // deadzone
        if (KeyR()) y += 1f;
        if (KeyF()) y -= 1f;
        return Mathf.Clamp(y, -1f, 1f);
    }

    void Haptic(float amp, float sec)
    {
        if (right.isValid) right.SendHapticImpulse(0, amp, sec);
    }

    void SetStatus(string s)
    {
        if (s != status) Debug.Log("[MG400] " + s);
        status = s;
    }

    void Send(string json)
    {
        if (udp == null) return;   // no valid Robot Address (warned once in Retarget)
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            udp.Send(bytes, bytes.Length);
            sendTracker.MarkSent(Clock());
        }
        catch (Exception e)
        {
            SetStatus("ส่งไม่ได้: " + e.Message);
        }
    }

    void OnDisable()
    {
        if (udp != null && engaged) Send(TeleopMessages.Release);
    }

    void OnDestroy()
    {
        udp?.Close();
        discovery?.Dispose();
    }

    // ----- คีย์บอร์ด (เฉพาะทดสอบใน Editor) -----
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
    static bool K(UnityEngine.InputSystem.Key k) =>
        UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[k].isPressed;
    static bool KeyG() => K(UnityEngine.InputSystem.Key.G);
    static bool KeyT() => K(UnityEngine.InputSystem.Key.T);
    static bool KeyH() => K(UnityEngine.InputSystem.Key.H);
    static bool KeySpace() => K(UnityEngine.InputSystem.Key.Space);
    static bool KeyR() => K(UnityEngine.InputSystem.Key.R);
    static bool KeyF() => K(UnityEngine.InputSystem.Key.F);
#else
    static bool KeyG() => false;
    static bool KeyT() => false;
    static bool KeyH() => false;
    static bool KeySpace() => false;
    static bool KeyR() => false;
    static bool KeyF() => false;
#endif
}
}
