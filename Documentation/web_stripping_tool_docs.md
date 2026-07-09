### Get WebBuildReport WebPlayerSettings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Retrieves the stored WebPlayerSettings used to create this build.

```csharp
public WebPlayerSettings GetWebPlayerSettings()
```

--------------------------------

### Get WebBuildReport Backup Folder Path

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Returns the absolute path to the folder where the build's backup files are stored. This is valid if the OutputPath is valid and Update() has been called.

```csharp
public string GetBackupFolderPath()
```

--------------------------------

### WebBuildReportList Properties

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Access properties of the WebBuildReportList class to get build information or the singleton instance.

```APIDOC
## Properties

### Builds

**Description**: A list of all web build reports.

**Declaration**: `public List<WebBuildReport> Builds { get; }`

### Instance

**Description**: Get instance of `WebBuildReportList` (Singleton pattern).

**Declaration**: `public static WebBuildReportList Instance { get; }`
```

--------------------------------

### codeOptimization

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Gets or sets the code optimization level for web builds as a string. This uses UnityEditor.WebGL.WasmCodeOptimization values.

```APIDOC
## codeOptimization

### Description
Refer to the description of **Code Optimization** in Web Build Settings. The values used here are the `UnityEditor.WebGL.WasmCodeOptimization` values as strings, not the UI strings visible to the user.

### Declaration
```csharp
public string codeOptimization
```

### Field Value
- **Type**: string
- **Description**: 
```

--------------------------------

### development

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Gets or sets a boolean value indicating whether development build is enabled for web builds. This corresponds to EditorUserBuildSettings.development.

```APIDOC
## development

### Description
Refer to `EditorUserBuildSettings.development`.

### Declaration
```csharp
public bool development
```

### Field Value
- **Type**: bool
- **Description**: 
```

--------------------------------

### connectProfiler

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Gets or sets a boolean value indicating whether the profiler should connect for web builds. This corresponds to EditorUserBuildSettings.connectProfiler.

```APIDOC
## connectProfiler

### Description
Refer to `EditorUserBuildSettings.connectProfiler`.

### Declaration
```csharp
public bool connectProfiler
```

### Field Value
- **Type**: bool
- **Description**: 
```

--------------------------------

### WebBuildReport LastModifiedAt Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Gets the timestamp indicating when the build was last modified.

```csharp
public DateTimeOffset LastModifiedAt { get; }
```

--------------------------------

### webGLBuildSubtarget

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Gets or sets the WebGL build subtarget, which can override texture compression settings for web builds. This corresponds to EditorUserBuildSettings.webGLBuildSubtarget.

```APIDOC
## webGLBuildSubtarget

### Description
Texture compression override. Refer to `EditorUserBuildSettings.webGLBuildSubtarget`.

### Declaration
```csharp
public WebGLTextureSubtarget webGLBuildSubtarget
```

### Field Value
- **Type**: WebGLTextureSubtarget
- **Description**: 
```

--------------------------------

### buildWithDeepProfilingSupport

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Gets or sets a boolean value indicating whether deep profiling support is enabled for web builds. This corresponds to EditorUserBuildSettings.buildWithDeepProfilingSupport.

```APIDOC
## buildWithDeepProfilingSupport

### Description
Refer to `EditorUserBuildSettings.buildWithDeepProfilingSupport`.

### Declaration
```csharp
public bool buildWithDeepProfilingSupport
```

### Field Value
- **Type**: bool
- **Description**: 
```

--------------------------------

### Accessing and Modifying Active Stripping Settings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.StrippingProjectSettings.html

This snippet demonstrates how to get and set the active submodule stripping settings for the project. The ActiveSettings property allows you to retrieve the current settings or assign new ones.

```APIDOC
## Accessing Active Stripping Settings

### Description
Retrieves the currently active submodule stripping settings for the project.

### Property
`public static SubmoduleStrippingSettings ActiveSettings { get; set; }`

### Example
```csharp
SubmoduleStrippingSettings currentSettings = StrippingProjectSettings.ActiveSettings;
```

## Modifying Active Stripping Settings

### Description
Assigns new submodule stripping settings to be active for the project.

### Property
`public static SubmoduleStrippingSettings ActiveSettings { get; set; }`

### Example
```csharp
SubmoduleStrippingSettings newSettings = new SubmoduleStrippingSettings();
// Configure newSettings as needed
StrippingProjectSettings.ActiveSettings = newSettings;
```
```

--------------------------------

### FromEditorUserBuildSettings Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Creates a new WebBuildSettings instance populated with the current Unity editor build settings.

```csharp
public static WebBuildSettings FromEditorUserBuildSettings()
```

--------------------------------

### WebPlayerSettings.FromPlayerSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Constructs a new WebPlayerSettings object by reading values from the current Unity Player settings.

```APIDOC
## WebPlayerSettings.FromPlayerSettings()

### Description
Constructs a new `WebPlayerSettings` object with values read from current Player settings.

### Method
static WebPlayerSettings

### Returns
- **WebPlayerSettings**: `WebPlayerSettings` with the current Player settings.

### See Also
- WebBuildSettings
- WebPlayerSettingsScope
```

--------------------------------

### Create WebBuildReport from Path

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Creates a WebBuildReport for a given path, automatically converting relative paths to absolute and updating all build paths.

```csharp
public static WebBuildReport CreateFromPath(string outputPath)
```

--------------------------------

### SubmoduleStrippingSettings Methods

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Methods for creating and saving stripping settings.

```APIDOC
### Methods

#### Create(string assetPath)

Creates a settings asset.

*   **Parameters:**
    *   `assetPath` (string) - Note that the final name can be different if an asset with the same name already exists.
*   **Returns:**
    *   `SubmoduleStrippingSettings` - The created asset.

#### Save()

Save changes to the settings to disk.
```

--------------------------------

### WebPlayerSettings Constructor

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Constructs a new instance of WebPlayerSettings with default values.

```csharp
public WebPlayerSettings()

```

--------------------------------

### Create Settings Asset

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Creates a new SubmoduleStrippingSettings asset at the specified path. If an asset with the same name exists, the final name may differ.

```csharp
public static SubmoduleStrippingSettings Create(string assetPath)
```

--------------------------------

### WebBuildSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Constructs a new WebBuildSettings object with default-initialized values.

```APIDOC
## WebBuildSettings()

### Description
Constructs with default-initialized values.

### Declaration
```csharp
public WebBuildSettings()
```
```

