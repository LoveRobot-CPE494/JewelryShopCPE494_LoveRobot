# C++ / UNO standards

Shared rules: [CODING_STANDARDS.md](../../CODING_STANDARDS.md). Base style and deviations:
[ADR-0007](../adr/0007-per-language-style-bases.md). Scope: `UNO/`.

## Naming

| Thing | Style |
|---|---|
| Types | `PascalCase` |
| Functions, variables, parameters | `camelCase` |
| Private members | `_camelCase` |
| Constants | `kPascalCase` |
| Pin numbers | `PIN_*` macros, only in the one pins header |

- Acronyms are cased as words, matching C#: `kUnoBaudRate`, `readUiButton`.

## Comments

- Doc comment: one `//` line above the declaration on public API.

## Testing

- doctest, compiled with host `g++` (no board needed). Test names: `TEST_CASE("<behavior>")`.

## Enforcement

`clang-format` in CI. Added by #36.
Names are not machine-checked (no `clang-tidy` for AVR); `code-review` checks them.

## Sources

- [Google C++ style guide](https://google.github.io/styleguide/cppguide.html)
- [Arduino library style guide](https://docs.arduino.cc/learn/contributions/arduino-library-style-guide)
- [ClangFormat](https://clang.llvm.org/docs/ClangFormat.html)
- [clang-tidy readability-identifier-naming](https://clang.llvm.org/extra/clang-tidy/checks/readability/identifier-naming.html)
