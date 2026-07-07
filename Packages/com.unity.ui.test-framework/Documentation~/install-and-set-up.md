---
uid: install-and-set-up-ui-test-framework
---
# Install and set up UI Test Framework

UI Test Framework leverages [NUnit](https://nunit.org) and works with the [Unity Test Framework (UTF)](xref:um-test-framework-intro). You can use it to create and execute automated UI tests in your Unity projects.

To set up your project to use the UI Test Framework, follow these steps.

## Install the packages

Follow these steps to install the required packages:

1. In **Package Manager**, install **UI Test Framework** from the **Unity Registry**.
2. In the **Built-in** tab of **Package Manager**, enable the **UIElements** package if it's not already enabled.

## Add assembly references and import namespaces

UI Test Framework functionality is distributed across two assemblies.

| Assembly | Namespace | Usage |
|:---|:---|:---|
| `Unity.UI.TestFramework.Runtime` | [UnityEngine.UIElements.TestFramework](xref:UnityEngine.UIElements.TestFramework) | For tests that need access to functionality available at Editor and Runtime. |
| `Unity.UI.TestFramework.Editor` | [UnityEditor.UIElements.TestFramework](xref:UnityEditor.UIElements.TestFramework) | For tests that only need access to Editor-specific functionality. |

To add references to the assemblies to your test assembly:

1. In the **Project** window, locate the test assembly that needs the reference.
2. In the **Inspector** window, In the **Assembly Definition Reference** section, select **+**.
3. Select the following assemblies as needed:
   - `Unity.UI.TestFramework.Runtime`
   - `Unity.UI.TestFramework.Editor`

To import the namespaces for UI Test Framework functionality, add the following `using` statements to your test scripts:

```csharp
// Use this when a test class needs access to functionality available at Editor and Runtime.
using UnityEngine.UIElements.TestFramework;
// Use this when a test class needs access to Editor-specific functionality.
using UnityEditor.UIElements.TestFramework;
```

## Additional resources

- [The Package Manager window](xref:um-upm-ui)
- [Organize scripts into assemblies](xref:um-script-compilation-assembly-definition-files)