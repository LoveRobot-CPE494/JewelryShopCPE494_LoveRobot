# Unity testing guide

How `Unity/JewelryShop/`'s automated tests are organised and run. Humans and agents run them the
same way. Paths are relative to `Unity/JewelryShop/`. Editor version: `6000.6.5f1`
(`ProjectSettings/ProjectVersion.txt`).

## The suites

| | EditMode | PlayMode |
|---|---|---|
| Assembly | `JewelryShop.Tests` | `JewelryShop.PlayModeTests` |
| Folder | `Assets/Scripts/JewelryShop/Tests/EditMode/` | `Assets/Scripts/JewelryShop/Tests/PlayMode/` |
| Runs in | Editor only | Play Mode |
| References | `JewelryShop.Core`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll` | `JewelryShop.Core`, `UnityEngine.TestRunner`, `nunit.framework.dll` |
| Use for | pure rules, and components built in memory | behaviour that needs frames, physics, or a scene |

Both have the `UNITY_INCLUDE_TESTS` define constraint, so they never ship in a build.

**Put logic in `JewelryShop.Core`** (`Assets/Scripts/JewelryShop/Core/`, `noEngineReferences: true`):
order building, pricing, Quest → MG400 coordinate mapping, protocol message encoding. It is plain
C#, so its tests need no scene and run fastest. A `MonoBehaviour` forwards to it. Prefer EditMode;
reach for PlayMode only when the behaviour truly needs the player loop.

## Writing a test

One `[TestFixture]` class per rule, namespace `JewelryShop.Tests`, file `<Subject>Tests.cs` in the
suite's folder (the asmdef picks it up, no edit needed). One `[Test]` per behaviour, named
`Method_Condition_Result`, using `Assert.That(actual, Is...)`. A PlayMode test is an
`[UnityTest]` returning `IEnumerator`. Add a new assembly to a test asmdef's `references` only
when the code under test lives outside `JewelryShop.Core`.

## Running the tests

### A. Editor open: Test Runner window (human)

`Window > General > Test Runner` → **EditMode** or **PlayMode** tab → **Run All**.

### B. Editor open: through `unity-mcp` (agents)

Batchmode cannot run while the Editor holds the project, so agents run tests in the open Editor
with `Unity_RunCommand`. Run the snippet below as is for EditMode; for PlayMode change the three constants at the top of
`Execute` to `TestMode.PlayMode`, `"JewelryShop.PlayModeTests"`, `"jewelry-playmode-mcp.txt"`.
Then poll the result file until it holds a `DONE` line. Check `Unity_GetConsoleLogs` for `error CS` first: compile errors stop the run.

```csharp
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    class Cb : ICallbacks
    {
        public string Path;
        public void RunStarted(ITestAdaptor t) {}
        public void TestStarted(ITestAdaptor t) {}
        public void TestFinished(ITestResultAdaptor r)
        {
            if (!r.HasChildren && r.TestStatus != TestStatus.Passed)
                File.AppendAllText(Path, "FAIL " + r.FullName + ": " + r.Message + "\n");
        }
        public void RunFinished(ITestResultAdaptor r) =>
            File.AppendAllText(Path, "DONE " + r.TestStatus + " passed=" + r.PassCount +
                " failed=" + r.FailCount + " skipped=" + r.SkipCount + "\n");
    }

    public void Execute(ExecutionResult result)
    {
        const TestMode mode = TestMode.EditMode;
        const string assembly = "JewelryShop.Tests";
        const string resultFile = "jewelry-editmode-mcp.txt";

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), resultFile);
        File.WriteAllText(path, "START\n");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Cb { Path = path });
        api.Execute(new ExecutionSettings(new Filter {
            testMode = mode, assemblyNames = new[] { assembly } }));
        result.Log("Started; results -> {0}", path);
    }
}
```

Run `AssetDatabase.Refresh()` the same way first when you added or changed scripts.

### C. Editor closed: command line

Unity runs headless, writes an NUnit XML file, and exits. Write results **outside the repo**. Never
pass `-quit` with `-runTests`.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
$p = Start-Process $unity -Wait -PassThru -ArgumentList @(
  '-batchmode', '-nographics',
  '-projectPath', '"<repo>\Unity\JewelryShop"',
  '-runTests', '-testPlatform', 'EditMode',
  '-assemblyNames', 'JewelryShop.Tests',
  '-testResults', "`"$env:TEMP\jewelry-editmode-results.xml`"",
  '-logFile', "`"$env:TEMP\jewelry-editmode.log`"")
"exit code: $($p.ExitCode)"
([xml](Get-Content "$env:TEMP\jewelry-editmode-results.xml")).'test-run' |
  Select-Object result, total, passed, failed, skipped
```

PlayMode: `-testPlatform PlayMode`, `-assemblyNames JewelryShop.PlayModeTests`, another results
file. Exit code `0` = all passed, `2` = some failed, anything else = the run did not complete (read
the log: `error CS`, licence, project locked).

After any run, check `git status`: Unity sometimes rewrites tracked project files; do not commit
those incidental changes. A PlayMode run started from `unity-mcp` can leave an
`Assets/InitTestScene<guid>.unity` (+ `.meta`) behind; delete it.

## When tests cannot run

If neither B nor C works (Editor open and `unity-mcp` not connected), report **tests NOT run** and
why; the human runs them with A.
