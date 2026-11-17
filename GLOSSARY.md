# JewelryShop

ร้านอัญมณีเสมือนบน Meta Quest 3 คู่กับสถานีบรรจุของขวัญอัตโนมัติ (MG400 + สายพาน) ตามโจทย์ CPE494

## Language

### หน้าร้าน

**Storefront**:
ร้านอัญมณีในโลกเสมือนที่ Customer เข้ามาเลือกดูและซื้อผ่าน Meta Quest 3
_Avoid_: AR shop, shop scene, หน้าร้าน AR

**Controller**:
คอนโทรลเลอร์มือถือที่มากับ Quest 3 ใช้เป็น "มือ" ของทั้ง Customer และ Maintenance Operator
_Avoid_: gripper (คำนี้หมายถึงหัวจับของแขนกล ดู Suction Cup), จอย, hand

**Customer**:
คนที่ใส่ Quest เข้า Storefront เพื่อดูและซื้อ Gem
_Avoid_: buyer, user, ผู้ใช้

**Gem**:
อัญมณีหนึ่งชิ้น ระบุด้วย Cut และ Color
_Avoid_: jewel, jewelry, stone, ของ

**Cut**:
ทรงของ Gem มีสองแบบเท่านั้น คือ Round และ Emerald
_Avoid_: shape, type, ทรง

**Color**:
สีของ Gem ชุดสีที่มีจริงขึ้นกับ Gem Piece ที่มีอยู่
_Avoid_: colour, variant

**Display Case**:
ตู้ใน Storefront ที่วาง Gem Model คละ Cut และ Color ให้ Customer หยิบดู
_Avoid_: showcase, shelf

**Gem Model**:
แบบ Gem 3 มิติใน Display Case เป็นแคตตาล็อก ซื้อแล้วไม่หายจากตู้
_Avoid_: virtual gem, 3D gem, สินค้าในตู้

**Order**:
รายการ Gem (Cut + Color แต่ละชิ้น) ที่ Customer จ่ายเงินครั้งเดียว ทุกชิ้นใน Order ลง Gift Bag ใบเดียวกัน
_Avoid_: purchase, cart, คำสั่งซื้อ

**Checkout**:
จุดใน Storefront ที่ Customer แตะบัตรกับเครื่องเสมือนเพื่อจ่ายเงิน
_Avoid_: payment area, cashier

**Staff**:
พนักงานขายของร้านที่ Customer พบใน Storefront ไม่ได้บังคับแขนกล
_Avoid_: salesperson, พนักงาน (กำกวมกับ Maintenance Operator)

**Staff Call**:
คำขอของ Customer ที่ให้ Staff มาคุยด้วยใน Storefront
_Avoid_: call button, help

### สถานีบรรจุ

**Packing Station**:
ชุดฮาร์ดแวร์จริงทั้งหมด ได้แก่ MG400 สายพาน Gem Stock และ Bag Closer
_Avoid_: cell, robot station

**Packing Sequence**:
งานบรรจุทั้งหมดของหนึ่ง Order บน Packing Station ตั้งแต่ Gift Bag มาถึงหน้าแขนจนออกไปปลายสายพาน
_Avoid_: packing flow, process

**Gem Piece**:
อัญมณีจำลองชิ้นจริงบน Packing Station มี Cut และ Color จริง
_Avoid_: real gem, block, cube, ก้อน

**Gem Stock**:
ที่เก็บ Gem Piece บน Packing Station ประกอบด้วย Stock Slot หลายช่อง
_Avoid_: storage, inventory, tray

**Stock Slot**:
ตำแหน่งหนึ่งใน Gem Stock สำหรับ Cut + Color เดียว มีป้ายบอกให้คนเห็น และใส่ Gem Piece ได้หลายชิ้น
_Avoid_: bin, cell, ช่อง

**Gift Bag**:
ถุงกระดาษบนสายพาน หนึ่งใบรับ Gem Piece ทุกชิ้นของหนึ่ง Order
_Avoid_: gift box, box, กล่อง

**Suction Cup**:
หัวดูดที่ปลายแขน MG400 ใช้หยิบ Gem Piece
_Avoid_: gripper, หัวจับ

**Bag Closer**:
กลไกที่ปิดปากถุง Gift Bag
_Avoid_: lid servo, gripper, มือปิดฝา

**Hub**:
ตัวกลางที่รับ Order และคำสั่ง Teleop จาก Quest แล้วสั่งงาน MG400 และ Arduino
_Avoid_: server, orchestrator, backend

### ซ่อมบำรุงทางไกล

**Maintenance Operator**:
พนักงานที่ใส่ Quest บังคับแขนกลทางไกลเพื่อเก็บ Dropped Gem
_Avoid_: technician, admin, ช่าง, ผู้บังคับ

**Teleop**:
โหมดที่ตำแหน่ง Controller ของ Maintenance Operator ถูกแปลงเป็นตำแหน่งปลายแขน MG400
_Avoid_: remote control, manual mode

**Dropped Gem**:
Gem Piece ที่ตกอยู่บนโต๊ะของ MG400 หรือบน Gem Stock แต่ไม่อยู่ใน Stock Slot ไหนเลย
_Avoid_: lost gem, ของหล่น

**Deadman**:
การกดค้างที่ Maintenance Operator ต้องทำไว้ แขนจึงจะเคลื่อนตาม Teleop ปล่อยแล้วแขนหยุดทันที
_Avoid_: enable button, safety button
