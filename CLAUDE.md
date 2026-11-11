# JewelryShopCPE494_LoveRobot

Monorepo: `Unity/JewelryShop/` (Meta Quest 3, C#), `Dobot/` (PC orchestrator, Python, MG400),
`UNO/` (Arduino UNO firmware). Brief in `Docs/brief/`, decisions in `Docs/adr/`.

## The pipeline

**Entry gate:** begin every non-trivial piece of work at stage 1, before `to-spec`; grilling
emits the ADRs and glossary terms the later stages consume. Skip stages only for a trivial or
mechanical change, and say so when you do.

| # | Stage | Skill | Output |
|---|---|---|---|
| 1 | Sharpen the idea | `grill-with-docs` (or `grill-me` when no doc trail is wanted) | decisions, `GLOSSARY.md` terms, ADRs in `Docs/adr/` |
| 2 | Write the spec | `to-spec` | a `NNN spec: …` issue |
| 3 | Split into tickets | `to-tickets` | `NNN-MM ticket: …` issues with `blocked_by` edges, `ready-for-agent` + one `area:` label |
| 4 | Implement | `grab-ticket` (never `implement` / `implement-spec` here) | local changes from a Role sub-agent |
| 5 | Review | `code-review` | findings fixed with tests green |

Keep stages 1–3 in one context window; `/clear` between tickets at stage 4.

**`code-review` runs after every change, whatever its size** — a ticket, a bug fix, a `tdd`
slice, a skill or doc edit. `grab-ticket`'s loop runs it inside the sub-agent; work done
outside `grab-ticket` runs it before being called done.

## Roles

A **Role** is a subagent in `.claude/agents/` (the only list), named
`<scope>-<function>-<focus>`; its **Level** (Haiku / Sonnet / Opus) is chosen at dispatch.
`pick-role` recommends one, `grab-ticket` dispatches it. Every `Agent` dispatch passes
`effort: medium`.

## The three pens (humans only)

- **GitHub pen:** commit, push, branch, PR, issue and label edits. **Nothing is pushed to
  `main` directly:** every change goes on a branch and merges through a PR whose body is
  written with the `pr` skill. Agents draft the branch name, commit message and PR body; the
  human runs the commands.
- **Editor pen:** Inspector wiring, placing or connecting scene/prefab elements.
- **Robot pen:** running code against the real MG400 or a real Arduino.

Agents edit code, logic, and tests locally, author seams with safe fallbacks, and hand each pen
back as an explicit human task.

## Unity MCP

The official Unity MCP (`unity-mcp`, from `com.unity.ai.assistant`) is configured at user scope.
Use it to read the Console, check compilation, and run tests while the Editor is open
(see `Docs/unity/TESTING.md`). It does not lift the editor pen.

## Agent skills

### Issue tracker

GitHub Issues on `LoveRobot-CPE494/JewelryShopCPE494_LoveRobot`, via `gh`. See `Docs/agents/issue-tracker.md`.

### Triage labels

The five default labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `Docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` at the root and `Docs/adr/`. See `Docs/agents/domain.md`.
