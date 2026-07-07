---
uid: test-with-coroutines
---
# Test with Coroutines

You might want to write a UI test using coroutines if you must yield frames to wait for an action or result to complete.

UI Test Framework provides you with various `IEnumerator` methods to simulate events and yield UI frames.

## Use the [UnityTest] attribute

To use coroutine (or `IEnumerator`) methods within a test, you must assign the `[UnityTest]` attribute to your test.

You can also execute coroutines within the SetUp or TearDown of a test class, by using `[UnitySetUp]`, `[UnityTearDown]`, `[UnityOneTimeSetUp]` or `[UnityOneTimeTearDown]` attributes.

## Call the SetUp and TearDown functions

UI Test Framework contains some `SetUp` and `TearDown` functions:

- Call the [EventHelpers.TestSetUp](xref:UnityEngine.UIElements.TestFramework.EventHelpers.TestSetUp) function in either the `[SetUp]` or `[UnitySetUp]` function pertaining to a test/test class.
- Call the [EventHelpers.TestTearDown](xref:UnityEngine.UIElements.TestFramework.EventHelpers.TestTearDown) function in either the `[TearDown]` or `[UnityTearDown]` function pertaining to a test/test class.

The following is an example of a test class that uses the `[UnityTest]` attribute and calls the `SetUp` and `TearDown` functions:

```csharp
using NUnit.Framework;
// This namespace is required here since it
// contains the TestsSetUp and TestTearDown functions.
using UnityEngine.UIElements.TestFramework;

namespace MyTestNamespace
{
    [TestFixture]
    public class MyTestClass
    {
        [SetUp]
        public void SetUp()
        {
            EventHelpers.TestSetUp();

            // Your other test setup steps.
            // ...
        }

        [TearDown]
        public void TearDown()
        {
            // Your other test tear down steps.
            // ...

            EventHelpers.TestTearDown();
        }

        [Test]
        public void MyTest()
        {

        }
    }
}
```

## UnityEditor.UIElements.TestFramework

Most of the helpers available in **UnityEngine.UIElements.TestFramework** that operate on a VisualElement have an EditorWindow equivalent in **UnityEditor.UIElements.TestFramework**.

Refer to the UnityEngine samples above for example usage, as they work much the same.

One specific UnityEditor example is provided below.

#### Click on an EditorWindow

The following examples show how to click on a custom EditorWindow:
