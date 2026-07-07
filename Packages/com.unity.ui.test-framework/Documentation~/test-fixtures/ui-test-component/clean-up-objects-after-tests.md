---
uid: clean-up-objects-after-tests
---

# Clean up objects after tests

To clean up objects after tests, call `AddTestComponent<CleanupUtil>()` to add a [`CleanupUtil`](xref:UnityEngine.UIElements.TestFramework.CleanupUtil) component to the test fixture. The `CleanupUtil` component helps you dispose of or destroy objects at the end of each test, ensuring that your tests don't leave behind unwanted state or resources.

The following example shows how to use the `CleanupUtil`:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#CleanupUtilExample)]

## Additional resources

- [UITestComponent](xref:UnityEngine.UIElements.TestFramework.UITestComponent)
- [`AddTestComponent<T>`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture.AddTestComponent``1)
