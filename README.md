# JewelryShopCPE494_LoveRobot

CPE494 Class Project: a jewelry store on Meta Quest 3 (AR) + a system that automatically packs gems into gift bags with a Dobot MG400 and an Arduino UNO
The full brief is in [`Docs/brief/`](Docs/brief/)

## Structure (monorepo)

```
.
├── Unity/JewelryShop/   Unity project on Meta Quest 3: AR storefront (4a) + remote arm control (4b)
├── Dobot/               Python on the PC: hub (ADR-0001) + MG400 control over TCP/IP
├── UNO/                 Arduino UNO firmware: Bag Closer servo only (ADR-0016, ADR-0005)
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

## Playing the Storefront without a headset (XR Interaction Simulator)

Open `Assets/Scenes/Storefront.unity` and press Play. The XR Interaction Simulator drives the XR Origin with keyboard and mouse; the panel in the corner of the Game view shows what you are moving. The Customer has only a **right** Controller (#30). Keys come from the Simulator sample in `Samples/XR Interaction Toolkit/3.6.1/`.

**Point And Click is on** (the Simulator's default): the right Controller always aims at the mouse cursor, and **left-click** grabs what the cursor points at.

**Two modes: `Tab` switches between them**

| Mode | `W` `A` `S` `D` move | Hold right mouse button + mouse turns |
|---|---|---|
| Body (start) | The whole Customer; the Controller stays in front of you | The whole Customer |
| Controller | The right Controller on its own | The view only |

You can do everything in Body mode; Controller mode only helps to move the Controller closer to something. `]` also switches to Controller mode. **Press it only once:** a second `]` switches to simulated hands, which this project doesn't have (no XR Hands package), so the Controller stops responding until you stop and restart Play. Also avoid `[` and holding `Shift`: they target the left Controller, which the Customer doesn't have. `H` moves the head alone (press `H` again to return); you rarely need it.

**Mouse**

| Input | Does |
|---|---|
| Move the cursor | The Controller aims at it (a held Gem Model turns with the Controller) |
| Hold left button | Grab what the cursor points at (Grip), or press a world-space button (Trigger); release to let go |
| Hold right button + move | Turn (see the modes table); the Controller stops following the cursor while you do |
| Hold right button + scroll | Move forward / back (Body mode); in Controller mode it tilts the view sideways instead, so avoid it there |

**Keyboard**

| Key | Does |
|---|---|
| `W` `A` `S` `D` | Forward / left / back / right |
| `Q` / `E` | Down / up |
| Arrow keys | Turn, like holding the right mouse button |
| Hold `C` / `V` | While turning: turn only left-right / only up-down |
| `R` | Put the Controller back in front of the head |
| `G` (hold) | Grip, the same as holding the left mouse button |
| `T` (hold) | Trigger |
| `I` | Right stick forward: aim a teleport, release to go there |
| `J` / `L` | Right stick left / right: snap turn |
| `` ` `` then `Space` | Controller mode: `` ` `` picks a quick action (Trigger, Grip, Primary, Secondary); `Space` latches it on or off |
| `1` / `2` | Primary / secondary button |
| `M` | Menu button |

**Grab and turn a Gem Model**

1. Walk to the Display Case (`W` `A` `S` `D`; hold right mouse button to look around).
2. Point the cursor at a Gem Model and **hold the left mouse button** (or hold `G`) to grab it.
3. Keep holding and move the cursor: the Controller aims at the cursor and the Gem Model turns with it.
4. Release the button to let go.

**Turn a held Gem Model (`Left Alt`)**

Point And Click only aims the Controller, so on its own a held Gem Model turns left-right and up-down but cannot roll. To see every side, **hold `Left Alt` while you hold a Gem Model**:

| Input (with `Left Alt` held) | Does |
|---|---|
| Move the mouse left / right | Turn the Gem Model left / right (yaw) |
| Move the mouse up / down | Tilt the Gem Model up / down (pitch) |
| Scroll the wheel | Roll the Gem Model |

While `Left Alt` is held the Controller stops following the cursor, and the grab stays. The Controller model (`Right Controller Visual`) turns with the Gem Model, as the wrist does on a Quest; the ray does not turn. Release `Left Alt`: the Gem Model keeps its rotation and the Controller follows the cursor again. Release `Left Alt` before you let go of the left mouse button: while `Alt` is held the Simulator doesn't see the release. Let go of the Gem Model and both rotations reset (the Controller model snaps back), so the next grab starts fresh. Speeds are on the `GemModelKeyRotator` component of the `Right Controller` (**Degrees Per Pixel**, **Degrees Per Scroll Unit**); you can change them during Play. This only works in the Editor with the Simulator; on a Quest the wrist turns the Gem Model.

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
