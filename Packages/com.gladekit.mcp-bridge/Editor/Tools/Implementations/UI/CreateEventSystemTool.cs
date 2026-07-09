
#if GLADE_INPUT_SYSTEM
#endif

#if GLADE_UGUI
namespace GladeAgenticAI.Core.Tools.Implementations.UI
{
    public class CreateEventSystemTool : ITool
    {
        public string Name => "create_event_system";

        public string Execute(Dictionary<string, object> args)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                return ToolUtils.CreateSuccessResponse("EventSystem already exists");
            }

            UnityEngine.GameObject obj = new UnityEngine.GameObject("EventSystem");
            obj.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && GLADE_INPUT_SYSTEM
            obj.AddComponent<InputSystemUIInputModule>();
#else
            obj.AddComponent<StandaloneInputModule>();
#endif

            Undo.RegisterCreatedObjectUndo(obj, "Create EventSystem");
            
            var extras = new Dictionary<string, object>
            {
                { "gameObjectPath", ToolUtils.GetGameObjectPath(obj) }
            };
            
            return ToolUtils.CreateSuccessResponse("Created EventSystem", extras);
        }
    }
}
#endif
