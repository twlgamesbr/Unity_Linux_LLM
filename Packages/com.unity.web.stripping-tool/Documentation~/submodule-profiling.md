# Submodule profiling

Submodule profiling automatically identifies used and unused submodules in a build as you play.

For step-by-step instructions on how to profile a build and remove unused submodules, refer to [Strip submodules](strip-submodules.md#profile-the-build-for-unused-submodules).

## Capturing profiling data

As you play through a game, profiling identifies which submodules the build uses and generates a report.

To capture all unused submodules, the recommended best practice is to play through the whole game. However, playing the whole game isn't necessary if the game uses the same effects in all levels. In that case, play a representative part of the game and any levels with unique effects.

### Profiling for multiple platforms

Profile your build on every platform you intend to deploy to because different platforms might use different submodules. A mobile build, for example, might use different compression methods than a desktop build.

To combine profiling data from multiple tests, import each report into the stripping settings and choose to [combine results](strip-submodules.md#profile-the-build-for-unused-submodules).

## Additional resources

* [Strip submodules](strip-submodules.md)
* [Test the stripped build](test-stripped-build.md)
* [Optimize the stripped build](optimize-stripped-build.md)