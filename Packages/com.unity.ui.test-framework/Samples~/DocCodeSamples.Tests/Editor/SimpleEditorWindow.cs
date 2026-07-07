using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SimpleEditorWindow : EditorWindow
{
    [MenuItem("Window/UI Toolkit/UI Test Framework/Simple Editor Window")]
    public static void ShowExample()
    {
        SimpleEditorWindow wnd = GetWindow<SimpleEditorWindow>();
        wnd.titleContent = new GUIContent("SimpleEditorWindow");
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        Button button = new Button() { text = "Button not clicked" };
        button.clicked += () => { button.text = "Button was clicked!"; };
        root.Add(button);
    }
}
