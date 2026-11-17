# C# / Unity standards

Shared rules: [CODING_STANDARDS.md](../../CODING_STANDARDS.md). Base style and deviations:
[ADR-0007](../adr/0007-per-language-style-bases.md). Scope: `Unity/JewelryShop/`.

## Naming

| Thing | Style |
|---|---|
| Types, methods, properties, public members, constants | `PascalCase` |
| Interfaces | `I` + `PascalCase` (e.g. `IConveyor`) |
| Locals, parameters | `camelCase` |
| Private fields, including `[SerializeField]` | `_camelCase` |
| Private statics | `s_camelCase` |

- Write `private` explicitly.
- Acronyms follow .NET: two letters all caps, longer ones PascalCase. `IO`, `Tcp`, `Mg400`;
  `Id` is a word.
- Renaming a serialized field needs `[FormerlySerializedAs]`, or Inspector wiring is lost.

## Comments

- Doc comment: a one-line `/// <summary>` on public members.

## Testing

- NUnit through Unity Test Framework. Test names: `Method_Condition_Expected`.
- Put pure logic in `JewelryShop.Core` and test it in EditMode.
  Use PlayMode only when a scene is needed.
- Layout and run steps: [Docs/unity/TESTING.md](../unity/TESTING.md).

## Enforcement

`Unity/JewelryShop/Assets/Scripts/.editorconfig` holds the naming rules and requires explicit
accessibility modifiers. They are warnings in the IDE only, not enforced at Unity's compile.
Unity is not in CI. Added by #34.
Anything the IDE does not flag is checked by `code-review`.

## Sources

- [C# identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [Capitalization conventions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions)
- [C# coding conventions: comments](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#comment-style)
- [Unity C# style guide, Unity 6 edition](https://unity.com/en/resources/c-sharp-style-guide-unity-6)
- [FormerlySerializedAs](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/serialization/formerlyserializedasattribute)
- [.NET naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)
