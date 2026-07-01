using UnityEngine;
using UnityEngine.UI;

namespace NPCSystem
{
    public class NotebookUIController : MonoBehaviour
    {
        [Header("Notebook / Panels")]
        public Button notesButton;
        public Button mapButton;
        public Button solveButton;
        public Button helpButton;
        public Button submitButton;
        public RawImage notebookImage;
        public GameObject notesPanel;
        public GameObject solvePanel;
        public GameObject helpPanel;
        public RawImage mapImage;
        public RawImage successImage;
        public Text failText;
        public Dropdown answer1;
        public Dropdown answer2;
        public Dropdown answer3;

        [Header("Solve Answers")]
        public string correctAnswer1 = "Professor Pluot";
        public string correctAnswer2 = "Living Room";
        public string correctAnswer3 = "A Hollow Bible";

        bool _listenersBound;

        void Awake()
        {
            ResolveReferences();
            BindListeners();
        }

        void OnDestroy()
        {
            UnbindListeners();
        }

        void ResolveReferences()
        {
            notesButton = notesButton != null ? notesButton : FindComponent<Button>("Canvas/NotesButton");
            mapButton = mapButton != null ? mapButton : FindComponent<Button>("Canvas/MapButton");
            solveButton = solveButton != null ? solveButton : FindComponent<Button>("Canvas/SolveButton");
            helpButton = helpButton != null ? helpButton : FindComponent<Button>("Canvas/HelpButton");
            submitButton = submitButton != null ? submitButton : FindComponent<Button>("Canvas/NotebookImage/SolvePanel/SubmitButton");
            notebookImage = notebookImage != null ? notebookImage : FindComponent<RawImage>("Canvas/NotebookImage");
            notesPanel = notesPanel != null ? notesPanel : FindObject("Canvas/NotebookImage/NotesPanel");
            solvePanel = solvePanel != null ? solvePanel : FindObject("Canvas/NotebookImage/SolvePanel");
            helpPanel = helpPanel != null ? helpPanel : FindObject("Canvas/NotebookImage/HelpPanel");
            mapImage = mapImage != null ? mapImage : FindComponent<RawImage>("Canvas/MapImage");
            successImage = successImage != null ? successImage : FindComponent<RawImage>("Canvas/SuccessImage");
            failText = failText != null ? failText : FindComponent<Text>("Canvas/NotebookImage/SolvePanel/FailText");
            answer1 = answer1 != null ? answer1 : FindComponent<Dropdown>("Canvas/NotebookImage/SolvePanel/Answer1");
            answer2 = answer2 != null ? answer2 : FindComponent<Dropdown>("Canvas/NotebookImage/SolvePanel/Answer2");
            answer3 = answer3 != null ? answer3 : FindComponent<Dropdown>("Canvas/NotebookImage/SolvePanel/Answer3");
        }

        void BindListeners()
        {
            if (_listenersBound) return;

            if (notesButton != null) notesButton.onClick.AddListener(ShowNotes);
            if (mapButton != null) mapButton.onClick.AddListener(ShowMap);
            if (solveButton != null) solveButton.onClick.AddListener(ShowSolve);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (submitButton != null) submitButton.onClick.AddListener(SubmitAnswer);
            if (answer1 != null) answer1.onValueChanged.AddListener(HideFail);
            if (answer2 != null) answer2.onValueChanged.AddListener(HideFail);
            if (answer3 != null) answer3.onValueChanged.AddListener(HideFail);

            _listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!_listenersBound) return;

            if (notesButton != null) notesButton.onClick.RemoveListener(ShowNotes);
            if (mapButton != null) mapButton.onClick.RemoveListener(ShowMap);
            if (solveButton != null) solveButton.onClick.RemoveListener(ShowSolve);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (submitButton != null) submitButton.onClick.RemoveListener(SubmitAnswer);
            if (answer1 != null) answer1.onValueChanged.RemoveListener(HideFail);
            if (answer2 != null) answer2.onValueChanged.RemoveListener(HideFail);
            if (answer3 != null) answer3.onValueChanged.RemoveListener(HideFail);

            _listenersBound = false;
        }

        public void ShowNotes()
        {
            if (notesPanel != null) notesPanel.SetActive(true);
            if (helpPanel != null) helpPanel.SetActive(false);
            if (solvePanel != null) solvePanel.SetActive(false);
            if (notebookImage != null) notebookImage.gameObject.SetActive(true);
        }

        public void ShowMap()
        {
            if (mapImage != null) mapImage.gameObject.SetActive(true);
        }

        public void ShowSolve()
        {
            HideFail(0);
            if (notesPanel != null) notesPanel.SetActive(false);
            if (helpPanel != null) helpPanel.SetActive(false);
            if (solvePanel != null) solvePanel.SetActive(true);
            if (notebookImage != null) notebookImage.gameObject.SetActive(true);
        }

        public void ShowHelp()
        {
            if (notesPanel != null) notesPanel.SetActive(false);
            if (helpPanel != null) helpPanel.SetActive(true);
            if (solvePanel != null) solvePanel.SetActive(false);
            if (notebookImage != null) notebookImage.gameObject.SetActive(true);
        }

        public void HideFail(int selection)
        {
            if (failText != null) failText.gameObject.SetActive(false);
        }

        public void SubmitAnswer()
        {
            if (answer1 == null || answer2 == null || answer3 == null || successImage == null) return;

            if (SelectedText(answer1) == correctAnswer1
                && SelectedText(answer2) == correctAnswer2
                && SelectedText(answer3) == correctAnswer3)
            {
                NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.UIInput, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "Mystery solve answer accepted.", source: nameof(NotebookUIController));
                if (notebookImage != null) notebookImage.gameObject.SetActive(false);
                successImage.gameObject.SetActive(true);
            }
            else if (failText != null)
            {
                NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.UIInput, NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                    "Mystery solve answer rejected.", source: nameof(NotebookUIController));
                failText.gameObject.SetActive(true);
            }
        }

        public void HandleGlobalClick(Vector2 mousePosition)
        {
            foreach (RawImage image in new[] { notebookImage, mapImage, successImage })
            {
                if (image != null && image.IsActive() && !RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, mousePosition))
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        static string SelectedText(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0) return string.Empty;
            int value = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            return dropdown.options[value].text;
        }

        static T FindComponent<T>(string path) where T : Component
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
