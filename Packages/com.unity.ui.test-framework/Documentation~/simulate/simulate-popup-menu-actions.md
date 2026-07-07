---
uid: simulate-popup-menu-actions
---

# Simulate popup menu actions

To validate and simulate popup menu actions in your tests, use the [`AddTestComponent<T>`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture.AddTestComponent``1) method to create a [`PopupMenuSimulator`](xref:UnityEngine.UIElements.TestFramework.PopupMenuSimulator) test component. The `PopupMenuSimulator` doesn't spawn popup menus, so you must activate them in the test.

The following example shows how to use the `PopupMenuSimulator` to validate and interact with a popup menu:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#PopupMenuSimulatorExample)]

## Additional resources

- [Simulate context menu actions](xref:simulate-context-menu-actions)