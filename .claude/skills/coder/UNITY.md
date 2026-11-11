# Unity know-how (`Unity/JewelryShop/`)

Adapted from the `unity-coder` skill of `where-it-went`.

- **Editor lock.** If the project is open in the Unity Editor, a batchmode run fails or stalls.
  Check the log for the lock error; on a lock, stop retrying and report **tests NOT run: Editor
  open, human must run them**.
- **Tests:** Unity Test Framework, EditMode first: pure logic (order building, coordinate mapping,
  pricing) lives in plain C# classes with no `MonoBehaviour`, which makes it the cheapest place to
  test. Run batchmode with results written outside the repo.
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
