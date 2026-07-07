using NUnit.Framework;
using UnityEngine.UIElements.TestFramework;

namespace UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests
{
    internal class UITestFixtureAutoDetectRuntimeExample
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region AutoDetect_RuntimeExample
        // This class is contained in a runtime test assembly.
        public class AutoDetect_RuntimeExample : UITestFixture
        {
            [SetUp]
            public void SetUp()
            {
                VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");
                uxml.CloneTree(rootVisualElement);

                simulate.FrameUpdate();
            }

            [Test]
            public void EditorAndRuntimeExampleTest()
            {
                // This test will run using an empty RuntimePanel (UIDocument)
                // when executing in PlayMode.

                // Test steps
                // ...
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region UITestFixture_RuntimeBase
        // This class is contained in a runtime test assembly.
        public class UITestFixture_RuntimeBase : UITestFixture
        {
            public UITestFixture_RuntimeBase() : base(debugMode: true) { }

            [Test]
            public void RuntimeTestToDebug()
            {
                // Test steps.
                // The UI will remain open and visible when the test fails.
                //Assert.Fail();
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region RuntimeUITestFixture_Debugging
        public class RuntimeUITestFixture_Debugging : RuntimeUITestFixture
        {
            [Test]
            public void TestToDebug()
            {
                debugMode = true;

                // Test steps.
                // The UI will remain open and visible when the test fails.
                //Assert.Fail();
            }
        }
        #endregion
    }
}
