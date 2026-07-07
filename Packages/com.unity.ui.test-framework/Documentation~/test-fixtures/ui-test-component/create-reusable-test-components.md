---
uid: create-reusable-test-components
---

# Create reusable test components

To create reusable test components with custom behavior for different phases of the test lifecycle, derive your class from [`UITestComponent`](xref:UnityEngine.UIElements.TestFramework.UITestComponent). Override its lifecycle methods to implement your logic at the appropriate stages.

## Lifecycle methods

`UITestComponent` provides four lifecycle methods you can override:

| Method | When it's called | Intended purpose |
|:---|:---|:---|
| [`Initialize(AbstractUITestFixture)`](xref:UnityEngine.UIElements.TestFramework.UITestComponent.Initialize(UnityEngine.UIElements.TestFramework.AbstractUITestFixture)) | When the component is added to the test fixture | Set up the component and initialize references |
| [`BeforeTest()`](xref:UnityEngine.UIElements.TestFramework.UITestComponent.BeforeTest) | Before each test runs | Prepare test-specific state |
| [`AfterTest()`](xref:UnityEngine.UIElements.TestFramework.UITestComponent.AfterTest) | After each test completes | Clean up test-specific state |
| [`Shutdown()`](xref:UnityEngine.UIElements.TestFramework.UITestComponent.Shutdown) | When removing the component or tearing down the fixture | Release resources and perform final cleanup |

## Example: Custom logging component

The following example creates a custom test component that logs messages during different stages of the test or test fixture:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#CustomTestLoggerComponent)]

Add your custom test component to a test fixture using the `AddTestComponent<T>()` method:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#TestLoggerComponentExample)]

## Example: UI setup component

The following example creates a custom test component that sets up the test fixture UI:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#CustomUISetupComponent)]

To use it in your tests:

[!code-cs[](../../../Samples~/DocCodeSamples.Tests/Runtime/UITestComponentExample.cs#TestUISetupComponentExample)]

## Best practices

When creating custom `UITestComponent` classes:

- **Keep components focused**: Design each component to have a single, clear responsibility.
- **Use appropriate lifecycle methods**: Initialize resources in `Initialize()` or `BeforeTest()`, clean up the state in `AfterTest()` or `Shutdown()`. Don't forget to call the base function when overriding the `Initialize()` method.
- **Manage and check references**: Be mindful of checking for null references and cleaning up the state properly between tests.

## Additional resources

- [UITestComponent](xref:UnityEngine.UIElements.TestFramework.UITestComponent)
- [`AddTestComponent<T>`](xref:UnityEngine.UIElements.TestFramework.AbstractUITestFixture.AddTestComponent``1)
