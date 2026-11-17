---
status: accepted
---

# Each language follows its own base style guide, enforced by tools where tools can

The monorepo holds three languages, so we follow each language's own community guide instead of one cross-language house style: **C#** follows Microsoft's .NET naming and C# conventions plus Unity's C# style guide e-book, **Python** (3.12) follows PEP 8 + PEP 257 with Google-style docstrings and type hints on every public function, **C++ (UNO)** follows a trimmed Google C++ Style with Arduino's camelCase functions. Shared rules (GLOSSARY terms in identifiers, units in names, why-not-what comments, `TODO(#NN)`, 100-column lines) sit on top, in `CODING_STANDARDS.md`. Sources and the full comparison: [Docs/research/coding-standards.md](../research/coding-standards.md).

## Deliberate deviations

- **C# private fields, `[SerializeField]` ones included, are `_camelCase`, not Unity's `m_`.** The Unity 6 edition of Unity's style e-book uses `m_` / `k_` / `s_`; its 2022 edition and Microsoft use `_camelCase`. Unity's guide lets a team's own guide take precedence. We pick `_camelCase` so C# and C++ members read the same, and IDE defaults match. Constants are `PascalCase` and private statics `s_camelCase` (Microsoft), and `private` is written explicitly.
- **Acronyms differ per language on purpose:** C# follows .NET (`IO`, `Tcp`; the product name MG400 counts as a word, `Mg400`); Python follows PEP 8 (`MG400Client`, `HTTPError`).
- **C++ functions and variables are camelCase** (Arduino API) where Google says PascalCase functions and `snake_case` variables; private members are `_camelCase` (Google: trailing `_`) to match C#; constants stay `kPascalCase`. Pin numbers are `PIN_*` macros in the one pins header, like the Arduino core's own `LED_BUILTIN`, so wiring reads differently from logic constants.
- **Lines are 100 columns in all three languages** (ruff 88, PEP 8 79, Google 80): one number to remember, with room for descriptive names with units.
- **Doc comments are one line on public API only**, lighter than Google Python (`Args:`/`Returns:` sections) and Google C++ (comment almost every function). Sections appear only when a parameter's meaning or unit is not obvious from its name. Arduino's beginner writing guide (comment every variable and loop) does not apply.

## Consequences

- Tools enforce only part of it, once spec 006 lands. Python: `ruff` (lint + format, N/D/ANN rules) runs in CI; TD002/TD003 are off, so the `TODO(#NN)` shape is checked in review, not by ruff. UNO: `clang-format` runs in CI, but it does not check names (clang-tidy is not set up for AVR). Unity: `.editorconfig` naming rules show only inside the IDE, not at Unity's compile, and Unity is not in CI (licensing). So C# and C++ naming rely on the IDE and `code-review`.
- Renaming a serialized C# field needs `[FormerlySerializedAs]`, or Inspector wiring is lost.
