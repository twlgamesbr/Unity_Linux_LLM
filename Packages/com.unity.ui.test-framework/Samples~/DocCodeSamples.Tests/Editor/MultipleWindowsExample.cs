using NUnit.Framework;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements.TestFramework;

namespace UnityEditor.UIElements.TestFrameworkDocCodeSamples.Editor.Tests
{
    internal class MultipleWindowsExample
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region SimulateMultipleWindows
        public class SimulateMultipleWindows : EditorWindowUITestFixture<UITestFrameworkDocSampleWindow>
        {
            [Test]
            public void MultipleWindowsExampleTest()
            {
                // Use the CleanupUtil UITestComponent to manage the ScriptableObject cleanup.
                CleanupUtil cleanupUtil = AddTestComponent<CleanupUtil>();

                // Ensure the main test UI is updated.
                simulate.FrameUpdate();

                // Create a new EditorWindow for testing.
                EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
                window.Show();

                // Ensure the new window will be properly destroyed.
                cleanupUtil.AddDestructible(window);

                // Hook up the EditorWindow as a simulator.
                EditorWindowPanelSimulator secondEditorWindow =
                    new EditorWindowPanelSimulator(window);

                // Ensure the UI for the second window is updated.
                secondEditorWindow.FrameUpdate();

                // Test steps.
                // ...
            }
        }
        #endregion
    }
}
