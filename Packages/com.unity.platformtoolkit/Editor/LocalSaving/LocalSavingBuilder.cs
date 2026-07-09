using Unity.PlatformToolkit.Editor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.PlatformToolkit.LocalSaving.Editor
{
    internal class LocalSavingBuilder : IPlatformToolkitBuilder
    {
        public void PostBuild(BuildReport buildReport) { }

        BaseRuntimeConfiguration IPlatformToolkitBuilder.PrepareBuild(BuildReport buildReport)
        {
            return ScriptableObject.CreateInstance<LocalSavingRuntimeConfiguration>();
        }
    }
}
