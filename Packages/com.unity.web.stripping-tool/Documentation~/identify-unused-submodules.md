# Identify unused submodules

To identify unused submodules, inspect what features a project uses. Strip the submodules that aren't used in the project.

For all submodules and their purposes, refer to [Submodule reference](submodule-reference.md).


## Platform-specific APIs
|**Submodule**|**Strip if...**|
|:------------|:------------|
|**AndroidJNIModule**|You're making a Web build. AndroidJNIModule's features aren't available on Web.|

## Graphics APIs

Choose the submodules to strip depending on the configured Graphics APIs.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**WebGPU Support**|The experimental WebGPU Support is disabled or not configured in the list of [Graphics APIs.](xref:GraphicsAPIs)|
|**WebGL Support**|Only **WebGPU** is configured in the list of [Graphics APIs](xref:GraphicsAPIs).|

## Rendering

Choose the submodules to strip depending on the rendering features.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**Compute Shader Support**|No [compute shaders](xref:class-ComputeShader) are used in the project.|
|**GPU Instancing**|[GPU Instancing](xref:GPUInstancing) isn't used in the project's materials.|
|**TetGen**|The project doesn't use [light probes](xref:LightProbes) or doesn't need to [recompute the light probes](xref:UnityEngine.LightProbes.Tetrahedralize) because of [additive scene loading](xref:UnityEngine.SceneManagement.LoadSceneMode.Additive).|

## 2D rendering

Choose the submodules to strip depending on what 2D rendering features are used.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**2D Rendering**|The project doesn't use [2D sprite rendering](xref:class-SpriteRenderer). </br> **Note:** some 2D rendering features might be used by UI frameworks.|
|**2D Rendering Sorting**|The project doesn't use [Sprite Sorting](xref:sprites-sort).|
|**2D Rendering Sprite Atlas**|The project doesn't use the [Sprite Atlas](xref:sprite-atlas).|
|**2D Rendering Sprite Tiling**|The project doesn't use [Sprite Tiling](xref:Tilemap).|

## UI frameworks

Choose the submodules to strip depending on the used UI framework.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**Unity UI**|The project doesn't use the [Unity UI framework](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html).|
|**IMGUI**|The project doesn't use the [IMGUI UI framework](xref:GUIScriptingGuide).|
|**UI Toolkit**|The project doesn't use the [UI Toolkit](xref:UIElements).|


## Text rendering

Choose the text rendering submodules depending on the used text rendering method.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**TextCoreFontEngine**|The project doesn't use [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html).|
|**Freetype2**|The project doesn't use the [legacy text rendering](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Text.html).|
|**Freetype2 CFF**|The project uses legacy text rendering, depending on the used font formats.|
|**Freetype2 Postscript**|The project uses legacy text rendering, depending on the used font formats.|
|**Freetype2 SFNT**|The project uses legacy text rendering, depending on the used font formats.|
|**Freetype2 TrueType**|The project uses legacy text rendering, depending on the used font formats.|
|**Freetype2 Raster**|The project uses legacy text rendering, depending on the used [font rendering mode](xref:UnityEditor.FontRenderingMode).|
|**Freetype2 SDF**|The project uses legacy text rendering, depending on the used [font rendering mode](xref:UnityEditor.FontRenderingMode).|
|**Freetype2 Smooth**|The project uses legacy text rendering, depending on the used [font rendering mode](xref:UnityEditor.FontRenderingMode).|

## Texture decompression

Texture decompression is used if textures are compressed in a native compression format that isn't supported by the device the Web build is running on. This submodule can be stripped if:
* No compression is used for textures
* The web build is only expected to run on a device that supports the selected texture compression format (for example: specialized builds for desktop and mobile devices).

Strip the submodules for individual compression formats if the format isn't used in the project. For example, strip the submodule **Texture Decompression BC** if the **BC** format isn't used.

Strip the submodule **Texture Decompression Crunch** if  [crunch compression](xref:texture-compression-formats#crunch-compression) isn't used.

## Mesh features

Choose the mesh submodules depending on the used mesh features.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**Mesh Optimizer**|The project doesn't use the C# Scripting API to [optimize meshes](xref:UnityEngine.Mesh.Optimize) at runtime.|
|**Mesh Combiner**|The project doesn't use [batch rendering](xref:DrawCallBatching) and doesn't [combine meshes](xref:UnityEngine.Mesh.CombineMeshes(UnityEngine.CombineInstance[])) using the C# Scripting API.|
|**Mesh Compression**|The project doesn't use [mesh compression](xref:mesh-compression).|
|**Mesh Blend Shapes**|The project doesn't use [blend shapes](xref:BlendShapes).|
|**Mesh Sprite Rendering**|The project doesn't use [sprite rendering](xref:class-SpriteRenderer).|
|**Mesh Script Bindings**|The project doesn't manipulate meshes using through [UnityEngine.Mesh C# API](xref:UnityEngine.Mesh).|
|**Mesh Async Upload**|The project doesn't enable [uploading mesh data to the GPU asynchronously](xref:LoadingTextureandMeshData).|
|**Mesh Skinning**|The project doesn't use [skinned meshes](xref:class-SkinnedMeshRenderer).|

## Particle system

Choose the particle system submodules depending on the used particle system features.

|**Submodule**|**Strip if...**|
|:------------|:------------|
|**ParticleSystem**|The project doesn't use the [Built-In Particle System](xref:class-ParticleSystem).|
|**ParticleSystem Script Bindings**|The project doesn't interact with the Built-In Particle System through the [C# API](xref:UnityEngine.ParticleSystem).|
|**ParticleSystem Events**|The project doesn't use the Built-In Particle System [collision events](xref:UnityEngine.ParticleCollisionEvent).|
|**ParticleSystem * Module**|Depending on which [particle system modules](xref:ParticleSystemModules) aren't used in the project.|
