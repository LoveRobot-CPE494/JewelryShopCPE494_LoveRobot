# JewelryShopCPE494_LoveRobot

CPE494 Class Project: ร้านอัญมณีบน Meta Quest 3 (AR) + ระบบบรรจุอัญมณีลงถุงของขวัญอัตโนมัติ ด้วย Dobot MG400 และ Arduino UNO
โจทย์ฉบับเต็มอยู่ที่ [`Docs/brief/`](Docs/brief/)

## โครงสร้าง (monorepo)

```
.
├── Unity/JewelryShop/   Unity project บน Meta Quest 3: หน้าร้าน AR (4a) + บังคับแขนกลทางไกล (4b)
├── Dobot/               Python บน PC: ตัวกลางสั่งงาน (ADR-0001) + ควบคุม MG400 ผ่าน TCP/IP
├── UNO/                 Arduino UNO firmware: สายพาน, เซนเซอร์, Bag Closer (ADR-0004, ADR-0005)
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

## วิธีทำงาน (ทุกคนทำแบบนี้)

**ห้าม push เข้า `main` ตรง ๆ** ทุกงานแตก branch แล้ว merge ผ่าน PR เท่านั้น กันงานชนกัน (merge conflict) และกัน `main` พัง

### 1. ก่อนเริ่มงาน

```powershell
git switch main
git pull
git switch -c <ประเภท>/<เรื่องสั้น ๆ>
```

ชื่อ branch: `feat/…` ของใหม่, `fix/…` แก้บั๊ก, `docs/…` เอกสาร, `chore/…` งานจุกจิก เช่น `feat/012-03-order-builder`
(ถ้าทำตาม ticket ให้ใส่เลข ticket ไว้ในชื่อ)

### 2. ระหว่างทำงาน (กับ Claude Code)

| ขั้น | พิมพ์ใน Claude Code | ได้อะไร |
|---|---|---|
| 1. คิดงานใหม่ | `/grill-with-docs` (หรือ `/grill-me`) | Claude ซักจนแนวคิดชัด บันทึก ADR / glossary |
| 2. เขียน spec | `/to-spec` | issue `NNN spec: …` บน GitHub |
| 3. แตก ticket | `/to-tickets` | issue `NNN-MM ticket: …` ติด `ready-for-agent` + `area:unity/dobot/uno` |
| 4. ลงมือทำ | `/grab-ticket <เลข issue>` | Claude สรุปความเข้าใจ ticket ให้ยืนยัน (ถามเพิ่มถ้าไม่ชัด) แล้วส่ง Role ประจำสายไปเขียนโค้ด + เทสต์ (tdd) |
| 5. รีวิว | `/code-review` | รีวิวทุกครั้งที่มีการแก้ ไม่ว่าจะเล็กแค่ไหน (`grab-ticket` รันให้แล้ว แต่งานที่แก้เองต้องสั่งเอง) |

- งานเล็ก/งานจุกจิก (แก้คำผิด, เปลี่ยนค่าคงที่) ข้ามขั้น 1–3 ได้ แต่ **ห้ามข้ามขั้น 5**
- `/clear` ระหว่าง ticket แต่ละใบ จะได้เริ่มใหม่สะอาด ๆ
- งาน Unity ต้องผ่าน Test Runner ทั้ง EditMode และ PlayMode ก่อนเปิด PR (ดู [`Docs/unity/TESTING.md`](Docs/unity/TESTING.md))
- Claude แก้แค่ไฟล์ในเครื่อง ส่วนที่ต้องใช้คน (ต่อสายใน Inspector, รันกับ MG400/Arduino ตัวจริง) Claude จะบอกเป็นงานให้คนทำ

### 3. ส่งงาน

พอ `code-review` ผ่าน Claude จะจบงานด้วยบล็อก **Handoff**: เขียน commit message ไว้ที่ `.git/COMMIT_MSG` และ PR body (skill `pr`) ไว้ที่ `.git/PR_BODY.md` แล้วพิมพ์คำสั่ง PowerShell ชุดเดียว (`git switch -c` → `git add` ทีละไฟล์ → `git commit -F` → `git push -u` → `gh pr create --body-file`) ให้คนตรวจแล้ว copy ไปรันเอง Claude ไม่ commit หรือ push เอง (กติกาเต็มอยู่ใน `CLAUDE.md` หัวข้อ GitHub pen)

- ไฟล์ที่ Unity มักแก้เองและ**ไม่ควร** commit ถ้าไม่ได้ตั้งใจ: `.vscode/`, `ProjectSettings/*.asset`, `Assets/Settings/*.asset`, `Assets/InitTestScene*.unity`
- ใน PR: แนะนำให้เพื่อนดูก่อน merge, ถ้าขึ้น conflict ให้ `git pull origin main` ใน branch ตัวเองแล้วแก้ก่อน

### 4. หลัง merge

```powershell
git switch main
git pull
git branch -d <ชื่อ branch>
```

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

**กติกา:** คนถือ **GitHub pen** (commit / push / PR / merge — ห้าม push เข้า `main` ตรง), **editor pen** (Inspector, จัดวาง scene) และ **robot pen** (รันโค้ดกับ MG400 / Arduino ตัวจริง)
