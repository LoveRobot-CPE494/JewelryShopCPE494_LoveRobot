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

// shortcut: teammate's working Teleop, kept as-is; naming, typed messages (ADR-0008) and
// tests are owed when it is brought up to standard (ADR-0017).

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
// หมายเหตุ: ไม่ using UnityEngine.InputSystem เพราะมีชื่อ InputDevice / CommonUsages ซ้ำกับ UnityEngine.XR

namespace JewelryShop.Teleop
{
public class MG400Teleop : MonoBehaviour
{
    [Header("ปลายทาง: คอมที่รัน mg400_bridge.py")]
    [Tooltip("IP ฝั่ง Wi-Fi ของคอม (ถ้ากด Play ผ่าน Quest Link บนคอมเครื่องเดียวกัน ใช้ 127.0.0.1)")]
    public string pcIp = "192.168.0.10";
    public int pcPort = 9000;
    [Range(5, 60)] public float sendHz = 20f;

    [Header("Controller ขวา")]
    [Tooltip("ลาก XR Origin > Camera Offset > Right Controller มาใส่ (เว้นว่างได้ จะอ่านจากอุปกรณ์ตรง ๆ)")]
    public Transform rightController;

    [Header("Thumbstick ขึ้น-ลง")]
    [Tooltip("ความเร็วขึ้น-ลงเมื่อดัน Thumbstick สุด (เมตร/วินาที ของ Controller)")]
    public float thumbstickSpeed = 0.05f;

    [Header("แสดงสถานะ (ไม่ใส่ก็ได้)")]
    public TMPro.TMP_Text statusText;

    UdpClient udp;
    InputDevice right;
    float nextSend;
    bool engaged, lastGrip, lastTrigger, lastA, lastB;
    float zOffset;
    string status = "พร้อม — กด Grip ค้างเพื่อบังคับแขน";

    void Start()
    {
        udp = new UdpClient();
        udp.Connect(pcIp, pcPort);
        Debug.Log($"[MG400] ส่งไปที่ {pcIp}:{pcPort}");
        Send("{\"type\":\"release\"}");   // บอก bridge ว่าเริ่มแบบยังไม่บังคับ
    }

    void Update()
    {
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
            for (int i = 0; i < 3; i++) Send("{\"type\":\"stop\"}");
            engaged = false;
            Haptic(1f, 0.3f);
            SetStatus("หยุดฉุกเฉิน — ปล่อย Grip แล้วกดใหม่เพื่อบังคับต่อ");
        }

        // ----- A: กลับ Home -----
        if (a && !lastA)
        {
            Send("{\"type\":\"home\"}");
            engaged = false;
            SetStatus("กลับ Home — กด Grip ใหม่เพื่อบังคับต่อ");
        }

        // ----- Grip: Deadman -----
        if (grip && !lastGrip)
        {
            zOffset = 0f;
            Send($"{{\"type\":\"engage\",\"x\":{F(pos.x)},\"y\":{F(pos.y)},\"z\":{F(pos.z)}}}");
            engaged = true;
            Haptic(0.4f, 0.1f);
            SetStatus("กำลังบังคับแขน");
        }
        else if (!grip && lastGrip && engaged)
        {
            Send("{\"type\":\"release\"}");
            engaged = false;
            SetStatus("ปล่อยแล้ว แขนหยุด");
        }

        // ----- Trigger: หัวดูด -----
        if (trigger != lastTrigger)
            Send($"{{\"type\":\"grip\",\"value\":{(trigger ? 1 : 0)}}}");

        // ----- ส่งตำแหน่งขณะบังคับ -----
        if (engaged)
        {
            zOffset += stickY * thumbstickSpeed * Time.deltaTime;
            if (Time.time >= nextSend)
            {
                nextSend = Time.time + 1f / sendHz;
                Send($"{{\"type\":\"pose\",\"frame\":\"headset\",\"x\":{F(pos.x)},\"y\":{F(pos.y + zOffset)},\"z\":{F(pos.z)}}}");
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

    static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);

    void Send(string json)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            udp.Send(bytes, bytes.Length);
        }
        catch (System.Exception e)
        {
            SetStatus("ส่งไม่ได้: " + e.Message);
        }
    }

    void OnDisable()
    {
        if (udp != null && engaged) Send("{\"type\":\"release\"}");
    }

    void OnDestroy() => udp?.Close();

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
