# Coding standards

Shared rules for all code in `Unity/`, `Dobot/` and `UNO/`. Per-language rules:
[C# / Unity](Docs/standards/csharp-unity.md), [Python / Dobot](Docs/standards/python-dobot.md),
[C++ / UNO](Docs/standards/cpp-uno.md). Why each base style was chosen:
[ADR-0007](Docs/adr/0007-per-language-style-bases.md). `code-review` checks changes against these.

## Naming

- Use [GLOSSARY.md](GLOSSARY.md) terms for domain concepts. Never use a term's *Avoid* words.
- Put the unit in the name when a value has one: `speedMmPerS`, `timeoutMs`, `angleDeg`.
- Booleans start with `is`, `has` or `can`.
- Abbreviate only these words: id, tcp, mg400, uno, xr, ui. Spell out everything else.
  Each language page gives their casing.
- Lines are at most 100 columns.

## Comments

- Explain why, not what. If a comment restates the code, rename or delete instead.
- Public API gets a one-sentence doc comment; private code gets none by default.
  Add parameter or return detail only when a meaning or unit is not obvious from its name.
- Cite the decision with `ADR-NNNN` where code follows or depends on an ADR.
- Never commit commented-out code.
- `TODO(#NN): <text>` always names an open issue.
- `shortcut: <what was skipped and why>` marks a deliberate shortcut with no issue yet.
  Once it gets an issue, rewrite it as `TODO(#NN):`.

## Patterns

Only the patterns in [ADR-0008](Docs/adr/0008-approved-pattern-menu.md), each at its own place,
from the first use. Any other pattern needs a new ADR.

| Pattern | Place |
|---|---|
| State | Packing Sequence (Dobot); UNO conveyor and Bag Closer sequence |
| Command | One typed message per Quest to Hub command |
| Adapter | Around the Dobot SDK and around the serial port |
| Proxy | Safety limiter on every motion: workspace bounds, minimum Z, speed caps, Deadman |
| Facade | Unity's single networking class |
| Observer | C# events |

- Banned: Singleton and Service Locator.
  Dependencies arrive as `[SerializeField]` references or constructor parameters from one
  composition root.
- Rule of Three: any other new abstraction waits for its third use.

## Refactoring

- A refactor changes no behaviour. Tests are green before and after.
- Label the issue and PR `refactor`.
- No refactors inside a feature PR, except tidying the files that PR already touches.
- Debt you do not fix in the change becomes a `refactor` issue.

## Testing

Frameworks, naming and run commands are on each language page.

- A good test checks external behaviour through the public interface, not internals.
- Must test: every safety rule; every state transition; every protocol message, encoded and
  decoded.
- Every bug fix starts with a failing test that reproduces the bug.
- No automated tests for visuals or scene wiring; check those by hand.
