---
uid: press-tab-or-return
---

# Press Tab or Return

> [!NOTE]
> This function doesn't generate Navigation events.
> Interactions with certain controls that depend on Navigation events are therefore not supported in Play mode tests.

To simulate pressing the Tab key, use the [`TabKeyPress()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_TabKeyPress_UnityEngine_EventModifiers_) or the [`ReturnKeyPress()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_ReturnKeyPress_UnityEngine_EventModifiers_) method from the [`PanelSimulator`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) class. Before calling these methods, focus the control or element that needs to react to them.

The following example shows how to simulate pressing Tab:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/SimulateUIInteractions_EditorExample.cs#TabKeyPressExample)]

The following example shows how to simulate pressing Return:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/SimulateUIInteractions_EditorExample.cs#ReturnKeyPressExample)]

## Additional resources

- [Press keys](xref:press-keys)
- [Navigation events](xref:uie-navigation-events)