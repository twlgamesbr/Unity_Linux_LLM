---
uid: press-keys
---

# Press keys

> [!NOTE]
> This function doesn't generate Navigation events.
> Interactions with certain controls that depend on Navigation events are therefore not supported in Play mode tests.

To simulate pressing certain keys, use the [`KeyPress()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_KeyPress_UnityEngine_KeyCode_UnityEngine_EventModifiers_) method from the [`PanelSimulator`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) class. Before calling this method, focus the control or element that needs to react to the `KeyPress()`.

The following example shows how to press keys while focus is on a button.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/SimulateUIInteractions_EditorExample.cs#TabKeyPressExample)]

## Additional resources

- [Move the mouse to a visual element](xref:move-mouse-to-a-visual-element)
- [Drag an element from one place to another](xref:drag-an-element-from-one-place-to-another)
- [Navigation events](xref:uie-navigation-events)