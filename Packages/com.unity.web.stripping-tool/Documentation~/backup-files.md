# Backup and additional files

This package creates additional files for the builds, for example, a submodule stripping report. It also creates backup files for all files it modifies. Some of these files are required for the submodule stripping process.

>[!NOTE]
>Backup files can take a considerable amount of disk space over time, so it's recommended to clean up the backups for the builds you don't work on any longer.

## View backup files

To view the backup files of a build:

1. In the **Submodule Stripping** window, right-click (macOS:**Ctrl**+**click**) on a build.
1. Select **Open Backup Folder in Explorer** (exact text varies by operating system).

The package stores backup files in the `Library/com.unity.web.stripping-tool` folder of your Unity project.

The package stores its settings in the following files:

- `ProjectSettings/Packages/com.unity.platform-web.stripping-tool/Settings.json`. It's recommended to add this file to version control.
- `UserSettings/Packages/com.unity.platform-web.stripping-tool/Settings.json`. This file is local to the user.

## Required files for submodule stripping

Files `player_settings.json` and `MethodMap.tsv` are required for submodule stripping. If either is missing, the **Strip** and **Add Profiling** buttons will be disabled. Refer to [Distribute builds for submodule stripping](#distribute-builds-for-submodule-stripping) for instructions on how to share a build between projects with these files included.

The package also creates a `build-guid.txt` file in the root of every build folder. You can omit this file when deploying a build, but it's required for mapping a build with its backup folder.

## Distribute builds for submodule stripping

Both the build and backup files are needed to distribute a build to other users for submodule stripping or to use a build with the Web Stripping Tool in other Unity projects.

To distribute a build:

1. In the **Submodule Stripping** window, right-click (macOS:**Ctrl**+**click**) on a build.
1. Select **Show Build Folder in Explorer** (exact text varies by operating system).
1. Compress the highlighted folder. For example, compress `MyBuild` into `MyBuild.zip`.
1. For the same build, in the **Submodule Stripping** window, right-click (macOS:**Ctrl**+**click**) on a build.
1. Select **Show Backup Folder in Explorer** (exact text varies by operating system).
1. Compress the highlighted folder. For example, compress `MyBuild-e2cfcc8cdc8a45a480d5881753bd196d` into `MyBuild-e2cfcc8cdc8a45a480d5881753bd196d.zip`.

    >[!NOTE]
    > The GUID (`e2cfcc8cdc8a45a480d5881753bd196d`) at the end of the backup folder's name must match the GUID in `build-guid.txt` of the build folder.

To add this existing build to your Unity project:

1. Extract `MyBuild.zip` into `C:\ProjectB\Builds\MyBuild`, where `C:\ProjectB` represents your Unity project.
1. Extract `MyBuild-e2cfcc8cdc8a45a480d5881753bd196d.zip` into `C:\ProjectB\Library\com.unity.web.stripping-tool\MyBuild-e2cfcc8cdc8a45a480d5881753bd196d`.
1. In the **Submodule Stripping** window, select **Add Build**.
1. Locate `C:\ProjectA\Builds\MyBuild` and select **Select Folder**.

If the build was added successfully, the **Strip** and **Add Profiling** buttons are now enabled.

## Additional resources

* [Strip submodules from a build](strip-submodules.md)
* [Submodule stripping window reference](submodule-stripping-window-reference.md)