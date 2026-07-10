# WebGL Submodule Stripping

## Installed Surface

This project embeds `com.unity.web.stripping-tool` 1.3.0. Its package source and documentation are under `Packages/com.unity.web.stripping-tool/`; use that installed copy as the version-matched API authority.

The project settings asset is `Assets/DefaultSubmoduleStrippingSettings.asset`. `Assets/Editor/WebGLStripPostBuild.cs` activates it before WebGL builds and enables the package's automatic post-build pass. The package then owns:

- enabling embedded debug information temporarily when required;
- enabling submodule-stripping compatibility;
- recording and backing up the Web build;
- restoring an earlier instrumented or stripped build;
- stripping configured submodules;
- restoring Player Settings; and
- throwing `BuildFailedException` when stripping fails.

Do not add a second `WebBuildProcessor.StripBuild` postprocessor. Callback ordering can make it run before the package records the Web build, and duplicate passes obscure failures.

## Efficient Workflow

1. Run `compile` and `test` before a WebGL build. IL2CPP and optimized stripping are expensive feedback loops.
2. Run `strip-status` to validate every configured submodule against the installed definitions.
3. For a new stripping selection, build with debug information and submodule-stripping compatibility, then use **Window > Web Optimization > Submodule Stripping > Add Profiling**.
4. Run the instrumented build in a browser and exercise every representative path: authentication, network connection, TextMeshPro UI, dialogue, NPC switching, inventory, physics, and unique visual effects.
5. Download the profiling report and import it into the stripping settings. Combine reports from distinct desktop/mobile scenarios instead of overwriting coverage.
6. During validation, set missing-submodule handling to log or throw and keep `Optimize Code After Stripping` disabled for faster iteration.
7. Test the stripped browser build and inspect the browser console for stripped-function calls, missing text/meshes/textures, and unusual startup failures.
8. For the final release, enable `Remove Debug Information`, set missing-submodule handling to `Ignore`, enable `Optimize Code After Stripping`, then run `build-webgl` once.

Optimization after stripping drastically increases processing time. Use it for release artifacts, not every diagnosis build.

## Project-Specific Risk Checks

- Do not strip `Unity UI` or `TextCoreFontEngine`; the project uses UGUI and TextMeshPro.
- Treat networking, JSON, XML, compression, and serialization submodules as evidence-driven decisions. Supabase, Newtonsoft.Json, UnityWebRequest, and runtime asset handling can use code paths not obvious from scene inspection.
- Profile each supported Web target separately when desktop and mobile texture formats or graphics APIs differ.
- A successful startup alone is insufficient. Missing submodule errors can appear only after a feature is exercised.

## Version-Matched Documentation

- Manual: `Packages/com.unity.web.stripping-tool/Documentation~/index.md`
- Workflow: `Packages/com.unity.web.stripping-tool/Documentation~/submodule-stripping-workflow.md`
- Profiling: `Packages/com.unity.web.stripping-tool/Documentation~/submodule-profiling.md`
- Testing: `Packages/com.unity.web.stripping-tool/Documentation~/test-stripped-build.md`
- Scripting: `Packages/com.unity.web.stripping-tool/Documentation~/scripting.md`
- Official package manual: `https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.3/manual/index.html`
