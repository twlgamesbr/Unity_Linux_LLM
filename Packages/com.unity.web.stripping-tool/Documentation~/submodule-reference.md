# Submodule reference

You can strip the following submodules from your project using the Web Stripping Tool package.

To learn what submodules you might want to strip from your project, refer to [Identify unused submodules](identify-unused-submodules.md).

To learn the difference between modules and submodules, refer to [Understand submodules](understand-submodules.md).

> [!NOTE]
> The availability of some submodules depends on your installed Unity version. If a submodule has a **Unity version** listed, it's only applicable if you use that version of Unity. Items marked with a dash (*-*) are available in all supported versions.

<!-- NOTE: A single word in the submodule's name in the first column should be less than 21 characters.
    For example, System.ComponentModel (21 characters) is too long, so we use zero-width space (&#8203;) to break it into two words.
    Also the name should be kept relatively short in the second column too, altought it can be a couple of characters longer.
    If we don't do that, the right border of the table is cut out and there will be a horizontal scroll bar.
    -->
|Submodule name||Purpose|Unity version|
|:--------------|:-------|:---|:---|
|**2D Rendering**||Provides support for [2D sprite rendering](xref:class-SpriteRenderer). Automatically includes all 2D rendering submodules.|-|
||**2D Rendering Common**|Common components for 2D rendering.|-|
||**2D Rendering Renderer**|[Sprite Renderer](xref:class-SpriteRenderer) Implementation|-|
||**2D Rendering Sorting**|[Sprite Sorting](xref:sprites-sort)|-|
||**2D Rendering Sprite Atlas**|[Sprite Atlas](xref:sprite-atlas)|-|
||**2D Rendering Sprite Tiling**|[Sprite Tiling](xref:Tilemap)|-|
|**Advanced Text Generator Support**||[Advanced Text Generator](xref:uie-advanced-text-generator) is a text rendering module that employs HarfBuzz, ICU, and FreeType to deliver comprehensive Unicode support and text shaping capabilities. With Advanced Text Generator, you can use a wide array of languages and scripts, such as right-to-left (RTL) languages like Arabic and Hebrew.|-|
||**HarfBuzz**|HarfBuzz text shaping engine.|-|
||**ICU**|International Components for Unicode.|-|
|**AndroidJNIModule**||[`AndroidJNIModule`](xref:UnityEngine.AndroidJNIModule) allows you to call Java code from C# scripts. `AndroidJNIModule`'s features aren't applicable in Unity Web builds.|-|
|**Animation**||The [Unity Animation module](https://docs.unity3d.com/Manual/AnimationSection.html). Automatically includes all Animation submodules.|≥6000.5.0a7|
||**Animation Allocators**|Internal memory allocators used by the Animation module.|≥6000.5.0a7|
||**Animation Animator**|[Animator class](https://docs.unity3d.com/Manual/class-Animator.html) of Unity Animation module.|≥6000.5.0a7|
||**Animation Constraints**|[Constraints](https://docs.unity3d.com/Manual/Constraints.html) in the Unity Animation module.|≥6000.5.0a7|
||**Animation Core**|Core implementation of Unity Animation module.|≥6000.5.0a7|
||**Animation Director**|[Playable Director](https://docs.unity3d.com/Manual/class-PlayableDirector.html) in the Unity Animation module.|≥6000.5.0a7|
||**Animation Human**|[Components for human avatar](https://docs.unity3d.com/Manual/AvatarCreationandSetup.html) in the Unity Animation module.|≥6000.5.0a7|
||**Animation Script Bindings**|C# Script Bindings of the Unity Animation module.|≥6000.5.0a7|
||**Animation State Machine**|[Animation State Machine](https://docs.unity3d.com/Manual/AnimationStateMachines.html) in the Unity Animation module.|≥6000.5.0a7|
|**ArticulationBody**||The [ArticulationBody component](xref:class-ArticulationBody) used in Unity's 3D physics system.|-|
|**Clipper**||A [polygon clipping library](https://sourceforge.net/projects/polyclipping/). Dependency of, for example, URP 2D shadows.|≥6000.5.0a7|
|**double-conversion**||A [library for operations on IEEE doubles](https://github.com/google/double-conversion).|≥6000.5.0a7|
|**Expression Evaluator**||Provides support for [evaluating simple math expressions](https://docs.unity3d.com/ScriptReference/ExpressionEvaluator.html).|-|
|**FreeType2**||[FreeType2 font library](https://freetype.org/) that is used by the legacy text renderer. Automatically includes all FreeType2 submodules.|-|
||**FreeType2 Base**|This submodule contains features of the FreeType2 font library.|-|
||**FreeType2 CFF**|Provides support for loading CFF (Compact Font Format) fonts.|-|
||**FreeType2 OpenType Validation**|Provides support for OpenType validation in FreeType2.|-|
||**FreeType2 Postscript**|Provides support for FreeType2's PostScript driver.|-|
||**FreeType2 Raster**|Provides support for FreeType2's monochrome rasterizer.|-|
||**FreeType2 SDF**|Provides support for signed distance field rasterizer in FreeType2.|-|
||**FreeType2 SFNT**|Provides support for SFNT (Scalable Font) font files in FreeType2.|-|
||**FreeType2 Smooth**|Provides support for anti-aliasing rasterizer in FreeType2.|-|
||**FreeType2 TrueType**|Provides support for TrueType font files in FreeType2.|-|
|**GPU Instancing**||Support for [GPU Instancing](xref:GPUInstancing): a draw call optimization for objects that share the same mesh and material.|-|
|**GPU Resident Drawer**||Enables using the [GPU Resident Drawer](xref:urp-gpu-resident-drawer) feature.|<6000.6|
||**GPU Resident Drawer Core**|Core implementation of the [GPU Resident Drawer](xref:urp-gpu-resident-drawer) feature.|<6000.6|
||**GPU Resident Drawer Rendering Debugger**|[Rendering debugger](xref:urp-rendering-debugger-use) for the GPU Resident Drawer.|<6000.6|
|**HierarchyCore**||The [Unity Hierarchy Core module](https://docs.unity3d.com/ScriptReference/UnityEngine.HierarchyCoreModule.html). Automatically includes all HierarchyCore submodules.|≥6000.5.0a7|
||**HierarchyCore Core**|Implementation of Hierarchy Core module.|≥6000.5.0a7|
||**HierarchyCore Script Bindings**|[C# Scripting API for Hierarchy Core module](https://docs.unity3d.com/ScriptReference/UnityEngine.HierarchyCoreModule.html).|≥6000.5.0a7|
|**IMGUI**||Provides support for the [IMGUI UI framework](xref:GUIScriptingGuide).|-|
|**IMGUI Script Bindings**||Provides support for using IMGUI in your game via scripting.|-|
|**libjpeg**||A [library for reading JPEG files](https://libjpeg-turbo.org).|≥6000.5.0a6|
|**libpng**||A [library for reading PNG files](https://www.libpng.org/pub/png/libpng.html).|≥6000.5.0a6|
|**libtess2**||A [library to tessellate and triangulate polygons](https://github.com/memononen/libtess2).|≥6000.5.0a6|
|**Mesh Features**||Various features related to [`UnityEngine.Mesh`](xref:UnityEngine.Mesh). Automatically includes all Mesh-related submodules.|-|
||**Mesh Advanced Editing API**|[Advanced C# API](xref:UnityEngine.Mesh.AllocateWritableMeshData(System.Int32)) for manipulating meshes.|-|
||**Mesh Optimizer**|Provides support for [optimizing meshes](xref:UnityEngine.Mesh.Optimize).|-|
||**Mesh Combiner**|Provides support for [batch rendering](xref:DrawCallBatching) of meshes and [combining meshes](xref:UnityEngine.Mesh.CombineMeshes(UnityEngine.CombineInstance[])).|-|
||**Mesh Compression**|Provides support for [compressing and decompressing mesh data](xref:mesh-compression).|-|
||**Mesh Blend Shapes**|Provides support for using [blend shapes in skinned meshes](xref:BlendShapes).|-|
||**Mesh Recalculate Bounds API**|C# API for [updating bounds of a mesh](xref:UnityEngine.Mesh.RecalculateBounds()).|-|
||**Mesh Recalculate Normals API**|C# API for [recalculating normals of a mesh](xref:UnityEngine.Mesh.RecalculateNormals()).|-|
||**Mesh Sprite Rendering**|Provides support for [rendering sprite meshes](xref:class-SpriteRenderer).|-|
||**Mesh Script Bindings**|Provides support for accessing and manipulating meshes through [`UnityEngine.Mesh` C# API](xref:UnityEngine.Mesh).|-|
||**Mesh Async Upload**|Provides support for [uploading mesh data to the GPU asynchronously](xref:LoadingTextureandMeshData).|-|
||**Mesh Skinning**|Provides [rendering support for skinned meshes](xref:class-SkinnedMeshRenderer).|-|
|**Mono**||Provides access to [Mono](https://www.mono-project.com/docs/) runtime internals and platform-specific implementations.|-|
||**Mono Globalization**|Provides globalization and locale-specific collation support for the Mono runtime.|-|
||**Mono Net**|Provides Mono-specific networking utilities and HTTP helpers.|-|
||**Mono Runtime**|Provides Mono runtime internal types including runtime handles, interop marshalling helpers, and dependency injection.|-|
||**Mono Security**|Provides [security-related classes and cryptographic implementations](https://www.mono-project.com/archived/cryptography/#assembly-monosecurity) from the Mono runtime, including X.509 certificate handling and system security providers.|-|
||**Mono Unity**|Provides Unity-specific extensions and integrations for the Mono runtime.|-|
|**Newtonsoft Json.NET**||Newtonsoft's [`Json.NET`](https://www.newtonsoft.com/json) framework, version 13.0.2.|-|
||**Newtonsoft.Json**|The [`Newtonsoft.Json`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json.htm) namespace provides classes that are used to implement the core services of the framework.|-|
||**Newtonsoft.Json.Bson**|The [`Newtonsoft.Json.Bson`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Bson.htm) namespace provides classes that are used to implement BSON.|-|
||**Newtonsoft.Json.Converters**|The [`Newtonsoft.Json.Converters`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Converters.htm) namespace provides classes that inherit from `JsonConverter`.|-|
||**Newtonsoft.Json.Linq**|The [`Newtonsoft.Json.Linq`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Linq.htm) namespace provides classes that are used to implement LINQ to JSON.|-|
||**Newtonsoft.Json.Schema**|The [`Newtonsoft.Json.Schema`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Schema.htm) namespace provides classes that are used to implement JSON schema.|-|
||**Newtonsoft.Json.&#8203;Serialization**|The [`Newtonsoft.Json.Serialization`](https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Serialization.htm) namespace provides classes that are used when serializing and deserializing JSON.|-|
|**ParticleSystem**||[Built-in particle system](xref:class-ParticleSystem). Automatically includes all ParticleSystem submodules.|-|
||**ParticleSystem Collision Module**|Controls the collision of particles with GameObjects.|-|
||**ParticleSystem Color by Speed Module**|Sets particle color depending on speed.|-|
||**ParticleSystem Color Module**|Sets particle color.|-|
||**ParticleSystem Core**|Built-in particle system core.|-|
||**ParticleSystem Curves**|Built-in particle system curves|-|
||**ParticleSystem Custom Data Module**|Sets custom particle data.|-|
||**ParticleSystem Emission Module**|Sets the rate and timing of particle system emissions.|-|
||**ParticleSystem Events**|Built-in particle system [collision events](xref:UnityEngine.ParticleCollisionEvent)|-|
||**ParticleSystem External Forces Module**|Controls wind zones and particle system force fields.|-|
||**ParticleSystem Force over Lifetime Module**|Controls forces acting on the particle.|-|
||**ParticleSystem Geometry Jobs**|Jobs for built-in particle system geometry generation.|-|
||**ParticleSystem Gradients**|Gradient math library for the built-in particle system|-|
||**ParticleSystem Inherit Velocity Module**|Controls the velocity of particles emitted by sub-emitter particles.|-|
||**ParticleSystem Initial Module**|Controls global properties that affect the whole system.|-|
||**ParticleSystem Lifetime by Emitter Speed Module**|Controls particle lifetime depending on the emitter speed.|-|
||**ParticleSystem Lights Module**|Adds real-time lights to particles.|-|
||**ParticleSystem Limit Velocity Module**|Limits velocity over time.|-|
||**ParticleSystem Modules**|[Built-in particle system modules](xref:ParticleSystemModules). Automatically includes all ParticleSystem modules.|-|
||**ParticleSystem Noise Module**|Adds turbulence to particle movement.|-|
||**ParticleSystem Base Module**|The base class for Particle System modules.|-|
||**ParticleSystem Renderer**|Built-in particle system renderer|-|
||**ParticleSystem Rotation By Speed Module**|Controls particle rotation depending on speed.|-|
||**ParticleSystem Rotation Module**|Controls particle rotation.|-|
||**ParticleSystem Script Bindings**|Support for interacting with built-in particle system using [C# API](xref:UnityEngine.ParticleSystem).|-|
||**ParticleSystem Shape Module**|Controls the shape of the particle system emitter.|-|
||**ParticleSystem Size By Speed Module**|Controls particle size by speed.|-|
||**ParticleSystem Size over Lifetime Module**|Controls particle size over its lifetime.|-|
||**ParticleSystem Sub Emitters Module**|Allows particles to also emit new particles.|-|
||**ParticleSystem Texture Sheet Animation Module**|Adds texture animation to a particle.|-|
||**ParticleSystem Trails Module**|Adds particle trails.|-|
||**ParticleSystem Triggers Module**|Modifies particles based on interaction with colliders.|-|
||**ParticleSystem Utils**|Built-in particle system utility classes|-|
|**Physics2D**||[Physics2D module](https://docs.unity3d.com/ScriptReference/UnityEngine.Physics2DModule.html) that implements 2D physics in Unity. Automatically includes all Physics2D submodules.|≥6000.5.0a8|
||**Box2D v2**|[Box 2D physics engine version 2.4](https://box2d.org/), which is used by the Physics2D module.|≥6000.5.0a8|
||**Box2D v3**|[Box 2D physics engine version 3.1.1](https://box2d.org/), which is used by the low-level API of the Physics2D module.|≥6000.5.0a8|
||**Physics2D Core**|Core implementation of the [Physics2D engine module](https://docs.unity3d.com/ScriptReference/UnityEngine.Physics2DModule.html).|≥6000.5.0a8|
||**Physics2D Script Bindings**|[C# Scripting API for Physics2D module](https://docs.unity3d.com/ScriptReference/UnityEngine.Physics2DModule.html).|≥6000.5.0a8|
||**PhysicsCore2D Core**|Low-level [2D physics engine module](https://docs.unity3d.com/ScriptReference/UnityEngine.PhysicsCore2DModule.html).|≥6000.5.0a8|
||**PhysicsCore2D Script Bindings**|[C# Scripting API for PhysicsCore2D module](https://docs.unity3d.com/ScriptReference/LowLevelPhysics2D.PhysicsWorld.html).|≥6000.5.0a8|
|**PhysX**||[NVIDIA PhysX physics engine](https://github.com/NVIDIA-Omniverse/PhysX). Automatically includes all PhysX submodules.|≥6000.5.0a7|
||**PhysX Character Kinematic**|Character Kinematic module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX Cloth**|Cloth module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX Common**|Provides common data structures, utilities, and foundational classes used throughout the PhysX SDK.|≥6000.5.0a7|
||**PhysX Cooking**|Cooking module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX Core**|Central library responsible for running physics calculations, including collision detection, rigid body dynamics, and constraints of PhysX SDK.|≥6000.5.0a7|
||**PhysX Extensions**|Extensions module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX Foundation**|Foundation module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX PvdSDK**|PhysX Visual Debugger module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
||**PhysX Vehicle**|Vehicle module of NVIDIA PhysX physics engine.|≥6000.5.0a7|
|**System.&#8203;ComponentModel**||Provides functionality for the [`.NET` Component Model](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel).|-|
||**System.ComponentModel Core**|Provides the core functionality for the [`.NET` Component Model](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel). This submodule contains the specific classes required by the `Newtonsoft.Json` library.|-|
||**System.ComponentModel Extra**|Provides all remaining functionality from the [`.NET` Component Model](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel) not included in `System.ComponentModel Core`. This submodule can be safely stripped if your project's dependencies only require the Core components.|-|
|**System.Data**||Provides access to classes that represent the [`ADO.NET` API](https://learn.microsoft.com/en-us/dotnet/api/system.data).|-|
|**System.DateTimeOffset**||Provides access to [`DateTimeOffset`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset), the struct that represents a point in time relative to Coordinate Universal Time (UTC).|-|
|**System.DateTimeParse**||Provides access to classes that convert strings to [`DateTime` objects](https://learn.microsoft.com/en-us/dotnet/api/system.datetime).|-|
|**System.Dynamic**||Provides access to classes and interfaces of the [Dynamic Language Runtime](https://learn.microsoft.com/en-us/dotnet/api/system.dynamic).|-|
|**System.Linq**||Provides support for [Language-Integrated Query (LINQ)](https://learn.microsoft.com/en-us/dotnet/api/system.linq).|-|
||**System.Linq Core**|Provides core functionality for [Language-Integrated Query (LINQ)](https://learn.microsoft.com/en-us/dotnet/api/system.linq).|-|
||**System.Linq Expressions**|Provides support for representing language-level code expressions as [expression trees](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions).|-|
|**System.Net**||Provides access to classes for [network protocols](https://learn.microsoft.com/en-us/dotnet/api/system.net).|-|
|**System.Numerics**||Provides types in [`System.Numerics`](https://learn.microsoft.com/en-us/dotnet/api/system.numerics) namespace, such as `BigInteger` and `Vector<T>`, that complement the numeric primitives defined by `.NET`.|-|
||**System.Numerics Core**|Provides types such as `BigInteger` and `Complex`.|-|
||**System.Numerics.Vector&lt;T&gt;**|Provides `Vector<T>` that represents a single vector of a specified numeric type that is suitable for low-level optimization of parallel algorithms.|-|
|**System.Security**||Provides access to the common language [security system](https://learn.microsoft.com/en-us/dotnet/api/system.security).|-|
|**System.TimeZoneInfo**||Provides access to [time zone information and conversions between time zones](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo).|-|
|**System.Xml**||Provides standards-based support for processing XML using the [`.NET` APIs](https://learn.microsoft.com/en-us/dotnet/api/system.xml).|-|
|**TextCore**||Unity's default text rendering module. Automatically includes all TextCore submodules.|≥6000.5.0a3|
||**TextCoreFontEngine**|Used to access data from TrueType (.ttf, .ttc) and OpenType (.otf) font files. Also used to raster the visual representation of characters known as glyphs in a given font atlas texture.|-|
||**TextCoreTextEngine**|Text engine that provides comprehensive Unicode support and text shaping capabilities.|≥6000.5.0a3|
|**Text Editing Utilities**||Provides support for editing text in UI Toolkit and IMGUI text editors.|-|
|**Text Rendering**||Provides support for [rendering high-quality text](xref:UIE-get-started-with-text) in a Unity project.|-|
|**Text Selecting Utilities**||Provides support for selecting text in UI Toolkit and IMGUI.|-|
|**TetGen**||[Tetrahedral Mesh Generator and 3D Delaunay Triangulator](https://wias-berlin.de/software/tetgen/) library. This submodule is a dependency of [light probes](xref:LightProbes).|-|
|**Texture Decompression**||Provides support for decompressing textures on the fly. You do not need this submodule if textures are already compressed in the correct format. Automatically includes texture decompression for DXT, BC, ETC and ASTC format.|-|
||**Texture Decompression ASTC**|Provides support for [ASTC](xref:class-TextureImporterOverride) decompression. You do not need this submodule if textures are already compressed in the correct format.|-|
||**Texture Decompression BC**|Provides support for [BC](xref:class-TextureImporterOverride) decompression. You do not need this submodule if textures are already compressed in the correct format.|-|
||**Texture Decompression Crunch**|Provides support for [crunch compression](xref:texture-compression-formats#crunch-compression). You do not need this submodule if crunch texture compression is not used.|-|
||**Texture Decompression DXT**|Provides support for [DXT](xref:class-TextureImporterOverride) decompression. You do not need this submodule if textures are already compressed in the correct format.|-|
||**Texture Decompression ETC**|Provides support for [ETC/ETC2](xref:class-TextureImporterOverride) decompression. You do not need this submodule if textures are already compressed in the correct format.|-|
|**Tilemap**||The [Unity Tilemap module](https://docs.unity3d.com/Manual/tilemaps/tilemaps-landing.html). Automatically includes all Tilemap submodules.|≥6000.5.0a7|
||**Tilemap Core**|Implementation of Tilemap module.|≥6000.5.0a7|
||**Tilemap Script Bindings**|[C# Scripting API for Tilemap module](https://docs.unity3d.com/ScriptReference/UnityEngine.TilemapModule.html).|≥6000.5.0a7|
|**UI Toolkit Framework**||[UI toolkit](xref:UIElements) is a collection of features, resources, and tools for developing user interface (UI).|-|
||**UI Toolkit**|Provides support for the [UI toolkit](xref:UIElements).|-|
||**UI Toolkit TextEditingManipulator**|Provides support for editing text in UI Toolkit.|-|
||**UI Toolkit TextSelectingManipulator**|Provides support for selecting text in UI Toolkit.|-|
||**UI Toolkit Visual Elements**|Various UI Elements of [UI toolkit](xref:UIElements).|-|
|**Unity 2D Animation**||The [2D Animation package](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/index.html) includes features and tools that allow you to quickly rig and animate 2D characters in Unity in a variety of ways.|-|
|**Unity 2D Feature Set**||Provides support for Unity's 2D graphics features including sprite atlases, sprite shapes, and tilemaps. For more information, refer to [2D feature set](xref:um-2d-feature).|-|
||**Unity 2D Core**|2D graphics features that are part of the core engine module.|-|
||**Unity 2D Packages**|Additional 2D features installed via packages referenced by the [2D feature set](xref:um-2d-feature).|-|
|**Unity Collections**||Provides support for Unity's native collection types from the [Unity Collections package](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/manual/index.html).|-|
||**Unity Collections - FixedList32Bytes**|Provides support for [`FixedList32Bytes<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList32Bytes-1.html), a fixed-size list with 32 bytes of embedded storage.|-|
||**Unity Collections - FixedList64Bytes**|Provides support for [`FixedList64Bytes<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList64Bytes-1.html), a fixed-size list with 64 bytes of embedded storage.|-|
||**Unity Collections - FixedList128Bytes**|Provides support for [`FixedList128Bytes<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList128Bytes-1.html), a fixed-size list with 128 bytes of embedded storage.|-|
||**Unity Collections - FixedList512Bytes**|Provides support for [`FixedList512Bytes<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList512Bytes-1.html), a fixed-size list with 512 bytes of embedded storage.|-|
||**Unity Collections - FixedList4096Bytes**|Provides support for [`FixedList4096Bytes<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList4096Bytes-1.html), a fixed-size list with 4096 bytes of embedded storage.|-|
||**Unity Collections - FixedString32Bytes**|Provides support for [`FixedString32Bytes`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString32Bytes.html), a fixed-size UTF-8 string with 32 bytes of storage.|-|
||**Unity Collections - FixedString64Bytes**|Provides support for [`FixedString64Bytes`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString64Bytes.html), a fixed-size UTF-8 string with 64 bytes of storage.|-|
||**Unity Collections - FixedString128Bytes**|Provides support for [`FixedString128Bytes`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString128Bytes.html), a fixed-size UTF-8 string with 128 bytes of storage.|-|
||**Unity Collections - FixedString512Bytes**|Provides support for [`FixedString512Bytes`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString512Bytes.html), a fixed-size UTF-8 string with 512 bytes of storage.|-|
||**Unity Collections - FixedString4096Bytes**|Provides support for [`FixedString4096Bytes`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString4096Bytes.html), a fixed-size UTF-8 string with 4096 bytes of storage.|-|
||**Unity Collections - NativeArray**|Provides support for [`NativeArray<T>`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html), a fixed-size native memory array.|-|
||**Unity Collections - NativeBitArray**|Provides support for [`NativeBitArray`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeBitArray.html), an arbitrarily-sized array of bits stored in native memory.|-|
||**Unity Collections - NativeHashMap**|Provides support for [`NativeHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeHashMap-2.html), a native key-value dictionary.|-|
||**Unity Collections - NativeHashSet**|Provides support for [`NativeHashSet<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeHashSet-1.html), a native set of unique values.|-|
||**Unity Collections - NativeKeyValueArrays**|Provides support for [`NativeKeyValueArrays<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeKeyValueArrays-2.html), parallel arrays for keys and values.|-|
||**Unity Collections - NativeList**|Provides support for [`NativeList<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeList-1.html), a resizable native memory list.|-|
||**Unity Collections - NativeParallelHashMap**|Provides support for [`NativeParallelHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeParallelHashMap-2.html), a parallel-safe native key-value dictionary.|-|
||**Unity Collections - NativeParallelHashSet**|Provides support for [`NativeParallelHashSet<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeParallelHashSet-1.html), a parallel-safe native set of unique values.|-|
||**Unity Collections - NativeParallelMultiHashMap**|Provides support for [`NativeParallelMultiHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeParallelMultiHashMap-2.html), a parallel-safe native multi-hash map allowing multiple values per key.|-|
||**Unity Collections - NativeQueue**|Provides support for [`NativeQueue<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeQueue-1.html), a native FIFO queue.|-|
||**Unity Collections - NativeReference**|Provides support for [`NativeReference<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeReference-1.html), a native reference to a single value.|-|
||**Unity Collections - NativeRingQueue**|Provides support for [`NativeRingQueue<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeRingQueue-1.html), a fixed-size circular buffer for single-threaded use.|-|
||**Unity Collections - NativeStream**|Provides support for [`NativeStream`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeStream.html), a native parallel data stream for writing and reading data in parallel jobs.|-|
||**Unity Collections - NativeText**|Provides support for [`NativeText`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeText.html), a resizable UTF-8 string stored in native memory.|-|
||**Unity Collections - UnsafeAppendBuffer**|Provides support for [`UnsafeAppendBuffer`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAppendBuffer.html), an unsafe unmanaged, untyped, heterogeneous buffer.|-|
||**Unity Collections - UnsafeAtomicCounter32**|Provides support for [`UnsafeAtomicCounter32`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter32.html), a 32-bit atomic counter for thread-safe incrementing.|-|
||**Unity Collections - UnsafeAtomicCounter64**|Provides support for [`UnsafeAtomicCounter64`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter64.html), a 64-bit atomic counter for thread-safe incrementing.|-|
||**Unity Collections - UnsafeBitArray**|Provides support for [`UnsafeBitArray`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeBitArray.html), an unsafe arbitrarily-sized array of bits.|-|
||**Unity Collections - UnsafeHashMap**|Provides support for [`UnsafeHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeHashMap-2.html), an unsafe key-value dictionary with no safety checks for maximum performance.|-|
||**Unity Collections - UnsafeHashSet**|Provides support for [`UnsafeHashSet<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeHashSet-1.html), an unsafe set of unique values with no safety checks for maximum performance.|-|
||**Unity Collections - UnsafeList**|Provides support for [`UnsafeList<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeList-1.html), an unsafe resizable list with no safety checks for maximum performance.|-|
||**Unity Collections - UnsafeParallelHashMap**|Provides support for [`UnsafeParallelHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMap-2.html), an unsafe parallel-safe key-value dictionary.|-|
||**Unity Collections - UnsafeParallelHashSet**|Provides support for [`UnsafeParallelHashSet<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashSet-1.html), an unsafe parallel-safe set of unique values.|-|
||**Unity Collections - UnsafeParallelMultiHashMap**|Provides support for [`UnsafeParallelMultiHashMap<TKey, TValue>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeParallelMultiHashMap-2.html), an unsafe parallel-safe multi-hash map allowing multiple values per key.|-|
||**Unity Collections - UnsafePtrList**|Provides support for [`UnsafePtrList<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafePtrList-1.html), an unsafe pointer list with no safety checks for maximum performance.|-|
||**Unity Collections - UnsafeRingQueue**|Provides support for [`UnsafeRingQueue<T>`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeRingQueue-1.html), an unsafe fixed-size circular buffer.|-|
||**Unity Collections - UnsafeScratchAllocator**|Provides support for [`UnsafeScratchAllocator`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeScratchAllocator.html), a fixed-size buffer from which temporary allocations can be made.|-|
||**Unity Collections - UnsafeStream**|Provides support for [`UnsafeStream`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeStream.html), an unsafe set of untyped append-only buffers.|-|
||**Unity Collections - UnsafeText**|Provides support for [`UnsafeText`](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeText.html), an unsafe unmanaged, mutable, resizable UTF-8 string.|-|
|**Unity In-App Purchasing**||The [Unity In-App Purchasing package](https://docs.unity.com/en-us/iap) lets you set up in-app purchases for your game across multiple app stores. The package isn't applicable for the Web platform.|-|
|**Unity UI**||Provides support for the [Unity UI framework](https://docs.unity3d.com/Packages/com.unity.ugui@3.0/manual/index.html).|-|
|**WebGL Support**||Provides support for using the [WebGL graphics API](https://www.khronos.org/webgl/).|-|
|**WebGPU Support**||Provides support for using the [WebGPU graphics API](https://www.w3.org/TR/webgpu/). Automatically includes: Compute Shader Support|-|
||**Compute Shader Support**|Provides support for [compute shaders](xref:class-ComputeShader).|-|
|**XR**||Provides support for [XR functionality](xref:XR).|-|
|**zlib**||A [general purpose compression library](https://www.zlib.net/).|≥6000.5.0a6|
