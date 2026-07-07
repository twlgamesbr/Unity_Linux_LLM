---
uid: simulate-context-menu-actions
---

# Simulate context menu actions

To validate and simulate context menu actions in your tests, use the [`AddTestComponent<T>`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture.AddTestComponent``1) method to create a [`ContextMenuSimulator`](xref:UnityEditor.UIElements.TestFramework.ContextMenuSimulator) test component. The `ContextMenuSimulator` doesn't spawn context menus, so you must activate them in the test.

The following examples show how to use the `ContextMenuSimulator` to validate and interact with a context menu:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/UITestComponent_EditorExample.cs#ContextMenuSimulatorExample)]

## Additional resources

- [Simulate popup menu actions](xref:simulate-popup-menu-actions)