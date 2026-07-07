---
uid: create-multi-window-tests
---

# Create multi-window tests

The test fixtures instantiate one panel and manage its lifetime for you. However, some tests require multiple panels or windows to simulate complex UI interactions. You can create and manage multiple [PanelSimulator](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) instances in your tests to achieve this. Each `PanelSimulator` can represent a different panel or window, allowing you to test interactions between them.

## PanelSimulator types

There are different types of `PanelSimulator`, each designed to work in specific environments or scenarios. The test fixture you select determines what type of UI the test fixture initializes.

The following table summarizes the different `PanelSimulator` types used by each test fixture:

| Test fixture | Simulator Type | Description |
|:---|:---|:---|
| [UITestFixture](xref:UnityEngine.UIElements.TestFramework.UITestFixture) | [RuntimePanelSimulator](xref:UnityEngine.UIElements.TestFramework.RuntimePanelSimulator) or [EditorPanelSimulator](xref:UnityEditor.UIElements.TestFramework.EditorPanelSimulator) | Simulates a panel in either the runtime or Editor environment. |
| [EditorWindowUITestFixture](xref:UnityEditor.UIElements.TestFramework.EditorWindowUITestFixture`1) | [EditorWindowPanelSimulator](xref:UnityEditor.UIElements.TestFramework.EditorWindowPanelSimulator) | Simulates a panel within an Editor window. |
| [RuntimeUITestFixture](xref:UnityEngine.UIElements.TestFramework.RuntimeUITestFixture) | [RuntimePanelSimulator](xref:UnityEngine.UIElements.TestFramework.RuntimePanelSimulator) | Simulates a panel in the runtime environment. |

## Example

The following example shows how to create multiple `EditorWindowPanelSimulator` instances in a test:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/MultipleWindowsExample.cs#SimulateMultipleWindows)]

## Additional resources

- [Trigger and update UI](xref:trigger-and-update-ui)
- [PanelSimulator](xref:UnityEngine.UIElements.TestFramework.PanelSimulator)