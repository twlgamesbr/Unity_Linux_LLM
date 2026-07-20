using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeControlsSaveDataInfoField : VisualElement
    {
        private string m_Name;

        public PlayModeControlsSaveDataInfoField(PlayModeSaveData saveData)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/Playmode/UI/Inspector/PlayModeControlsSaveDataInfoField.uxml"
            );
            uxml.CloneTree(this);

            PlayModeImportExportSave importExportSave = new PlayModeImportExportSave(saveData);
            Button importButton = this.Q<Button>("SaveImportButton");
            importButton.RegisterCallback<ClickEvent>(async _ =>
            {
                var selectedZipFile = EditorUtility.OpenFilePanel(
                    "Select a Platform Toolkit save zip file to override this save with",
                    Application.dataPath,
                    "zip"
                );
                if (string.IsNullOrEmpty(selectedZipFile))
                    return;

                try
                {
                    await importExportSave.ImportFilesToReplaceSaveData(m_Name, selectedZipFile);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error importing save file:\n{ex.Message}");
                }
            });

            Button exportButton = this.Q<Button>("SaveExportButton");
            exportButton.RegisterCallback<ClickEvent>(async _ =>
            {
                var selectedFolder = EditorUtility.OpenFolderPanel(
                    "Select a folder to export the save to",
                    Application.dataPath,
                    ""
                );
                if (string.IsNullOrEmpty(selectedFolder))
                    return;

                try
                {
                    await importExportSave.ExportFilesFromSave(m_Name, selectedFolder);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error attempting to export save:\n{ex.Message}");
                }
            });

            Button deleteButton = this.Q<Button>("SaveDeleteButton");
            deleteButton.RegisterCallback<ClickEvent>(_ => saveData.RemoveSave(m_Name));
        }

        internal void Bind(PlayModeSaveDataInfo saveInfo)
        {
            dataSource = saveInfo;
            m_Name = saveInfo.Name;
        }
    }
}
