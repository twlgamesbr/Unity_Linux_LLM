---
uid: test-ui-with-editor-window-instances
---

# Test UI with Editor window instances

If your tests require an actual `EditorWindow` instance, use [`EditorWindowUITestFixture`](xref:UnityEditor.UIElements.TestFramework.EditorWindowUITestFixture`1) to create tests that spawn and manage an `EditorWindow` instance. With this fixture, an Editor panel attached to a real `EditorWindow` hosts your UI, so the UI is rendered and visible on the screen during testing.

> [!NOTE]
> To test your UXML or your custom control, use [`UITestFixture`](xref:UnityEngine.UIElements.TestFramework.UITestFixture).

The following example shows how to set up your test class to use `EditorWindowUITestFixture`:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/EditorWindowUITestFixtureExample.cs#BasicEditorWindowExample)]

## Limitations of the FrameUpdate function for Editor windows

The [`FrameUpdate()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator.FrameUpdate) method executes the UI Toolkit update loop. 

However, if your Editor window relies on custom logic defined in an [`Update()`](xref:UnityEditor.EditorWindow.Update) method or is tied to the [Editor update loop](xref:UnityEditor.EditorApplication.update), tests must either call the update methods directly or wait for them to execute normally via coroutines.

The following example shows how to write a test that validates the custom logic of a window's Update function:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/EditorWindowWithCustomUpdate.cs)]

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/EditorWindowWithCustomUpdateTest.cs)]

## Additional resources

- [Choose the appropriate test fixture](xref:introduction-to-test-fixtures#choose-the-appropriate-test-fixture)
- [EditorWindow](xref:UnityEditor.EditorWindow)
