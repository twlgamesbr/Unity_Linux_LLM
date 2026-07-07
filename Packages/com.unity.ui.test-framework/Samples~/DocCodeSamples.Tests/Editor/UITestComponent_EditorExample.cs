using NUnit.Framework;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using static UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests.UITestComponentExample;

namespace UnityEditor.UIElements.TestFrameworkDocCodeSamples.Editor.Tests
{
    internal class UITestComponent_EditorExample
    {
        public class CleanupUtil_EditorExample : CleanupUtilExampleClass
        {

        }

        public class PopupMenuSimulator_EditorExample : PopupMenuSimulatorExampleClass
        {

        }

        public class TestLoggerComponent_EditorExample : TestLoggerComponentExample { }

        public class TestUISetupComponent_EditorExample : TestUISetupComponentExample { }

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region StylesApplicatorExample
        public class StylesApplicatorExampleClass : UITestFixture
        {
            StylesApplicator m_StylesApplicator;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                // Add the test component and keep its reference
                // to add styles to elements later.
                m_StylesApplicator = AddTestComponent<StylesApplicator>();
            }

            [Test]
            public void StylesApplicatorExample()
            {
                Button button = rootVisualElement.Q<Button>("MyButton");

                Assume.That(button.resolvedStyle.backgroundColor,
                    Is.Not.EqualTo(Color.red));

                // Add a style to the button.
                m_StylesApplicator.AddStylesToElement(
                    button, ".unity-button { background-color: rgb(255, 0, 0); }");
                simulate.FrameUpdate();

                Assert.That(button.resolvedStyle.backgroundColor,
                    Is.EqualTo(Color.red));

                // Add a style to the root.
                m_StylesApplicator.AddStylesToRoot(
                    "Button:hover { color: rgb(0, 0, 255); }");
                simulate.FrameUpdate();

                Assume.That(button.resolvedStyle.color,
                    Is.Not.EqualTo(Color.blue));

                // Mouse over the button to activate the :hover
                // style that was added to the root.
                simulate.MouseMove(button.worldBound.center);
                simulate.FrameUpdate();

                Assert.That(button.resolvedStyle.color,
                    Is.EqualTo(Color.blue));
            }

            [SetUp]
            public void SetUp()
            {
                VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");
                uxml.CloneTree(rootVisualElement);

                simulate.FrameUpdate();
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region ContextMenuSimulatorExample
        public class ContextMenuSimulatorExampleClass : UITestFixture
        {
            ContextMenuSimulator m_ContextMenuSimulator;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                // Add the test component and keep its reference
                // to add styles to elements later.
                m_ContextMenuSimulator = AddTestComponent<ContextMenuSimulator>();
            }

            [Test]
            public void ContextMenuSimulatorExample()
            {
                Label label = rootVisualElement.Q<Label>("MyLabel");

                Vector2 clickLocation = new Vector2(label.worldBound.x, label.worldBound.yMin);

                // Select some text from the label.
                simulate.DoubleClick(clickLocation);
                simulate.FrameUpdate();

                // Right-click over the text to activate the context menu.
                simulate.Click(clickLocation,
                    MouseButton.RightMouse);
                simulate.FrameUpdate();

                // Check that the menu is displayed.
                Assert.That(m_ContextMenuSimulator.menuIsDisplayed,
                    Is.True, "ContextMenu was not displayed.");

                // Check that the menu contains the item we want to select.
                m_ContextMenuSimulator.AssertContainsAction("Copy");

                // Execute the Copy menu action.
                m_ContextMenuSimulator.SimulateMenuSelection("Copy");

                // Fetch and focus the textField's textElement.
                var textField = rootVisualElement.Q<TextField>("MyTextField");
                var textElement = textField.Q<TextElement>();
                Assume.That(textField, Is.Not.Null);
                textField.Focus();
                Assert.That(textField.value, Is.Empty);

                // Paste the text in textField.
                simulate.ExecuteCommand("Paste");
                simulate.FrameUpdate();

                Assert.That(textField.value, Is.Not.Empty);
            }

            [SetUp]
            public void SetUp()
            {
                VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");
                uxml.CloneTree(rootVisualElement);

                simulate.FrameUpdate();
            }
        }
        #endregion

    }
}
