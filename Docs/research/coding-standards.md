# Coding standards: source check for ADR-0007 and ADR-0008

Researched 2026-11-17 against primary sources. Every claim links to the page it came from.
Quotes are verbatim. Anything marked *inference* is ours, not the source's.
The open choices below ("ADR must pick") are settled in [ADR-0007](../adr/0007-per-language-style-bases.md) and [ADR-0008](../adr/0008-approved-pattern-menu.md).

## Verdicts

| # | Proposed choice | Verdict |
|---|---|---|
| 1 | C#: PascalCase types, methods, properties | **Confirmed** (Microsoft, Unity) |
| 2 | C#: camelCase locals and parameters | **Confirmed** (Microsoft, Unity) |
| 3 | C#: `_camelCase` private fields, incl. `[SerializeField] private` | **Confirmed by Microsoft; differs from Unity 6 e-book**, which uses `m_` as Unity's house style (see 1.2) |
| 4 | C#: no `m_` prefix | **Differs:** Unity 6 e-book recommends `m_` / `k_` / `s_`; Microsoft never uses `m_` |
| 5 | C#: acronyms, 2 letters all caps (`IO`), longer PascalCase (`Tcp`) | **Confirmed** (Framework Design Guidelines). Note `Id`/`Ok` are words, not acronyms |
| 6 | C#: static and constant fields (not in proposal) | **Gap:** Microsoft docs team uses `s_` / `t_` statics and PascalCase constants; Unity uses `k_` constants. ADR must pick |
| 7 | `.editorconfig` naming rules for Unity | **Confirmed as a mechanism, but IDE-only in Unity** (see 1.4) |
| 8 | Python: PEP 8 | **Confirmed.** PEP 8 acronym rule is the opposite of .NET's (`HTTPServerError`) |
| 9 | Python: Google-style docstrings | **Confirmed**, ruff supports `convention = "google"` |
| 10 | Python: type hints on public functions | **Confirmed** (Google: "At least annotate your public APIs") |
| 11 | Python: ruff lint + format via `pyproject.toml`, 3.12 | **Confirmed.** Line length differs: ruff 88, PEP 8 79, Google 80 |
| 12 | C++/UNO: Google C++ PascalCase types, `kPascalCase` constants | **Confirmed** |
| 13 | C++/UNO: Arduino camelCase functions | **Confirmed**, and it **overrides** Google C++ (PascalCase functions) |
| 14 | C++/UNO: variables (not in proposal) | **Gap:** Google = `snake_case` (+ trailing `_` members); Arduino examples = camelCase |
| 15 | clang-format | **Confirmed for layout only.** It does not check names; that is clang-tidy |
| 16 | Patterns: State, Command, Adapter, Proxy, Facade, Observer | **Confirmed** on refactoring.guru |
| 17 | Singleton banned | **Supported** by refactoring.guru cons; **Unity's patterns e-book teaches Singleton** as acceptable for small games |
| 18 | Service Locator banned | **Differs:** not on refactoring.guru at all; Fowler (the primary source) does not condemn it |
| 19 | Rule of Three | **Confirmed** (refactoring.guru) |
| 20 | Comments: why-not-what | **Confirmed** (Unity, Google, refactoring.guru); **Arduino writing guide says the opposite** for examples |
| 21 | One-sentence doc comments on public API | **Partly differs:** Google Python wants `Args:`/`Returns:`/`Raises:` unless a one-liner is enough; Google C++ wants function comments on almost every declaration, private too |
| 22 | `TODO(#NN)` referencing an issue | **Confirmed** (Google Py/C++ format `TODO(bug)`); Unity suggests name + date instead |
| 23 | Renaming serialized fields needs `[FormerlySerializedAs]` | **Confirmed** |

## 1. C# (Unity/JewelryShop)

### 1.1 Microsoft .NET naming

- C# docs: "C# programs use `PascalCase` for type names, namespaces, and all public members";
  "Use camelCase for method arguments, local variables, and private and internal non-constant
  fields"; "Private and internal non-constant instance fields start with an underscore (`_`)."
  ([Identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names))
- Same page: "When working with `static` fields that are `private` or `internal`, use the `s_`
  prefix and for thread static use `t_`." And: "use PascalCase for constant names, both fields and
  local constants, including `private` and `internal` constants."
