---
uid: test-fixtures-landing
---

# Test with test fixtures

The UI Test Framework provides several test fixtures that help you create and manage panel instances for testing UI Toolkit-based UI.

| Topic | Description |
|:---|:---|
| [Introduction to test fixtures](xref:introduction-to-test-fixtures) | Overview of the available test fixtures and their intended purposes. |
| [Test in both Editor and runtime states](xref:test-in-both-editor-and-runtime-states) | Use `UITestFixture` to create tests that run in both Editor and Runtime states.  |
| [Test UI with Editor window instances](xref:test-ui-with-editor-window-instances) | Use `EditorWindowUITestFixture<T>` to create tests that require an actual `EditorWindow` instance.  |
| [Test UI without Editor window dependencies](xref:test-ui-without-editor-window-dependencies) | Use `EditorUITestFixture` to create tests that do not require an actual `EditorWindow` instance.  |
| [Test UI in runtime](xref:test-ui-in-runtime) | Use `RuntimeUITestFixture` to create tests that run in Play mode.  |
| [Debug UI test fixtures](xref:debug-ui-test-fixtures) | Display the UI during test execution for debugging purposes.  |
| [Trigger and update UI](xref:trigger-and-update-ui) | Use the `PanelSimulator` API provided by the test fixtures to simulate UI interactions and update the UI.  |
| [Create multi-window tests](xref:create-multi-window-tests) | Create tests that involve multiple panels or windows.  |
| [Customize test fixtures with UITestComponent](xref:ui-test-component-landing) | Add functionality to tests by using UITestComponents. |

## Additional resources

- [Simulate UI interactions](xref:simulate-ui-interaction-landing)