--------------------------------

### WebPlayerSettingsScope Class Overview

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettingsScope.html

This snippet provides an overview of the WebPlayerSettingsScope class, its purpose, inheritance, and implementation details. It highlights the OriginalSettings property and the Dispose method.

```APIDOC
## Class WebPlayerSettingsScope

A helper class for saving and restoring Web Player and build settings when making changes.

### Inheritance
object
WebPlayerSettingsScope

### Implements
IDisposable

### Namespace
Unity.Web.Stripping.Editor

### Assembly
Unity.Web.Stripping.Editor.dll

### Syntax
```csharp
public class WebPlayerSettingsScope : IDisposable
```

### Properties
#### OriginalSettings
The original settings when an instance of this class is created.

##### Declaration
```csharp
public WebPlayerSettings OriginalSettings { get; }
```

##### Property Value
Type | Description
---|---
WebPlayerSettings | The original settings.

### Methods
#### Dispose()
Restores the original settings.

##### Declaration
```csharp
public void Dispose()
```

### Implements
IDisposable
```

--------------------------------

### FromEditorUserBuildSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Constructs a new WebBuildSettings object populated with the current build settings from UnityEditor.EditorUserBuildSettings.

```APIDOC
## FromEditorUserBuildSettings()

### Description
Constructs a new `WebBuildSettings` with values read from current build settings.

### Declaration
```csharp
public static WebBuildSettings FromEditorUserBuildSettings()
```

### Returns
- **Type**: WebBuildSettings
- **Description**: `WebPlayerSettings` with the current Player settings.
```

--------------------------------

### GetBuild Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Retrieves a web build report by its path. Handles both relative and absolute paths.

```csharp
public WebBuildReport GetBuild(string path)
```

--------------------------------

### SubmoduleStrippingSettings Fields

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Configuration options for handling missing submodules, optimizing code, setting the root menu name, and specifying submodules to strip.

```APIDOC
### Fields

#### MissingSubmoduleErrorHandling

The error handling behavior when a stripped submodule is used. The usage of a stripped submodule can be ignored, logged to the browser console, or thrown as an exception.

*   **Type:** MissingSubmoduleErrorHandlingType

#### OptimizeCodeAfterStripping

Run code optimization to reduce final build size and improve performance. Increases the stripping time significantly. Use on release builds.

*   **Type:** bool

#### RootMenuName

The root menu name used for various menu items.

*   **Type:** string
*   **Value:** "Web Optimization"

#### SubmodulesToStrip

The list of submodules to strip from a build.

*   **Type:** List<string>
```

--------------------------------

### WebPlayerSettings.WriteToPlayerSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Applies the settings from the current WebPlayerSettings object to UnityEditor.PlayerSettings.WebGL.

```APIDOC
## WebPlayerSettings.WriteToPlayerSettings()

### Description
Applies the settings to `UnityEditor.PlayerSettings.WebGL`.

### Method
void

### See Also
- WebBuildSettings
- WebPlayerSettingsScope
```

--------------------------------

### Create WebPlayerSettings from Player Settings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Constructs a new WebPlayerSettings object by reading values from the current Unity Player settings. This is useful for initializing settings based on the project's current configuration.

```csharp
public static WebPlayerSettings FromPlayerSettings()

```

--------------------------------

### WebBuildReport Constructor

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Initializes a new instance of the WebBuildReport class with default settings.

```APIDOC
## WebBuildReport()

### Description
Default constructor for the WebBuildReport class.

### Method
`WebBuildReport()`

### Declaration
```csharp
public WebBuildReport()
```
```

--------------------------------

### MethodMapFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Path to the IL2CPP method map file associated with this build.

```csharp
public string MethodMapFilePath

```

--------------------------------

### SubmodulesToStrip Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Lists the submodules that should be stripped from the build to reduce its size.

```csharp
[Tooltip("The list of submodules to strip from a build.")]
public List<string> SubmodulesToStrip
```

--------------------------------

### WebBuildSettings Constructor

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Constructs a new WebBuildSettings object with default values.

```csharp
public WebBuildSettings()
```

--------------------------------

### ReadFromEditorUserBuildSettings Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Applies the current Unity editor build settings to this WebBuildSettings object.

```csharp
public void ReadFromEditorUserBuildSettings()
```

--------------------------------

### Update Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates the information for all builds in the list to reflect their current state.

```csharp
public void Update()
```

--------------------------------

### WebPlayerSettings BuildSettings Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Stores the build settings used in conjunction with Player settings.

```csharp
public WebBuildSettings BuildSettings

```

--------------------------------

### Name Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

The display name of the build.

```csharp
public string Name

```

--------------------------------

### WriteToEditorUserBuildSettings Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Applies the settings stored in this WebBuildSettings object to the UnityEditor.EditorUserBuildSettings.

```csharp
public void WriteToEditorUserBuildSettings()
```

--------------------------------

### WriteToEditorUserBuildSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Applies the settings stored in this WebBuildSettings object to UnityEditor.EditorUserBuildSettings.

```APIDOC
## WriteToEditorUserBuildSettings()

### Description
Applies the settings to `UnityEditor.EditorUserBuildSettings`.

### Declaration
```csharp
public void WriteToEditorUserBuildSettings()
```
```

--------------------------------

### WebBuildReport BuildPath Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Retrieves the path to the build data within the build directory.

```csharp
public string BuildPath { get; }
```

--------------------------------

### Restore WebBuildReport

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Restores the build from its backup files and removes any extraneous files.

```csharp
public void Restore()
```

--------------------------------

### SubmoduleStrippingSettings Class

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

This class serves as an asset for configuring submodule stripping settings. It inherits from ScriptableObject and is intended to be created as an asset in the Unity Editor.

```APIDOC
## Class SubmoduleStrippingSettings

An asset for configuring submodule stripping settings.

##### Inheritance
object
Object
ScriptableObject
SubmoduleStrippingSettings

##### Syntax
```
[CreateAssetMenu(fileName = "SubmoduleStrippingSettings", menuName = "Web Optimization/Submodule Stripping Settings")]
[HelpURL("https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.3/manual/submodule-reference.html")]
public class SubmoduleStrippingSettings : ScriptableObject
```

**Namespace**: Unity.Web.Stripping.Editor
**Assembly**: Unity.Web.Stripping.Editor.dll
```

--------------------------------

### OutputPath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

The root folder path where the build is located.

```csharp
public string OutputPath

```

--------------------------------

