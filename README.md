# JewelryShopCPE494_LoveRobot

CPE494 Class Project: ร้านอัญมณีบน Meta Quest 3 (AR) + ระบบบรรจุอัญมณีลงกล่องของขวัญอัตโนมัติ ด้วย Dobot MG400 และ Arduino UNO
โจทย์ฉบับเต็มอยู่ที่ [`Docs/brief/`](Docs/brief/)

## โครงสร้าง (monorepo)

```
.
├── Unity/JewelryShop/   Unity project บน Meta Quest 3: หน้าร้าน AR (4a) + บังคับแขนกลทางไกล (4b)
├── Dobot/               Python บน PC: ตัวกลางสั่งงาน (ADR-0001) + ควบคุม MG400 ผ่าน TCP/IP
├── UNO/                 Arduino UNO firmware: สายพาน, เซนเซอร์, servo ปิดฝา, โบว์ (ADR-0004)
├── Docs/
│   ├── brief/           โจทย์จากอาจารย์ (PDF) + สรุป
│   └── adr/             บันทึกการตัดสินใจ (Architecture Decision Records)
└── .claude/skills/      skills ของ Claude Code (ดูหัวข้อด้านล่าง)
```

## ทีมและการแบ่งงาน

| สมาชิก | ฝ่าย | หน้าที่ | โฟลเดอร์หลัก |
|---|---|---|---|
| Heart | Software | VR / AR บน Meta Quest 3 ใน Unity | `Unity/` |
| Focus | Hardware | Dobot MG400 และ Arduino UNO | `Dobot/`, `UNO/` |
| Tangmo | Hardware | Dobot MG400 และ Arduino UNO | `Dobot/`, `UNO/` |
| Taxi | Design | ออกแบบร้าน Jewelry | `Unity/` (scene, assets) |
| Earth | 3D / Support | โมเดล 3D ของสถานี Dobot และชิ้นส่วนประกอบ, ซัพพอร์ตทีม | — |

กลุ่มมี 5 คน จึงไม่ต้องทำ Digital Twin (โจทย์ข้อ 7 บังคับเฉพาะกลุ่ม 6 คนขึ้นไป)

## ทำงานกับ Claude Code

| Skill | ใช้เมื่อ |
|---|---|
| `grab-ticket` | ส่ง sub-agent ไปทำ GitHub issue ที่ติด label `ready-for-agent` |
| `coder` | กฎประจำของการเขียนโค้ดในแต่ละโฟลเดอร์ (`grab-ticket` โหลดให้เอง) |
| `grilling` → `to-spec` → `to-tickets` | ซักแนวคิด → เขียน spec → แตกเป็น ticket |
| `domain-modeling` | บันทึก ADR ลง `Docs/adr/` |

**กติกา:** คนถือ **GitHub pen** (commit / PR / merge), **editor pen** (Inspector, จัดวาง scene) และ **robot pen** (รันโค้ดกับ MG400 / Arduino ตัวจริง)
