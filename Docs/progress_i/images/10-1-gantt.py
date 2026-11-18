"""Generate the Gantt chart for Progress Report 1 (figure 10.1).

Edit TASKS below, then run:  python Docs/progress_i/images/10-1-gantt.py
It writes 10-1-gantt.html next to this file; screenshot it (or open it in a browser
and print/save) to refresh 10-1-gantt.png.
"""
from datetime import date, timedelta
from pathlib import Path

START, END = date(2026, 11, 11), date(2026, 12, 11)
TODAY = date(2026, 11, 18)

# (group, task, start, end inclusive, done?)
TASKS = [
    ("Unity", "ฉากร้าน + หยิบหมุน Gem Model", (11, 11), (11, 18), True),
    ("Unity", "Order (001): Order Tray + แผง Order", (11, 18), (11, 23), False),
    ("Unity", "Checkout (002): แตะบัตร + Receipt", (11, 21), (11, 25), False),
    ("Unity", "Staff Call: Netcode + เสียง + Avatar", (11, 26), (12, 3), False),
    ("Unity", "Gem Details Panel", (12, 1), (12, 5), False),
    ("Unity", "Build ลง Quest 3 + ทดสอบบนแว่น", (12, 3), (12, 8), False),
    ("Hub", "ทดสอบสั่ง MG400 ผ่าน TCP", (11, 14), (11, 18), True),
    ("Hub", "รวมโค้ด TCP + สายพาน/เซนเซอร์ผ่าน I/O", (11, 18), (11, 22), False),
    ("Hub", "Packing Sequence (state machine)", (11, 20), (11, 25), False),
    ("Hub", "รับ Order จาก Quest (Q-02)", (11, 24), (11, 29), False),
    ("Hub", "Safety proxy + Deadman", (11, 27), (12, 2), False),
    ("Hub", "โหมด Teleop (Q-04)", (11, 30), (12, 6), False),
    ("UNO", "CommandParser + CI", (11, 13), (11, 18), True),
    ("UNO", "Serial loop + servo Bag Closer", (11, 18), (11, 23), False),
    ("UNO", "E-stop ตัดไฟจริง", (11, 24), (11, 28), False),
    ("ทางกล", "ออกแบบ Gem Stock / ฐานถุง / Bag Closer", (11, 12), (11, 20), False),
    ("ทางกล", "พิมพ์ 3D + ประกอบ", (11, 19), (11, 24), False),
    ("ทางกล", "ทดสอบทางกล end-to-end", (11, 23), (11, 25), False),
    ("ทางกล", "ปรับจูน + เผื่อแก้", (11, 26), (12, 7), False),
    ("รวมระบบ + ส่งงาน", "รายงาน 1", (11, 16), (11, 18), True),
    ("รวมระบบ + ส่งงาน", "รายงาน 2 + วิดีโอสาธิตบางส่วน", (11, 23), (11, 25), False),
    ("รวมระบบ + ส่งงาน", "รวมระบบ Quest → Hub → Packing", (12, 1), (12, 8), False),
    ("รวมระบบ + ส่งงาน", "ซ้อมนำเสนอ", (12, 6), (12, 9), False),
    ("รวมระบบ + ส่งงาน", "รายงานสรุป + วิดีโอสรุป", (12, 9), (12, 11), False),
]
COLORS = {"Unity": "#2a78d6", "Hub": "#eb6834", "UNO": "#1baf7a", "ทางกล": "#eda100", "รวมระบบ + ส่งงาน": "#e87ba4"}
MILESTONES = [((11, 18), "รายงาน 1"), ((11, 25), "รายงาน 2"), ((12, 9), "นำเสนอ + Demo"), ((12, 11), "รายงานสรุป")]

LABEL_W, DAY_W, ROW_H, BAR_H, TOP, GROUP_H = 290, 30, 28, 16, 78, 30
ndays = (END - START).days + 1
d = lambda m, dd: date(2026, m, dd)
x = lambda day: LABEL_W + (day - START).days * DAY_W

rows, y = [], TOP
for g in COLORS:
    rows.append(("group", g, y)); y += GROUP_H
    for t in TASKS:
        if t[0] == g:
            rows.append(("task", t, y)); y += ROW_H
H, W = y + 58, LABEL_W + ndays * DAY_W + 110

s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="Leelawadee UI, Tahoma, sans-serif">',
     f'<rect width="{W}" height="{H}" fill="#fcfcfb"/>']
for i in range(ndays):  # weekend shading + day ticks
    day = START + timedelta(i)
    if day.weekday() >= 5:
        s.append(f'<rect x="{x(day)}" y="{TOP-24}" width="{DAY_W}" height="{H-TOP-34}" fill="#f0efec"/>')
    s.append(f'<line x1="{x(day)}" y1="{TOP-24}" x2="{x(day)}" y2="{H-58}" stroke="#e6e5e1" stroke-width="1"/>')
    s.append(f'<text x="{x(day)+DAY_W/2}" y="{TOP-8}" font-size="11" fill="#52514e" text-anchor="middle">{day.day}</text>')
s.append(f'<text x="{x(START)}" y="{TOP-34}" font-size="12" fill="#52514e">พ.ย. 69</text>')
s.append(f'<text x="{x(d(12,1))}" y="{TOP-34}" font-size="12" fill="#52514e">ธ.ค. 69</text>')
for kind, item, ry in rows:
    if kind == "group":
        s.append(f'<rect x="16" y="{ry+9}" width="12" height="12" rx="3" fill="{COLORS[item]}"/>')
        s.append(f'<text x="36" y="{ry+20}" font-size="14" font-weight="700" fill="#0b0b0b">{item}</text>')
        continue
    g, name, a, b, done = item
    a, b = d(*a), d(*b)
    s.append(f'<text x="36" y="{ry+ROW_H/2+5}" font-size="13" fill="#0b0b0b">{name}</text>')
    bx, bw = x(a) + 2, (b - a).days * DAY_W + DAY_W - 4
    fill = COLORS[g]
    op = "1" if done else "0.85"
    s.append(f'<rect x="{bx}" y="{ry+(ROW_H-BAR_H)/2}" width="{bw}" height="{BAR_H}" rx="4" fill="{fill}" fill-opacity="{op}"/>')
    if done:
        s.append(f'<text x="{bx+bw+6}" y="{ry+ROW_H/2+4}" font-size="11" font-weight="700" fill="#52514e">✓ เสร็จ</text>')
for i, ((m, dd), label) in enumerate(MILESTONES):
    mx = x(d(m, dd)) + DAY_W
    ly = H - 36 + (i % 2) * 18
    s.append(f'<line x1="{mx}" y1="{TOP-24}" x2="{mx}" y2="{H-58}" stroke="#0b0b0b" stroke-width="1.5" stroke-dasharray="4 3"/>')
    s.append(f'<text x="{mx}" y="{ly}" font-size="12" font-weight="700" fill="#0b0b0b" text-anchor="middle">{label}</text>')
s.append('</svg>')
html = '<!doctype html><meta charset="utf-8"><title>Gantt</title><body style="margin:0;background:#fcfcfb">' + "".join(s)
out = Path(__file__).with_name("10-1-gantt.html")
out.write_text(html, encoding="utf-8")
print(out, W, H)