### powerPreference Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Configures the power preference for WebGL builds. This setting can influence performance and battery consumption.

```csharp
public WebGLPowerPreference powerPreference

```

--------------------------------

### ReadFromEditorUserBuildSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Applies the current build settings from UnityEditor.EditorUserBuildSettings to this WebBuildSettings object.

```APIDOC
## ReadFromEditorUserBuildSettings()

### Description
Applies the current build settings to this object.

### Declaration
```csharp
public void ReadFromEditorUserBuildSettings()
```
```

--------------------------------

### WebPlayerSettings initialMemorySize Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the initial memory size for WebGL. Refer to PlayerSettings.WebGL.initialMemorySize.

```csharp
public int initialMemorySize

```

--------------------------------

### WebPlayerSettings exceptionSupport Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Configures exception support for WebGL. Refer to PlayerSettings.WebGL.exceptionSupport.

```csharp
public WebGLExceptionSupport exceptionSupport

```

--------------------------------

### InstrumentBuild

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildProcessor.html

Adds submodule profiling to a web build. This method shows a progress bar and is not available for stripped or already instrumented builds.

```APIDOC
## InstrumentBuild

### Description
Adds submodule profiling to a build. This method shows a progress bar and is not available for stripped or already instrumented builds.

### Method
`public static bool InstrumentBuild(WebBuildReport build)`

### Parameters
#### Path Parameters
- None

#### Query Parameters
- None

#### Request Body
- None

### Parameters
- **build** (WebBuildReport) - Required - The Web build.

### Returns
- **bool** - Returns 'true' if the build was successfully instrumented, 'false' otherwise.

### Remarks
Shows a progress bar. This option isn't available for stripped builds or builds that are already instrumented for submodule profiling.
```

--------------------------------

### Update()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates the information for all builds in the list to reflect their current build directory contents.

```APIDOC
## Update()

### Description

Updates the information of all builds to match the contents of their build directories.

### Remarks

Doesn't trigger `BuildsUpdated` as the list itself doesn't change.

### Method

`public void Update()`
```

--------------------------------

### WebPlayerSettings.ReadFromPlayerSettings()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Applies the settings from UnityEditor.PlayerSettings.WebGL to the current WebPlayerSettings object.

```APIDOC
## WebPlayerSettings.ReadFromPlayerSettings()

### Description
Applies the settings from `UnityEditor.PlayerSettings.WebGL` to this object.

### Method
void

### See Also
- WebBuildSettings
- WebPlayerSettingsScope
```

--------------------------------

### OptimizeCodeAfterStripping Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Enables code optimization to reduce build size and improve performance. This significantly increases stripping time and should be used on release builds.

```csharp
[Tooltip("Run code optimization to reduce final build size and improve performance. Increases the stripping time significantly. Use on release builds.")]
public bool OptimizeCodeAfterStripping
```

--------------------------------

### SubmoduleStrippingSettings Class Definition

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Defines the SubmoduleStrippingSettings class, inheriting from ScriptableObject. It includes attributes for creating an asset menu item and a help URL.

```csharp
[
    CreateAssetMenu(fileName = "SubmoduleStrippingSettings", menuName = "Web Optimization/Submodule Stripping Settings")
]
[HelpURL("https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.3/manual/submodule-reference.html")]
public class SubmoduleStrippingSettings : ScriptableObject

```

--------------------------------

### StripBuild

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildProcessor.html

Performs submodule stripping for a web build using specified settings. The build is restored to its original state before stripping.

```APIDOC
## StripBuild

### Description
Perform submodule stripping for a build. Before a build is stripped, it is restored to its original state, meaning, existing submodule profiling instrumentation or submodule stripping will be reverted. Shows a progress bar.

### Method
`public static bool StripBuild(WebBuildReport build, SubmoduleStrippingSettings settings)`

### Parameters
#### Path Parameters
- None

#### Query Parameters
- None

#### Request Body
- None

### Parameters
- **build** (WebBuildReport) - Required - The Web build.
- **settings** (SubmoduleStrippingSettings) - Required - The stripping settings to be used.

### Returns
- **bool** - Returns 'true' if submodule stripping was performed, 'false' otherwise.

### Remarks
Before a build is stripped, it is restored to its original state, meaning, existing submodule profiling instrumentation or submodule stripping will be reverted. Shows a progress bar.
```

--------------------------------

### FrameworkFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Path to the JavaScript framework file used in the build.

```csharp
public string FrameworkFilePath

```

--------------------------------

### WebPlayerSettings il2CppCompilerConfiguration Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the IL2CPP compiler configuration. Refer to Il2CppCompilerConfiguration.

```csharp
public Il2CppCompilerConfiguration il2CppCompilerConfiguration

```

--------------------------------

### WebBuildReport HasStrippingInfo Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Checks if the build includes a stripping info file.

```csharp
public bool HasStrippingInfo { get; }
```

--------------------------------

### memorySize Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the initial memory size for the WebGL build. This is specified in megabytes.

```csharp
public int memorySize

```

--------------------------------

### PlayerSettingsFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Path to the Player settings file used for this specific build.

```csharp
public string PlayerSettingsFilePath

```

--------------------------------

### WebBuildReport TemplateDataPath Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Retrieves the path to the template data within the build directory.

```csharp
public string TemplateDataPath { get; }
```

--------------------------------

### WebPlayerSettings Class

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

The WebPlayerSettings class aggregates various Web Player and Web Build settings, particularly those related to WebGL. It provides fields to access and configure settings such as graphics APIs, memory management, compression, and debugging.

