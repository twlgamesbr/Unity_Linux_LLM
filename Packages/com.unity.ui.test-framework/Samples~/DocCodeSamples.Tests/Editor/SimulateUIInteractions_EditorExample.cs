using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests;

namespace UnityEditor.UIElements.TestFrameworkDocCodeSamples.Editor.Tests
{
    internal class SimulateUIInteractions_EditorExample : SimulateUIInteractionsExample
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region PressKeysExample
        [Test]
        // KeyPress is only officially supported for Editor.
        public void PressKeysExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch and focus the Slider.
            var slider = rootVisualElement.Q<Slider>("MySlider");
            var dragger = slider.Q<VisualElement>("unity-dragger");
            slider.Focus();
            simulate.FrameUpdate();

            Assume.That(dragger.ClassListContains("unity-base-slider--movable"));
            Assume.That(slider.hasFocusPseudoState, Is.True);

            var startingPosition = slider.value;

            // Send RightArrow key presses.
            simulate.KeyPress(KeyCode.RightArrow);
            simulate.KeyPress(KeyCode.RightArrow);
            simulate.KeyPress(KeyCode.RightArrow);
            simulate.FrameUpdate();

            var endingPosition = slider.value;
            Assert.That(endingPosition, Is.GreaterThan(startingPosition),
                "Slider value did not update as expected.");
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region TabKeyPressExample
        // TabKeyPress is only officially supported for Editor.
        [Test]
        public void TabKeyPressExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch and focus the Button.
            var button = rootVisualElement.Q<Button>("MyButton");
            button.Focus();
            simulate.FrameUpdate();

            Assume.That(button.hasFocusPseudoState, Is.True);

            // Send Tab key press.
            simulate.TabKeyPress();
            simulate.FrameUpdate();

            Assert.That(button.hasFocusPseudoState, Is.False);
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region ReturnKeyPressExample
        // ReturnKeyPress is only officially supported for Editor.
        [Test]
        public void ReturnKeyPressExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            var actionWasExecuted = false;

            // Set up the button's clicked functionality to flip a Boolean.
            Button button = rootVisualElement.Q<Button>("MyButton");
            button.clicked += () => { actionWasExecuted = true; };

            // Click on the button's position.
            button.Focus();
            simulate.ReturnKeyPress();
            simulate.FrameUpdate();

            Assert.That(actionWasExecuted, Is.True,
                "Button action was not executed.");
        }
        #endregion
    }
}
