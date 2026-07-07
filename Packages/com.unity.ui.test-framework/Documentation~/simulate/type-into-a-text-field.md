---
uid: type-into-a-text-field
---
# Type into a TextField

To simulate typing text into a text field in your tests, use the [`TypingText()`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator#UnityEngine_UIElements_TestFramework_PanelSimulator_TypingText_System_String_System_Boolean_) method from the [`PanelSimulator`](xref:UnityEngine.UIElements.TestFramework.PanelSimulator) class. This method sends the appropriate keyboard events to the target element, mimicking a real user interaction. Focus the text field before calling this method.

## Example

The following example shows how to simulate typing text into a text field:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/SimulateUIInteractionsExample.cs#TypingTextExample)]

## Platform differences at runtime

When running tests in Play mode or in a runtime assembly, the behavior of the `TypingText()` method can vary based on the platform due to its different requirements and behaviors at runtime.

The following table summarizes the differences:

| Platform     | Events sent    | Requirements        |
|--------------|----------------|---------------------|
| **Windows**, **Mac**, **Linux**, **WebGL** | UI Toolkit events | You must focus on the `TextElement`.  |
| **XboxOne**, **PS5**, **PS4**, **Android**, **iPhone**, **tvOS** | Directly on `TouchScreenKeyboard` | You must focus on the `TextElement` to summon the `TouchScreenKeyboard`. |
| **Nintendo Switch**   | Unsupported  | N/A   |

### Usage when a `TouchScreenKeyboard` is present

You can use the test fixtures to execute UI interactions synchronously and force the UI Toolkit loop to run.

However, for platforms that pop a [`TouchScreenKeyboard`](xref:UnityEngine.TouchScreenKeyboard), such as an iPhone, tests must wait in real time before calling TypingText. Because the `TouchScreenKeyboard` functionality is not controlled by UI Toolkit, tests must wait until the keyboard is visible and ready before trying to type.

The following example shows how to simulate typing text into a text field when a `TouchScreenKeyboard` is involved:

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/SimulateUIInteractionsExample.cs#TypingText_TouchScreenKeyboard_Example)]

## Additional resources

- [Click on a visual element](xref:click-on-a-visual-element)
- [Double-click on a visual element](xref:double-click-on-a-visual-element)