```APIDOC
## Class WebPlayerSettings
A structure that gathers up all Web Player settings, in particular, those defined in (`UnityEditor.PlayerSettings.WebGL`), and Web Build Settings.

### Constructors
#### WebPlayerSettings()
Constructs with default-initialized values.

### Fields
#### BuildSettings
The build settings used together with the Player settings.
- **Type**: WebBuildSettings

#### autoGraphicsAPI
Whether Auto Graphics API is enabled for WebGL. Refer to `PlayerSettings.GetUseDefaultGraphicsAPIs`.
- **Type**: bool?

#### closeOnQuit
Refer to `PlayerSettings.WebGL.closeOnQuit`.
- **Type**: bool

#### compressionFormat
Refer to `PlayerSettings.WebGL.compressionFormat`.
- **Type**: WebGLCompressionFormat

#### dataCaching
Refer to `PlayerSettings.WebGL.dataCaching`.
- **Type**: bool

#### debugSymbolMode
Refer to `PlayerSettings.WebGL.debugSymbolMode`.
- **Type**: WebGLDebugSymbolMode

#### decompressionFallback
Refer to `PlayerSettings.WebGL.decompressionFallback`.
- **Type**: bool

#### enableSubmoduleStrippingCompatibility
Refer to `PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility`.
- **Type**: bool

#### exceptionSupport
Refer to `PlayerSettings.WebGL.exceptionSupport`.
- **Type**: WebGLExceptionSupport

#### geometricMemoryGrowthStep
Refer to `PlayerSettings.WebGL.geometricMemoryGrowthStep`.
- **Type**: float

#### graphicsAPIs
The list of Graphics APIs enabled for WebGL. Refer to `PlayerSettings.GetGraphicsAPIs`.
- **Type**: GraphicsDeviceType[]

#### il2CppCodeGeneration
Refer to `Il2CppCodeGeneration`.
- **Type**: Il2CppCodeGeneration

#### il2CppCompilerConfiguration
Refer to `Il2CppCompilerConfiguration`.
- **Type**: Il2CppCompilerConfiguration

#### initialMemorySize
Refer to `PlayerSettings.WebGL.initialMemorySize`.
- **Type**: int

#### linearMemoryGrowthStep
Refer to `PlayerSettings.WebGL.linearMemoryGrowthStep`.
- **Type**: int
```

--------------------------------

### Save Settings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Saves any changes made to the settings asset to disk.

```csharp
public void Save()
```

--------------------------------

### WebBuildReportList Instance Property (Singleton)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Retrieves the singleton instance of the WebBuildReportList class.

```csharp
public static WebBuildReportList Instance { get; }
```

--------------------------------

### WebPlayerSettings enableSubmoduleStrippingCompatibility Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables compatibility for submodule stripping in WebGL. Refer to PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility.

```csharp
public bool enableSubmoduleStrippingCompatibility

```

--------------------------------

### WebBuildReportList Builds Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Accesses the list of all web build reports managed by the WebBuildReportList.

```csharp
public List<WebBuildReport> Builds { get; }
```

--------------------------------

### UnityVersion Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Stores the Unity version that was used to create this build.

```csharp
public string UnityVersion

```

--------------------------------

### FrameworkBackupFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Optional path to the backup JavaScript framework file. This is created when a build is instrumented for profiling.

```csharp
public string FrameworkBackupFilePath

```

--------------------------------

### WasmFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Path to the WebAssembly file of the build.

```csharp
public string WasmFilePath

```

--------------------------------

### WebPlayerSettings Fields

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

This section outlines the configurable fields within WebPlayerSettings for WebGL builds. Each field provides specific control over build parameters and runtime behavior.

```APIDOC
## linkerTarget

### Description
Configures the linker target for WebGL builds. This setting determines how the C++ code is linked.

### Field Value
- **linkerTarget** (WebGLLinkerTarget) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## managedStrippingLevel

### Description
Determines the level of managed code stripping to apply to the build. Higher levels result in smaller build sizes but may remove necessary code.

### Field Value
- **managedStrippingLevel** (ManagedStrippingLevel) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## maximumMemorySize

### Description
Sets the maximum memory size that the WebGL application can allocate. This is a hard limit for the application's memory footprint.

### Field Value
- **maximumMemorySize** (int) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## memoryGeometricGrowthCap

### Description
Defines the cap for geometric memory growth. This setting controls how much memory can grow in a geometric fashion.

### Field Value
- **memoryGeometricGrowthCap** (int) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## memoryGrowthMode

### Description
Specifies the mode for memory growth in WebGL applications. This affects how the application manages its memory allocation over time.

### Field Value
- **memoryGrowthMode** (WebGLMemoryGrowthMode) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## memorySize

### Description
Sets the initial memory size for the WebGL application. This is the amount of memory allocated when the application starts.

### Field Value
- **memorySize** (int) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## nameFilesAsHashes

### Description
When enabled, this setting names files using hashes instead of their original names. This can help with caching and content delivery.

### Field Value
- **nameFilesAsHashes** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## powerPreference

### Description
Determines the power preference for the WebGL build. This can affect performance and battery consumption.

### Field Value
- **powerPreference** (WebGLPowerPreference) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## runInBackground

### Description
Allows the WebGL application to continue running even when it is not in the foreground. Note: This is a nullable boolean.

### Field Value
- **runInBackground** (bool?) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## scriptingDefineSymbols

### Description
Sets custom scripting define symbols for the WebGL build. These symbols can be used in conditional compilation within your scripts.

### Field Value
- **scriptingDefineSymbols** (string) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## showDiagnostics

### Description
Enables or disables the display of diagnostic information in the WebGL build. Useful for debugging.

### Field Value
- **showDiagnostics** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## showSplashScreen

### Description
Controls whether the splash screen is displayed when the WebGL application starts. Note: This is a nullable boolean.

### Field Value
- **showSplashScreen** (bool?) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## stripEngineCode

### Description
When enabled, this setting strips unused engine code from the build to reduce its size. Note: This is a nullable boolean.

### Field Value
- **stripEngineCode** (bool?) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## template

### Description
Specifies the HTML template to be used for the WebGL build. This allows for custom branding and layout.

### Field Value
- **template** (string) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## threadsSupport

### Description
Enables or disables support for multi-threading in the WebGL build. This can improve performance for CPU-intensive tasks.

### Field Value
- **threadsSupport** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## useEmbeddedResources

### Description
Determines whether to embed resources directly into the build or load them separately. Embedding can simplify deployment but may increase build size.

### Field Value
- **useEmbeddedResources** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## wasm2023

### Description
Enables support for the WASM 2023 features. This flag is used to opt into newer WebAssembly specifications.

### Field Value
- **wasm2023** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## webAssemblyBigInt

### Description
Enables support for BigInt operations within WebAssembly. This is useful for calculations involving arbitrarily large integers.

### Field Value
- **webAssemblyBigInt** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope

## webAssemblyTable

### Description
Enables or disables the WebAssembly table. The table is used for indirect function calls and managing function pointers.

### Field Value
- **webAssemblyTable** (bool) - Description: N/A

### See Also
- WebBuildSettings
- WebPlayerSettingsScope
```

--------------------------------

### GetBuild(string)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Finds the web build report for a given path, converting relative paths to absolute paths automatically.

