# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-04-16

### Added

- Added support for Emscripten 4.
- Added support for running optimization passes (**Optimize Code After Stripping** and **Remove Debug Information**) without stripping submodules.
- Added stripping support for AndroidJNIModule.
- Added stripping support for Unity In-App Purchasing package.
- Added new submodule for System.Dynamic.
- Added new submodule for System.Numerics.
- Added new submodules for System.DateTimeParse and System.DateTimeOffset.
- Added new submodule for Text Selecting Utilities.
- Added new submodule for Text Editing Utilities.
- Added new submodule for Expression Evaluator.
- Added new submodule for IMGUI Script Bindings.
- Added new submodules for Unity 2D Feature Set.
- Added new submodules for Unity.Collections.
- Added new submodules for Mono.
- Added new submodule for System.TimeZoneInfo.
- Added new submodules for GPU Resident Drawer.

### Changed
- Added more information about a Web Build to the Submodule Stripping Window (player settings, stripping settings).

### Fixed

- Fixed compilation error with Unity 6.5.
- Fixed a bug where changes to Submodule Stripping Settings were not being saved.

## [1.2.1] - 2026-02-20

### Fixed
- Fixed compilation error with Unity 6.4.

## [1.2.0] - 2025-09-05

### Added

- Added new submodule for System.Data.
- Added new submodule for System.Net.
- Added new submodules for UI Toolkit Visual Elements.
- Added new submodule for System.Security.
- Added new submodules for System.Linq.
- Added new submodule for System.ComponentModel.

### Changed

- Disabled submodule profiling and stripping when incompatible Emscripten arguments `--profiling` and `--profiling-funcs` are used with external debug symbols in a build and added instructions for the user about how to make the build compatible.
- Renamed **Remove Embedded Debug Symbols** to **Remove Debug Information** in Submodule Stripping Settings UI to clarify what the function does.

### Fixed

- Fixed bug that caused a null reference exception when Submodule Stripping window was docked and hidden.
- Fixed bug that caused the 7-Zip path to be wrong for Unity 6.3 and newer.
- Fixed bug that caused stripping to fail when stripping C# submodules that don't match any code inside a build.

## [1.1.0] - 2025-05-13

### Added

- Added new submodules for Advanced Text Generator.
- Added new submodules for Newtonsoft's Json.NET framework.
- Added **Decompression Fallback** to the list of build details shown in the **Submodule Stripping** window.

### Fixed

- Fixed bug that prevented adding and using submodule profiling in all possible Web templates.
- Fixed bug that prevented using profiling and missing submodule error handling on builds with external debug symbols.
- Fixed bug that prevented builds with Decompression Fallback and Gzip/Brotli compression enabled to be stripped or instrumented for submodule profiling.
- Fixed bug that showed **Show Backup Folder in Explorer** in context menu when no backup folder exists.

## [1.0.0] - 2025-02-14

### Added

- Added new Player setting fields, `managedStrippingLevel`, `il2CppCodeGeneration`, and `il2CppCompilerConfiguration`, to `WebPlayerSettings`.
- Added new class `WebBuildSettings` and made `WebPlayerSettings` to store its information.
- Added `WebPlayerSettingsScope` for easily saving and restoring Player and build settings.
- Added the new Player and build settings to the **Submodule Stripping** window.

### Changed

- Package settings that deal with local file paths are now stored in `UserSettings` folder instead of `ProjectSettings` folder.
- Updated instructions in **Submodule Stripping** window and in package manual for dealing with builds that have prerequisite files for submodule stripping missing.

### Fixed

- Fixed bug that made it possible to load any JSON file as a profiling report which resulted in all selected submodules being deselected.
- Fixed bug in missing submodule error handling with multithreaded builds: errors were not logged/thrown in worker threads.
- Fixed bug that prevented **Add Profiling** and **Strip** to work on multithreaded builds if **Target WebAssembly 2023** was not explicitly enabled.
- Fixed bug that prevent submodule profiling to work with multithreaded builds.

## [1.0.0-pre.2] - 2025-01-22

### Fixed

- Fixed a bug that made it possible to add submodule profiling to a stripped build. Submodule profiling cannot be added to a build that's already stripped or instrumented for submodule profiling.
- Fixed an issue where Missing Submodule Error Handling was not working for submodules that contain nested submodules.

## [1.0.0-pre.1] - 2025-01-06

### Added

- Added `WebBuildProcessor` class for submodule profiling and stripping.
- Added samples for submodule profiling and stripping.

### Fixed

- Fixed an issue where files created by **Missing Submodule Error Handling** were removed when **Optimize Code After Stripping** was also enabled.

## [0.1.0-exp.1] - 2024-11-28

The first version of the package is a work in progress.
