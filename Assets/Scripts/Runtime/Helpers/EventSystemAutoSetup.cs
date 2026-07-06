using System;
using System.Reflection;
using UnityEngine;

namespace NPCSystem
{
    [RequireComponent(typeof(EventSystem))]
    [DefaultExecutionOrder(-1000)]
    public class EventSystemAutoSetup : MonoBehaviour
    {
        void Awake()
        {
            SetupInputModule();
        }

        private void SetupInputModule()
        {
            var eventSystem = GetComponent<EventSystem>();

            Type inputSystemModuleType = FindInputSystemModuleType();

            if (inputSystemModuleType != null)
            {
                var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (oldModule != null)
                    DestroyImmediate(oldModule);

                Component inputModule = eventSystem.GetComponent(inputSystemModuleType);
                if (inputModule == null)
                    inputModule = eventSystem.gameObject.AddComponent(inputSystemModuleType);

                EnsureDefaultActions(inputModule);
            }
            else
            {
                Type newModuleType = Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"
                );
                if (newModuleType != null)
                {
                    var newModule = eventSystem.GetComponent(newModuleType);
                    if (newModule != null)
                        DestroyImmediate(newModule);
                }

                if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                    eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private Type FindInputSystemModuleType()
        {
            return Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"
            );
        }

        private void EnsureDefaultActions(Component inputModule)
        {
            if (inputModule == null)
                return;

            MethodInfo assignDefaultActions = inputModule
                .GetType()
                .GetMethod(
                    "AssignDefaultActions",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
            assignDefaultActions?.Invoke(inputModule, null);
        }
    }
}
