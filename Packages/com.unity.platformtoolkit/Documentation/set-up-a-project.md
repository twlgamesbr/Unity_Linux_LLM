# Set up a project with the Platform Toolkit package

Use the following workflow to set up a project with the Platform Toolkit package.

1. [Install the Platform Toolkit package](#install-the-platform-toolkit-package)
1. [Install platform modules and packages](#install-platform-modules-and-packages)
1. [Create a Play Mode Controls Settings asset](#create-a-play-mode-controls-settings-asset)
1. [Configure Platform Toolkit Project settings](#configure-platform-toolkit-project-settings)
1. [Initialize Platform Toolkit](#initialize-platform-toolkit)
1. [Additional configuration](#additional-configuration)

## Install the Platform Toolkit package

For information on how to access and install the Platform Toolkit package, refer to [Install the Platform Toolkit package](install-the-pt-package.md).

## Install platform modules and packages

Install the required platform modules and platform packages for your project. For further information, refer to [Install platform modules and packages](install-platform-modules.md).

## Create a Play Mode Controls Settings asset

A Play Mode Controls Settings asset is required to manage the Play Mode Controls feature. For further information, refer to [Create a Play Mode Controls Settings asset](./playmodecontrols/play-mode-create.md).

## Configure Platform Toolkit implementation settings

Configure which Platform Toolkit implementation to use for each of your target platforms. There are two ways to do this:

* **Build profile overrides:** Configure the implementation per build profile so you can maintain multiple configurations without changing your project settings each time. For more information, refer to [Configure build profiles for Platform Toolkit](configure-pt-build-profiles.md).
* **Project settings**: Use the Platform Toolkit project settings (menu: **Edit** > **Project Settings** > **Platform Toolkit**) to set a global implementation for each platform. A default value is automatically selected for each available platform.

## Initialize Platform Toolkit

Use [`PlatformToolkit.Initialize()`](xref:Unity.PlatformToolkit.PlatformToolkit.Initialize) to initialize the Platform Toolkit package and access the available services and systems for your target platforms. It's recommended to initialize Platform Toolkit early in the application lifecycle, such as in the initial loading scene.

## Additional configuration

There might be additional configuration steps required depending on your project's specific needs.

### Configure attributes

Configure attributes to create platform agnostic references to account information. This allows you to access user data consistently across different platforms.

For more information, refer to [Get started with attributes](./accounts/get-started-with-attributes.md)

### Add achievements

Use the **Achievement Editor** to add achievements to your project. For further information, refer to [Configure achievements with the Achievement Editor](./achievements/configure-achievements.md).

### Configure Play Mode Controls

Configure Play Mode Controls to simulate different user scenarios and ensure that your implementation works as expected. For more information, refer to [Create a Play Mode Controls Settings asset](./playmodecontrols/play-mode-create.md).

## Additional resources

* [Play mode controls](./playmodecontrols/play-mode-controls.md)
* [Configure achievements with the Achievement Editor](./achievements/configure-achievements.md)
* [Get started with attributes](./accounts/get-started-with-attributes.md)