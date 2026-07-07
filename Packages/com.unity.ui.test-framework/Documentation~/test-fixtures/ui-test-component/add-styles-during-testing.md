---
uid: add-styles-during-testing
---

# Add styles during testing

To add styles to elements during testing, call `AddTestComponent<StylesApplicator>()` to add a [`StylesApplicator`](xref:UnityEditor.UIElements.TestFramework.StylesApplicator) component to the test fixture. The styles are automatically cleaned up at the end of the test.

The following example shows how to add styles using the `StylesApplicator`:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Editor/UITestComponent_EditorExample.cs#StylesApplicatorExample)]

## Additional resources

- [UITestComponent](xref:UnityEngine.UIElements.TestFramework.UITestComponent)
- [`AddTestComponent<T>`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture.AddTestComponent``1)