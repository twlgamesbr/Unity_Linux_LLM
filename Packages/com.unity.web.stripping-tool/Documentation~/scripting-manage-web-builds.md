# Manage Web builds with scripting

To manage Web builds, use the `WebBuildReport` and `WebBuildReportList` classes.

This script displays how to use the C# Scripting API to add an existing Web build to the build list, remove a build from the build list and access the build list.
Create a script in `Assets\Editor`:

```C#
using UnityEngine;
using UnityEditor;
using Unity.Web.Stripping.Editor;

public class SubmoduleStrippingMenu
{
    [MenuItem("Window/Submodule Stripping/Import Web Build")]
    public static void ImportWebBuild()
    {
        // Adds an existing web build (from the following directory: [your Unity project]/Builds/ExistingWebBuild) to the build list.
        var buildPath = "Builds/ExistingWebBuild";
        WebBuildReport buildReport = WebBuildReportList.Instance.AddOrUpdateBuild(buildPath);

        Debug.Log($"Added build at: {buildReport.OutputPath}");
    }

    [MenuItem("Window/Submodule Stripping/Remove Web Build")]
    public static void RemoveWebBuild()
    {
        // Remove a build from the build list
        var buildPath = "Builds/ExistingWebBuild";
        WebBuildReport buildReport = WebBuildReportList.Instance.GetBuild(buildPath);
        WebBuildReportList.Instance.RemoveBuild(buildReport);

        Debug.Log($"Removed build at: {buildReport.OutputPath}");
    }

    [MenuItem("Window/Submodule Stripping/List Web Builds")]
    public static void ListWebBuilds()
    {
        // Print a list of all web builds to the console
        foreach(var buildReport in WebBuildReportList.Instance.Builds)
        {
            Debug.Log($"Web build at: {buildReport.OutputPath}\nWeb Assembly File at: {buildReport.WasmFilePath}");
        }
    }
}
```
