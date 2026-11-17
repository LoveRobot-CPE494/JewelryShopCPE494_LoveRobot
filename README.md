# JewelryShopCPE494_LoveRobot

CPE494 Class Project: a jewelry store on Meta Quest 3 (AR) + a system that automatically packs gems into gift bags with a Dobot MG400 and an Arduino UNO
The full brief is in [`Docs/brief/`](Docs/brief/)

## Structure (monorepo)

```
.
├── Unity/JewelryShop/   Unity project on Meta Quest 3: AR storefront (4a) + remote arm control (4b)
├── Dobot/               Python on the PC: hub (ADR-0001) + MG400 control over TCP/IP
├── UNO/                 Arduino UNO firmware: conveyor, sensors, Bag Closer (ADR-0004, ADR-0005)
├── Docs/
│   ├── brief/           Brief from the instructor (PDF) + summary
│   └── adr/             Decision records (Architecture Decision Records)
└── .claude/skills/      Claude Code skills (see the section below)
```

## Assets in the Unity project

Third-party art and samples under `Unity/JewelryShop/Assets/`. Models and textures go through Git LFS (see "Before starting" below).

| Folder | What it is | Source / License | Planned for | Notes |
|---|---|---|---|---|
| `Samples/XR Interaction Toolkit/3.6.1/` | Starter Assets (XR Origin, Controller input) + XR Interaction Simulator | Unity XR Interaction Toolkit samples | In use: XR Origin and headset-free testing in the Storefront (#18) | |
| `jewelry_shop/` | Jewelry-shop interior: shop shell, display cases, props (models, prefabs, textures, a demo scene) | Unity Asset Store, free | In use: Storefront interior (#22): `SM_shop` shell, `SM_showcase_1` display case | Materials are URP Lit, except `DemoScene/0/sky.mat`; the two atlas textures `T_`/`T_1` are set to max 512, compressed (ASTC on Android) |
| `ThirdParty/TinyTreats/` | Tiny Treats Bakery Interior 1.1: the Unity FBX models and their textures (display cases, cash register, counters, …) | CC0, see `License.txt` | In use: `countertop_straight_A_short` as the Checkout counter (#22) | Texture set to max 512, compressed |
| `Low poly Gems/` | 6 low-poly gems (`Stone1`–`Stone6`): meshes, materials, prefabs, textures, a demo scene | Unity Asset Store, free | In use: meshes only, Gem Models (#22): `Stone4` is Round, `Stone1` is Emerald (`Assets/Prefabs/GemModel_*.prefab`) | The pack's own materials and textures are not used (Standard shader, 2048²); gems use `Assets/Materials/Gems/Gem_*.mat` (URP Lit, Transparent) |

Not listed: `TextMesh Pro/`, `TutorialInfo/` and `Readme.asset` (Unity template defaults), and `XR/`, `XRI/` (settings, not art). The Storefront (`Assets/Scenes/Storefront.unity`) keeps the #20 `Greybox` objects: on `DisplayCase` and `CheckoutCounter` their renderers are hidden and real art sits beside them (#22); `CardReader`, `StaffCallButton` and `StaffSpot` are still greybox.

## Team and division of work

| Member | Side | Role | Main folders |
|---|---|---|---|
| Heart | Software | VR / AR on Meta Quest 3 in Unity | `Unity/` |
| Focus | Hardware | Dobot MG400 and Arduino UNO | `Dobot/`, `UNO/` |
| Tangmo | Hardware | Dobot MG400 and Arduino UNO | `Dobot/`, `UNO/` |
| Taxi | Design | Designs the jewelry store | `Unity/` (scene, assets) |
| Earth | 3D / Support | 3D models of the Dobot station and its parts, team support | - |

The group has 5 members, so no Digital Twin is needed (brief item 7 requires it only for groups of 6 or more)

## How we work (everyone does it this way)

**Never push to `main` directly.** Every piece of work gets a branch and merges through a PR only, to avoid merge conflicts and keep `main` from breaking.

### 1. Before starting

First time on a new machine: run `git lfs install` before cloning (asset files such as `.fbx`, `.png`, `.wav` are stored through Git LFS, see `.gitattributes`). If you cloned first and textures show up pink, run `git lfs pull`.

```powershell
git switch main
git pull
git switch -c <type>/<short-topic>
```

Branch names: `feat/…` new work, `fix/…` bug fixes, `docs/…` documentation, `chore/…` small chores, e.g. `feat/012-03-order-builder`
(when working from a ticket, put the ticket number in the name)

### 2. While working (with Claude Code)

| Stage | Type in Claude Code | What you get |
|---|---|---|
| 1. Think through new work | `/grill-with-docs` (or `/grill-me`) | Claude grills you until the idea is clear and records ADRs / glossary |
| 2. Write the spec | `/to-spec` | An `NNN spec: …` issue on GitHub |
| 3. Split into tickets | `/to-tickets` | `NNN-MM ticket: …` issues labeled `ready-for-agent` + `area:unity/dobot/uno` |
| 4. Implement | `/grab-ticket <issue number>` | Claude summarizes its understanding of the ticket for you to confirm (asking more if unclear), then dispatches the Role for that area to write code + tests (tdd) |
| 5. Review | `/code-review` | Review after every change, however small (`grab-ticket` already runs it, but work you edit yourself needs you to run it) |

- For small or chore work (typo fixes, changing a constant) you may skip stages 1-3, but **never skip stage 5**
- `/clear` between tickets so each one starts clean
- Unity work must pass the Test Runner in both EditMode and PlayMode before opening a PR (see [`Docs/unity/TESTING.md`](Docs/unity/TESTING.md))
- Claude only edits local files; anything that needs a human (wiring in the Inspector, running against a real MG400/Arduino) Claude hands back as a task for you

### 3. Handing off the work

Once `code-review` passes, Claude ends the work with a **Handoff** block: it writes the commit message to `.git/COMMIT_MSG` and the PR body (skill `pr`) to `.git/PR_BODY.md`, then prints one PowerShell command set (`git switch -c` → `git add` file by file → `git commit -F` → `git push -u` → `gh pr create --label … --body-file`) for a human to check, copy and run. Claude does not commit or push itself (full rules are in `CLAUDE.md`, section GitHub pen)

- Files Unity often changes by itself and that you should **not** commit unless you meant to: `.vscode/`, `ProjectSettings/*.asset`, `Assets/Settings/*.asset`, `Assets/InitTestScene*.unity`
- In a PR: have a teammate look before merging; if a conflict appears, run `git pull origin main` in your own branch and fix it first

### 4. After merging

```powershell
git switch main
git pull
git branch -d <branch name>
```

## Working with Claude Code

The full workflow is in [`CLAUDE.md`](CLAUDE.md): `grill-with-docs` / `grill-me` → `to-spec` → `to-tickets` → `grab-ticket` → `code-review` (run after every change, however small)

| Skill | Use when |
|---|---|
| `grill-with-docs` / `grill-me` | Grilling an idea before starting work (records ADRs / glossary) |
| `to-spec` → `to-tickets` | Write a spec → split it into tickets labeled `ready-for-agent` + `area:` |
| `grab-ticket` | Grill-check understanding of a ticket, then dispatch a Role sub-agent to do it |
| `pick-role` | Recommends a Role + model for a ticket (`grab-ticket` calls it automatically) |
| `coder` | Standing rules for writing code in each folder (the Role loads it itself) |
| `code-review` | Reviews the diff every round |
| `domain-modeling` | Records ADRs into `Docs/adr/` |

Roles (per-area sub-agents) live in [`.claude/agents/`](.claude/agents/): `general-coder-unity`, `general-coder-dobot`, `general-coder-uno`

Unity tests: see [`Docs/unity/TESTING.md`](Docs/unity/TESTING.md) (Test Runner: EditMode + PlayMode)

**Rules:** humans hold the **GitHub pen** (commit / push / PR / merge, never push directly to `main`), the **editor pen** (Inspector, laying out scenes) and the **robot pen** (running code against the real MG400 / Arduino)
