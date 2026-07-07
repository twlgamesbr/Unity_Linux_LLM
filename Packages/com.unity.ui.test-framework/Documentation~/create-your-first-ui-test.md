---
uid: create-your-first-ui-test
---
# Create your first UI test

Use this simple example to get started with the UI Test Framework. This example demonstrates how to set up a simple UI, create a test using the test fixtures, and run a test that simulates a button click to verify its behavior.

## Create a simple UI for testing

Create a simple Editor window with a button to test. When the button is clicked, its text changes to `Button was clicked!`.

1. Create a Unity project and [install the UI Test Framework package](xref:install-and-set-up-ui-test-framework).
2. In the **Project** window, create a folder named `Editor` (if it doesn't already exist).
3. In the `Editor` folder, create a new C# script named `SimpleEditorWindow.cs` with the following content:

    [!code-cs[](../Samples~/DocCodeSamples.Tests/Editor/SimpleEditorWindow.cs)]

## Create the assembly definition 

Create an assembly definition for the `SimpleEditorWindow` script to allow the test class to reference it. Also create an assembly definition for the test fixture itself and reference the necessary test framework assemblies.

1. In the `Editor` folder, right-click and select **Create** > **Scripting** > **Assembly Definition** to create an assembly definition file named `SimpleEditorWindow.Editor.asmdef` with the following content:

    ```json
    {
        "name": "SimpleEditorWindow.Editor",
        "includePlatforms": [
            "Editor"
        ]
    }
    ```
2. Inside the `Editor` folder, create a folder named `Tests`.
3. Inside the `Tests` folder, right-click and select **Create** > **Scripting** > **Assembly Definition** to create an assembly definition file named `SimpleEditorWindow.Tests.Editor.asmdef` with the following content:

    ```json
    {
        "name": "SimpleEditorWindow.Tests.Editor",
        "references": [
            "UnityEngine.TestRunner",
            "UnityEditor.TestRunner",
            "Unity.UI.TestFramework.Editor",
            "Unity.UI.TestFramework.Runtime",
            "SimpleEditorWindow.Editor"
        ],
        "includePlatforms": [
            "Editor"
        ]
    }
    ```

## Create the test script and run the tests

Create a test class that inherits from the `EditorWindowUITestFixture` because the test requires an actual `EditorWindow` instance. For more information, refer to [Introduction to test fixtures](xref:introduction-to-test-fixtures). The test simulates a button click, and verifies that the button text changes as expected.

1. Inside the `Editor/Tests` folder, create a C# script named `SimpleEditorWindowTest.cs` with the following content:

    [!code-cs[](../Samples~/DocCodeSamples.Tests/Editor/SimpleEditorWindowTest.cs)]

2. From the menu, select **Window** > **General** > **Test Runner**.
3. In the **Test Runner** window, select the **EditMode** tab.
4. Select `SimpleEditorWindow.Tests.Editor.dll`.
5. Select **Run** to execute your tests.

## Additional resources

- [Introduction to test fixtures](xref:introduction-to-test-fixtures)
- [Simulate UI interactions](xref:simulate-ui-interaction-landing)