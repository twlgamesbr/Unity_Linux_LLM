using UnityEditor.PackageManager;

namespace Unity.Web.Stripping.Editor
{
    static class PackageConstants
    {
        // Used for documentation URLs. Major.minor is enough as the package docs are always uploaded for the most
        // the most recent major.minor version, e.g. docs for 1.2.0 and 1.2.3 are both uploaded as docs for 1.2.
        public const string MajorMinorVersion = "1.3";
        public static string VerboseVersion => PackageInfo.FindForPackageName(PackageName).version;
        // e.g. https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@0.1 which will be redirected to
        // e.g. https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@0.1/manual/index.html
        public const string DocumentationUrl = "https://docs.unity3d.com/Packages/" + PackageName + "@" + MajorMinorVersion;
        public const string PackageDisplayName = "Web Stripping Tool";
        public const string PackageName = "com.unity.web.stripping-tool";

        // If no 'page' provided, returns the default manual index page
        public static string GetDocumentationUrl(string page)
        {
            if (!string.IsNullOrEmpty(page))
                return $"{DocumentationUrl}/manual/{page}.html";
            else
                return DocumentationUrl;
        }
    }
}
