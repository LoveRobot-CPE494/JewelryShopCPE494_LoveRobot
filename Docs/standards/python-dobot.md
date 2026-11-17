# Python / Dobot standards

Shared rules: [CODING_STANDARDS.md](../../CODING_STANDARDS.md). Base style and deviations:
[ADR-0007](../adr/0007-per-language-style-bases.md). Scope: `Dobot/`, Python 3.12.

## Naming

PEP 8: `snake_case` functions and variables, `CapWords` classes, `UPPER_SNAKE` constants,
one leading underscore for non-public members.

- Acronyms follow PEP 8: all caps. `MG400Client`, `HTTPError`.

## Comments

- Doc comment: a one-line Google-style docstring on public API.
  Parameter or return detail, when the shared rule calls for it, goes in `Args:` / `Returns:`.
- Type hints on every public function.

## Testing

- pytest. Test names: `test_<behavior>`.
- Fakes for the MG400 and the serial port live in one tests folder.

## Enforcement

`ruff` lint and format with the `N`, `D` and `ANN` rule groups, run in CI. Added by #35.
`TD002` and `TD003` are off, so the `TODO(#NN)` shape is checked in review.

## Sources

- [PEP 8](https://peps.python.org/pep-0008/)
- [PEP 257](https://peps.python.org/pep-0257/)
- [Google Python style guide](https://google.github.io/styleguide/pyguide.html)
- [ruff configuration](https://docs.astral.sh/ruff/configuration/)
- [ruff formatter](https://docs.astral.sh/ruff/formatter/)
