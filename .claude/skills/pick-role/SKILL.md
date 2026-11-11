---
name: pick-role
description: Recommend which Role (subagent in `.claude/agents/`) and Level (model tier) should take a task — recommends only, never dispatches. `grab-ticket` calls it when no `role=` is given. Use on "pick a role", "which agent should do this", "เลือก role", or when unsure which Role fits a ticket.
---

# Pick a Role

Recommend **one Role and one Level**. A **Role** is a subagent in `.claude/agents/`; a
**Level** is the model tier it runs at. You recommend; `grab-ticket` or the human dispatches.

## 1. Know the task

Read what you were given: a ticket (`gh issue view <n> --repo LoveRobot-CPE494/JewelryShopCPE494_LoveRobot --comments`), a description, or a diff.
Settle two facts: the **area** (`area:` label, else the folder the work lands in) and whether the
work **edits code** or only **reads**.

## 2. List the candidates

- **Our Roles:** every `.claude/agents/*.md` — read each `name` and `description`. Names are
  `<scope>-<function>-<focus>`: `coder` edits code; focus is the area (`unity`, `dobot`, `uno`);
  scope `general` covers any work in the area, a narrower scope marks a specialist — prefer it
  when the task sits squarely in its niche. Leave out `trial-` Roles unless asked for by name.
- **Built-ins:** `Explore` (read-only search), `Plan` (implementation plan), `general-purpose`
  (fallback when no Role fits).

## 3. Choose

The Role whose function matches (edits → `coder`; reads → `Explore`) and whose focus matches the
area. Choose the Level with `grab-ticket` step 3's rubric — that rubric is the single source.
When no Role fits, recommend the closest built-in and name the missing Role.

## 4. Report

```
Role:  <name>   Level: <haiku|sonnet|opus>
Why:   <one line>
Run:   grab-ticket <issue#> role=<name>
```
