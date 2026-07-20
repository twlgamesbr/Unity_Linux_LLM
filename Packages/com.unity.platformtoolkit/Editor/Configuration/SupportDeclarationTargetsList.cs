using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.Editor
{
    internal class SupportDeclarationTargetsList : VisualElement
    {
        public SupportDeclarationTargetsList()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/EditorResources/UI/SupportDeclarationTargetsList.uxml"
            );
            uxml.CloneTree(this);

#if UNITY_6000_4_OR_NEWER
            this.Q<Button>("edit-build-profile-button").clicked += () =>
            {
                BuildPlayerWindow.ShowBuildPlayerWindow();
            };
#endif

            var declarationsList = this.Q<ScrollView>("declaration-targets-list");
            var targetsManager = PlatformToolkitSettings.instance.SupportDeclarationTargetsManager;

            foreach (var supportDeclaration in SupportDeclarationManager.SupportDeclarations.OrderBy(p => p.ToString()))
            {
                var spt = new SupportDeclarationTarget(supportDeclaration, targetsManager);
                var field = new SupportDeclarationTargetField(spt);
                declarationsList.Add(field);
            }
        }
    }
}
