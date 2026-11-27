# CPE494 Class Project Summary

> CPE494 Special Topic IV: Industry Automation and Robotics for Computer Engineering
> Source: [`CPE494-Class-Project-Brief.pdf`](CPE494-Class-Project-Brief.pdf)

## Overview

- Work in groups of **5–6 people**
- Build an **AR jewelry sales system (Meta Quest 3)** paired with a **100% automatic gem-packing system that packs gems into gift boxes**
- The system must be **safe** in its mechanical structure, electrical side and software
- **Background:** a jewelry shop in the Bang Mod area is moving online because rents are rising. The owner wants customers to wear a Meta Quest 3 and feel as if they are buying in the real shop, and wants an automatic system to pack gifts instead of hiring staff

## Total score

| Part | Points |
|---|---|
| 4a AR storefront system | 30 |
| 4b Remote maintenance system | 15 |
| 5 Mechanical system | 15 |
| 6 Reports and presentation | 30 |
| **Total** | **90** |

## 4a. AR storefront system (30 points)

### i. Gem display case
A case holds 9 gems, in both **Round** and **Emerald** cuts, with mixed cuts and colors. The buyer can pick up any piece and view it in 3D.
- [ ] Rotate the gem to view it from every angle in 3D (5)
- [ ] The gem's view changes with the movement of the buyer's hand (5)

### ii. Payment area
The buyer picks up a card and taps it on a terminal in the virtual world to pay.
- [ ] There is an animation sequence during payment (5)
- [ ] Create a good buyer experience during payment (5)

### iii. Staff call button
Pressing it shows the staff member's animation and audio in real time.
- [ ] Pressing the button lets the user actually talk with the staff (5)

### iv. Extra feature
- [ ] One additional feature that increases sales or gives the buyer a better experience (5)

## 4b. Remote maintenance system (15 points)

### i. Picking up a dropped gem
When a gem ends up in an unplanned position, the maintenance staff must drive the robot arm through a Meta Quest (AR) from some point of the station to pick it up. Markers or extra parts may be attached to identify the gem's position.
- [ ] Actually pick up a dropped gem, with the examiners choosing the position at random (5)
- [ ] Actually pick up a dropped gem, with the pick position following the operator's hand position (10)

## 5. Mechanical system (15 points)

**All** of the following must work to receive any points
1. A pre-folded paper box is carried along the conveyor and stops in front of the robot arm
2. The arm picks gems from the storage according to the **cut and color the customer chose**, and places them in the box
3. Close the box lid, **attach a ribbon**, then carry the box to the end of the conveyor

> ℹ️ **Reality differs from the brief:** the instructor provided a paper bag instead of a box. A person attaches the ribbon to the bag beforehand, as the instructor allows. See [ADR-0005](../adr/0005-paper-gift-bag.md)

## 6. Reports and presentation (30 points)

| Task | Deadline | File | Content |
|---|---|---|---|
| Progress report 1 | **Wed 18 Nov 2026, 23:59** | PDF | Inspiration, work plan, system design overview, equipment comparison, comparison of techniques for items 4 and 5 (both mechanical and software), parts that are stuck or cannot be solved, and parts not yet done |
| Progress report 2 + demo video | **Wed 25 Nov 2026, 23:59** | PDF + MP4 | Must be **at least 50%** complete across all functions, and the **mechanical system must work completely**. A group representative presents the same day, 13:30–16:30 |
| Final presentation | **Wed 9 Dec 2026, 13:30–16:30** | - | 3 examiners score how complete the system is |
| Summary report + summary video | **Fri 11 Dec 2026, 23:59** | PDF + MP4 | No required format |

Every report has **no required document format**; design it freely.

## Other conditions

- **Groups of 6 or more:** must build a real-time simulation of the robot arm as a **Digital Twin** (RoboDK, ROS, Python or any other tool is fine)
- **Work for items 4-6:** if not submitted in full or on time, no points are given for that part
- **Demo day:** the examiners test the system themselves while the group presents, then **ask technical questions to each person individually**

> ✅ **Demo day:** the documents contradict each other (item 9 says 25 Nov, item 6c says 9 Dec). The team confirmed it will demo the complete system on **Wed 9 Dec 2026**. On 25 Nov the second report is still due per the table.

## Criteria for assessing presentation and report content

| Topic | What the examiners look for |
|---|---|
| **Design Planning** | Engineering principles back the design decisions, a system workflow diagram, 3D drafts of the self-designed parts, attention to safety and maintenance, and equipment the brief does not specify is compared with similar equipment |
| **Job Description** | Each member's position and duties are clearly stated (e.g. Mr. A researches and writes the program) |
| **Working of individual system** | Each device works as designed, e.g. reads sensor values, commands the arm to a given position, shows sensor values on the computer |
| **System integration** | The combined system really works, there are test results, analysis of problems both solved and unsolved (with proposed fixes), and neat assembly (tidy wiring, tidy circuits, sturdy mechanical parts) |
| **Presentation** | Covers the material completely and stays on point, explains equations or concepts correctly and clearly, uses images or video, has a readable layout, and has sharp video and audio with neat editing |

## Timeline (counted from today, 11 Nov 2026)

```
11 Nov  ── today
18 Nov  ── submit progress report 1 (PDF)                              [+7 days]
25 Nov  ── submit report 2 + video, present 13:30–16:30                [+14 days]
           (≥50% done and the mechanical system must be finished)
 9 Dec  ── final presentation 13:30–16:30 (3 examiners)                [+28 days]
11 Dec  ── submit summary report + summary video (PDF + MP4)           [+30 days]
```
