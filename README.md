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

Workflow เต็มอยู่ใน [`CLAUDE.md`](CLAUDE.md): `grill-with-docs` / `grill-me` → `to-spec` → `to-tickets` → `grab-ticket` → `code-review` (รันทุกครั้งที่มีการแก้ ไม่ว่าจะเล็กแค่ไหน)

| Skill | ใช้เมื่อ |
|---|---|
| `grill-with-docs` / `grill-me` | ซักแนวคิดก่อนเริ่มงาน (บันทึก ADR / glossary) |
| `to-spec` → `to-tickets` | เขียน spec → แตกเป็น ticket ที่ติด `ready-for-agent` + `area:` |
| `grab-ticket` | grill check ความเข้าใจ ticket แล้วส่ง Role sub-agent ไปทำ |
| `pick-role` | แนะนำ Role + model ให้ ticket (`grab-ticket` เรียกให้เอง) |
| `coder` | กฎประจำของการเขียนโค้ดในแต่ละโฟลเดอร์ (Role โหลดให้เอง) |
| `code-review` | รีวิว diff ทุกรอบ |
| `domain-modeling` | บันทึก ADR ลง `Docs/adr/` |

Role (sub-agent ประจำสาย) อยู่ใน [`.claude/agents/`](.claude/agents/): `general-coder-unity`, `general-coder-dobot`, `general-coder-uno`

เทสต์ Unity: ดู [`Docs/unity/TESTING.md`](Docs/unity/TESTING.md) (Test Runner: EditMode + PlayMode)

**กติกา:** คนถือ **GitHub pen** (commit / PR / merge), **editor pen** (Inspector, จัดวาง scene) และ **robot pen** (รันโค้ดกับ MG400 / Arduino ตัวจริง)
