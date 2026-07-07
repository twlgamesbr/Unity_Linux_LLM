---
uid: click-on-a-visual-element
---

# Click on a visual element

To simulate a mouse click on a visual element in your tests, use the [`Click(VisualElement)`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_Click_UnityEngine_UIElements_VisualElement_UnityEngine_UIElements_MouseButton_UnityEngine_EventModifiers_) or [`Click(Vector2)`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_Click_UnityEngine_Vector2_UnityEngine_UIElements_MouseButton_UnityEngine_EventModifiers_) method from the [`PanelSimulator`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) class. This method sends the appropriate mouse events to the target element, mimicking a real user interaction.

The following example shows how to simulate a click on a button:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/SimulateUIInteractionsExample.cs#ClickExample)]

## Additional resources

- [Double-click on a visual element](xref:double-click-on-a-visual-element)