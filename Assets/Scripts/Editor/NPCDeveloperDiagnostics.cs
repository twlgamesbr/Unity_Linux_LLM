using System;
using System.IO;
using System.Linq;
using Unity.Profiling.Memory;
using Unity.ProjectAuditor.Editor;
using Unity.ProjectAuditor.Editor.Core;
using UnityEditor;
using UnityEditor.TestTools.CodeCoverage;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace NPCSystem.Editor
{
    public static class NPCDeveloperDiagnostics
    {
        const string DiagnosticsRootName = "Diagnostics";
        const string CoverageDirectoryName = "CodeCoverage";
        const string MemoryDirectoryName = "MemorySnapshots";
        const string AuditorDirectoryName = "ProjectAuditor";
        const string LogDirectoryName = "Logs";
        const string CoverageWindowMenuItem = "Window/Analysis/Code Coverage";
        const string ProfileAnalyzerWindowMenuItem = "Window/Analysis/Profile Analyzer";
        const string MemoryProfilerWindowMenuItem = "Window/Analysis/Memory Profiler";
        const string EditModeTestResultsRelativePath = "Diagnostics/Logs/EditMode-test-results.xml";
        const string EditModeTestResultsFileName = "EditMode-test-results.xml";
        static readonly string[] ProjectEditModeTestAssemblies = { "NPCSystem.Tests" };
        static readonly SerializableEnum<IssueCategory>[] AllIssueCategories = Enum.GetValues(
                typeof(IssueCategory)
            )
            .Cast<IssueCategory>()
            .Select(category => new SerializableEnum<IssueCategory>(category))
            .ToArray();

        static bool s_AuditInProgress;

        public static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        public static string DiagnosticsRoot =>
            EnsureDirectory(Path.Combine(ProjectRoot, DiagnosticsRootName));
        public static string CoverageDirectory =>
            EnsureDirectory(Path.Combine(DiagnosticsRoot, CoverageDirectoryName));
        public static string MemorySnapshotDirectory =>
            EnsureDirectory(Path.Combine(DiagnosticsRoot, MemoryDirectoryName));
        public static string ProjectAuditorDirectory =>
            EnsureDirectory(Path.Combine(DiagnosticsRoot, AuditorDirectoryName));
        public static string LogDirectory =>
            EnsureDirectory(Path.Combine(DiagnosticsRoot, LogDirectoryName));
        public static string EditModeTestResultsPath =>
            Path.Combine(LogDirectory, EditModeTestResultsFileName);

        public static bool OpenCodeCoverageWindow() =>
            EditorApplication.ExecuteMenuItem(CoverageWindowMenuItem);

        public static bool OpenProfileAnalyzerWindow() =>
            EditorApplication.ExecuteMenuItem(ProfileAnalyzerWindowMenuItem);

        public static bool OpenMemoryProfilerWindow() =>
            EditorApplication.ExecuteMenuItem(MemoryProfilerWindowMenuItem);

        public static void StartCoverageRecording()
        {
            CodeCoverage.VerbosityLevel = LogVerbosityLevel.Info;
            CodeCoverage.StartRecording();
            Debug.Log("[NPC Diagnostics] Code Coverage recording started.");
        }

        public static void PauseCoverageRecording()
        {
            CodeCoverage.PauseRecording();
            Debug.Log("[NPC Diagnostics] Code Coverage recording paused.");
        }

        public static void ResumeCoverageRecording()
        {
            CodeCoverage.UnpauseRecording();
            Debug.Log("[NPC Diagnostics] Code Coverage recording resumed.");
        }

        public static void StopCoverageRecording()
        {
            CodeCoverage.StopRecording();
            Debug.Log(
                "[NPC Diagnostics] Code Coverage recording stopped and report generation requested."
            );
        }

        public static string BuildCoverageCommand()
        {
            string editorPath = EditorApplication.applicationPath;
            string testResultsPath = Path.Combine(LogDirectory, "editmode-test-results.xml");
            string editorLogPath = Path.Combine(LogDirectory, "editmode-coverage.log");
            string coverageOptions =
                "generateHtmlReport;generateAdditionalMetrics;generateBadgeReport;generateAdditionalReports;"
                + "assemblyFilters:+NPCSystem.Runtime,+NPCSystem.Editor,+NPCSystem.Tests,+Assembly-CSharp,+Assembly-CSharp-Editor;"
                + "pathFilters:+Assets/Scripts";

            return $"{QuoteShell(editorPath)} "
                + $"-batchmode -quit -projectPath {QuoteShell(ProjectRoot)} "
                + "-runTests -testPlatform editmode "
                + $"-testResults {QuoteShell(testResultsPath)} "
                + $"-logFile {QuoteShell(editorLogPath)} "
                + "-enableCodeCoverage "
                + $"-coverageResultsPath {QuoteShell(CoverageDirectory)} "
                + $"-coverageOptions {QuoteShell(coverageOptions)}";
        }

        public static void RunProjectAuditorInteractive()
        {
            RunProjectAuditorInternal(revealReport: true, quitEditorOnCompletion: false);
        }

        public static void RunProjectAuditorBatch()
        {
            RunProjectAuditorInternal(revealReport: false, quitEditorOnCompletion: true);
        }

        public static void RunEditModeTestsWithCoverageBatch()
        {
            EnsureDirectory(LogDirectory);
            EnsureDirectory(CoverageDirectory);

            if (File.Exists(EditModeTestResultsPath))
            {
                File.Delete(EditModeTestResultsPath);
            }

            ClearCoverageRecordingDirectory();

            var callbacks = new BatchEditModeTestCallbacks();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunnerApi.RegisterCallbacks(callbacks);

            CodeCoverage.VerbosityLevel = LogVerbosityLevel.Info;
            CodeCoverage.StartRecording();

            var executionSettings = new ExecutionSettings(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = ProjectEditModeTestAssemblies,
                }
            )
            {
                runSynchronously = true,
            };

            try
            {
                testRunnerApi.Execute(executionSettings);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SafeStopCoverage();
                EditorApplication.Exit(1);
                throw;
            }
            finally
            {
                testRunnerApi.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
            }
        }

        public static void TakeMemorySnapshot()
        {
            string outputPath = Path.Combine(
                MemorySnapshotDirectory,
                $"memory-{DateTime.Now:yyyyMMdd-HHmmss}.snap"
            );

            CaptureFlags flags =
                CaptureFlags.ManagedObjects
                | CaptureFlags.NativeObjects
                | CaptureFlags.NativeAllocations
                | CaptureFlags.NativeAllocationSites;

            MemoryProfiler.TakeSnapshot(
                outputPath,
                (path, succeeded) =>
                {
                    if (succeeded)
                    {
                        Debug.Log($"[NPC Diagnostics] Memory snapshot saved to {path}");
                        EditorUtility.RevealInFinder(path);
                        return;
                    }

                    Debug.LogError($"[NPC Diagnostics] Memory snapshot failed for {path}");
                },
                flags
            );
        }

        public static void RevealDiagnosticsRoot()
        {
            EditorUtility.RevealInFinder(DiagnosticsRoot);
        }

        static void RunProjectAuditorInternal(bool revealReport, bool quitEditorOnCompletion)
        {
            if (s_AuditInProgress)
            {
                Debug.LogWarning("[NPC Diagnostics] Project Auditor is already running.");
                return;
            }

            s_AuditInProgress = true;
            string reportPath = Path.Combine(
                ProjectAuditorDirectory,
                $"project-auditor-{DateTime.Now:yyyyMMdd-HHmmss}.projectauditor"
            );

            int issueCount = 0;
            var analysisParams = new AnalysisParams(false)
            {
                Categories = AllIssueCategories,
                CodeOptimization = CodeOptimization.Debug,
                OnStarted = (_, _, _) =>
                    Debug.Log("[NPC Diagnostics] Project Auditor analysis started."),
                OnIncomingIssues = issues => issueCount += issues.Count(),
                OnCompleted = report =>
                {
                    try
                    {
                        report.Save(reportPath);
                        Debug.Log(
                            $"[NPC Diagnostics] Project Auditor saved {issueCount} issues to {reportPath}"
                        );
                        if (revealReport)
                        {
                            EditorUtility.RevealInFinder(reportPath);
                        }
                    }
                    finally
                    {
                        s_AuditInProgress = false;
                        if (quitEditorOnCompletion)
                        {
                            EditorApplication.Exit(0);
                        }
                    }
                },
            };

            new ProjectAuditor().AuditAsync(analysisParams, null);
        }

        static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        static string QuoteShell(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        static void SafeStopCoverage()
        {
            try
            {
                CodeCoverage.StopRecording();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        static void ClearCoverageRecordingDirectory()
        {
            string recordingDirectory = Path.Combine(
                CoverageDirectory,
                $"{Application.productName}-opencov",
                "Recording"
            );
            if (!Directory.Exists(recordingDirectory))
            {
                return;
            }

            try
            {
                Directory.Delete(recordingDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[NPC Diagnostics] Failed to clear stale coverage recording directory: {ex.Message}"
                );
            }
        }

        sealed class BatchEditModeTestCallbacks : ICallbacks
        {
            int _failureCount;

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[NPC Diagnostics] Running EditMode tests: {testsToRun?.Name}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, EditModeTestResultsRelativePath);
                    Debug.Log(
                        $"[NPC Diagnostics] Saved EditMode test results to {EditModeTestResultsPath}"
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    _failureCount++;
                }
                finally
                {
                    SafeStopCoverage();
                    int exitCode =
                        _failureCount > 0 || result == null || result.FailCount > 0 ? 1 : 0;
                    EditorApplication.Exit(exitCode);
                }
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result != null && result.TestStatus == TestStatus.Failed)
                {
                    _failureCount++;
                    Debug.LogError(
                        $"[NPC Diagnostics] Failed test: {result.FullName}\n{result.Message}"
                    );
                }
            }
        }
    }
}
