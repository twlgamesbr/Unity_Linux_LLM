---
uid: debug-ui-test-fixtures
---

# Debug UI test fixtures

The UI test fixtures manage, set up, and tear down the panel tracked by the fixtures.

[`UITestFixture`](xref:UnityEngine.UIElements.TestFramework.UITestFixture) applied to an Editor test creates a panel that isn't attached to an `EditorWindow`. Therefore, the UI isn't rendered on the screen during test execution and is therefore not visible. Tests that inherit from [`EditorWindowUITestFixture`](xref:UnityEditor.UIElements.TestFramework.EditorWindowUITestFixture`1) or [`RuntimeUITestFixture`](xref:UnityEngine.UIElements.TestFramework.RuntimeUITestFixture) may have their UI rendered and visible on the screen.

You might need to visualize your tests' UI during test development or execution to inspect its state. This is especially useful when a test fails, as it allows you to view the UI at the point of failure.

> [!NOTE]
> Only enable `debugMode` temporarily while debugging, then remove it from your code. Debug mode suspends cleanup of certain states and can cause subsequent tests to fail if a test has already failed.

When you set `debugMode` to `true`, the UI of your test stays active when the test fails.

## State management during debugging 

The UI test fixtures manage the state of your UI and the [UITestComponent](xref:UnityEngine.UIElements.TestFramework.UITestComponent) states.

During debugging, the test fixtures don't clean up these states. Therefore, you must manually clean up the state after you finish debugging:

- **Editor tests**: If the test fails, the Editor window remains open for debugging. To clean up, close the spawned Editor window if it's still present.
- **Play mode tests**: If the test fails, the Game view enters Pause mode. To clean up, select the **Pause** button to resume Play mode.

## Debug a test that uses EditorWindowUITestFixture

To debug a test that inherits from `EditorWindowUITestFixture`, set [`debugMode`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture#UnityEngine_UIElements_TestFramework_AbstractUITestFixture_debugMode) to `true` within the code.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/EditorWindowUITestFixtureExample.cs#EditorWindowDebugging)]

## Debug a test that uses UITestFixture

To debug a test that inherits from `UITestFixture`, you can temporarily pass a value to the `UITestFixture` constructor to enable debug mode.

This example shows how to enable debugging for an Editor test that inherits from a runtime test class, which inherits from `UITestFixture` (such as [this example](xref:test-in-both-editor-and-runtime-states#Example)).

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/UITestFixture_RuntimeExample.cs#UITestFixture_RuntimeBase)]

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/UITestFixture_EditorExample.cs#RuntimeBase_EditorDebugging)]

## Debug a test that uses RuntimeUITestFixture

To debug a test that inherits from `RuntimeUITestFixture`, set [`debugMode`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture#UnityEngine_UIElements_TestFramework_AbstractUITestFixture_debugMode) to `true` within the code.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/UITestFixture_RuntimeExample.cs#RuntimeUITestFixture_Debugging)]

## Additional resources

- [Test in both Editor and runtime states](xref:test-in-both-editor-and-runtime-states)
- [`EditorWindow`](xref:UnityEditor.EditorWindow)