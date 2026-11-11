# Unity know-how (`Unity/JewelryShop/`)

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
  not create, move, or wire scene/prefab objects with it.
- **Compile errors first.** When the build is red, fix with the smallest diff that turns it green
  (an import, a type, a missing reference) before any refactor. Compile errors hide test results.
- **Input:** Quest controllers drive tele-op (ADR-0003); keep hand tracking working for the
  storefront grab (brief §4a.i is still open). Read input through the Meta XR / XR Interaction
  APIs, never hard-coded device buttons scattered across scripts.
- **Talking to the PC:** all messages to `Dobot/` go through one networking class with a typed
  message per command (order, tele-op pose, gripper/suction, stop). A lost connection stops
  tele-op motion and shows it to the user.
- Visual and spatial checks (does the gem look right, is the POS terminal reachable) are human
  tasks: say what to look at.
