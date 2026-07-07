---
uid: drag-an-element-from-one-place-to-another
---

# Drag an element from one place to another

To drag an element from the specified `positionFrom` to the specified `positionTo`, use the [`DragAndDrop()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_DragAndDrop_UnityEngine_Vector2_UnityEngine_Vector2_UnityEngine_UIElements_MouseButton_UnityEngine_EventModifiers_) method from the [`PanelSimulator`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) class.

The following example shows how to use the `DragAndDrop` function to drag on a slider:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/SimulateUIInteractionsExample.cs#MouseDragExample)]

## Additional resources

- [Move the mouse to a visual element](xref:move-mouse-to-a-visual-element)