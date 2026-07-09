using UnityEngine.Analytics;

namespace UnityEditor.UI.Analytics
{
    internal static class UGUIAnalytics
    {
        public static void Send (IAnalytic analytic)
        {
            if (!EditorAnalytics.enabled)
                return;
            EditorAnalytics.SendAnalytic(analytic);
        }
    }
}
