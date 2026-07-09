# Changelog

## [1.1.0] - 2026-05-25

### Added
- Support for Build Profiles has been added for 6000.4+. This is provided via a new Build Profile component which allows an implementation to be configured to override the Platform Toolkit settings in Project Settings. These settings still function as the default for when this component is non in-use.
- Added a new validation option to Play Mode Controls to help identify storage operations which may lead to issues or violate submission requirements on some platforms.
- Support for configuring and testing account Attributes in the Editor through Play Mode Controls.

### Fixed
- Play Mode Controls account sign ins via the PT APIs now only throw for the 'offline' environment option if using a platform behaviour which could do so in the equivalent runtime.
- Fixed an issue where a build target not having a valid PT implementation would cause an exception to throw from the builder.
- Fixed some minor issues with exception documentation on saving system APIs.
- Fixed a bug where Play Mode Control Settings test accounts could not have their pictures set to 'none'.
- Fixed undocumented CorruptedSaveException and NotEnoughSpaceException in ISavingSystem.
- Fixed a Play Mode Controls issue where UI would not update when adding or deleting a save
- Fixed a bug where rapid input device change events could trigger an exception.
- Fixed ISaveWritable.WriteFile() accepting null data without throwing; it now throws ArgumentNullException immediately.
- Fixed an incorrect ArgumentNullException being thrown when an empty save name is passed to saving APIs; these now correctly throw ArgumentException.
- Fixed an Error where creating a large number of Play Mode Control accounts would throw NullReferenceExceptions.
- Fixed the Warning for creating duplicate Attributes with the same name only showing on initial string definition but not after returning to the settings.
- Fixed broken link behind the Help button of the Play Mode Controls Settings asset.


## [1.0.1] - 2026-02-26

### Changed
- The Achievement editor rows now allow for reordering.
- The Achievement editor's platform-specific ID field validation has been relaxed to support more of what some platform backends allow.
- Archive-based saves now use a different zip library to allow for streamed file read/write to reduce memory overhead with these. System.IO.Compression.ZipArchive would load the entire archive into memory on Dispose which prevented this.
- Archive-based saves no longer apply compression to reduce access overheads.
- The LocalSaving implementation now provides an atomic save commit process.

### Fixed
- Fixed a bug where the Initialize method for Platform Toolkit was not properly guarded from double-initialization.
- Fixed an issue where archive-based saves could corrupt data if the length of data was reduced in a subsequent write.
- Some achievement validation errors are now no longer logged outside of development builds.
- Fixed some excessive GC allocs from an internally used type.


## [1.0.0] - 2025-11-03
- Initial release