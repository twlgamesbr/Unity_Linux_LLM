using System;
using NUnit.Framework;
using UnityEngine.UIElements.TestFramework;

namespace UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests
{
    internal class UITestComponentExample : UITestFixture
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region CleanupUtilExample
        public class CleanupUtilExampleClass : UITestFixture
        {
            CleanupUtil m_CleanupUtil;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                // Add the test component and keep its reference
                // to add objects to be destroyed later.
                m_CleanupUtil = AddTestComponent<CleanupUtil>();
            }

            [Test]
            public void CleanupUtilExample()
            {
                // Create a game object to use in the test.
                GameObject go = new GameObject("GameObjectToDispose");

                // Add the game object to be destroyed after the test.
                // GameObject is of type Object so we use the AddDestructible method.
                m_CleanupUtil.AddDestructible(go);

                // Create another item.
                DisposableTestItem item = new DisposableTestItem();

                // Add the item to be disposed after the test.
                // DisposableTestItem implements the IDisposable interface
                // so we use the AddDisposable method.
                m_CleanupUtil.AddDisposable(item);

                // Test steps.
                // ...
            }
        }

        internal class DisposableTestItem : IDisposable
        {
            public void Dispose()
            {
                // Dispose steps.
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region PopupMenuSimulatorExample
        public class PopupMenuSimulatorExampleClass : UITestFixture
        {
            PopupMenuSimulator m_PopupMenuSimulator;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                // Add the test component and keep its reference
                // to interact with popup menus later.
                m_PopupMenuSimulator = AddTestComponent<PopupMenuSimulator>();
            }

            [Test]
            public void PopupMenuSimulatorExample()
            {
                // Fetch the DropdownMenu to interact with.
                DropdownField dropdownField = rootVisualElement.Q<DropdownField>("MyDropdownField");

                VisualElement dropdown = dropdownField.Q<VisualElement>(className: "unity-base-popup-field__input");

                Assume.That(dropdownField.value, Is.Null);

                // Click on the dropdown.
                simulate.Click(dropdown);
                simulate.FrameUpdate();

                // Check that the menu is displayed.
                Assert.That(m_PopupMenuSimulator.menuIsDisplayed,
                    Is.True, "PopupMenu was not displayed.");

                // Check that the menu contains the item we want to select.
                m_PopupMenuSimulator.AssertContainsAction("Option1");

                // Select Option1 by its name.
                m_PopupMenuSimulator.SimulateMenuSelection("Option1");
                Assert.That(dropdownField.value, Is.EqualTo("Option1"));

                // Discard the menu.
                // PopupMenuSimulator does not currently automatically discard the menu,
                // so it needs to be manually discarded before opening another menu.
                m_PopupMenuSimulator.DiscardMenu();

                // Click on the dropdown again.
                simulate.Click(dropdown);
                simulate.FrameUpdate();

                Assert.That(m_PopupMenuSimulator.menuIsDisplayed,
                    Is.True, "PopupMenu was not displayed.");

                m_PopupMenuSimulator.AssertContainsAction("Option2");

                // Simulate item selection by its index.
                var itemIndex = m_PopupMenuSimulator.FindActionIndex("Option2");
                m_PopupMenuSimulator.SimulateItemSelection(itemIndex);

                Assert.That(dropdownField.value, Is.EqualTo("Option2"));
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
        #region CustomTestLoggerComponent
        public class TestLoggerComponent : UITestComponent
        {
            string testLoggerTag = "|| TestLoggerComponent || ";
            string currentTest;

            // Invoked when the test component is added to the test fixture.
            protected override void Initialize(AbstractUITestFixture testFixture)
            {
                // Required: call the base Initialize.
                base.Initialize(testFixture);

                Debug.Log($"{testLoggerTag} Component Initialized");
            }

            // Invoked before each test.
            protected override void BeforeTest()
            {
                currentTest = TestContext.CurrentContext.Test.Name;
                Debug.Log($"{testLoggerTag} Beginning test: {currentTest}");
            }

            // Invoked after each test.
            protected override void AfterTest()
            {
                Debug.Log($"{testLoggerTag} Ending test: {currentTest}");
            }

            // Invoked at the end of the test fixture.
            protected override void Shutdown()
            {
                currentTest = null;
                Debug.Log($"{testLoggerTag} Test Fixture Shutdown");
            }

            public void LogMessage(string message)
            {
                Debug.Log($"{currentTest}: {message}");
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region TestLoggerComponentExample
        public class TestLoggerComponentExample : UITestFixture
        {
            TestLoggerComponent testLoggerComponent;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                testLoggerComponent = AddTestComponent<TestLoggerComponent>();
            }

            [Test]
            public void MyTestWithLoggingComponent()
            {
                // Test steps.
                // ...
            }

            [Test]
            public void MyOtherTestWithLoggingComponent()
            {
                testLoggerComponent.LogMessage("Adding more logs here");

                // Test steps.
                // ...
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region CustomUISetupComponent
        public class UISetupComponent : UITestComponent
        {
            public Button testButton { get; private set; }

            protected override void Initialize(AbstractUITestFixture testFixture)
            {
                // Required: call the base Initialize.
                base.Initialize(testFixture);

                // Force fixtures that use this test component to clear
                // the content of the rootVisualElement at the end of each test.
                fixture.clearContentAfterTest = true;
            }

            protected override void BeforeTest()
            {
                // Instantiate your elements.
                testButton = new Button() { name = "UISetupComponentButton" };

                // Add them to the rootVisualElement.
                fixture.rootVisualElement.Add(testButton);
            }

            protected override void AfterTest()
            {
                // Clean up of the rootVisualElement is automatic
                // when clearContentAfterTest is true.
                // You can add custom cleanup here if needed.
                testButton = null;
            }
        }
        #endregion

        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region TestUISetupComponentExample
        public class TestUISetupComponentExample : UITestFixture
        {
            UISetupComponent uiSetupComponent;

            [OneTimeSetUp]
            public void OneTimeSetUp()
            {
                uiSetupComponent = AddTestComponent<UISetupComponent>();
            }

            [Test]
            public void MyTestWithUISetupComponent()
            {
                Button button = rootVisualElement.Q<Button>("UISetupComponentButton");
                Assume.That(button, Is.Not.Null);

                // Test steps.
                // ...
            }

            [Test]
            public void MyOtherTestWithUISetupComponent()
            {
                Button button = rootVisualElement.Q<Button>("UISetupComponentButton");
                Assume.That(button, Is.Not.Null);

                // Test steps.
                // ...
            }
        }
        #endregion
    }
}
