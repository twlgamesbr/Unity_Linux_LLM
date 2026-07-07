using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements.TestFramework;

namespace UnityEngine.UIElements.TestFrameworkDocCodeSamples.Runtime.Tests
{
    internal class RuntimeUITestFixtureExample
    {
        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region BasicUIDocumentRuntimeExampleClass
        // This class is contained in a runtime test assembly.
        public class BasicUIDocumentRuntimeExampleClass : RuntimeUITestFixture
        {
            [UnityOneTimeSetUp]
            public IEnumerator UnityOneTimeSetUp()
            {
                // Load the scene and yield a player
                // frame to ensure it is up to date.
                yield return SceneManager.LoadSceneAsync("UIDocumentTestScene", LoadSceneMode.Single);
                yield return null;
            }

            [Test]
            public void BasicRuntimeExampleTest()
            {
                // Fetch the UIDocument from the Scene.
                GameObject gameObject = GameObject.Find("UIDocument");
                UIDocument uiDocument = gameObject.GetComponent<UIDocument>();

                // Hook up the UIDocument for simulation.
                SetUIContent(uiDocument);

                simulate.FrameUpdate();

                Button button = rootVisualElement.Q<Button>("MyButton");
                Assert.That(button, Is.Not.Null);

                // Test steps.
                // ...
            }
        }
        #endregion


        // Important: This code appears in our package documentation.
        // If you need to disable it, please do so outside the #region tag.
        #region BasicPanelRendererRuntimeExampleClass
        // This class is contained in a runtime test assembly.
        public class BasicPanelRendererRuntimeExampleClass : RuntimeUITestFixture
        {
            [UnityOneTimeSetUp]
            public IEnumerator UnityOneTimeSetUp()
            {
                // Load the scene and yield a player
                // frame to ensure it is up to date.
                yield return SceneManager.LoadSceneAsync("PanelRendererTestScene", LoadSceneMode.Single);
                yield return null;
            }

            [Test]
            public void BasicRuntimeExampleTest()
            {
                // Fetch the PanelRenderer from the Scene.
                PanelRenderer panelRenderer = GameObject.FindFirstObjectByType<PanelRenderer>();

                // Hook up the PanelRenderer for simulation.
                SetPanelRenderer(panelRenderer);

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
