using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class EditorWindowWithCustomUpdate : EditorWindow
{
    [MenuItem("Window/UI Toolkit/UI Test Framework/Editor Window With Custom Update")]
    public static void ShowExample()
    {
        EditorWindowWithCustomUpdate wnd = GetWindow<EditorWindowWithCustomUpdate>();
        wnd.titleContent = new GUIContent("EditorWindowWithCustomUpdate");
    }

    int m_ButtonClickedCount = 0;
    public Button Button;
    public Label ButtonClickedCountLabel;
    string m_Text = "Times button was clicked: ";

    public void CreateGUI()
    {
        m_ButtonClickedCount = 0;

        VisualElement root = rootVisualElement;

        Button = new Button() { text = "Button not clicked" };
        Button.clicked += () =>
        {
            Button.text = "Button was clicked!"; m_ButtonClickedCount++;
        };
        root.Add(Button);

        ButtonClickedCountLabel = new Label()
        {
            text = m_Text + m_ButtonClickedCount
        };
        root.Add(ButtonClickedCountLabel);
    }

    public void Update()
    {
        ButtonClickedCountLabel.text = m_Text + m_ButtonClickedCount;
    }
}
