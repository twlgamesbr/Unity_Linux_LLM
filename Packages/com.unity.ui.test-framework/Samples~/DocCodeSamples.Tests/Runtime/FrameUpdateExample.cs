using NUnit.Framework;
using Unity.Properties;
using UnityEngine.UIElements.TestFramework;

namespace UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests
{
    internal class FrameUpdateExample : UITestFixture
    {
        [SetUp]
        public void SetUp()
        {
            VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");
            uxml.CloneTree(rootVisualElement);

            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            simulate.ResetTimePerSimulatedFrameToDefault();
        }

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region FrameUpdate_UpdatesUI
        [Test]
        public void FrameUpdate_UpdatesUI()
        {
            // Use the CleanupUtil UITestComponent to manage the ScriptableObject cleanup.
            CleanupUtil cleanupUtil = AddTestComponent<CleanupUtil>();

            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            // Query for our label that is hooked up to Data Bindings.
            Label labelWithBindings = rootVisualElement.Q<Label>("MyLabelWithBindings");

            // Create a LabelTextScriptableObject to use just for the test.
            LabelTextScriptableObject testScriptableObjectLabel = ScriptableObject.CreateInstance<LabelTextScriptableObject>();

            // Assign the scriptable object to be cleaned up during the test tear down.
            cleanupUtil.AddDestructible(testScriptableObjectLabel);

            // Hook up our test's scriptable object to the label.
            labelWithBindings.dataSource = testScriptableObjectLabel;
            labelWithBindings.SetBinding("text", new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(testScriptableObjectLabel.labelText)),
                bindingMode = BindingMode.ToTarget
            });

            var newLabelText = "This text was set in a test via databindings";
            Assert.That(labelWithBindings.text, Is.Not.EqualTo(newLabelText));

            // Assign the new text to our scriptable object.
            testScriptableObjectLabel.labelText = newLabelText;

            // This step is just to illustrate that since bindings update
            // as part of the UI Toolkit update loop,
            // they won't have updated unless we call simulate.FrameUpdate().
            Assert.That(labelWithBindings.text, Is.Not.EqualTo(newLabelText));

            // Ensure the UI Toolkit update loop runs.
            simulate.FrameUpdate();

            // Bindings are updated successfully!
            Assert.That(labelWithBindings.text, Is.EqualTo(newLabelText));
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region FrameUpdate_ToControlTime
        [Test]
        public void FrameUpdate_ToControlTime()
        {
            // Set the time that gets incremented during all
            // simulation or FrameUpdate() calls to 0,
            // to give the test full control over the passage of time.
            simulate.timePerSimulatedFrameMs = 0;

            // Make sure the UI is totally up to date.
            simulate.FrameUpdate();

            var buttonWasClicked = false;
            var actionWasExecuted = false;

            Button button = rootVisualElement.Q<Button>("MyButton");

            void ExecuteAction()
            {
                actionWasExecuted = true;
            }

            // Set up the button's clicked functionality.
            // This button will flip one boolean immediately, but schedule
            // another boolean to be flipped 300ms from when it is clicked.
            button.RegisterCallback<PointerDownEvent>(e =>
            {
                buttonWasClicked = true;
                rootVisualElement.schedule.Execute(ExecuteAction).ExecuteLater(300);
            }, TrickleDown.TrickleDown);

            // Click on the button's position.
            simulate.Click(button);

            // Make sure the UI Toolkit update loop executes.
            // Since we set the timePerSimulatedFrameMs to 0,
            // no time will pass during this FrameUpdate.
            simulate.FrameUpdate();

            // Check that the scheduled button action is the only one not executed.
            Assert.That(buttonWasClicked, Is.True,
                "Button click was not received.");
            Assert.That(actionWasExecuted, Is.False,
                "Button action was executed unexpectedly.");

            // Simulate the passage of time by 10ms.
            simulate.FrameUpdateMs(10);

            Assert.That(actionWasExecuted, Is.False,
                "Button action was executed unexpectedly.");

            // Make sure the UI Toolkit update loop executes.
            // Simulate the passage of time by another 290ms.
            // This totals 300ms of time passed since the button was clicked.
            simulate.FrameUpdateMs(290);

            Assert.That(actionWasExecuted, Is.True,
                "Button action was not executed.");

            // Reset the value that timePerSimulatedFrameMs was set to.
            // This value does not get automatically reset by the fixtures.
            simulate.ResetTimePerSimulatedFrameToDefault();
        }
        #endregion
    }
}
