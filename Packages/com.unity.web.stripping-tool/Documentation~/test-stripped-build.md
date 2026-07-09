# Test the stripped build

After you remove submodules, test the build to ensure the game still works as expected.

For best results, [profile the build](strip-submodules.md#profile-the-build-for-unused-submodules) to find out which submodules to remove.

To test the build:

1. [Run the stripped build in a browser](strip-submodules.md#run-the-stripped-build).
1. Enable **Missing Submodule Error Handling** in the [Submodule Stripping Settings](submodule-stripping-window-reference.md)
1. Check the build for:
    * "Stripped function 'X' (used in submodule 'Y') was called" (or similar) messages, either in the browser console or as an explicit error dialog,
    depending on the used **Missing Submodule Error Handling** option.
    * Unusual error messages in the browser console.
    * Missing or broken in-game elements, such as missing text, missing meshes, or corrupted textures.

1. If the build works as expected, [optimize the final build](optimize-stripped-build.md).
    >[!NOTE]
    >If the build doesn’t work as expected, it likely needs a submodule you stripped out. [Profile the build](strip-submodules.md#profile-the-build-for-unused-submodules) to make sure you remove only unused submodules.

## Additional resources

* [Strip submodules from a build](strip-submodules.md)
* [Optimize the stripped build](optimize-stripped-build.md)
