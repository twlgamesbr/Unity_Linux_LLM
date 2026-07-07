using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements.TestFrameworkDocCodeSamples.Editor.Tests
{
    internal class UITestFrameworkDocSampleWindow : EditorWindow
    {
        [MenuItem("Window/UI Toolkit/UI Test Framework/DocSampleWindow")]
        public static void ShowExample()
        {
            UITestFrameworkDocSampleWindow wnd = GetWindow<UITestFrameworkDocSampleWindow>();
            wnd.titleContent = new GUIContent("UITestFrameworkDocSampleWindow");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML.
            var m_VisualTreeAsset = Resources.Load<VisualTreeAsset>("UITestFrameworkDocSample");

            VisualElement uxml = m_VisualTreeAsset.Instantiate();
            root.Add(uxml);
        }
    }
}
