using EditorAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Void = EditorAttributes.Void;

namespace NPCSystem
{
    public class NotebookUIController : MonoBehaviour
    {
        [HelpBox(
            "Manages the in-game notebook/map/solve UI panels. Subscribes to NPCDialogueManager or NPCDialogueNetworkBridge to reflect evidence state in notes text.",
            MessageMode.Log,
            drawAbove: true
        )]
        [SerializeField]
        Void _docsGroup;

        [FoldoutGroup("References", true, nameof(dialogueManager), nameof(networkBridge))]
        [SerializeField]
        Void referencesGroup;

        [SerializeField, HideProperty, Required]
        public NPCDialogueManager dialogueManager;

        [SerializeField, HideProperty]
        public NPCDialogueNetworkBridge networkBridge;

        [FoldoutGroup(
            "Notebook Buttons",
            true,
            nameof(notesButton),
            nameof(mapButton),
            nameof(solveButton),
            nameof(helpButton),
            nameof(submitButton)
        )]
        [SerializeField]
        Void notebookButtonsGroup;

        [SerializeField, HideProperty, Required]
        public Button notesButton;

        [SerializeField, HideProperty]
        public Button mapButton;

        [SerializeField, HideProperty]
        public Button solveButton;

        [SerializeField, HideProperty]
        public Button helpButton;

        [SerializeField, HideProperty]
        public Button submitButton;

        [FoldoutGroup(
            "Notebook UI Panels",
            true,
            nameof(notebookImage),
            nameof(notesPanel),
            nameof(solvePanel),
            nameof(helpPanel),
            nameof(mapImage),
            nameof(successImage),
            nameof(failText)
        )]
        [SerializeField]
        Void notebookPanelsGroup;

        [SerializeField, HideProperty, Required]
        public RawImage notebookImage;

        [SerializeField, HideProperty]
        public GameObject notesPanel;

        [SerializeField, HideProperty]
        public GameObject solvePanel;

        [SerializeField, HideProperty]
        public GameObject helpPanel;

        [SerializeField, HideProperty]
        public RawImage mapImage;

        [SerializeField, HideProperty]
        public RawImage successImage;

        [SerializeField, HideProperty]
        public TMP_Text failText;

        [FoldoutGroup("Dropdown Answers", true, nameof(answer1), nameof(answer2), nameof(answer3))]
        [SerializeField]
        Void dropdownAnswersGroup;

        [SerializeField, HideProperty]
        public TMP_Dropdown answer1;

        [SerializeField, HideProperty]
        public TMP_Dropdown answer2;

        [SerializeField, HideProperty]
        public TMP_Dropdown answer3;

        [FoldoutGroup("Notes Text", true, nameof(notesText1), nameof(notesText2))]
        [SerializeField]
        Void notesTextGroup;

        [SerializeField, HideProperty, Required]
        public TMP_Text notesText1;

        [SerializeField, HideProperty, Required]
        public TMP_Text notesText2;

        [Title("Correct Solve Answers")]
        [SerializeField]
        public string correctAnswer1 = "Professor Pluot";

        [SerializeField]
        public string correctAnswer2 = "Living Room";

        [SerializeField]
        public string correctAnswer3 = "A Hollow Bible";

        [Title("Runtime Status")]
        [ShowInInspector, ReadOnly]
        string CurrentNpcSlug =>
            networkBridge?.currentProfile?.GetNpcSlug()
            ?? dialogueManager?.currentProfile?.GetNpcSlug()
            ?? "<none>";

        [ShowInInspector, ReadOnly]
        bool HasEvidence =>
            (dialogueManager?.CaptureEvidenceSnapshot() ?? new NPCEvidenceStateSnapshot())
                .discoveredClues
                .Count > 0
            || (dialogueManager?.CaptureEvidenceSnapshot() ?? new NPCEvidenceStateSnapshot())
                .obtainedItems
                .Count > 0;

        string _defaultNotesPageLeft = string.Empty;
        string _defaultNotesPageRight = string.Empty;
        bool _listenersBound;
        bool _runtimeEventsBound;
        int _overlayOpenedFrame = -1;

        void Awake()
        {
            ResolveReferences();
            CacheDefaultNotebookText();
            BindListeners();
            BindRuntimeEvents();
            RefreshNotebookState();
        }

        void OnDestroy()
        {
            UnbindRuntimeEvents();
            UnbindListeners();
        }

        [Button("Refresh Notebook State")]
        void RefreshNotebookStateButton()
        {
            RefreshNotebookState();
        }

        [Button("Reset Notebook to Defaults")]
        void ResetNotebookToDefaults()
        {
            ApplyNotebookState(
                new NPCNotebookStateMessage
                {
                    notesPageLeft = _defaultNotesPageLeft,
                    notesPageRight = _defaultNotesPageRight,
                }
            );
        }

