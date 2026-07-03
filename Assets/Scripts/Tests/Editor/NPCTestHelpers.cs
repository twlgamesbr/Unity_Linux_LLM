using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NPCSystem.Tests
{
    internal static class NPCTestHelpers
    {
        public const string MainScenePath = "Assets/Scenes/NPCDialoguePrototype1.unity";

        public static T CreateComponent<T>(string name = null) where T : Component
        {
            var gameObject = new GameObject(name ?? typeof(T).Name);
            return gameObject.AddComponent<T>();
        }

        public static void Destroy(params Object[] objects)
        {
            foreach (Object obj in objects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        public static Scene OpenMainScene()
        {
            return EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        public static GameObject RequireGameObject(string path)
        {
            GameObject gameObject = GameObject.Find(path);
            if (gameObject == null)
            {
                throw new AssertionException($"Required GameObject not found in scene: {path}");
            }

            return gameObject;
        }

        public static T RequireComponent<T>(string gameObjectPath) where T : Component
        {
            GameObject gameObject = RequireGameObject(gameObjectPath);
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new AssertionException($"Required component {typeof(T).Name} not found on {gameObjectPath}.");
            }

            return component;
        }
    }
}
