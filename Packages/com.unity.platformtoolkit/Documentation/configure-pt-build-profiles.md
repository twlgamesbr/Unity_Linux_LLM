# Configure build profiles for Platform Toolkit

Use [build profile overrides](https://docs.unity3d.com/Documentation/Manual/build-profiles-override-settings.html) to select which Platform Toolkit implementation to use for each of your target builds. This allows you to maintain multiple build configurations with different implementations without modifying your project settings each time.

> [!NOTE]
> Build profile overrides for Platform Toolkit are available in Unity 6.4 and later.

There are two ways to configure the Platform Toolkit implementation for your builds:

* **Build profile overrides (recommended)**: Attach a Platform Toolkit settings component to a build profile. Each profile can use a different implementation, and the override takes precedence over global project settings when you build using that profile.
* **Project settings**: Set a global Platform Toolkit implementation in **Edit** > **Project Settings** > **Platform Toolkit**. This setting applies to all builds unless you use a build profile with an active override.

## Supported implementations

The implementations available in the **Platform implementation** dropdown depend on which Platform Toolkit platform packages are installed in your project and which build profile platform you're configuring.

| **Build profile platform** | **Available implementations** |
| :---- | :---- |
| Windows | Local Saving, GDK, Steam |
| macOS | Local Saving, Steam |
| iOS | GameKit |
| Android | Google Play Games Services |

Platform Toolkit also supports build profile overrides for closed platforms. If you have access to the relevant console packages and platform modules, the corresponding implementation appears in the dropdown. For more information, refer to [Install platform modules and packages.](install-platform-modules.md)

## Add a Platform Toolkit override to a build profile

To configure a Platform Toolkit override for a build profile:

1. Open the **Build Profiles** window (menu: **File** > **Build Profiles**).
2. Select the build profile to configure.
3. Select **Add Settings** and then select **Platform Toolkit Settings**. A **Platform Toolkit Settings** foldout appears in the build profile.
4. In the **Platform implementation** dropdown, select the platform implementation to use. The available implementations depend on the Platform Toolkit packages and platform modules installed in your project. Where local saving is supported, it's selected as the default.

When you build using this profile, Platform Toolkit uses the selected implementation regardless of the global project settings.

> [!NOTE]
> Each build profile maintains its own Platform Toolkit override. For example, you can configure separate Windows build profiles targeting Local Saving and Steam, and each profile produces a build with the corresponding implementation.

## Configure Platform Toolkit using project settings

If you don't use build profile overrides, configure a global Platform Toolkit implementation that applies to all builds.

To configure Platform Toolkit in project settings:

1. Open **Edit** > **Project Settings** > **Platform Toolkit**.
2. For each implementation, select the platform to use from the available options.

> [!NOTE]
> When an active build profile includes a Platform Toolkit override, the global project settings don't apply to builds using that profile. A warning also appears in **Project Settings** > **Platform Toolkit**. Select **Edit Build Profile** from the warning to open the **Build Profiles** window and review the active profile's override settings.

## Reset or remove build profile overrides

You can remove and reset the Platform Toolkit Settings override for your build profile using the available options from the **More** (⋮) menu.

## Restore a missing implementation

If a Platform Toolkit override references an implementation that's been removed from the project, the **Platform implementation** property displays the implementation name followed by `(Missing)`, for example `Unity.Steam (Missing)`. Platform Toolkit preserves the override data, and reinstalling the platform package restores the setting.

To change to a different installed implementation:

1. In the **Build Profiles** window, select the build profile with the missing implementation.
2. Expand the **Platform Toolkit Settings** foldout.
3. In the **Platform implementation** dropdown, select an alternative implementation.

## Additional resources

- [Build Profiles](https://docs.unity3d.com/Documentation/Manual/build-profiles.html)
- [Override settings with build profiles](https://docs.unity3d.com/Documentation/Manual/build-profiles-override-settings.html)
- [Set up a project with the Platform Toolkit package](set-up-a-project.md)
- [Install platform modules and packages](install-platform-modules.md)