---
uid: test-in-both-editor-and-runtime-states
---

# Test in both Editor and runtime states

If your tests need to run in both Editor and runtime states, use [UITestFixture](xref:UnityEngine.UIElements.TestFramework.UITestFixture). This hybrid test fixture initializes the appropriate state (Editor or runtime) based on conditions set in your code. `UITestFixture` reduces code duplication when you need to execute the same tests in multiple contexts.

## Usage

To enable automatic state initialization, inherit from `UITestFixture` in your test class. By default, `UITestFixture` determines and sets up the test environment.

You can explicitly specify the state by passing a [FixtureType](xref:UnityEngine.UIElements.TestFramework.UITestFixture.FixtureType) (Editor or runtime) to the `UITestFixture` constructor. If you don't specify a type, initialization follows the following rules:

``` mermaid
flowchart LR
    A(Test starts) --> B{{Test in PlayMode?}}
    B -- Yes --> D[Runtime State]
    B -- No --> C[Editor State]

    classDef question fill:#FFF
    class B question
```

### Editor state

If the test isn't in PlayMode, the fixture provides an empty Editor panel for you to populate during the test. This Editor panel exists without creating an `EditorWindow`, which allows for faster test execution.

During test execution, no UI is displayed because the Editor panel isn't attached to an `EditorWindow`. If you need to visually inspect or debug your tests, see how to [Debug a test that uses UITestFixture](xref:debug-ui-test-fixtures#debug-a-test-that-uses-uitestfixture). This shows the UI during test runs for easier development and troubleshooting.

The test fixture initializes the UI to an empty Editor panel. To populate it in the test, access the `rootVisualElement` property of the test fixture and add your elements to it.

### Runtime state

If the test is in PlayMode, the fixture initializes in the runtime state and provides an empty `UIDocument` object for runtime testing.

The tests' UI is initialized to an empty `UIDocument`. To populate it in the test, access the `rootVisualElement` property of the test fixture and add your elements to it.

If you need to load a scene already containing a `UIDocument` for testing, inherit from [`RuntimeUITestFixture`](xref:test-ui-in-runtime) in your test class.

## Example

This code illustrates how to set up your test classes to run in both Editor and runtime to minimize code duplication.

Write the tests in a runtime test assembly using a test class that inherits from `UITestFixture`.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/UITestFixture_RuntimeExample.cs#AutoDetect_RuntimeExample)]

You can inherit from that class in the Editor test assembly.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Editor/UITestFixture_EditorExample.cs#AutoDetect_EditorExample)]

If you only need to write tests for the Editor, you can use `UITestFixture` directly in an Editor test class.

## Additional resources

- [`EditorWindow`](xref:UnityEditor.EditorWindow)
- [`UIDocument`](xref:UnityEngine.UIElements.UIDocument)
- [Test UI with Editor window instances](xref:test-ui-with-editor-window-instances)
- [Test UI without Editor window dependencies](xref:test-ui-without-editor-window-dependencies)
- [Choose the appropriate test fixture](xref:introduction-to-test-fixtures#choose-the-appropriate-test-fixture)