```APIDOC
## GetBuild(string)

### Description

Find the web build report for the given path in the build list. If the path is a relative path, it will be automatically converted to an absolute path.

### Method

`public WebBuildReport GetBuild(string path)`

### Parameters

#### Path Parameters

- **path** (string) - Required - Path to a web build.

### Returns

- **WebBuildReport** - A web build report or null if build is not in the list.
```

--------------------------------

### AddOrUpdateBuild Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Adds a new web build report or updates an existing one based on the provided path.

```csharp
public WebBuildReport AddOrUpdateBuild(string path)
```

--------------------------------

### WebPlayerSettings compressionFormat Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Specifies the compression format for WebGL builds. Refer to PlayerSettings.WebGL.compressionFormat.

```csharp
public WebGLCompressionFormat compressionFormat

```

--------------------------------

### WebPlayerSettings il2CppCodeGeneration Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Configures IL2CPP code generation settings. Refer to Il2CppCodeGeneration.

```csharp
public Il2CppCodeGeneration il2CppCodeGeneration

```

--------------------------------

### StrippingInfoFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Optional path to the `stripping_info.json` file, which contains information about the stripping process.

```csharp
public string StrippingInfoFilePath

```

--------------------------------

### SubmoduleStrippingSettings Events

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Event raised when settings values are changed.

```APIDOC
### Events

#### ValuesChanged

Raised when the values of the settings are changed.

*   **Type:** Action
```

--------------------------------

### Subscribing to Settings Changes

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.StrippingProjectSettings.html

This snippet demonstrates how to subscribe to the SettingsChanged event, which is raised whenever the active submodule stripping settings are modified.

```APIDOC
## Subscribing to SettingsChanged Event

### Description
Subscribes a method to be called when the active submodule stripping settings change.

### Event
`public static event Action<SubmoduleStrippingSettings> SettingsChanged`

### Example
```csharp
public void OnSettingsChanged(SubmoduleStrippingSettings newSettings)
{
    Debug.Log("Stripping settings have been updated.");
    // Handle the new settings
}

void Start()
{
    StrippingProjectSettings.SettingsChanged += OnSettingsChanged;
}

void OnDestroy()
{
    StrippingProjectSettings.SettingsChanged -= OnSettingsChanged;
}
```
```

--------------------------------

### Check if Build Path is Valid

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Verifies if the specified build path contains the expected files for a valid web build.

```csharp
public static bool IsValidBuildPath(string buildPath)
```

--------------------------------

### OriginalPlayerSettingsFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Stores the original player settings file path if they were modified by SubmoduleStrippingBuildProcessor.

```csharp
public string OriginalPlayerSettingsFilePath

```

--------------------------------

### webGLBuildSubtarget Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Sets the texture compression override for WebGL builds, referencing UnityEditor.EditorUserBuildSettings.webGLBuildSubtarget.

```csharp
public WebGLTextureSubtarget webGLBuildSubtarget
```

--------------------------------

### UpdateBuild(WebBuildReport)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates a given WebBuildReport object and serializes the build list, triggering BuildsUpdated callbacks.

```APIDOC
## UpdateBuild(WebBuildReport)

### Description

Update the given build report object and serialize build list. This will also trigger the BuildsUpdated callbacks.

### Method

`public void UpdateBuild(WebBuildReport build)`

### Parameters

#### Path Parameters

- **build** (WebBuildReport) - Required - A web build report object.

### Remarks

This method will also trigger the `BuildsUpdated` callbacks.
```

--------------------------------

### buildWithDeepProfilingSupport Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Represents the buildWithDeepProfilingSupport setting, referencing UnityEditor.EditorUserBuildSettings.buildWithDeepProfilingSupport.

```csharp
public bool buildWithDeepProfilingSupport
```

--------------------------------

### WebPlayerSettings Class Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Declares the WebPlayerSettings class, which is marked as serializable.

```csharp
public class WebPlayerSettings

```

--------------------------------

### WasmBackupFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Optional path to the backup WebAssembly file. This is created when a build is stripped or instrumented for profiling.

```csharp
public string WasmBackupFilePath

```

--------------------------------

### UpdateBuild Method (by WebBuildReport object)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates a given web build report object and serializes the build list, triggering callbacks.

```csharp
public void UpdateBuild(WebBuildReport build)
```

--------------------------------

### Read WebPlayerSettings from Player Settings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Applies the settings from UnityEditor.PlayerSettings.WebGL to the current WebPlayerSettings object. Use this to update existing settings with values from the Unity Editor's Player Settings.

```csharp
public void ReadFromPlayerSettings()

```

--------------------------------

### PlayerSettingFileName Constant Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Defines the constant file name for storing player and build settings.

```csharp
public const string PlayerSettingFileName = "player_settings.json"

```

--------------------------------

### development Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Indicates if the build is a development build, referencing UnityEditor.EditorUserBuildSettings.development.

```csharp
public bool development
```

--------------------------------

### showSplashScreen Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Controls whether the splash screen is displayed for WebGL builds. This is a nullable boolean.

```csharp
public bool? showSplashScreen

```

--------------------------------

### runInBackground Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Determines whether the WebGL application should continue running when in the background. This is a nullable boolean.

```csharp
public bool? runInBackground

```

--------------------------------

### WebBuildReport IsValid Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Determines if the build located at the specified path is a valid web build.

```csharp
public bool IsValid { get; }
```

--------------------------------

### connectProfiler Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Determines whether to connect the profiler during the build, referencing UnityEditor.EditorUserBuildSettings.connectProfiler.

```csharp
public bool connectProfiler
```

--------------------------------

### SymbolFilePath Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Optional path to the external debug symbols file for the build.

```csharp
public string SymbolFilePath

```

--------------------------------

### WebPlayerSettings linearMemoryGrowthStep Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the linear memory growth step for WebGL. Refer to PlayerSettings.WebGL.linearMemoryGrowthStep.

```csharp
public int linearMemoryGrowthStep

```

--------------------------------

### WebBuildReport Methods

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Methods of the WebBuildReport class.

