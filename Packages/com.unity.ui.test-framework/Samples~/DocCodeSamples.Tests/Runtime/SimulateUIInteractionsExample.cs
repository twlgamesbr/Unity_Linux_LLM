using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements.TestFramework;

namespace UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests
{
    internal class SimulateUIInteractionsExample : UITestFixture
    {
        [SetUp]
        public void SetUp()
        {
            VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");
            uxml.CloneTree(rootVisualElement);

            simulate.FrameUpdate();
        }

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region ClickExample
        [Test]
        public void ClickExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            var actionWasExecuted = false;

            // Set up the button's clicked functionality to flip a Boolean.
            Button button = rootVisualElement.Q<Button>("MyButton");
            button.clicked += () => { actionWasExecuted = true; };

            // Click on the button's position.
            simulate.Click(button);

            // Ensure the UI Toolkit update loop executes.
            // This step isn't required for this specific case, 
            // because this button just flips a Boolean when it's clicked.
            // However, if the button performs actions linked to the UI Toolkit 
            // update loop, like styling or scheduling, you need to run the 
            // update loop after clicking.
            simulate.FrameUpdate();

            Assert.That(actionWasExecuted, Is.True,
                "Button action was not executed.");
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region DoubleClickExample
        [Test]
        public void DoubleClickExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            var actionWasExecuted = false;

            void ValidateDoubleClickAction(PointerDownEvent e)
            {
                if (e.clickCount == 2)
                {
                    actionWasExecuted = true;
                }
            }

            // Set up the button's double click functionality to flip a boolean.
            Button button = rootVisualElement.Q<Button>("MyButton");
            button.RegisterCallback<PointerDownEvent>(e => ValidateDoubleClickAction(e),
                TrickleDown.TrickleDown);

            // Validate that a single click does not trigger
            // our button's custom functionality.
            simulate.Click(button);
            Assert.That(actionWasExecuted, Is.False,
                "Button action was executed unexpectedly.");

            // Double click on the button's position.
            simulate.DoubleClick(button);

            // Make sure the UI Toolkit update loop executes.
            // This is technically not required for this specific case,
            // since this button just flips a boolean when it is clicked.
            // However, if the button performs actions linked to the UI Toolkit
            // update loop, like styling or scheduling, it is required.
            simulate.FrameUpdate();

            Assert.That(actionWasExecuted, Is.True,
                "Button action was not executed.");
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        [UnityPlatform(include = new[] {
            RuntimePlatform.WindowsEditor, RuntimePlatform.WindowsPlayer,
            RuntimePlatform.OSXEditor, RuntimePlatform.OSXPlayer,
            RuntimePlatform.LinuxEditor, RuntimePlatform.LinuxPlayer })]
        #region TypingTextExample
        [Test]
        public void TypingTextExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch and focus the textField's textElement.
            var textField = rootVisualElement.Q<TextField>("MyTextField");
            var textElement = textField.Q<TextElement>();
            Assume.That(textField, Is.Not.Null);
            textField.Focus();

            Assume.That(textField.text, Is.Empty);
            Assume.That(textField.hasFocusPseudoState, Is.True);

            // Type into the textField.
            var typedText = "Typed text.";
            simulate.TypingText(typedText);
            simulate.FrameUpdate();

            Assert.That(textField.text, Is.EqualTo(typedText));
        }
        #endregion
        
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        //[UnityPlatform(include = new[] { RuntimePlatform.IPhonePlayer, RuntimePlatform.Android })]
        [Ignore("Unstable: https://jira.unity3d.com/browse/UUM-131105")]
        #region TypingText_TouchScreenKeyboard_Example
        [UnityTest]
        public IEnumerator TypingText_TouchScreenKeyboard_Example()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch and focus the textField's textElement.
            var textField = rootVisualElement.Q<TextField>("MyTextField");
            var textElement = textField.Q<TextElement>();
            Assume.That(textField, Is.Not.Null);
            textField.Focus();

            Assume.That(textField.text, Is.Empty);
            Assume.That(textField.hasFocusPseudoState, Is.True);

            // Wait real time for the
            // TouchScreenKeyboard to pop on the device.
            float timeout = Time.realtimeSinceStartup + 3f;
            while (!TouchScreenKeyboard.visible)
            {
                if (Time.realtimeSinceStartup > timeout)
                {
                    throw new TimeoutException("TouchScreenKeyboard did not pop within the timeout period.");
                }
                yield return null;
            }
            
            // Type into the textField.
            var typedText = "Typed text.";
            simulate.TypingText(typedText);
            simulate.FrameUpdate();

            Assert.That(textField.text, Is.EqualTo(typedText));
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region ScrollExample
        [Test]
        public void ScrollExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch the scrollView.
            var scrollView = rootVisualElement.Q<ScrollView>("MyScrollView");

            var scrollViewStateBefore = scrollView.scrollOffset;

            // Send events to scroll the mouse wheel.
            simulate.ScrollWheel(new Vector2(0, 0.5f),
                rootVisualElement.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(scrollView.scrollOffset, Is.Not.EqualTo(scrollViewStateBefore));
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region MouseMoveExample
        [Test]
        public void MouseMoveExample()
        {
            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Fetch the button
            var button = rootVisualElement.Q<Button>("MyButton");

            Assume.That(button.hasHoverPseudoState, Is.False);

            // Send events to mouse over the button.
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(button.hasHoverPseudoState, Is.True);
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region MouseDragExample
        [Test]
        public void MouseDragExample()
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
            simulate.DragAndDrop(dragger.worldBound.center,
                dragger.worldBound.center + new Vector2(10, 10));
            simulate.FrameUpdate();

            var endingPosition = slider.value;
            Assert.That(endingPosition, Is.GreaterThan(startingPosition),
                "Slider value did not update as expected.");
        }
        #endregion
    }
}
