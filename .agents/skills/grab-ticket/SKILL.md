---
name: grab-ticket
description: Implement or resume one ready-for-agent GitHub issue in LoveRobot. Use when asked to grab a ticket, รับงาน ticket, or implement a specific LoveRobot issue, including while Claude works on another ticket.
---

# Grab ticket

Use `LoveRobot-CPE494/JewelryShopCPE494_LoveRobot` as `<repo>`. Read the checkout's `CLAUDE.md` for the project pipeline and human-owned GitHub, Unity Editor, and robot operations. This workflow reads GitHub and makes local changes. Prepare Git commands and PR text for the user where applicable.

## Select and read the issue

- `$grab-ticket 43` selects GitHub issue #43. When working alongside another agent or taking over its work, use an explicit issue number. For a solo run without a number, list open `ready-for-agent` issues with `gh issue list --repo <repo> --state open --label ready-for-agent --json number,title,labels` and pick the lowest-numbered eligible ticket after checking blockers. Refresh if the result is paginated and the first page cannot establish the lowest eligible number.
- Read the body, labels, status and comments with `gh issue view <n> --repo <repo> --json number,title,body,state,labels,comments`. Check native dependencies with `gh api repos/<repo>/issues/<n>/dependencies/blocked_by --paginate --jq '.[] | select(.state == "open") | .number'`. An API error leaves blocker status unknown. Report known open blockers and pause implementation when ticket readiness cannot be established. For automatic selection, skip blocked tickets.
- Work on open implementation tickets marked `ready-for-agent`. Read the applicable comments in chronological order and use the latest clarification from the ticket owner or maintainer. Surface conflicting or materially changed scope and stale tickets before editing.
- Resolve the area from `area:unity`, `area:dobot`, or `area:uno`, or infer it from the change when the label is absent. Ask the user to split a ticket if implementation spans multiple areas. Read `.claude/skills/coder/SKILL.md` and its area reference as project guidance. If a `role=<name>` is supplied, validate it against `.claude/agents/` and use its applicable project guidance.

## Select the checkout and model

For parallel work, use the worktree assigned to this ticket. Check `git worktree list` and `git status --short --branch` before edits. If isolation is needed, use a Codex worktree when available or obtain authorization for any Git operation that needs it. When resuming, inspect the supplied worktree, current diff, commits and test evidence, and confirm ownership has passed if the other agent may still be editing. Preserve existing changes and keep the ticket diff isolated.

Recommend a model and reasoning level at the read-back. These are workflow defaults.

| Ticket | Model | Reasoning |
| --- | --- | --- |
| Mechanical change with an established pattern and clear checks | GPT-6.2 Sol | Low |
| Typical bug or feature requiring implementation judgment | GPT-6.2 Sol | Medium |
| Subtle state or protocol behavior, or a consequential design decision requiring sustained reasoning | GPT-6.2 Astra | High |

Use the active session by default. Skill invocation cannot switch its model. Report the actual model when known and suggest the app model picker or CLI `/model` when the recommended model differs and matters. With explicit delegation and a supported tool, the worker may use the recommended available model. Give it the clarified ticket, worktree, area, constraints and verification requirements. Model availability depends on the client and plan.

## Read back, implement and review

1. State the ticket, worktree, area, expected changes, acceptance criteria, boundaries, model recommendation and any human steps. Resolve material ambiguity using `grilling` when available. For a settled ticket, get the same brief go-ahead as the Claude workflow unless the user already told you to proceed without that checkpoint.
2. Apply `ponytail` for the smallest complete change. For a bug, use `diagnosing-bugs` and reproduce the failure before fixing it when feasible. For changed behavior, use `tdd` with a failing check, fix, then refactor. Record the reason when a mechanical change needs no new behavior test. When a skill is unavailable, follow the corresponding steps directly and record the fallback.
3. Run the area's tests and record the observed results. For Unity, read `Docs/unity/TESTING.md` for the available test route. Report editor or robot verification reserved for the user.
4. Review the ticket diff against its acceptance criteria and the area's standards on every change. Use the Codex `code-review` skill when available and compatible with the session's delegation permissions. Otherwise apply both review checks in the current session. Fix findings and repeat affected checks.
5. Report the issue, area, worktree, files changed, relevant clarifications, skill fallbacks, and evidence for each criterion, including anything still unverified. Give exact test commands and results. For a handoff, include the checkout path and any outstanding validation. Follow the GitHub pen in `CLAUDE.md` for the user's commit and PR handoff. If Git metadata is read-only, provide the message or PR text in chat.
