# คู่มือใช้งานประจำวัน: ทำงานกับ Robot PC (ภาษาไทย)

คู่มือสั้น ๆ สำหรับวันที่แลป ใช้ SSH Client (laptop ของเรา, Quest PC หรือ Staff PC) สั่งงาน Robot PC
รายละเอียดเต็มและเหตุผล: [LAN_SETUP.md](LAN_SETUP.md) (ส่วน 1, 7, 11) และ [ADR-0027](../adr/0027-three-pcs-quest-staff-robot.md)
ถ้าคู่มือนี้ขัดกับ LAN_SETUP.md ให้ถือ LAN_SETUP.md เป็นหลัก

## 1. ความปลอดภัยก่อนอื่น

- **ทุกครั้งที่สั่งอะไรที่ขยับแขนจริง** ต้องมีคนอีกคน (ไม่ได้สวมแว่น) ยืนใกล้ปุ่ม E-stop จริงตลอดการรัน
- **ประกาศก่อนรัน** ในแชททีมหรือพูดออกเสียง แล้วรอให้เขาตอบ OK (LAN_SETUP ส่วน 1)
- ยกเว้น: `--dry-run`, อ่าน log, `git pull`
- **อย่าใช้ `suck 1` ตอนนี้**: คำสั่ง suction ยังสั่ง DO1 ซึ่งต่อกับสายพานอยู่ (จนกว่า #235 จะเสร็จ)
- ค่า Z_MIN ยังเป็นค่าชั่วคราว 0 mm (จนกว่า #49 จะเสร็จ) อย่าสั่ง z ต่ำใกล้โต๊ะ
- **ห้ามรัน Preset กับแขนจริง** (ต้องใช้ `--dry-run` เท่านั้น) จนกว่า #49 จะเสร็จ (LAN_SETUP ส่วน 1)
- ช่วงที่แขนไปถึงได้: ระยะ `sqrt(x² + y²)` อยู่ระหว่าง 225 ถึง 365 mm, z อยู่ระหว่าง 0 ถึง 150 mm

## 2. เตรียมครั้งเดียวต่อเครื่อง

| เรื่อง | ทำที่ไหน | อ่านที่ |
|---|---|---|
| ติดตั้ง OpenSSH Server และเปิดพอร์ต | Robot PC (PowerShell แบบ admin) | LAN_SETUP ส่วน 4 |
| สร้าง SSH key แล้วส่งไป Robot PC | SSH Client | LAN_SETUP ส่วน 5 |
| เปิดพอร์ต UDP 7777, 9001, 7778 | Quest PC และ Staff PC | LAN_SETUP ส่วน 8 |
| ตั้ง Sleep เป็น Never | Robot PC | คำสั่งด้านล่าง |

```powershell
powercfg /change standby-timeout-ac 0
```

## 3. ขั้นตอนประจำวัน (ใช้ VS Code)

เปิด VS Code ที่ **repo root** (ไม่ใช่ `Unity/JewelryShop`) แล้ว:

1. `Ctrl+Shift+P` แล้วเลือก **Tasks: Run Task**
2. เลือก **Robot: Start**: หาที่อยู่ Robot PC ให้ครั้งเดียว (ถ้าหาไม่เจอจะถาม) แล้วเปิด Teleop Bridge กับ pc_sender วางข้างกัน
3. ประกาศให้ทีมรู้ แล้วในช่อง **Sender** พิมพ์ `enable` ก่อนเสมอ (Robot Status จะมาหลังส่งคำสั่งแล้วเท่านั้น)
4. สั่งงาน (ดูหัวข้อ 6)
5. เลิกใช้: รัน **Robot: Stop** (เป็น task แยก รันได้ทั้งที่ Sender ยังเปิดอยู่) แล้วพิมพ์ `q` ในช่อง Sender
6. อยากอัปเดตโค้ดบน Robot PC: รัน **Robot: Shell** แล้วพิมพ์

```
cd .. && git switch main && git pull
```

**Ctrl+C ไม่หยุด Bridge ผ่าน SSH** ต้องใช้ Robot: Stop เท่านั้น

## 4. ทำเหมือนกันโดยไม่ใช้ VS Code

รันที่ repo root (`R` คือคำสั่งด้านล่าง):

```powershell
powershell -ExecutionPolicy Bypass -File Dobot\scripts\robot.ps1
```

| คำสั่ง | ทำอะไร |
|---|---|
| `R` | SSH เข้า Robot PC ที่โฟลเดอร์ `Dobot` พร้อม venv |
| `R -Bridge` | เริ่ม Teleop Bridge (หยุดตัวเก่าก่อนให้เอง) เพิ่มอาร์กิวเมนต์: `-BridgeArgs '--speed 20'` หรือ `'--dry-run'` |
| `R -Sender` | รัน pc_sender ชี้ไป Robot PC |
| `R -Stop` | สั่ง disable แล้วหยุด Bridge |
| `R -Find` | แค่พิมพ์ที่อยู่ Robot PC |
| `R -Ip <ที่อยู่>` | ระบุที่อยู่เอง |

ใช้ทีละ switch ต่อหนึ่งหน้าต่าง: เปิด 3 หน้าต่างสำหรับ `-Bridge`, `-Sender` และ `-Stop`

## 5. อยู่หน้าจอ Robot PC เอง

```
Dobot\scripts\start-bridge.cmd
Dobot\scripts\stop-bridge.cmd
```

ที่นี่ Ctrl+C หยุด Bridge ได้ตามปกติ

## 6. คำสั่ง pc_sender ที่ใช้บ่อย

| คำสั่ง | ความหมาย |
|---|---|
| `enable` | เปิดมอเตอร์แขน (พิมพ์ก่อนเสมอ) |
| `status` | พิมพ์ Robot Status ล่าสุด |
| `jog x\|y\|z <step>` | ขยับทีละ step mm เช่น `jog x 10` (ใช้ +/-1, 10, 50) |
| `mv x y z r` | ไปพิกัดที่ระบุ (mm) ครั้งเดียว |
| `stop` | หยุดฉุกเฉิน (ล็อก stop ไว้) |
| `resume` | ปลดล็อก stop |
| `clear` | ล้าง error ของแขน |
| `disable` | ปิดมอเตอร์แขน |
| `q` | ออกจาก pc_sender |

**ฟิลด์ใน Robot Status**

| ฟิลด์ | ความหมาย |
|---|---|
| `mode` | สถานะแขน เช่น enabled หรือ error |
| `error_id` | รหัส error (มีเมื่อ mode เป็น error) |
| `x y z r` | ตำแหน่งปัจจุบันของแขน (mm, องศา) |
| `suction` | ปั๊มดูด ON หรือ off |
| `stop` | `LATCHED` คือ stop ค้างอยู่ ต้อง `resume` |
| `last` | คำสั่งล่าสุดและผล (ถ้าถูกปฏิเสธจะมีเหตุผล) |
| `preset` | Preset ที่รันอยู่ และรายการ Preset ที่มี |

**ลำดับทดสอบที่ปลอดภัย** (มีคนที่ E-stop ตลอด)

1. `enable` แล้ว `status` ดูว่ามี Robot Status กลับมา
2. `jog x 1` ก่อน แล้วค่อย `jog x 10` (ขยับน้อย ๆ)
3. `mv 300 0 80 0` (อยู่กลางช่วงที่ไปถึงได้)
4. ลอง `stop` แล้ว `resume` ดูว่าแขนหยุดและกลับมาขยับได้

## 7. แก้ปัญหา

| อาการ | สาเหตุและวิธีแก้ |
|---|---|
| สคริปต์ถามที่อยู่ หรือที่อยู่เปลี่ยน | Address ของ Robot PC มาจาก DHCP และเปลี่ยนได้ทุกวัน (ยังไม่มีการจอง) รัน `ipconfig` บน Robot PC แล้วใช้ IPv4 ที่**ไม่ใช่** `192.168.1.x` (อันนั้นเป็นวงของแขน) |
| `ssh` ขึ้น "Connection timed out" | เครื่องอยู่ Wi-Fi เดียวกันหรือไม่? Robot PC หลับอยู่หรือไม่ (ตั้ง Sleep เป็น Never)? ลอง `ping <robot-ip>` แล้วดู LAN_SETUP ส่วน 2 และ 7 |
| login ไม่ได้เพราะชื่อผู้ใช้ | ชื่อที่มีช่องว่างต้องใส่เครื่องหมายคำพูด: `ssh "cpe kmutt@<robot-ip>"` และไม่ต้องมี prefix `DESKTOP-...\` |
| `jog` ไม่ขยับเลย | อาจมี Bridge ตัวเก่าค้างอยู่ถือพอร์ต UDP 9000 และ link ของแขน: รัน Robot: Stop แล้ว Robot: Start ใหม่ ถ้ายังไม่ขยับ ดู `status`: ถ้า stop เป็น `LATCHED` ให้ `resume`; ถ้า mode เป็น error ให้ `clear` แล้ว `enable` |
| แขนขึ้น Safety Mode | `clear` แล้ว `enable` แล้ว `resume` (ตามลำดับนี้) |
| log ขึ้น "unreachable" | แขนปฏิเสธจุดนั้นก่อนขยับ (Bridge บีบระยะให้อยู่ใน 225–365 mm แล้ว แต่บางความสูงที่ขอบช่วงแขนยังไปไม่ถึง) แขนไม่ได้ขยับ: สั่งจุดที่ใกล้กลางกว่า เช่น `mv 300 0 80 0` |
| `git pull` ไม่ยอม หลังเคย scp ไฟล์ไปใส่ | ไฟล์ที่ copy เข้าไปชนกับของใน git: `git status` แล้วใช้ `git checkout -- <file>` (ไฟล์ที่แก้) หรือ `del <file>` (ไฟล์ใหม่ที่ copy ไป) ทำ**ก่อน** pull เท่านั้น ห้ามทำหลัง pull |
| ไม่เห็น task ใน VS Code | ต้องเปิดที่ repo root (ไม่ใช่ `Unity/JewelryShop`) แล้วรัน **Developer: Reload Window** |
| ไม่มี Robot Status บน Quest PC / Unity ไม่รับ | มี Block firewall rule ของ Unity บล็อก UDP อยู่ ดู LAN_SETUP ส่วน 7 และ 8 (รัน `setup-lan.ps1 -Role host -DryRun`) |
