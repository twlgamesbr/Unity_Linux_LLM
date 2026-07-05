using UnityEditor;
using UnityEngine;

namespace NPCSystem.Editor
{
    public sealed class NPCDeveloperDiagnosticsWindow : EditorWindow
    {
        Vector2 _scrollPosition;

        [MenuItem("NPC/Diagnostics/Developer Diagnostics")]
        static void OpenWindow()
        {
            var window = GetWindow<NPCDeveloperDiagnosticsWindow>();
            window.titleContent = new GUIContent("NPC Diagnostics");
            window.minSize = new Vector2(640f, 520f);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Developer Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this window to keep code intelligence, coverage, memory capture, and static auditing close to the project instead of relying on package menus alone.",
                MessageType.Info);

            DrawPathSection();
            EditorGUILayout.Space(8f);
            DrawWindowSection();
            EditorGUILayout.Space(8f);
            DrawCoverageSection();
            EditorGUILayout.Space(8f);
            DrawAuditorSection();
            EditorGUILayout.Space(8f);
            DrawMemorySection();
        }

        void DrawPathSection()
        {
            EditorGUILayout.LabelField("Output Paths", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(NPCDeveloperDiagnostics.DiagnosticsRoot, EditorStyles.textField, GUILayout.Height(18f));
            EditorGUILayout.SelectableLabel(NPCDeveloperDiagnostics.CoverageDirectory, EditorStyles.textField, GUILayout.Height(18f));
            EditorGUILayout.SelectableLabel(NPCDeveloperDiagnostics.ProjectAuditorDirectory, EditorStyles.textField, GUILayout.Height(18f));
            EditorGUILayout.SelectableLabel(NPCDeveloperDiagnostics.MemorySnapshotDirectory, EditorStyles.textField, GUILayout.Height(18f));
            if (GUILayout.Button("Open Diagnostics Folder"))
            {
                NPCDeveloperDiagnostics.RevealDiagnosticsRoot();
            }
        }

        void DrawWindowSection()
        {
            EditorGUILayout.LabelField("Unity Analysis Windows", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Code Coverage"))
                {
                    NPCDeveloperDiagnostics.OpenCodeCoverageWindow();
                }

                if (GUILayout.Button("Profile Analyzer"))
                {
                    NPCDeveloperDiagnostics.OpenProfileAnalyzerWindow();
                }

                if (GUILayout.Button("Memory Profiler"))
                {
                    NPCDeveloperDiagnostics.OpenMemoryProfilerWindow();
                }
            }
        }

        void DrawCoverageSection()
        {
            EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Recording"))
                {
                    NPCDeveloperDiagnostics.StartCoverageRecording();
                }

                if (GUILayout.Button("Pause"))
                {
                    NPCDeveloperDiagnostics.PauseCoverageRecording();
                }

                if (GUILayout.Button("Resume"))
                {
                    NPCDeveloperDiagnostics.ResumeCoverageRecording();
                }

                if (GUILayout.Button("Stop + Report"))
                {
                    NPCDeveloperDiagnostics.StopCoverageRecording();
                }
            }

            EditorGUILayout.LabelField("Batch Command", EditorStyles.miniBoldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(96f));
            EditorGUILayout.SelectableLabel(NPCDeveloperDiagnostics.BuildCoverageCommand(), EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy Coverage Command"))
            {
                EditorGUIUtility.systemCopyBuffer = NPCDeveloperDiagnostics.BuildCoverageCommand();
            }
        }

        void DrawAuditorSection()
        {
            EditorGUILayout.LabelField("Project Auditor", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Auditor and Export Report"))
            {
                NPCDeveloperDiagnostics.RunProjectAuditorInteractive();
            }
        }

        void DrawMemorySection()
        {
            EditorGUILayout.LabelField("Memory Snapshot", EditorStyles.boldLabel);
            if (GUILayout.Button("Capture Memory Snapshot"))
            {
                NPCDeveloperDiagnostics.TakeMemorySnapshot();
            }
        }
    }
}
