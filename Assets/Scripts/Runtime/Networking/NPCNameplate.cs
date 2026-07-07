using TMPro;
using UnityEngine;

namespace NPCSystem
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class NPCNameplate : MonoBehaviour
    {
        [Header("References")]
        public TextMeshProUGUI nameLabel;

        NPCServerCharacter _npc;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;

            if (nameLabel == null)
                nameLabel = GetComponentInChildren<TextMeshProUGUI>();

            _npc = GetComponentInParent<NPCServerCharacter>();
            if (_npc != null)
            {
                nameLabel.text = _npc.DisplayName;
                _npc.npcDisplayName.OnValueChanged += OnNpcNameChanged;
            }
        }

        void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            transform.forward = _camera.transform.forward;
        }

        void OnNpcNameChanged(string _, string newValue)
        {
            if (nameLabel != null)
                nameLabel.text = string.IsNullOrWhiteSpace(newValue) ? "NPC" : newValue;
        }

        void OnDestroy()
        {
            if (_npc != null)
                _npc.npcDisplayName.OnValueChanged -= OnNpcNameChanged;
        }
    }
}
