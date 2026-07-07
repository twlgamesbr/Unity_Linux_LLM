---
uid: test-ui-without-editor-window-dependencies
---

# Test UI without Editor window dependencies

If your tests don't require an actual `EditorWindow` instance, use [`UITestFixture`](xref:UnityEngine.UIElements.TestFramework.UITestFixture) to create Editor tests that spawn and manage an empty Editor panel. This fixture doesn't create an `EditorWindow` instance, which allows for faster test execution. The Editor panel hosts your UI, but the UI isn't rendered or visible on the screen during testing.

> [!NOTE]
> If your UI is defined in the `CreateGUI()` or requires a GUI view to work properly, use [`EditorWindowUITestFixture`](xref:UnityEditor.UIElements.TestFramework.EditorWindowUITestFixture`1) to spawn a real `EditorWindow` instance for your tests.

The following example shows how to set up your test class to use `UITestFixture` in an Editor test assembly:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/UITestFixture_EditorExample.cs#BasicEditorExample)]

## Additional resources

- [Test UI with Editor window instances](xref:test-ui-with-editor-window-instances)
- [Choose the appropriate test fixture](xref:introduction-to-test-fixtures#choose-the-appropriate-test-fixture)
- [`EditorWindow`](xref:UnityEditor.EditorWindow)
- [`CreateGUI()`](xref:EditorWindow.CreateGUI)