        void ResolveReferences()
        {
            dialogueManager =
                dialogueManager != null
                    ? dialogueManager
                    : FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
            networkBridge =
                networkBridge != null
                    ? networkBridge
                    : FindAnyObjectByType<NPCDialogueNetworkBridge>(FindObjectsInactive.Include);
            notesButton =
                notesButton != null ? notesButton : FindComponent<Button>("Canvas/NotesButton");
            mapButton = mapButton != null ? mapButton : FindComponent<Button>("Canvas/MapButton");
            solveButton =
                solveButton != null ? solveButton : FindComponent<Button>("Canvas/SolveButton");
            helpButton =
                helpButton != null ? helpButton : FindComponent<Button>("Canvas/HelpButton");
            submitButton =
                submitButton != null
                    ? submitButton
                    : FindComponent<Button>("Canvas/NotebookImage/SolvePanel/SubmitButton");
            notebookImage =
                notebookImage != null
                    ? notebookImage
                    : FindComponent<RawImage>("Canvas/NotebookImage");
            notesPanel =
                notesPanel != null ? notesPanel : FindObject("Canvas/NotebookImage/NotesPanel");
            solvePanel =
                solvePanel != null ? solvePanel : FindObject("Canvas/NotebookImage/SolvePanel");
            helpPanel =
                helpPanel != null ? helpPanel : FindObject("Canvas/NotebookImage/HelpPanel");
            mapImage = mapImage != null ? mapImage : FindComponent<RawImage>("Canvas/MapImage");
            successImage =
                successImage != null
                    ? successImage
                    : FindComponent<RawImage>("Canvas/SuccessImage");
            failText =
                failText != null
                    ? failText
                    : FindComponent<TMP_Text>("Canvas/NotebookImage/SolvePanel/FailText");
            answer1 =
                answer1 != null
                    ? answer1
                    : FindComponent<TMP_Dropdown>("Canvas/NotebookImage/SolvePanel/Answer1");
            answer2 =
                answer2 != null
                    ? answer2
                    : FindComponent<TMP_Dropdown>("Canvas/NotebookImage/SolvePanel/Answer2");
            answer3 =
                answer3 != null
                    ? answer3
                    : FindComponent<TMP_Dropdown>("Canvas/NotebookImage/SolvePanel/Answer3");
            notesText1 =
                notesText1 != null
                    ? notesText1
                    : FindComponent<TMP_Text>("Canvas/NotebookImage/NotesPanel/NotesText1");
            notesText2 =
                notesText2 != null
                    ? notesText2
                    : FindComponent<TMP_Text>("Canvas/NotebookImage/NotesPanel/NotesText2");
        }

        void CacheDefaultNotebookText()
        {
            _defaultNotesPageLeft = notesText1 != null ? notesText1.text : string.Empty;
            _defaultNotesPageRight = notesText2 != null ? notesText2.text : string.Empty;
        }

        void BindListeners()
        {
            if (_listenersBound)
                return;

            if (notesButton != null)
                notesButton.onClick.AddListener(ShowNotes);
            if (mapButton != null)
                mapButton.onClick.AddListener(ShowMap);
            if (solveButton != null)
                solveButton.onClick.AddListener(ShowSolve);
            if (helpButton != null)
                helpButton.onClick.AddListener(ShowHelp);
            if (submitButton != null)
                submitButton.onClick.AddListener(SubmitAnswer);
            if (answer1 != null)
                answer1.onValueChanged.AddListener(HideFail);
            if (answer2 != null)
                answer2.onValueChanged.AddListener(HideFail);
            if (answer3 != null)
                answer3.onValueChanged.AddListener(HideFail);

            _listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!_listenersBound)
                return;

            if (notesButton != null)
                notesButton.onClick.RemoveListener(ShowNotes);
            if (mapButton != null)
                mapButton.onClick.RemoveListener(ShowMap);
            if (solveButton != null)
                solveButton.onClick.RemoveListener(ShowSolve);
            if (helpButton != null)
                helpButton.onClick.RemoveListener(ShowHelp);
            if (submitButton != null)
                submitButton.onClick.RemoveListener(SubmitAnswer);
            if (answer1 != null)
                answer1.onValueChanged.RemoveListener(HideFail);
            if (answer2 != null)
                answer2.onValueChanged.RemoveListener(HideFail);
            if (answer3 != null)
                answer3.onValueChanged.RemoveListener(HideFail);

            _listenersBound = false;
        }

        void BindRuntimeEvents()
        {
            if (_runtimeEventsBound)
                return;

            if (networkBridge != null)
            {
                networkBridge.onNotebookStateChanged.AddListener(ApplyNotebookState);
                _runtimeEventsBound = true;
            }
            else if (dialogueManager != null)
            {
                dialogueManager.onNPCChanged.AddListener(HandleNpcChanged);
                dialogueManager.onResponseComplete.AddListener(HandleResponseComplete);
                _runtimeEventsBound = true;
            }
        }

        void UnbindRuntimeEvents()
        {
            if (!_runtimeEventsBound)
                return;

            if (networkBridge != null)
            {
                networkBridge.onNotebookStateChanged.RemoveListener(ApplyNotebookState);
            }

            if (dialogueManager != null)
            {
                dialogueManager.onNPCChanged.RemoveListener(HandleNpcChanged);
                dialogueManager.onResponseComplete.RemoveListener(HandleResponseComplete);
            }

            _runtimeEventsBound = false;
        }

