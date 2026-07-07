using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using UnityEditor.UIElements.TestFramework;
using static UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests.UITestFixtureAutoDetectRuntimeExample;

namespace UnityEditor.UIElements.TestFrameworkDocCodeSamples.Editor.Tests
{
    internal class UITestFixture_EditorExample
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region AutoDetect_EditorExample
        // This class is contained in an Editor test assembly.
        public class AutoDetect_EditorExample : AutoDetect_RuntimeExample
        {
            // This test class will inherit the tests from its base,
            // but the tests will run against an EditorPanel.
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region RuntimeBase_EditorDebugging
        // This class is contained in an Editor test assembly.
        public class RuntimeBase_EditorDebugging : UITestFixture_RuntimeBase
        {
            // This test class will run in DebugMode because its base
            // class passes the debugMode parameter to the UITestFixture constructor.

            [Test]
            public void TestToDebug()
            {
                // Test steps.
                // The UI will remain open and visible when the test fails.
                //Assert.Fail();
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region BasicEditorExample
        // This class is contained in an Editor test assembly.
        public class BasicEditorExample : UITestFixture
        {
            // This test class will spawn an EditorPanel that you
            // can add UI elements to via the rootVisualElement property.
            [Test]
            public void EditorPanelTest()
            {
                // Set the panelSize to ensure the panel is
                // large enough to display our UI.
                panelSize = new Vector2(800, 900);
                
                // Use the rootVisualElement property to add elements
                // to your UI.
                rootVisualElement.Add(new Button() { name = "MyButton" });

                // Ensure the panel's UI is up to date.
                simulate.FrameUpdate();

                Button button = rootVisualElement.Q<Button>("MyButton");
                Assert.That(button, Is.Not.Null);

                // Test steps.
                // ...
            }
        }
        #endregion
    }
}
