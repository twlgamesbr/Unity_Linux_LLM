using System;
using System.Linq;
using UnityEditor;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace Unity.Web.Stripping.Editor
{
    // Need to use reflection for BuildProfileWindow as its an internal class
    static class BuildProfileWindowHelper
    {
        public static readonly Type Type =
#if UNITY_6000_5_OR_NEWER
            CurrentAssemblies.GetLoadedAssemblies()
#else
            AppDomain.CurrentDomain.GetAssemblies()
#endif
                .SingleOrDefault(assembly => assembly.GetName().Name == "UnityEditor.BuildProfileModule")
            ?.GetType("UnityEditor.Build.Profile.BuildProfileWindow");

        public static EditorWindow GetWindow()
        {
            return EditorWindow.GetWindow(Type);
        }
    }
}
