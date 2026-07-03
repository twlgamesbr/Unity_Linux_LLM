using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NPCSystem.Editor
{
    public class HeadlessPlayMode
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/NPCDialoguePrototype1.unity");
        EditorApplication.isPlaying = true;
    }
}
}
