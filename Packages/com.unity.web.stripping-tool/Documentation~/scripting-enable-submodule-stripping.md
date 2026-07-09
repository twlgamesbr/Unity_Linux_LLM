# Enable submodule stripping with scripting

To configure submodule stripping, use the `SubmoduleStrippingSettings` class.

## Enable submodule stripping code example

This script displays how to use the C# Scripting API to enable or disable submodule stripping at the end of the build process.
Create a script in `Assets\Editor`:

```C#
using System.Collections.Generic;
using UnityEditor;
using Unity.Web.Stripping.Editor;

public class SubmoduleStrippingMenu
{
    [MenuItem("Window/Submodule Stripping/Enable Submodule Stripping")]
    public static void EnableSubmoduleStripping()
    {
        // Automatically run submodule stripping after the build
        StrippingProjectSettings.StripAutomaticallyAfterBuild = true;

        // Get the currently active stripping settings
        var settings = StrippingProjectSettings.ActiveSettings;

        // Set the list of submodules that should be stripped
        settings.SubmodulesToStrip = new List<string>() {
            "WebGPU Support"
        };

        // Run additional compiler optimizations pass after stripping
        settings.OptimizeCodeAfterStripping = true;
        // Remove embedded debug symbols (PlayerSettings.WebGL.debugSymbolMode == WebGLDebugSymbolMode.Embedded)
        // from build to save space
        settings.RemoveDebugInformation = true;

        settings.Save();
    }

    [MenuItem("Window/Submodule Stripping/Disable Submodule Stripping")]
    public static void DisableSubmoduleStripping()
    {
        // Disable automatic submodule stripping after the build
        StrippingProjectSettings.StripAutomaticallyAfterBuild = false;
    }
}
```

## Additional resources

* [Submodule reference](submodule-reference.md)
* [Identify unused submodules](identify-unused-submodules.md)