- Same page frames these as the docs team's conventions: "the compiler doesn't enforce them.
  You're free to use different conventions in your projects."
- Framework Design Guidelines on fields: "Internal and private fields are not covered by
  guidelines" and "DO NOT use a prefix for field names" applies to public/protected static
  fields only. ([Names of type members](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-type-members#names-of-fields))
  So `_camelCase` comes from the C# docs/runtime style, not from the FDG.
- Acronyms: PascalCase "capitalizes the first character of each word (including acronyms over two
  letters in length)" e.g. `HtmlTag`; "A special case is made for two-letter acronyms in which both
  letters are capitalized", e.g. `IOStream`; in camelCase "two-letter acronyms that begin a
  camel-cased identifier are both lowercase" (`ioStream`). `Id` and `Ok` are listed as words, not
  `ID` / `OK`. ([Capitalization conventions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions))
  That page carries a note that it reprints the 2008 2nd edition and "may be out-of-date".
- Comments (docs team): use `//`, "For describing methods, classes, fields, and all public members
  use XML comments", "Place the comment on a separate line", "Begin comment text with an uppercase
  letter", "End comment text with a period."
  ([Coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#comment-style))

### 1.2 Unity C# style guide (Unity 6 edition) -- DISAGREES on prefixes

Source: [Use a C# style guide for clean and scalable game code (Unity 6 edition)](https://unity.com/en/resources/c-sharp-style-guide-unity-6),
dated 2025-01-21, PDF linked from that page (pages cited below).

- Basis: "These are mostly a subset of the Microsoft Framework Design Guidelines" (p. 9) and
  "When your style guide conflicts with this document or the Microsoft Framework Design
  Guidelines, it should take precedence over them" (p. 10). So a team choice is explicitly allowed.
- Casing: "Use pascal case (MyPropertyName) for public fields. Use camel case
  (myPrivateVariable) for private variables" (p. 14). "Local variables and parameters use camel
  case with no prefix." -- **agrees.**
- **Prefixes (p. 14):** "Some guides suggest adding a prefix to private member variables with an
  underscore (_) ... In our style guides we use prefixes for private member variables (m_),
  constants (k_), or static variables (s_) ... For example, movementSpeed becomes
  m_movementSpeed." It also allows using `this` and skipping the prefix.
- **Constants (p. 15):** "Name constant variables with k_ as prefix and in PascalCase", example
  `public const int k_MaxItems = 100;` -- conflicts with Microsoft's PascalCase constants.
- **Serialized fields (p. 26):** example is `[SerializeField] private PlayerStats m_stats;`.
- Access modifiers (p. 15): Unity recommends leaving out the implicit `private`; Microsoft
  "recommended to explicitly specify private". The ADR should pick one.
- **Edition drift:** the 2022 first edition ([resource page](https://unity.com/en/resources/create-code-c-sharp-style-guide-e-book))
  said "Private member variables are camel case and use underscores (_) as a prefix", and the
  2022 [Unity blog post](https://unity.com/en/blog/engine-platform/clean-up-your-code-how-to-create-your-own-c-code-style)
  says the same. The Unity 6 edition switched its house style to `m_`. The current
  [how-to page](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity) matches the
  Unity 6 edition ("we use prefixes for private member variables (m_)"). Cite the edition by name
  in the ADR.
- Inspector labels are unaffected by the choice: `ObjectNames.NicifyVariableName` "will insert
  spaces before capital letters and remove optional `m_`, `_` or `k` followed by uppercase letter in
  front of the name." ([ScriptReference](https://docs.unity.com/en-us/engine/current/script-reference/unityeditor/objectnames/nicifyvariablename))
  So `_moveSpeed` shows as "Move Speed".

### 1.3 Serialized-field renames

- `FormerlySerializedAs`: "Use this attribute to rename a field without losing its serialized
  value." Namespace `UnityEngine.Serialization`; "This is an Editor-only attribute and can't be
  used at runtime." Example: `[FormerlySerializedAs("hitpoints")] public int health;`
  ([ScriptReference](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/serialization/formerlyserializedasattribute))
- The page does not say *why* data is lost. *Inference:* Unity matches serialized data to fields
  by name, so a rename orphans the value. Adopting `_camelCase` on existing `m_`/plain fields is
  itself a rename and needs the attribute.

### 1.4 `.editorconfig` enforcement

- Naming rules = symbol group + naming style (`capitalization`, `required_prefix`, ...) +
  severity. Microsoft's own example for private fields uses `required_prefix = _` with
  `capitalization = pascal_case`; for `_camelCase` set `camel_case`.
  ([Naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules))
- Same page: "Severity specification within a naming rule is only respected inside development
  IDEs ... not respected during build." Build enforcement needs `dotnet_diagnostic.IDE1006.severity
  = warning` (or `error`) **and** `EnforceCodeStyleInBuild = true`
  ([Code analysis overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview#enable-on-build)).
- Unity 6 e-book (p. 31) endorses EditorConfig: "Many IDEs, like Visual Studio and Rider, come
  bundled with native support".
- Unity supports Roslyn analyzers only via a manual process ("Unity doesn't support the
  installation of Roslyn Analyzers or source generators through NuGet directly") and in Visual
  Studio / Rider. ([Unity Manual: Roslyn analyzers](https://docs.unity3d.com/Manual/roslyn-analyzers.html))
- *Inference:* Unity does not build through an SDK-style MSBuild project, so
  `EnforceCodeStyleInBuild` does not apply; for Unity the naming rules are an IDE-time check.
  That fits "CI for Dobot+UNO only", but the ADR should say Unity naming is IDE-enforced, not
  build-enforced.

## 2. Python (Dobot)

- PEP 8 naming: functions "lowercase, with words separated by underscores"; "Class names should
  normally use the CapWords convention"; constants "all capital letters with underscores";
  "Use one leading underscore only for non-public methods and instance variables."
  ([PEP 8](https://peps.python.org/pep-0008/#naming-conventions))
- **PEP 8 acronyms are the opposite of .NET:** "When using acronyms in CapWords, capitalize all
  the letters of the acronym. Thus HTTPServerError is better than HttpServerError."
  ([PEP 8](https://peps.python.org/pep-0008/#descriptive-naming-styles)) So the ".NET acronym
  rule" must be scoped to C# only (`TcpClient` in C#, `TCPClient` in Python).
- PEP 8 line length: "Limit all lines to a maximum of 79 characters."
  ([PEP 8](https://peps.python.org/pep-0008/#maximum-line-length))
- PEP 8 comments: "Comments that contradict the code are worse than no comments"; inline comments
  "are unnecessary and in fact distracting if they state the obvious."
  ([PEP 8](https://peps.python.org/pep-0008/#comments))
- PEP 257: "all functions and classes exported by a module should also have docstrings";
  "Public methods (including the `__init__` constructor) should also have docstrings";
  one-liners "are for really obvious cases"; "The docstring is a phrase ending in a period."
  ([PEP 257](https://peps.python.org/pep-0257/#one-line-docstrings))
- Google Python docstrings: summary line "not exceeding 80 characters", then sections `Args:`,
  `Returns:` / `Yields:`, `Raises:`. "These sections can be omitted in cases where the function's
  name and signature are informative enough that it can be aptly described using a one-line
  docstring." A docstring "is mandatory for every function" that is public API, nontrivial in size,
  or has non-obvious logic. ([pyguide 3.8.3](https://google.github.io/styleguide/pyguide.html#s3.8.3-functions-and-methods))
  -- **Differs** from "one-sentence doc comments": Google expects the sections whenever the
  signature is not self-explanatory.
- Type hints: "At least annotate your public APIs"; "You are not required to annotate all the
  functions in a module." ([pyguide 3.19](https://google.github.io/styleguide/pyguide.html#s3.19-type-annotations))
- Google line length: "Maximum line length is 80 characters."
  ([pyguide 3.2](https://google.github.io/styleguide/pyguide.html#s3.2-line-length))
- Google tools: "Make sure you run `pylint`"; "Many teams use the Black or Pyink auto-formatter".
  ruff replaces both here.

### Ruff

- Config lives in `pyproject.toml` under `[tool.ruff]`; ruff infers a missing `target-version`
  "from the requires-python field in a pyproject.toml file in the same directory".
  ([Configuration](https://docs.astral.sh/ruff/configuration/)) `py312` is a valid value.
- `line-length` default `88` ([Settings](https://docs.astral.sh/ruff/settings/#line-length)) --
  neither PEP 8 (79) nor Google (80). The ADR should state the number.
- `lint.pydocstyle.convention`: "Whether to use Google-style, NumPy-style conventions, or the PEP
  257 defaults", values `"google" | "numpy" | "pep257"`, default `null`.
  ([Settings](https://docs.astral.sh/ruff/settings/#lint_pydocstyle_convention))
- The proposed rules need explicit selection: `N` (pep8-naming), `D` (pydocstyle), `ANN`
  (flake8-annotations), `TD` (flake8-todos). ([Rules](https://docs.astral.sh/ruff/rules/))
- `TD003` "Checks that a TODO comment is associated with a link to a relevant issue or ticket";
  its accepted examples include `# TODO(charlie): #003 ...`.
  ([TD003](https://docs.astral.sh/ruff/rules/missing-todo-link/)) `TD002` requires an author in
  the parentheses. *Unverified:* whether `# TODO(#12): text` satisfies both; test before relying
  on it.
- Formatter: "designed as a drop-in replacement for Black"; `ruff format --check` will "exit with a
  non-zero status code upon detecting any unformatted files" (use in CI). Rules that conflict with
  the formatter include `COM812` and `D206`; `E501` can still fire.
  ([Formatter](https://docs.astral.sh/ruff/formatter/))

## 3. C++ / Arduino UNO

- Google C++ types: "Type names start with a capital letter and have a capital letter for each new
  word, with no underscores." ([cppguide](https://google.github.io/styleguide/cppguide.html#Type_Names))
- Constants: "Variables declared constexpr or const, and whose value is fixed for the duration of
  the program, are named with a leading 'k' followed by mixed case", e.g. `kDaysInAWeek`; enumerators
  use `kEnumName` "not ENUM_NAME". ([cppguide](https://google.github.io/styleguide/cppguide.html#Constant_Names))
- **Functions -- Google differs:** "Ordinarily, functions follow PascalCase"; accessors may be
  `snake_case`. ([cppguide](https://google.github.io/styleguide/cppguide.html#Function_Names))
- **Variables -- not covered by the proposal:** "variables (including function parameters) and
  data members are snake_case ... Data members of classes (but not structs) additionally have
  trailing underscores." ([cppguide](https://google.github.io/styleguide/cppguide.html#Variable_Names))
- Google also: "We do not use C++ exceptions"; "at most 80 characters"; "indent 2 spaces at a time".
- Arduino library guide: "Use camel case function names, not underscore. For example,
  **analogRead**, not **analog_read**"; "Use `begin()` to initialize a library instance ... Use
  `end()` to stop it"; "LONG_CONSTANT_NAMES_FULL_OF_CAPS are hard to read"; "Don't assume
  knowledge of pointers." ([Arduino library style guide](https://docs.arduino.cc/learn/contributions/arduino-library-style-guide))
  The guide itself says "Some of these run counter to professional programming practice."
- Arduino writing guide (examples): descriptive camelCase names like `buttonState`, `switchPin`;
  "Avoid `#defines`"; "use const ints" for pins. **Comments -- differs:** "Comment every variable or
  constant declaration", "Comment every code block", "Comment every for loop".
  ([Arduino writing style guide](https://docs.arduino.cc/learn/contributions/arduino-writing-style-guide))
  This targets beginner teaching sketches, not production firmware.
- clang-format: "A tool to format C/C++/Java/JavaScript/JSON/Objective-C/Protobuf/C# code";
  `--style=file` loads ".clang-format file in one of the parent directories"; `--dry-run` and
  `--Werror` turn it into a CI check. ([ClangFormat](https://clang.llvm.org/docs/ClangFormat.html))
  `BasedOnStyle` accepts `Google`. ([Style options](https://clang.llvm.org/docs/ClangFormatStyleOptions.html))
- **clang-format does not check names.** Naming is clang-tidy's `readability-identifier-naming`
  ("Checks for identifiers naming style mismatch"; casings `CamelCase`, `camelBack`, ...; options
  such as `ConstantPrefix`). ([clang-tidy](https://clang.llvm.org/extra/clang-tidy/checks/readability/identifier-naming.html))
  Without it, UNO naming is review-enforced only.

## 4. Comment rules (cross-language)

- Why-not-what: Unity 6 e-book p. 48, "Rather than answering 'what,' useful comments fill in the
  gaps and tell you 'why.'"; Google Python "never describe the code"
  ([pyguide 3.8.5](https://google.github.io/styleguide/pyguide.html#s3.8.5-block-and-inline-comments));
  refactoring.guru lists Comments as a smell, "The best comment is a good name for a method or
  class", but ignore it "When explaining why something is being implemented in a particular way."
  ([Comments smell](https://refactoring.guru/smells/comments))
- Doc comments on public API: Microsoft "use XML comments" for "all public members"; Unity "You can
  also use a summary XML tag in front of public methods" (p. 50) and prefers `[Tooltip]` over a
  comment on serialized fields (p. 49). Google C++: "Almost every function declaration should have
  comments immediately preceding it ... Private methods ... are not exempt"
  ([cppguide](https://google.github.io/styleguide/cppguide.html#Function_Comments)) -- stricter
  than the proposal.
- TODOs: Google Python "A TODO comment begins with the word TODO in all caps, and a parenthesized
  link to a resource that contains the context, ideally a bug reference"
  ([pyguide 3.12](https://google.github.io/styleguide/pyguide.html#s3.12-todo-comments)); Google
  C++ "followed by the bug ID, name, e-mail address", e.g. `TODO(bug 12345)`
  ([cppguide](https://google.github.io/styleguide/cppguide.html#TODO_Comments)). Unity instead
  suggests "a name and date to a TODO" and "Delete the TODO comment until you need to implement
  it" (p. 50). `TODO(#NN)` matches Google's shape.

## 5. Design patterns (ADR-0008)

Intents from refactoring.guru:

- State: "lets an object alter its behavior when its internal state changes." ([State](https://refactoring.guru/design-patterns/state))
- Command: "turns a request into a stand-alone object" with all the request's info. ([Command](https://refactoring.guru/design-patterns/command))
- Adapter: "allows objects with incompatible interfaces to collaborate." ([Adapter](https://refactoring.guru/design-patterns/adapter))
- Proxy: "lets you provide a substitute or placeholder for another object." ([Proxy](https://refactoring.guru/design-patterns/proxy))
- Facade: "provides a simplified interface to a library, a framework" or other complex classes;
  con: "A facade can become a god object coupled to all classes of an app." ([Facade](https://refactoring.guru/design-patterns/facade))
- Observer: "lets you define a subscription mechanism to notify multiple objects" about events. ([Observer](https://refactoring.guru/design-patterns/observer))
  Unity 6 style e-book: "Events in C# implement the observer pattern" (p. 19).

Singleton ([refactoring.guru](https://refactoring.guru/design-patterns/singleton#pros-cons)) cons:
"Violates the Single Responsibility Principle"; "can mask bad design"; "requires special
treatment in a multithreaded environment"; "may be difficult to unit test the client code".

- **Unity differs:** [Level up your code with design patterns and SOLID](https://unity.com/en/resources/design-patterns-solid-ebook)
  (2024-07-23) teaches Singleton as one of its 11 patterns. Its cons match ("hide many
  dependencies", "make testing difficult", "encourage tight coupling") and it says enterprise
  projects "might want to steer clear of singletons", but adds that for "a small game that doesn't
  need extensibility" singletons are "quick to learn", "user-friendly" and "performant" (PDF
  pp. 66-72). A ban is a stricter team choice than Unity's guidance; say so in the ADR.

Service Locator:

- **Not on refactoring.guru** -- it is absent from the [catalog](https://refactoring.guru/design-patterns/catalog),
  so the ADR cannot cite refactoring.guru for the ban.
- **Fowler does not ban it:** "The choice between Service Locator and Dependency Injection is less
  important than the principle" of separating configuration from use; for application classes he
  gives Service Locator a "slight edge", and says both "are very amenable to stubbing".
  ([Fowler, Inversion of Control Containers and the Dependency Injection pattern](https://martinfowler.com/articles/injection.html))
  The ban must rest on the team's own reasons (hidden dependencies, same testability problem as
  Singleton in Unity), stated as such.

Rule of Three ([refactoring.guru, When to refactor](https://refactoring.guru/refactoring/when)):
"When you're doing something for the first time, just get it done." / second time, "cringe ... but
do the same thing anyway." / "When you're doing something for the third time, start refactoring."

Smells catalogue groups: Bloaters, Object-Orientation Abusers, Change Preventers, Dispensables
(includes Comments, Duplicate Code, Speculative Generality), Couplers.
([Smells](https://refactoring.guru/refactoring/smells))