        public void ShowNotes()
        {
            MarkOverlayOpenedThisFrame();
            RefreshNotebookState();
            if (notesPanel != null)
                notesPanel.SetActive(true);
            if (helpPanel != null)
                helpPanel.SetActive(false);
            if (solvePanel != null)
                solvePanel.SetActive(false);
            if (notebookImage != null)
                notebookImage.gameObject.SetActive(true);
        }

        public void ShowMap()
        {
            MarkOverlayOpenedThisFrame();
            if (mapImage != null)
                mapImage.gameObject.SetActive(true);
        }

        public void ShowSolve()
        {
            MarkOverlayOpenedThisFrame();
            HideFail(0);
            if (notesPanel != null)
                notesPanel.SetActive(false);
            if (helpPanel != null)
                helpPanel.SetActive(false);
            if (solvePanel != null)
                solvePanel.SetActive(true);
            if (notebookImage != null)
                notebookImage.gameObject.SetActive(true);
        }

        public void ShowHelp()
        {
            MarkOverlayOpenedThisFrame();
            if (notesPanel != null)
                notesPanel.SetActive(false);
            if (helpPanel != null)
                helpPanel.SetActive(true);
            if (solvePanel != null)
                solvePanel.SetActive(false);
            if (notebookImage != null)
                notebookImage.gameObject.SetActive(true);
        }

        public void HideFail(int selection)
        {
            if (failText != null)
                failText.gameObject.SetActive(false);
        }

        public void SubmitAnswer()
        {
            if (answer1 == null || answer2 == null || answer3 == null || successImage == null)
                return;

            if (
                SelectedText(answer1) == correctAnswer1
                && SelectedText(answer2) == correctAnswer2
                && SelectedText(answer3) == correctAnswer3
            )
            {
                MarkOverlayOpenedThisFrame();
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.UIInput,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Info,
                        "Mystery solve answer accepted.",
                        source: nameof(NotebookUIController)
                    );
                if (notebookImage != null)
                    notebookImage.gameObject.SetActive(false);
                successImage.gameObject.SetActive(true);
            }
            else if (failText != null)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.UIInput,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        "Mystery solve answer rejected.",
                        source: nameof(NotebookUIController)
                    );
                failText.gameObject.SetActive(true);
            }
        }

        public void HandleGlobalClick(Vector2 mousePosition)
        {
            // Button onClick is processed by EventSystem.Update before MonoBehaviour.Update.
            // Without this guard, the same click that opens Notes/Map/Solve/Help is seen here
            // as an "outside overlay" click and immediately closes the overlay again.
            if (_overlayOpenedFrame == Time.frameCount)
                return;

            foreach (RawImage image in new[] { notebookImage, mapImage, successImage })
            {
                if (
                    image != null
                    && image.IsActive()
                    && !RectTransformUtility.RectangleContainsScreenPoint(
                        image.rectTransform,
                        mousePosition
                    )
                )
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        void MarkOverlayOpenedThisFrame()
        {
            _overlayOpenedFrame = Time.frameCount;
        }

        void HandleNpcChanged(string npcName)
        {
            RefreshNotebookState();
        }

        void HandleResponseComplete(string npcName, string response)
        {
            RefreshNotebookState();
        }

        void RefreshNotebookState()
        {
            if (networkBridge != null)
            {
                ApplyNotebookState(networkBridge.CurrentNotebookState);
                return;
            }

            if (dialogueManager == null)
            {
                ApplyNotebookState(
                    new NPCNotebookStateMessage
                    {
                        notesPageLeft = _defaultNotesPageLeft,
                        notesPageRight = _defaultNotesPageRight,
                    }
                );
                return;
            }

            NPCNotebookStateMessage message = NPCNotebookStateFormatter.Build(
                dialogueManager.CaptureEvidenceSnapshot(),
                dialogueManager.currentProfile != null
                    ? dialogueManager.currentProfile.GetNpcSlug()
                    : string.Empty
            );
            ApplyNotebookState(message);
        }

        void ApplyNotebookState(NPCNotebookStateMessage message)
        {
            if (notesText1 != null)
            {
                notesText1.text = string.IsNullOrWhiteSpace(message.notesPageLeft)
                    ? _defaultNotesPageLeft
                    : message.notesPageLeft;
            }

            if (notesText2 != null)
            {
                notesText2.text = string.IsNullOrWhiteSpace(message.notesPageRight)
                    ? _defaultNotesPageRight
                    : message.notesPageRight;
            }
        }

        static string SelectedText(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return string.Empty;
            int value = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            return dropdown.options[value].text;
        }

        static T FindComponent<T>(string path)
            where T : Component
        {
            GameObject gameObject = FindObject(path);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        static GameObject FindObject(string path)
        {
            return GameObject.Find(path);
        }
    }
}