```APIDOC
## Methods

### CreateFromPath(string)
Create a web build report for a given path. Automatically updates all paths of a build. If the path is a relative path, it will be automatically converted to an absolute path.

**Declaration**
```csharp
public static WebBuildReport CreateFromPath(string outputPath)
```

**Parameters**
Type | Name | Description
---|---|---
string | outputPath | For example, "D:/MyProject/Builds/MyBuild", "Builds/MyBuild"

**Returns**
Type | Description
---|---
WebBuildReport | The build report

### GetBackupFolderPath()
Returns the folder path where this build's back-up files are stored.

**Declaration**
```csharp
public string GetBackupFolderPath()
```

**Returns**
Type | Description
---|---
string | Absolute path to the back-up folder.

**Remarks**
Valid if OutputPath is valid and Update() has been called.

### GetWebPlayerSettings()
Get the stored `WebPlayerSettings` for this build.

**Declaration**
```csharp
public WebPlayerSettings GetWebPlayerSettings()
```

**Returns**
Type | Description
---|---
WebPlayerSettings | A `WebPlayerSettings` object with all settings that were used to create this build.

### IsValidBuildPath(string)
Checks whether the "/Build" folder of a build contains the expected files.

**Declaration**
```csharp
public static bool IsValidBuildPath(string buildPath)
```

**Parameters**
Type | Name | Description
---|---|---
string | buildPath | E.g. "Path/To/MyBuild/Build"

**Returns**
Type | Description
---|---
bool | True if build path is valid.

### Restore()
Restores the build from its back-up files and removes any additional files we might have.

**Declaration**
```csharp
public void Restore()
```

### Update()
Reads the files from the build path and updates the fields accordingly.

**Declaration**
```csharp
public void Update()
```
```

--------------------------------

### template Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Specifies the HTML template used for the WebGL build. This allows for custom web page structures.

```csharp
public string template

```

--------------------------------

### Write WebPlayerSettings to Player Settings

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Applies the current WebPlayerSettings to UnityEditor.PlayerSettings.WebGL. This method saves the settings, making them active for WebGL builds.

```csharp
public void WriteToPlayerSettings()

```

--------------------------------

### MethodMapFileName Constant Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Defines the constant file name for the method map.

```csharp
public const string MethodMapFileName = "MethodMap.tsv"

```

--------------------------------

### WebPlayerSettingsScope Class Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettingsScope.html

Declares the WebPlayerSettingsScope class, which inherits from object and implements IDisposable. This class is used to manage build settings.

```csharp
public class WebPlayerSettingsScope : IDisposable
```

--------------------------------

### UpdateBuild Method (by path)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates the build report for a specific path if it exists in the list.

```csharp
public WebBuildReport UpdateBuild(string path)
```

--------------------------------

### AdditionalFiles Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Represents a list of additional files that may be added to the build folder by submodule stripping or profiling instrumentation.

```csharp
public List<string> AdditionalFiles

```

--------------------------------

### webAssemblyBigInt Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables support for BigInt in WebAssembly for WebGL builds. This allows handling of arbitrarily large integers.

```csharp
public bool webAssemblyBigInt

```

--------------------------------

### AddOrUpdateBuild(string)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Adds a build at the specified path to the list or updates an existing build report if it's already present.

```APIDOC
## AddOrUpdateBuild(string)

### Description

Add a build at the given path to the list of builds or update the existing build report if the build is already in the list.

### Method

`public WebBuildReport AddOrUpdateBuild(string path)`

### Parameters

#### Path Parameters

- **path** (string) - Required - Path to a web build.

### Returns

- **WebBuildReport** - A web build report object.
```

--------------------------------

### WebBuildReport Default Constructor

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

The default constructor for the WebBuildReport class. It initializes a new instance of the class.

```csharp
public WebBuildReport()

```

--------------------------------

### WebBuildReportList Class Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Declares the WebBuildReportList class within the Unity.Web.Stripping.Editor namespace.

```csharp
public class WebBuildReportList
{
}
```

--------------------------------

### scriptingDefineSymbols Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Retrieves or sets the scripting define symbols for the WebGL build. These symbols can be used to conditionally compile code.

```csharp
public string scriptingDefineSymbols

```

--------------------------------

### SubmoduleStrippingSettings Properties

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Properties for controlling debug information removal and accessing settings.

```APIDOC
### Properties

#### RemoveDebugInformation

Remove debug information after stripping. Debug symbols are required to identify functions during stripping but they increase the size of WebAssembly files. Use on release builds if debug symbols are not required for other use cases.

*   **Type:** bool
*   **Access:** get; set;
```

--------------------------------

### WebBuildReport Fields

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

The WebBuildReport class contains fields that provide details about a Unity Web build, such as file paths, versions, and build characteristics.

```APIDOC
## WebBuildReport Fields

This document outlines the fields available in the `WebBuildReport` class.

### Fields

- **AdditionalFiles** (List<string>) - Additional files that may be added to the build folder by submodule stripping or profiling instrumentation.
- **EmscriptenVersion** (string) - The Emscripten version used to create this build.
- **FrameworkBackupFilePath** (string) - Optional: Path to the backup JavaScript framework file. This file is created when a build is instrumented for profiling.
- **FrameworkFilePath** (string) - Path to the JavaScript framework file.
- **HasSubmoduleProfiling** (bool) - Indicates if the build was instrumented for submodule profiling.
- **LastModifiedAtUniversal** (string) - Timestamp when the build was last modified, in universal time format.
- **MethodMapFileName** (const string) - The file name of the method map (`MethodMap.tsv`).
- **MethodMapFilePath** (string) - Path to the IL2CPP method map of this build.
- **Name** (string) - Display name of the build.
- **OriginalPlayerSettingsFilePath** (string) - Stores original player settings if modified by `SubmoduleStrippingBuildProcessor`.
- **OriginalWasmSize** (long) - Size of the original WebAssembly file in bytes.
- **OutputPath** (string) - The root folder of the build.
- **PlayerSettingFileName** (const string) - The file name used to store Player and build settings (`player_settings.json`).
- **PlayerSettingsFilePath** (string) - Path to the Player settings file used for this build.
- **StrippedWasmSize** (long) - Optional: Size of the stripped WebAssembly file in bytes.
- **StrippingInfoFilePath** (string) - Optional: Path to the `stripping_info.json` file.
- **SymbolFilePath** (string) - Optional: Path to the external debug symbols file.
- **UnityVersion** (string) - The Unity version used to create this build.
- **WasmBackupFilePath** (string) - Optional: Path to the backup WebAssembly file. This file is created when a build is stripped or instrumented for profiling.
- **WasmFilePath** (string) - Path to the WebAssembly file.
```

--------------------------------

### HasSubmoduleProfiling Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Indicates whether the build has been instrumented for submodule profiling.

```csharp
public bool HasSubmoduleProfiling

```

--------------------------------

### UpdateBuild(string)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Updates the build information for a build located at the specified path.

