using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// The main window of Web Stripping Tool.
    /// </summary>
    class SubmoduleStrippingWindow : EditorWindow
    {
        // OS-specific strings similar to the other parts of the Editor
        internal const string k_RemoveBuildText = "Remove Build From List";
        internal const string k_ShowBuildFolderText =
#if UNITY_EDITOR_WIN
            "Show Build Folder in Explorer";
#elif UNITY_EDITOR_OSX
            "Reveal Build Folder in Finder";
#else
            "Open Build Folder";
#endif
        internal const string k_ShowBackUpFolderText =
#if UNITY_EDITOR_WIN
            "Show Backup Folder in Explorer";
#elif UNITY_EDITOR_OSX
            "Reveal Backup Folder in Finder";
#else
            "Open Backup Folder";
#endif

        // Path inside "Documentation~" folder to the documentation page containing the docs of this asset, no file extension.
        internal static readonly string[] k_DocumentationPages = new[]
        {
            "submodule-stripping-window-reference", // main docs
            "backup-files", // detailed information about additional and backup files
        };

        [SerializeField]
        VisualTreeAsset m_VisualTreeAsset = default;
        SubmoduleStrippingSettings m_Settings;
        SerializedObject m_SerializedSettings;
        internal MultiColumnListView m_BuildListView;
        VisualElement m_DetailPane;
        ObjectField m_ActiveSettingsField;
        Button m_StripButton;
        Button m_StripAndRunButton;
        Button m_RunButton;
        internal Button m_AddProfilingButton;
        internal const string k_AddProfilingDefaultTooltip = "Instruments a build for submodule profiling.";
        Button m_RestoreButton;
        internal VisualElement m_InstructionsContainer;
        VisualElement m_SelectSubmoduleButtonContainer;
        internal WebBuildReport SelectedBuild =>
            m_BuildListView.selectedIndex >= 0 && m_BuildListView.selectedIndex < Builds.Count
                ? Builds[m_BuildListView.selectedIndex]
                : null;

        [ExcludeFromCodeCoverage] // used only by human-driven file dialog code
        static string LastBuildPath
        {
            // default to project's root
            get => PackageSettings.GetUserSetting("SubmoduleStrippingWindow.LastBuildPath", Utils.ProjectPath);
            set => PackageSettings.SetUserSetting("SubmoduleStrippingWindow.LastBuildPath", value);
        }

        SubmoduleSelectionWindow m_SelectionWindow;

        // Convenience accessors to the global build list
        WebBuildReportList BuildList => WebBuildReportList.Instance;

        // This window's own version of the build reports in the global list
        internal List<WebBuildReport> Builds { get; set; } = new();

        [MenuItem("Window/" + SubmoduleStrippingSettings.RootMenuName + "/Submodule Stripping")]
        public static void ShowWindow() => GetWindow<SubmoduleStrippingWindow>();

        void CopyBuildsFromGlobalBuildList()
        {
            Builds = new List<WebBuildReport>(BuildList.Builds);
            m_BuildListView.itemsSource = Builds;
            m_BuildListView.Rebuild();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Submodule Stripping");
            minSize = new Vector2(1000, 250);
            BuildList.BuildsUpdated += OnBuildsUpdated;
            // Make sure we're working on up-to-date data
            BuildList.Update();
        }

        void OnDisable()
        {
            BuildList.BuildsUpdated -= OnBuildsUpdated;
            if (m_SelectionWindow != null)
                m_SelectionWindow.Close();
            if (m_Settings != null)
                m_Settings.ValuesChanged -= OnSettingsValuesChanged;
        }

        [ExcludeFromCodeCoverage] // File dialogs, impossible / very tricky to drive via CI
        void AddExistingBuildUsingFileDialog()
        {
            // Same behavior as Build dilaog has: e.g. "D:/MyProject/Builds/" as folderName and "MyBuild" as defaultName
            var path = EditorUtility.OpenFolderPanel(
                "Choose 'Build' folder of the build",
                Path.GetDirectoryName(LastBuildPath),
                Path.GetFileName(LastBuildPath)
            );
            [ExcludeFromCodeCoverage] // Can't test dialog with automated testing
            static bool ForceAdd() =>
                EditorUtility.DisplayDialog(
                    "Invalid Build folder",
                    "The folder doesn't appear to be a valid build folder. Do you want to proceed?",
                    "OK",
                    "Cancel"
                );

            var build = AddExistingBuild(path, ForceAdd);
            if (build != null)
                LastBuildPath = path;
        }

        /// <summary>
        /// Adds a build to the window, checks that the build folder appears to be valid.
        /// </summary>
        /// <param name="path">"D:/Builds/MyBuild" and "D:/Builds/MyBuild/Build" both accepted</param>
        /// <param name="forceAdd">Function for force-adding the build even it appears to be invalid</param>
        /// <returns>WebBuildReport if build was added to the build list, null otherwise</returns>
        internal WebBuildReport AddExistingBuild(string path, Func<bool> forceAdd)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Accept both
            // build output path, e.g., "D:/MyProject/Builds/MyBuild"
            // build path: "D:/MyProject/Builds/MyBuild/Build"
            // but continue only with build output path
            if (WebBuildReport.IsValidBuildPath(path))
                path = Path.GetDirectoryName(path);
            // else: assume it's build output path

            if (!WebBuildReport.IsValidBuildPath(Path.Combine(path, "Build")) && !forceAdd())
                return null;

            return BuildList.AddOrUpdateBuild(path);
        }

        internal void OnBuildsUpdated(List<WebBuildReport> builds)
        {
            // Skip UI update if window is not visible.
            // Build list will be updated automatically when window becomes visible.
            if (m_BuildListView == null)
                return;

            CopyBuildsFromGlobalBuildList();
            SortBuildList();
            RefreshUI();
        }

        void OnSettingsValuesChanged()
        {
            RefreshUI();
        }

        void BindToSettings(SubmoduleStrippingSettings settings)
        {
            if (m_Settings != null)
                m_Settings.ValuesChanged -= OnSettingsValuesChanged;

            m_Settings = settings;

            if (m_Settings != null)
                m_Settings.ValuesChanged += OnSettingsValuesChanged;

            m_SerializedSettings = settings != null ? new SerializedObject(settings) : null;
            var hasSettings = m_SerializedSettings != null;

            var propFields = rootVisualElement.Query<PropertyField>().ToList();
            if (!propFields.Any())
                CreatePropertyFields();

            propFields = rootVisualElement.Query<PropertyField>().ToList();
            propFields.ForEach(prop =>
            {
                UIUtils.SetVisible(prop, hasSettings);
                // Manual binding needed for PropertyFields when creating them outside of Editor.CreateInspectorGUI()
                if (hasSettings)
                    prop.Bind(m_SerializedSettings);
                else
                    prop.Unbind();

                // Make sure to update the submodule list
                if (hasSettings && prop.bindingPath == nameof(SubmoduleStrippingSettings.SubmodulesToStrip))
                    SubmoduleStrippingSettingsEditor.InitSubmoduleList(prop, m_Settings.SubmodulesToStrip);
            });

            if (m_SelectSubmoduleButtonContainer != null)
                UIUtils.SetVisible(m_SelectSubmoduleButtonContainer, hasSettings);

            // If Submodule Selection window is open, make sure to refresh it
            var selectionWnd = Resources.FindObjectsOfTypeAll<SubmoduleSelectionWindow>().FirstOrDefault();
            if (selectionWnd != null)
                OpenOrRefreshSubmoduleSelectionWindow();

            // Make sure instructions are updated.
            RefreshUI();
        }

        static string GetBuildLastModifiedAtWithoutSeconds(WebBuildReport build)
        {
            var format = (DateTimeFormatInfo)CultureInfo.CurrentCulture.DateTimeFormat.Clone();
            format.LongTimePattern = format.LongTimePattern.Replace(":ss", "");
            return GetBuildLastModifiedAt(build, format);
        }

        static string GetBuildLastModifiedAt(WebBuildReport build, DateTimeFormatInfo format = null)
        {
            format ??= CultureInfo.CurrentCulture.DateTimeFormat;
            return new DirectoryInfo(build.OutputPath).Exists
                ? build.LastModifiedAt.DateTime.ToLocalTime().ToString(format)
                : "directory missing";
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            m_VisualTreeAsset.CloneTree(root);

            m_DetailPane = root.Q("details-pane");
            m_InstructionsContainer = root.Q("instructions-container");
            m_StripButton = root.Q<Button>("strip-build-button");
            m_StripAndRunButton = root.Q<Button>("strip-and-run-build-button");
            m_RunButton = root.Q<Button>("run-build-button");
            m_AddProfilingButton = root.Q<Button>("add-profiling-button");
            m_AddProfilingButton.tooltip = k_AddProfilingDefaultTooltip;
            m_RestoreButton = root.Q<Button>("restore-build-button");

            m_BuildListView = root.Q<MultiColumnListView>("builds-list");
            m_BuildListView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            m_BuildListView.selectionType = SelectionType.Single;
            m_BuildListView.makeNoneElement = () => new Label("Make a build or add an existing build");

            // Register manipulator in makeCell() instead of bindItem() as the latter is tricky place
            // due to the "Visual Element recycling" optimization.
            m_BuildListView.columns[0].makeCell = () =>
            {
                var label = new Label();
                AddBuildContextMenu(this, label);
                return label;
            };
            m_BuildListView.columns[1].makeCell = () =>
            {
                var label = new Label();
                AddBuildContextMenu(this, label);
                return label;
            };

            m_BuildListView.columns[0].bindCell = (item, index) =>
            {
                var label = (item as Label);
                var build = Builds[index];
                label.userData = build;
                label.text = build.Name;
            };
            m_BuildListView.columns[1].bindCell = (item, index) =>
            {
                var label = (item as Label);
                var build = Builds[index];
                label.userData = build;
                label.text = GetBuildLastModifiedAt(build);
            };

            m_BuildListView.sortingMode = ColumnSortingMode.Custom;
            m_BuildListView.columnSortingChanged += () => SortBuildList();
            // NOTE: we cannot bindly trust any ListView events that pass on objects,
            // the objects point to wrong indices once the list is sorted
            m_BuildListView.selectedIndicesChanged += OnSelectedIndicesChanged;
            CopyBuildsFromGlobalBuildList();

            // Toolbar buttons
            root.Q<Button>("add-build-button").clicked += AddExistingBuildUsingFileDialog;

            root.Q<Button>("player-settings-button").clicked += () =>
                SettingsService.OpenProjectSettings("Project/Player");

            root.Q<Button>("build-profiles-button").clicked += () => BuildProfileWindowHelper.GetWindow();

            UIUtils.AddHelpButton(root.Q<Toolbar>(), k_DocumentationPages[0]);

            // Strip, Run etc. buttons
            m_StripButton.clicked += () =>
            {
                if (m_BuildListView.selectedItem is WebBuildReport build)
                    StripBuild(build);
            };

            m_StripAndRunButton.clicked += () =>
            {
                if (m_BuildListView.selectedItem is WebBuildReport build)
                    StripAndRunBuild(build);
            };

            m_RunButton.clicked += () =>
            {
                if (m_BuildListView.selectedItem is WebBuildReport build)
                    RunBuild(build);
            };

            m_AddProfilingButton.clicked += () =>
            {
                if (m_BuildListView.selectedItem is WebBuildReport build)
                    AddProfilingToBuild(build);
            };

            m_RestoreButton.clicked += () =>
            {
                if (SelectedBuild is WebBuildReport build)
                    RestoreBuild(build);
            };

            ClearUI(); // show initial instructions

            var stripAfterBuild = root.Q<Toggle>("strip-after-build-toggle");
            stripAfterBuild.value = StrippingProjectSettings.StripAutomaticallyAfterBuild;
            stripAfterBuild.RegisterCallback<ChangeEvent<bool>>(
                (evt) =>
                {
                    StrippingProjectSettings.StripAutomaticallyAfterBuild = evt.newValue;
                    RefreshUI();
                }
            );

            m_ActiveSettingsField = root.Q<ObjectField>("active-settings-field");
            m_ActiveSettingsField.value = StrippingProjectSettings.ActiveSettings;
            m_ActiveSettingsField.RegisterCallback<ChangeEvent<UnityEngine.Object>>(
                (evt) =>
                {
                    StrippingProjectSettings.ActiveSettings = evt.newValue as SubmoduleStrippingSettings;
                }
            );

            StrippingProjectSettings.SettingsChanged += (settings) =>
            {
                m_ActiveSettingsField.SetValueWithoutNotify(settings);
                BindToSettings(settings);
            };

            BindToSettings(StrippingProjectSettings.ActiveSettings);
        }

        // NOTE: actual sorting happens only if the columns are clicked or set programmatically prior calling this
        void SortBuildList()
        {
            foreach (var sort in m_BuildListView.sortedColumns)
            {
                SortBuildList(sort.columnName, sort.direction);
            }
        }

        internal void SortBuildList(string criterion, SortDirection direction)
        {
            // Make sure to keep the currently selected build intact
            var selectedBuild = SelectedBuild;

            if (string.Equals(criterion, "name", StringComparison.OrdinalIgnoreCase))
            {
                Builds.Sort((x, y) => x.Name.CompareTo(y.Name));
            }
            else if (string.Equals(criterion, "date", StringComparison.OrdinalIgnoreCase))
            {
                Builds.Sort((x, y) => x.LastModifiedAt.CompareTo(y.LastModifiedAt));
            }
            else
            {
                Debug.LogWarning($"Unsupported criterion '{criterion}' for sorting");
                return;
            }

            if (direction == SortDirection.Descending)
                Builds.Reverse();

            m_BuildListView.RefreshItems();

            var newIndex = selectedBuild != null ? Builds.FindIndex(b => b.OutputPath == selectedBuild.OutputPath) : -1;
            if (newIndex >= 0)
                m_BuildListView.SetSelection(new[] { newIndex });
        }

        internal static void AddBuildContextMenu(SubmoduleStrippingWindow window, Label label)
        {
            label.AddManipulator(
                new ContextualMenuManipulator(
                    (evt) =>
                    {
                        if (evt.currentTarget is VisualElement { userData: WebBuildReport build })
                        {
                            evt.menu.AppendAction(k_RemoveBuildText, (_) => window.RemoveBuild(build));
                            evt.menu.AppendSeparator();
                            if (Directory.Exists(build.OutputPath))
                            {
                                evt.menu.AppendAction(
                                    k_ShowBuildFolderText,
                                    (_) => EditorUtility.RevealInFinder(build.OutputPath)
                                );
                            }
                            if (build.IsBackupFolderValid())
                            {
                                evt.menu.AppendAction(
                                    k_ShowBackUpFolderText,
                                    (_) => EditorUtility.RevealInFinder(build.GetBackupFolderPath())
                                );
                            }
                        }
                    }
                )
            );
        }

        internal void RemoveBuild(WebBuildReport build)
        {
            if (!BuildList.RemoveBuild(build))
            {
                Debug.LogError($"Failed to remove build '{build.Name}' from the list");
                return;
            }

            // The selected build was removed, make sure the UI is updated
            m_BuildListView.ClearSelection();
            ClearUI();
        }

        bool StripBuild(WebBuildReport build)
        {
            var success = WebBuildProcessor.StripBuild(build, StrippingProjectSettings.ActiveSettings);

            BuildList.UpdateBuild(build.OutputPath);
            if (!success)
                AddInstruction(
                    "An error occurred during submodule stripping, see Console for more details.",
                    HelpBoxMessageType.Error
                );

            return success;
        }

        void StripAndRunBuild(WebBuildReport build)
        {
            if (StripBuild(build))
                RunBuild(build);
        }

        static void RunBuild(WebBuildReport build)
        {
            var dialog = GetWindow<BuildRunnerDialog>();
            dialog.Run(build.OutputPath);
        }

        void AddProfilingToBuild(WebBuildReport build)
        {
            var instrumentedSuccessfully = WebBuildProcessor.InstrumentBuild(build);
            if (instrumentedSuccessfully)
            {
                // Update build details
                BuildList.UpdateBuild(build.OutputPath);
            }
            else
            {
                AddInstruction(
                    "An error occurred when adding submodule profiling to build. See Console.",
                    HelpBoxMessageType.Error
                );
            }
        }

        void RestoreBuild(WebBuildReport build)
        {
            build.Restore();
            RefreshUI();
        }

        void CreatePropertyFields()
        {
            if (m_SerializedSettings == null)
                return;

            var optionsContainer = rootVisualElement.Q("settings-container");

            // We want to show editors for all public instance fields of SubmoduleStrippingSettings
            foreach (
                var prop in typeof(SubmoduleStrippingSettings)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name)
            )
            {
                // Use different label for RemoveEmbeddedDebugSymbols
                string label =
                    prop == nameof(SubmoduleStrippingSettings.RemoveEmbeddedDebugSymbols)
                        ? ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings.RemoveDebugInformation))
                        : ObjectNames.NicifyVariableName(prop);
                var propField = new PropertyField(m_SerializedSettings.FindProperty(prop), label);
                // SubmodulesToStrip is a special case, the rest are generic
                if (prop == nameof(SubmoduleStrippingSettings.SubmodulesToStrip))
                {
                    propField.RegisterCallbackOnce<GeometryChangedEvent>(
                        (evt) =>
                        {
                            var list = SubmoduleStrippingSettingsEditor.InitSubmoduleList(
                                evt.target as PropertyField,
                                m_Settings.SubmodulesToStrip
                            );
                            list.style.maxHeight = 300;
                        }
                    );
                }
                optionsContainer.Add(propField);
            }
            optionsContainer.TrackSerializedObjectValue(m_SerializedSettings, _ => m_Settings?.Save());

            m_SelectSubmoduleButtonContainer = new VisualElement();
            m_SelectSubmoduleButtonContainer.style.alignItems = Align.Center;
            var button = SubmoduleStrippingSettingsEditor.CreateSelectButton();
            button.clicked += () => OpenOrRefreshSubmoduleSelectionWindow();
            m_SelectSubmoduleButtonContainer.Add(button);
            optionsContainer.Add(m_SelectSubmoduleButtonContainer);
        }

        void OpenOrRefreshSubmoduleSelectionWindow()
        {
            if (m_SerializedSettings == null)
                return;

            if (m_SelectionWindow != null)
                m_SelectionWindow.SelectedSubmodulesChanged -= SetSubmodules;

            m_SelectionWindow = SubmoduleSelectionWindow.GetAsUtilityPopup();
            var prop = m_SerializedSettings.FindProperty(nameof(SubmoduleStrippingSettings.SubmodulesToStrip));
            m_SelectionWindow.SelectedSubmodules = PropertyUtils.GetHashSetPropertyValue(prop);
            m_SelectionWindow.SelectedSubmodulesChanged += SetSubmodules;
        }

        void SetSubmodules(HashSet<string> selectedSubmodules)
        {
            m_SerializedSettings.Update();
            var prop = m_SerializedSettings.FindProperty(nameof(SubmoduleStrippingSettings.SubmodulesToStrip));
            PropertyUtils.SetHashSetPropertyValue(prop, selectedSubmodules);
            m_SerializedSettings.ApplyModifiedProperties();
        }

        void OnSelectedIndicesChanged(IEnumerable<int> selectedItems)
        {
            var index = selectedItems.Any() ? selectedItems.FirstOrDefault() : -1;
            var build = index >= 0 && index < Builds.Count ? Builds[index] : null;
            UpdateUI(build);
        }

        void RefreshUI() => UpdateUI(SelectedBuild);

        void ClearUI() => UpdateUI(default);

        void UpdateUI(WebBuildReport build)
        {
            // Make sure we always work with the latest data — the build folder could have been modified externally
            // while the window was open
            build?.Update();

            // 1. Show build details
            ShowBuildDetails(build);

            // 2. Update buttons
            // Without Web Build Support there's not much to do, Restore can be functional still though.
            var hasWebBuildSupport = BuildToolsLocator.IsWebBuildSupportInstalled;
            var playerSettings = build?.GetWebPlayerSettings();
            var isValidBuild = build?.IsValid == true;
            var hasStrippingSettings = m_Settings != null;
            var hasDebugInfo = playerSettings?.HasDebugInfo == true;
            var hasIncompatibleEmscriptenArg = playerSettings?.HasIncompatibleEmscriptenArg == true;
            var canRunStripping = (
                hasStrippingSettings && m_Settings.CanRunStripping && hasDebugInfo && !hasIncompatibleEmscriptenArg
            );
            var isInstrumented = build?.HasSubmoduleProfiling == true;
            var isStripped = build?.HasStrippingInfo == true;
            var buildIsModified = isInstrumented || isStripped;
            var canAddProfiling = (
                !buildIsModified
                && build?.IsBackupFolderValid() == true
                && hasDebugInfo
                && !hasIncompatibleEmscriptenArg
            );
            m_StripButton.SetEnabled(hasWebBuildSupport && canRunStripping);
            m_StripAndRunButton.SetEnabled(hasWebBuildSupport && canRunStripping);
            m_RunButton.SetEnabled(hasWebBuildSupport && isValidBuild);
            m_AddProfilingButton.SetEnabled(hasWebBuildSupport && canAddProfiling);
            if (isInstrumented)
                m_AddProfilingButton.tooltip =
                    "The selected build is already instrumented for submodule profiling. Restore the build to remove profiling.";
            else if (isStripped)
                m_AddProfilingButton.tooltip =
                    "The selected build is already stripped. Restore the build to add profiling.";
            else if (!hasDebugInfo)
                m_AddProfilingButton.tooltip =
                    "The selected build does not have debug information. Rebuild with debug information.";
            else if (hasIncompatibleEmscriptenArg)
            {
                var incompatibleArgs = WebPlayerSettings.FindIncompatibleEmscriptenArgs(playerSettings.emscriptenArgs);
                m_AddProfilingButton.tooltip =
                    $"The selected build has the incompatible Emscripten arguments \"{string.Join(' ', incompatibleArgs)}\". Rebuild without the setting.";
            }
            else
                m_AddProfilingButton.tooltip = k_AddProfilingDefaultTooltip;
            m_RestoreButton.SetEnabled(buildIsModified);

            // 3. Show instructions
            ShowInstructions(build);
        }

        void ShowBuildDetails(WebBuildReport build)
        {
            m_DetailPane.Clear();

            if (build == null)
            {
                var l = new DetailLabel(
                    "Create/add a build and select it to view its details.\nNew builds are added automatically to the list."
                );
                m_DetailPane.Add(l);
                return;
            }

            // Helper funcs
            // Unsure what would be the best format for time, going with ISO 806 w/ offset for now
            static string ToIso806(DateTimeOffset dt) => $"{dt:o}";
            static string ToIso8061Offset(DateTime dt) => ToIso806(new DateTimeOffset(dt));
            // Returns e.g. "12,017,950 bytes (11.5 MB)"
            static string FormatBytes(long bytes) => $"{ByteString(bytes)} ({MultiByteString(bytes)})";
            static string FormatByteDiff(long bytes) =>
                $"{(Math.Sign(bytes) > 0 ? "+" : "")}{ByteString(bytes)} ({MultiByteString(bytes)})";
            const string unknownStr = "Unknown";
            string ValueOrFallback(object value, string fallback = unknownStr) => value?.ToString() ?? fallback;
            string CondValueOrFallback(bool cond, object value, string fallback = unknownStr) =>
                cond ? ValueOrFallback(value) : fallback;
            static string ToTitleCase(string str) => Regex.Replace(str, "(\\B[A-Z])", " $1");

            // Title
            m_DetailPane.Add(UIUtils.CreateTitleLabel($"{build.Name} ({GetBuildLastModifiedAtWithoutSeconds(build)})"));

            // Details
            // 1. basic info
            var playerSettings = build.GetWebPlayerSettings();
            var buildSettings = playerSettings?.BuildSettings;
            var detailsText = new StringBuilder();
            detailsText.AppendLine($"Last modified: {ToIso806(build.LastModifiedAt)}");
            detailsText.AppendLine($"Output path: {build.OutputPath}");
            if (!string.IsNullOrEmpty(build.UnityVersion))
                detailsText.AppendLine($"Unity version: {build.UnityVersion}");
            if (!string.IsNullOrEmpty(build.EmscriptenVersion))
                detailsText.AppendLine($"Emscripten version: {build.EmscriptenVersion}");
            detailsText.AppendLine(
                $"Compression: {CondValueOrFallback(build.IsValid, playerSettings?.compressionFormat)}"
            );
            detailsText.AppendLine(
                $"Decompression Fallback: {CondValueOrFallback(build.IsValid, playerSettings?.decompressionFallback)}"
            );
            detailsText.AppendLine(
                $"Debug Symbols: {CondValueOrFallback(buildSettings is not null, playerSettings?.DebugSymbolMode)}"
            );

            // 2. build settings, introduced in 1.0.0-final
            if (buildSettings != null)
            {
                detailsText.AppendLine($"Development Build: {ValueOrFallback(buildSettings.development)}");
                if (buildSettings.development)
                {
                    detailsText.AppendLine($"Autoconnect Profiler: {buildSettings.connectProfiler}");
                    detailsText.AppendLine($"Deep Profiling Support: {buildSettings.buildWithDeepProfilingSupport}");
                }
                else
                {
                    detailsText.AppendLine(
                        $"Code Optimization: {CodeOptimizationString(buildSettings.codeOptimization)}"
                    );
                }
                detailsText.AppendLine(
                    $"Texture Compression: {TextureCompressionString(buildSettings.webGLBuildSubtarget)}"
                );
            }
            // 3. Presentation settings added in 1.3.0
            detailsText.AppendLine();
            detailsText.AppendLine("Presentation");
            detailsText.AppendLine($"Web Template: {ValueOrFallback(playerSettings?.template)}");
            detailsText.AppendLine($"Run In Background: {ValueOrFallback(playerSettings?.runInBackground)}");
            detailsText.AppendLine($"Show Splash Screen: {ValueOrFallback(playerSettings?.showSplashScreen)}");

            // 3. player settings with major impact on codegen
            detailsText.AppendLine();
            detailsText.AppendLine("Script Compilation");
            detailsText.AppendLine(
                $"Managed Code Stripping Level: {ValueOrFallback(playerSettings?.managedStrippingLevel)}"
            );
            detailsText.AppendLine($"Strip Engine Code: {ValueOrFallback(playerSettings?.stripEngineCode)}");
            detailsText.AppendLine(
                $"Scripting Define Symbols: {ValueOrFallback(playerSettings?.scriptingDefineSymbols)}"
            );
            detailsText.AppendLine(
                $"IL2CPP Code Generation: {(playerSettings != null ? IL2CppCodeGenerationString(playerSettings.il2CppCodeGeneration) : unknownStr)}"
            );
            detailsText.AppendLine(
                $"IL2CPP Compiler Configuration: {ValueOrFallback(playerSettings?.il2CppCompilerConfiguration)}"
            );
            detailsText.AppendLine(
                $"Submodule Stripping Compatibility: {ValueOrFallback(playerSettings?.enableSubmoduleStrippingCompatibility)}"
            );

            // 3. Graphics API Player Settings added in 1.3.0
            detailsText.AppendLine();
            detailsText.AppendLine("Rendering");
            detailsText.AppendLine($"Auto Graphics API: {ValueOrFallback(playerSettings?.autoGraphicsAPI)}");
            detailsText.AppendLine($"Graphics APIs: {GraphicsAPIsString(playerSettings?.graphicsAPIs)}");

            // 4. Wasm language features
            detailsText.AppendLine();
            detailsText.AppendLine("Publishing Settings");
            detailsText.AppendLine($"Exceptions: {ToTitleCase(ValueOrFallback(playerSettings?.exceptionSupport))}");
            detailsText.AppendLine($"Native Multithreading: {ValueOrFallback(playerSettings?.MultithreadingEnabled)}");
            detailsText.AppendLine($"WebAssembly 2023: {ValueOrFallback(playerSettings?.Wasm2023Enabled)}");
            detailsText.AppendLine($"WebAssembly.Table: {ValueOrFallback(playerSettings?.WasmTableEnabled)}");
            detailsText.AppendLine($"BigInt: {ValueOrFallback(playerSettings?.WasmBigIntEnabled)}");
            if (!string.IsNullOrEmpty(playerSettings?.emscriptenArgs))
            {
                detailsText.AppendLine($"Additional Emscripten Arguments: {playerSettings?.emscriptenArgs}");
            }
            detailsText.AppendLine();

            // 5. original wasm size
            var orgSize = build.OriginalWasmSize;
            detailsText.AppendLine($"Original wasm size: {CondValueOrFallback(build.IsValid, FormatBytes(orgSize))}");

            // 6. stripping info
            var strippedSize = build.StrippedWasmSize;
            if (build.IsValid && strippedSize > 0)
            {
                detailsText.AppendLine($"Current wasm size: {FormatBytes(strippedSize)}");
                detailsText.AppendLine($"Difference: {FormatByteDiff(strippedSize - orgSize)}");
                if (build.HasStrippingInfo)
                {
                    var strippingInfo = JsonConvert.DeserializeObject<StrippingInfo>(
                        File.ReadAllText(build.StrippingInfoFilePath)
                    );
                    detailsText.AppendLine();
                    detailsText.AppendLine(
                        $"Last stripped: {ToIso8061Offset(File.GetLastWriteTime(build.StrippingInfoFilePath))}"
                    );
                    detailsText.AppendLine($"Package version used: {strippingInfo.version}");
                    detailsText.AppendLine(
                        $"Optimize Code After Stripping: {ValueOrFallback(strippingInfo.optimizeCodeAfterStripping)}"
                    );
                    detailsText.AppendLine(
                        $"Remove Debug Information: {ValueOrFallback(strippingInfo.removeDebugInformation)}"
                    );
                    detailsText.AppendLine(
                        $"Missing Submodule Error Handling: {ValueOrFallback(strippingInfo.missingSubmoduleErrorHandling)}"
                    );
                    detailsText.AppendLine(
                        $"Stripped submodules: {string.Join(", ", strippingInfo.strippedSubmodules)}"
                    );
                    detailsText.AppendLine($"Amount stripped: {FormatBytes(strippingInfo.strippedSize)}");

                    if (strippingInfo.strippedCodeSize != null)
                        detailsText.AppendLine(
                            $"Amount code stripped: {FormatBytes((long)strippingInfo.strippedCodeSize)}"
                        );
                    if (strippingInfo.strippedNameSize != null)
                        detailsText.AppendLine(
                            $"Amount debug information stripped: {FormatBytes((long)strippingInfo.strippedNameSize!)}"
                        );
                    if (strippingInfo.strippedDwarfSize > 0)
                        detailsText.AppendLine(
                            $"Amount DWARF debug information stripped: {FormatBytes((long)strippingInfo.strippedDwarfSize!)}"
                        );
                }
            }

            // 7. submodule profiling info
            if (build.HasSubmoduleProfiling)
            {
                detailsText.AppendLine("Build is enabled for submodule profiling.");
            }

            m_DetailPane.Add(UIUtils.CreateDescriptionLabel(detailsText.ToString()));
        }

        void ShowInstructions(WebBuildReport build)
        {
            m_InstructionsContainer.Clear();

            if (!BuildToolsLocator.IsWebBuildSupportInstalled)
            {
                AddInstruction(
                    $"The {PackageConstants.PackageDisplayName} requires the Web Build Support module. Add the module with the Unity Hub.",
                    HelpBoxMessageType.Error
                );
                return;
            }

            var isBuildSelected = build != null;
            var isValidBuild = build?.IsValid == true;
            var playerSettings = build?.GetWebPlayerSettings();
            var buildHasPlayerSettings = playerSettings != null;

            var hasStrippingSettings = m_Settings != null;
            var canRunStripping = hasStrippingSettings && m_Settings.CanRunStripping;
            var hasDebugInfo = playerSettings?.HasDebugInfo == true;
            var hasIncompatibleEmscriptenArg = playerSettings?.HasIncompatibleEmscriptenArg == true;
            var hasSubmoduleStrippingCompatibility =
                playerSettings != null && playerSettings.enableSubmoduleStrippingCompatibility;

            // Instructions applicable even when we have no builds currently
            if (!isBuildSelected)
            {
                if (StrippingProjectSettings.StripAutomaticallyAfterBuild)
                {
                    if (!hasStrippingSettings)
                    {
                        // case 1: no build selected, auto-stripping true, settings object null
                        AddInstruction("Automatic stripping requires active submodule stripping settings.");
                    }
                    else if (!canRunStripping)
                    {
                        // case 2: no build selected, auto-stripping true, settings object not null, submodules.length == 0
                        var submodulesPropName = ObjectNames.NicifyVariableName(
                            nameof(SubmoduleStrippingSettings.SubmodulesToStrip)
                        );
                        var optimizePropName = ObjectNames.NicifyVariableName(
                            nameof(SubmoduleStrippingSettings.OptimizeCodeAfterStripping)
                        );
                        var removeDebugInformationPropName = ObjectNames.NicifyVariableName(
                            nameof(SubmoduleStrippingSettings.RemoveDebugInformation)
                        );
                        AddInstruction(
                            $"Submodule stripping requires at least one {submodulesPropName} to be specified or '{optimizePropName}' or '{removeDebugInformationPropName}' to be enabled."
                        );
                    }
                }
                return;
            }

            // The rest are applicable only when we have builds
            if (isValidBuild)
            {
                if (!build.IsBackupFolderValid())
                {
                    // case 3: build selected, .wasm exists, backup folder not valid (stripping prerequisites missing)
                    AddInstruction(
                        "Prerequisite files for submodule stripping are missing. "
                            + $"See <a href=\"{PackageConstants.GetDocumentationUrl(k_DocumentationPages[1])}\">documentation</a> "
                            + "for more details."
                    );
                }
                else
                {
                    // case 9: build settings introduced in 1.0.0 not in player_settings.json
                    if (playerSettings.BuildSettings is null)
                    {
                        AddInstruction(
                            "Some build settings were not stored for the build and can't be displayed. Rebuild to fix."
                        );
                    }
                }

                if (!hasStrippingSettings)
                {
                    // case 4: build selected, settings object null
                    AddInstruction($"Submodule stripping requires active submodule stripping settings.");
                }
                else if (!canRunStripping)
                {
                    // case 5: build selected, settings object not null, submodules.length == 0
                    var submodulesPropName = ObjectNames.NicifyVariableName(
                        nameof(SubmoduleStrippingSettings.SubmodulesToStrip)
                    );
                    var optimizePropName = ObjectNames.NicifyVariableName(
                        nameof(SubmoduleStrippingSettings.OptimizeCodeAfterStripping)
                    );
                    var removeDebugInformationPropName = ObjectNames.NicifyVariableName(
                        nameof(SubmoduleStrippingSettings.RemoveDebugInformation)
                    );
                    AddInstruction(
                        $"Submodule stripping requires at least one {submodulesPropName} to be specified or '{optimizePropName}' or '{removeDebugInformationPropName}' to be enabled."
                    );
                }

                if (buildHasPlayerSettings && !hasDebugInfo)
                {
                    // case 6: build selected, .wasm exists, player_settings.json exists, no debug info
                    AddInstruction("Submodule stripping requires debug information. Rebuild with debug information.");
                }

                if (
                    buildHasPlayerSettings
                    && PlayerSettingsHelper.IsSubmoduleStrippingCompatibilityAvailable
                    && !hasSubmoduleStrippingCompatibility
                )
                {
                    // case 7: build selected, .wasm exists,  player_settings.json exists, compatibility mode is not enabled
                    AddInstruction(
                        "Submodule stripping works best with \"Enable Submodule Stripping Compatibility\". Rebuild with the setting enabled for best results."
                    );
                }

                if (hasIncompatibleEmscriptenArg)
                {
                    // case 8: build selected, .wasm exists, player_settings.json exists, build uses additional emscripten arguments incompatible with submodule stripping
                    var incompatibleArgs = WebPlayerSettings.FindIncompatibleEmscriptenArgs(
                        playerSettings.emscriptenArgs
                    );
                    AddInstruction(
                        $"Incompatible setting in \"PlayerSettings.WebGl.emscriptenArgs\" detected: remove \"{string.Join(' ', incompatibleArgs)}\" and rebuild. Consider using \"PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Embedded\" instead.",
                        HelpBoxMessageType.Error
                    );
                }
            }
            else
            {
                // case 9: build is not valid
                AddInstruction("The build appears to be missing files. Rebuild to fix.", HelpBoxMessageType.Error);
            }
        }

        void AddInstruction(string text, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var helpBox = new HelpBox(text, type);
            var label = helpBox.Q<Label>();
            label.selection.isSelectable = true;
            m_InstructionsContainer.Add(helpBox);
        }

        // returns e.g. "1.0 MB"
        // B: 0 decimals
        // KB/MB: 1 decimal
        // GB: 2 decimals
        internal static string MultiByteString(long bytes)
        {
            // It's not completely impossible to end up with negative savings, EditorUtility.FormatBytes doesn't support those.
            var sign = Math.Sign(bytes);
            return $"{(sign < 0 ? "-" : "")}{EditorUtility.FormatBytes(Math.Abs(bytes))}";
        }

        // returns e.g. "1,000,000 bytes"
        internal static string ByteString(long bytes, string bytesWord = "bytes")
        {
            return $"{bytes.ToString("N0", CultureInfo.CurrentCulture)} {bytesWord}";
        }

        internal static string TextureCompressionString(WebGLTextureSubtarget t) =>
            t switch
            {
                WebGLTextureSubtarget.Generic => "Use Player Settings",
                _ => t.ToString(), // the rest seem to map to the their enum names as is
            };

        internal static string CodeOptimizationString(string str) =>
            str switch
            {
                "BuildTimes" => "Shorter Build Time",
                "RuntimeSpeed" => "Runtime Speed",
                "RuntimeSpeedLTO" => "Runtime Speed with LTO",
                "DiskSize" => "Disk Size",
                "DiskSizeLTO" => "Disk Size with LTO",
                _ => str,
            };

        internal static string IL2CppCodeGenerationString(Il2CppCodeGeneration v) =>
            v switch
            {
                Il2CppCodeGeneration.OptimizeSpeed => "Faster runtime",
                Il2CppCodeGeneration.OptimizeSize => "Faster (smaller) builds",
                _ => v.ToString(),
            };

        internal static string GraphicsAPIsString(UnityEngine.Rendering.GraphicsDeviceType[] graphicsAPIs)
        {
            if (graphicsAPIs == null)
                return "Unknown";

            if (graphicsAPIs.Length == 0)
                return string.Empty;

            var apiNames = new string[graphicsAPIs.Length];
            for (int i = 0; i < graphicsAPIs.Length; i++)
            {
                apiNames[i] =
                    graphicsAPIs[i] == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
                        ? "WebGL 2"
                        : graphicsAPIs[i].ToString();
            }
            return string.Join(", ", apiNames);
        }
    }
}
