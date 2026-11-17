# Unity know-how (`Unity/JewelryShop/`)

Language page: [C# / Unity standards](../../../Docs/standards/csharp-unity.md).

Adapted from the `unity-coder` skill of `where-it-went`.

- **Tests:** read `Docs/unity/TESTING.md` before your first run. Two suites: EditMode
  (`JewelryShop.Tests`) and PlayMode (`JewelryShop.PlayModeTests`) under
  `Assets/Scripts/JewelryShop/Tests/`. Pure logic (order building, coordinate mapping, pricing,
  protocol messages) lives in `JewelryShop.Core` (`noEngineReferences`), the cheapest place to test.
- **Editor open → `unity-mcp`.** Batchmode fails while the Editor holds the project. Run tests and
  read the Console through `unity-mcp` (`Unity_RunCommand`, `Unity_GetConsoleLogs`) as
  `Docs/unity/TESTING.md` §B shows. If `unity-mcp` is not connected either, report **tests NOT run: Editor open, human must
  run them**.
- **`unity-mcp` does not lift the editor pen.** Use it to read state, compile, and run tests; do
  not create, move, or wire scene/prefab objects with it. (ADR-0014's rough-layout
  exception belongs to the main session only, never to a Role sub-agent.)
- **Compile errors first.** When the build is red, fix with the smallest diff that turns it green
  (an import, a type, a missing reference) before any refactor. Compile errors hide test results.
- **Input:** Quest Controllers drive both tele-op and the Storefront grab (ADR-0003). Read input
  through the XR Interaction Toolkit APIs, never hard-coded device buttons scattered across scripts.
- **Talking to the PC:** all messages to `Dobot/` go through one networking class with a typed
  message per command (order, tele-op pose, suction on/off, stop). A lost connection stops
  tele-op motion and shows it to the user.
- Visual and spatial checks (does the gem look right, is the POS terminal reachable) are human
  tasks: say what to look at.