```APIDOC
## UpdateBuild(string)

### Description

Update the build at the given path.

### Method

`public WebBuildReport UpdateBuild(string path)`

### Parameters

#### Path Parameters

- **path** (string) - Required - Path to a web build.

### Returns

- **WebBuildReport** - A web build report object or null if the build is not in the build list.
```

--------------------------------

### managedStrippingLevel Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the level of managed code stripping for WebGL builds. Higher levels result in smaller build sizes by removing unused code.

```csharp
public ManagedStrippingLevel managedStrippingLevel

```

--------------------------------

### OriginalWasmSize Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

The size of the original WebAssembly file in bytes before any stripping or instrumentation.

```csharp
public long OriginalWasmSize

```

--------------------------------

### nameFilesAsHashes Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables naming files as hashes for WebGL builds. When true, files are named using their hash values.

```csharp
public bool nameFilesAsHashes

```

--------------------------------

### WebPlayerSettings geometricMemoryGrowthStep Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the geometric memory growth step for WebGL. Refer to PlayerSettings.WebGL.geometricMemoryGrowthStep.

```csharp
public float geometricMemoryGrowthStep

```

--------------------------------

### Controlling Automatic Stripping After Build

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.StrippingProjectSettings.html

This snippet shows how to enable or disable the automatic submodule stripping pass that occurs after a build is completed.

```APIDOC
## Enabling Automatic Stripping After Build

### Description
Enables a submodule stripping pass to run automatically after a build completes, using the currently active settings.

### Property
`public static bool StripAutomaticallyAfterBuild { get; set; }`

### Example
```csharp
StrippingProjectSettings.StripAutomaticallyAfterBuild = true;
```

## Disabling Automatic Stripping After Build

### Description
Disables the automatic submodule stripping pass that runs after a build.

### Property
`public static bool StripAutomaticallyAfterBuild { get; set; }`

### Example
```csharp
StrippingProjectSettings.StripAutomaticallyAfterBuild = false;
```
```

--------------------------------

### linkerTarget Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Configures the linker target for WebGL builds. This setting determines how the C++ code is linked.

```csharp
public WebGLLinkerTarget linkerTarget

```

--------------------------------

### WebBuildSettings Class Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Declares the WebBuildSettings class, marked as Serializable.

```csharp
public class WebBuildSettings
```

--------------------------------

### Dispose Method Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettingsScope.html

Declares the Dispose method for the WebPlayerSettingsScope class. This method is part of the IDisposable interface and is responsible for restoring the original build settings.

```csharp
public void Dispose()
```

--------------------------------

### StrippedWasmSize Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Optional: The size of the stripped WebAssembly file in bytes.

```csharp
public long StrippedWasmSize

```

--------------------------------

### LastModifiedAtUniversal Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Timestamp of the last build modification, formatted as a string in universal time.

```csharp
public string LastModifiedAtUniversal

```

--------------------------------

### ClearBuilds()

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Removes all builds from the build list.

```APIDOC
## ClearBuilds()

### Description

Removes all builds from the build list.

### Method

`public void ClearBuilds()`
```

--------------------------------

### WebBuildReport Properties

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Properties of the WebBuildReport class.

```APIDOC
## Properties

### BuildPath
Path to build data inside build for example, "D:/MyProject/Builds/MyBuild/Build"

**Declaration**
```csharp
public string BuildPath { get; }
```

**Property Value**
Type | Description
---|---
string |

### HasStrippingInfo
Is true when the build has a stripping info file.

**Declaration**
```csharp
public bool HasStrippingInfo { get; }
```

**Property Value**
Type | Description
---|---
bool |

### IsValid
Returns true if the build at Path is a valid web build.

**Declaration**
```csharp
public bool IsValid { get; }
```

**Property Value**
Type | Description
---|---
bool |

### LastModifiedAt
Timestamp when build was last modified.

**Declaration**
```csharp
public DateTimeOffset LastModifiedAt { get; }
```

**Property Value**
Type | Description
---|---
DateTimeOffset |

### TemplateDataPath
Path to template data inside build for example, "D:/MyProject/Builds/MyBuild/TemplateData"

**Declaration**
```csharp
public string TemplateDataPath { get; }
```

**Property Value**
Type | Description
---|---
string |

```

--------------------------------

### WebPlayerSettings graphicsAPIs Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Specifies the list of Graphics APIs enabled for WebGL. Refer to PlayerSettings.GetGraphicsAPIs.

```csharp
public GraphicsDeviceType[] graphicsAPIs

```

--------------------------------

### RootMenuName Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Specifies the root menu name used for various menu items within the Unity editor.

```csharp
public const string RootMenuName = "Web Optimization"
```

--------------------------------

### useEmbeddedResources Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Determines whether resources should be embedded directly into the WebGL build. When true, resources are included within the build files.

```csharp
public bool useEmbeddedResources

```

--------------------------------

### showDiagnostics Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables the display of diagnostic information for WebGL builds. Useful for debugging.

```csharp
public bool showDiagnostics

```

--------------------------------

### WebPlayerSettings closeOnQuit Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Controls whether the application closes when quitting. Refer to PlayerSettings.WebGL.closeOnQuit.

```csharp
public bool closeOnQuit

```

--------------------------------

### ClearBuilds Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Removes all web build reports from the list.

```csharp
public void ClearBuilds()
```

--------------------------------

### codeOptimization Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildSettings.html

Specifies the code optimization level for web builds. Uses UnityEditor.WebGL.WasmCodeOptimization values as strings.

```csharp
public string codeOptimization
```

--------------------------------

### stripEngineCode Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Determines whether engine code should be stripped from the WebGL build to reduce its size. This is a nullable boolean.

```csharp
public bool? stripEngineCode

```

--------------------------------

### memoryGrowthMode Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Specifies the memory growth mode for WebGL builds. This determines the strategy Unity uses to manage memory allocation.

```csharp
public WebGLMemoryGrowthMode memoryGrowthMode

```

--------------------------------

### MissingSubmoduleErrorHandling Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Defines the error handling behavior for stripped submodules. Options include ignoring, logging, or throwing exceptions.

```csharp
[Tooltip("The error handling behavior when a stripped submodule is used. The usage of a stripped submodule can be ignored, logged to the browser console, or thrown as an exception.")]
public MissingSubmoduleErrorHandlingType MissingSubmoduleErrorHandling
```

--------------------------------

### maximumMemorySize Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Defines the maximum memory size allocated for the WebGL build. This is specified in megabytes.

