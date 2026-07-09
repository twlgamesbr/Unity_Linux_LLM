# Optimize the stripped build

Optimize the stripped build to reduce the final build size and improve the performance of your game.

Before optimizing:

* [Remove all unused submodules](strip-submodules.md)
* [Test the build](test-stripped-build.md)
* Enable **Remove Debug Information** in the [Submodule Stripping Settings](submodule-stripping-window-reference.md) or enable external [debug symbols](xref:class-PlayerSettingsWebGL#Publishing).
* Set **Missing Submodule Error Handling** to **Ignore** in the [Submodule Stripping Settings](submodule-stripping-window-reference.md).

To optimize the build:

1. In the [Submodule Stripping Settings](submodule-stripping-window-reference.md), enable **Optimize Code After Stripping**.
2. Select **Strip**.

>[!NOTE]
>**Optimize Code After Stripping** drastically increases the runtime of the stripping tool. Disable this setting for testing.

## Additional resources

* [Strip submodules from a build](strip-submodules.md)
* [Test the stripped build](test-stripped-build.md)
* [Submodule Stripping Settings reference](submodule-stripping-window-reference.md)
