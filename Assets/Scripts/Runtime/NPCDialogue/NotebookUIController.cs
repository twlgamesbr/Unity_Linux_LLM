using UnityEngine;

namespace NPCSystem
{
    public class NotebookUIController : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        public void ToggleNotebook()
        {
            IsOpen = !IsOpen;
        }
    }
}