```csharp
public int maximumMemorySize

```

--------------------------------

### webAssemblyTable Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables the WebAssembly table for WebGL builds. This is related to function table management in WebAssembly.

```csharp
public bool webAssemblyTable

```

--------------------------------

### WebPlayerSettings autoGraphicsAPI Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Indicates whether Auto Graphics API is enabled for WebGL. This corresponds to PlayerSettings.GetUseDefaultGraphicsAPIs.

```csharp
public bool? autoGraphicsAPI

```

--------------------------------

### OriginalSettings Property Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettingsScope.html

Declares the OriginalSettings property of the WebPlayerSettingsScope class. This property provides access to the initial build settings saved when the scope was created.

```csharp
public WebPlayerSettings OriginalSettings { get; }
```

--------------------------------

### RemoveInstrumentationFromBuild

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildProcessor.html

Removes submodule profiling from a web build, restoring backup files and deleting additional template data.

```APIDOC
## RemoveInstrumentationFromBuild

### Description
Remove submodule profiling from build. This restores the backup files of the WebAssembly and JavaScript Framework and deletes any additional TemplateData, for example, icons.

### Method
`public static bool RemoveInstrumentationFromBuild(WebBuildReport build)`

### Parameters
#### Path Parameters
- None

#### Query Parameters
- None

#### Request Body
- None

### Parameters
- **build** (WebBuildReport) - Required - The Web build.

### Returns
- **bool** - Returns 'true' if submodule profiling code was removed, 'false' otherwise.

### Remarks
Remove submodule profiling from build. This restores the backup files of the WebAssembly and JavaScript Framework and deletes any additional TemplateData, for example, icons.
```

--------------------------------

### WebPlayerSettings debugSymbolMode Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Configures the debug symbol mode for WebGL. Refer to PlayerSettings.WebGL.debugSymbolMode.

```csharp
public WebGLDebugSymbolMode debugSymbolMode

```

--------------------------------

### WebBuildReportList BuildsUpdated Event Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

This snippet shows the declaration of the BuildsUpdated event, which is triggered when the web build report list is updated. It uses an Action delegate that accepts a List of WebBuildReport objects.

```csharp
public event Action<List<WebBuildReport>> BuildsUpdated
```

--------------------------------

### WebPlayerSettings decompressionFallback Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables decompression fallback for WebGL. Refer to PlayerSettings.WebGL.decompressionFallback.

```csharp
public bool decompressionFallback

```

--------------------------------

### WebBuildReport Class Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Declares the WebBuildReport class, which is marked as Serializable. This class is part of the Unity.Web.Stripping.Editor namespace.

```csharp
public class WebBuildReport

```

--------------------------------

### RemoveDebugInformation Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

Controls whether to remove debug information after stripping. Debug symbols increase WebAssembly file size and are only needed for identifying functions during stripping.

```csharp
[Tooltip("Remove debug information after stripping. Debug symbols are required to identify functions during stripping, but they increase the size of WebAssembly files. Use on release builds if debug symbols are not required for other use cases.")]
public bool RemoveDebugInformation { get; set; }
```

--------------------------------

### threadsSupport Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables support for threads in WebGL builds. This impacts the ability to use multithreading.

```csharp
public bool threadsSupport

```

--------------------------------

### EmscriptenVersion Field Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReport.html

Stores the Emscripten version used during the creation of this build.

```csharp
public string EmscriptenVersion

```

--------------------------------

### memoryGeometricGrowthCap Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Sets the geometric growth cap for memory allocation in WebGL builds. This controls how memory usage can increase over time.

```csharp
public int memoryGeometricGrowthCap

```

--------------------------------

### wasm2023 Property

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Enables or disables support for the WASM 2023 standard in WebGL builds. This relates to WebAssembly features.

```csharp
public bool wasm2023

```

--------------------------------

### RemoveBuild Method

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Removes a specific web build report from the list.

```csharp
public bool RemoveBuild(WebBuildReport build)
```

--------------------------------

### BuildsUpdated Event

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

An event that is triggered when the web build report list is updated. It provides a list of WebBuildReport objects.

```APIDOC
### Event: BuildsUpdated

An event that is triggered when the web build report list is updated.

#### Declaration
```csharp
public event Action<List<WebBuildReport>> BuildsUpdated
```

#### Event Type
- **Action<List<WebBuildReport>>**: Represents a callback that is invoked when the event is triggered, passing a list of `WebBuildReport` objects.
```

--------------------------------

### WebPlayerSettings dataCaching Field

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebPlayerSettings.html

Determines if data caching is enabled for WebGL. Refer to PlayerSettings.WebGL.dataCaching.

```csharp
public bool dataCaching

```

--------------------------------

### Enum MissingSubmoduleErrorHandlingType

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.MissingSubmoduleErrorHandlingType.html

Controls what happens if a function of a stripped submodule is called.

```APIDOC
## Enum MissingSubmoduleErrorHandlingType

Controls what happens if a function of a stripped submodule is called.

**Namespace**: Unity.Web.Stripping.Editor
**Assembly**: Unity.Web.Stripping.Editor.dll

### Syntax
```csharp
public enum MissingSubmoduleErrorHandlingType
```

### Fields

*   **Ignore**: Do nothing if a function of a stripped submodule is called.
*   **LogError**: Log an error to the browser console if a function of a stripped submodule is called, but try to continue execution.
*   **ThrowException**: Throw an exception if a function of a stripped submodule is called and halt execution.
```

--------------------------------

### RemoveBuild(WebBuildReport)

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.WebBuildReportList.html

Removes a specific web build report from the list based on the provided build object.

```APIDOC
## RemoveBuild(WebBuildReport)

### Description

Removes a build from the build list based on the output path.

### Method

`public bool RemoveBuild(WebBuildReport build)`

### Parameters

#### Path Parameters

- **build** (WebBuildReport) - Required - The web build report to remove from the list.

### Returns

- **bool** - Returns 'true' if the build was removed, 'false' otherwise.
```

--------------------------------

### Enum Definition: MissingSubmoduleErrorHandlingType

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.MissingSubmoduleErrorHandlingType.html

Defines the possible handling types for calls to stripped submodules.

```csharp
public enum MissingSubmoduleErrorHandlingType
{
    Ignore,
    LogError,
    ThrowException
}
```

--------------------------------

### ValuesChanged Event Declaration

Source: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool%401.3/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html

An event that is raised whenever the values within the settings are modified.

```csharp
public event Action ValuesChanged
```