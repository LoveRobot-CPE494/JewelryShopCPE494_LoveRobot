---
name: grab-ticket
description: Dispatch a Role sub-agent (named with `role=<name>`, else chosen by `pick-role`) at a Haiku/Sonnet/Opus Level to implement one `ready-for-agent` GitHub issue in this monorepo, after a grill check of the ticket. Use on "grab ticket", "รับงาน ticket", or when the user names an issue to work on.
---

# Grab ticket

Dispatch one sub-agent to implement one ticket in one **area** (`unity`, `dobot`, `uno`; see the
`coder` skill). Adapted from `grab-ticket` in `where-it-went`, cut down to a single repo.

Repo: `LoveRobot-CPE494/JewelryShopCPE494_LoveRobot` (`<REPO>`); the repo root is `<ROOT>`.
The sub-agent makes **local changes only**; GitHub (assign, comment, commit, branch, PR) stays with
the human.

**Optional `role=<name>`** (`grab-ticket 13 role=general-coder-unity`) dispatches that Role, a
subagent in `.claude/agents/`. Confirm the file exists; if not, stop and list the Roles that do.
Without `role=`, step 3 lets `pick-role` choose.

## Issue conventions

- **Spec:** `NNN spec: …`, the what/why of a feature.
- **Ticket:** `NNN-MM ticket: …`, one implementable unit, linked to blockers by native `blocked_by`.
- **Bug:** `NNN-MM-K Bug: …`, labelled `bug`, referencing its ticket.
- Labels: `ready-for-agent`, and one of `area:unity`, `area:dobot`, `area:uno`.

## 1. Resolve the ticket

The id is the **GitHub issue number**: `grab-ticket 13` means `#13`. With no number, take the
**frontier**: the lowest-numbered open `ready-for-agent` issue with no open blocker.
```
gh issue list --repo <REPO> --label ready-for-agent --state open --json number,title,labels
```

## 2. Check it is unblocked, then read it

```
gh api repos/<REPO>/issues/<n> --jq .issue_dependencies_summary.blocked_by
```
Greater than `0` → **stop** and name the open blockers. Otherwise read it **with comments**:
```
gh issue view <n> --repo <REPO> --comments
```
The **latest** comment wins over the body. If a comment says the ticket is stale, re-scoped, or
waiting on other work, stop and tell the user. Carry amending comments into the prompt.

## 3. Choose the area, the Role and the model

**Area:** the `area:` label; without one, the folder the ticket's work lands in. A ticket spanning
two areas → ask the user to split it.

**Role:** with `role=`, use it. Otherwise apply the `pick-role` skill to this ticket and use its
pick (never a `trial-` Role); with no fitting Role, fall back to `general-purpose`.

**Model:**
- **Haiku**: mechanical and fully specified (a pattern already in the repo, a constant, a doc).
- **Sonnet** (default): needs judgment (a new seam or protocol message, first-of-its-kind code,
  ambiguous criteria).
- **Opus**: only when the core risk is a hard-to-reverse design or subtle correctness: the
  Quest → MG400 coordinate mapping, motion safety limits, the packing state machine's error paths.
  Size alone never earns Opus.

The first ticket to introduce a pattern gets at least Sonnet. State the area, the Role, the model
and a one-line reason before dispatching. The model chosen here overrides the Role's default.

## 4. Grill check

Before dispatching, confirm you and the user read the ticket the same way. Write back, in a few
lines: what will change, where, what "done" looks like (each acceptance criterion), and what is
out of scope. Then list the open questions: gaps, ambiguous criteria, conflicts with an ADR or a
comment, and any human pen the work will need.

- **No open questions** → ask the user to confirm the read-back in one word, then dispatch.
- **Open questions** → grill them with the `grilling` skill (the primitive behind `grill-me`), one question at a time with your
  recommended answer, until each is settled. Carry every answer into the dispatch prompt as an
  amendment. If an answer changes the ticket's scope, stop and tell the user to update the issue
  first (the GitHub pen is theirs).

Skip the questions only when the user said to dispatch without them; say that you skipped.

## 5. Dispatch

`Agent` tool, `subagent_type:` the Role's `name` from step 3 (or `general-purpose`),
`model: haiku | sonnet | opus`, `effort: medium`. The prompt is the same either way; the Role
adds its own rules on top. The prompt contains:

- **The ticket**: full body and acceptance criteria, corrected by amending comments (assume no
  GitHub access).
- **Where to work**: `<ROOT>` and the area folder. Edit only inside that folder.
- **Standing rules**: invoke the `coder` skill first and read its know-how file for the area.
- **The full loop, which defines done** (the first green is not done):
  0. `ponytail` at `full`: the smallest complete change; it never decides whether something is
     tested. **Bug ticket** (`bug` label or `Bug:` title): run `diagnosing-bugs` first; the red
     test reproduces the bug.
  1. `tdd`, red → green → **refactor**. Skip only for a change with no new or changed logic (a
     rename, doc, constant, `[SerializeField]` with no behaviour) and report
     `tdd skipped: no logic change: <why>`.
  2. **Run the tests** for the area and observe the result. Unity: follow
     `Docs/unity/TESTING.md` (Test Runner through `unity-mcp` when the Editor is open, batchmode
     when it is closed).
  3. `code-review` on the diff — **always**, even for a one-line or `tdd skipped` change. Fix its
     findings, then re-run the tests; review again if the fixes changed behaviour.

  If a skill is missing, apply its discipline by hand, still run tests and self-review, and mark
  the run degraded. If tests cannot run by any route the area's know-how gives, say **tests NOT run** and why.
- **Report back, as evidence:** files changed; each acceptance criterion as **met** (tests ran
  green), **implemented, tests not run**, or **needs a human** (editor pen, robot pen: say exactly
  what to wire or run on hardware); which skills actually ran; the real test outcome; review
  findings and their fixes; anything uncertain; any safety limit added, loosened or removed.

## 6. Relay the result

Summarise: ticket, area, Role (and whether `role=` or `pick-role` chose it), model, comments and
grill-check answers that changed the work, files changed, criteria met vs. needing a human, and
the next step (the human commits and opens the PR). Leave the issue untouched.

Call a criterion met only when the evidence shows the full loop ran. A missing `coder: loaded`
canary, a skipped test run, or `tdd skipped` on a logic change → mark those criteria
**not yet verified** and say so plainly.

**If the report shows no `code-review` run, run `code-review` yourself** on the working-tree diff
before relaying (it is read-only), and include its findings. Never relay an unreviewed change as
done.
