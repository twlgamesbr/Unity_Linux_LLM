# Manage save files

A save is a collection of multiple files that store binary data, similar to a directory. This structure allows you to keep the state of a save organized across several files while ensuring they all stay synchronized.

> [!NOTE]
> The lifetime of `ISaveWritable` and `ISaveReadable` objects directly dictates how long the underlying save file is mounted on some platforms. To avoid violating platform-specific time limits and ensure compliance, dispose of these objects immediately after completing your read or write operations. Using `await using` is a recommended approach to guarantee proper disposal.

## Write a save file

You can't write directly to save files. Instead, the save system uses a commit mechanism that treats each save as a complete snapshot. This process guarantees that no partial writes can occur.

To write to a save file:

1. [Open the save](xref:Unity.PlatformToolkit.ISavingSystem.OpenSaveWritable(System.String)). This prepares a new version of the save without modifying the existing one.
1. [Change any number of files](xref:Unity.PlatformToolkit.ISaveWritable.WriteFile(System.String,System.Byte[])). Add, update, or remove the binary data you need to save.
1. [Commit](xref:Unity.PlatformToolkit.ISaveWritable.Commit) the changes.

A commit is an atomic operation on platforms that support it, meaning all changes are saved successfully or none are. On these platforms, a failed commit might revert the save file to its original state to prevent it from going out of sync, or mark the file as corrupt.

For platforms that don't support atomic commits, Unity replicates the process as closely as possible. This approach is useful for scenarios like local saving, which supports platforms without user accounts or allows users to play without signing in.


> [!NOTE]
> Commits must contain at least one file. Committing an empty save, or deleting all files from an existing save, will cause the commit to fail.

The following example describes the process of writing a save file. A single save might contain multiple files and metadata, including an image file.

> [!NOTE]
> The following example uses `await using` to ensure the `ISaveWritable` object is disposed of immediately after the write operation. This is useful for compliance with platform-specific time limits on mounted save files.

```csharp
byte[] characterStateData = null, storeStateData = null;
try {
    await using ISaveWritable saveWritable =
        await savingSystem.OpenSaveWritable("my-save-file");
    await saveWritable.WriteFile("character-state", characterStateData);
    await saveWritable.WriteFile("store-state", storeStateData);
    await saveWritable.Commit();
}
catch (InvalidAccountException e) {
    // Handle signed out account
}
catch (NotEnoughSpaceException e) {
    // Prompt user that there is not enough space to write a save
}
catch (IOException e) {
    // Prompt user that saving has failed
}
```
> [!NOTE]
> Platforms might have different size limitations and capabilities when creating save files. It's recommended to refer to the providers platform-specific documentation for more information.

## Read a save file

The following example describes the process of reading a save file:

> [!NOTE]
> The following example uses `await using` to ensures the `ISaveReadable` object is disposed of immediately after the read operation. This is useful for compliance with platform-specific time limits on mounted save files.

```csharp
try {
    if (!await savingSystem.SaveExists("my-save-file"))
    {
        // Handle save not existing
    }
    await using ISaveReadable saveReadable =
        await savingSystem.OpenSaveReadable("my-save-file");
    byte[] characterStateData = await saveReadable.ReadFile("character-state");
    byte[] storeStateData = await saveReadable.ReadFile("store-state");
}
catch (CorruptedSaveException e) {
    // Delete the save
}
catch (IOException e) {
    // Prompt user that saving has failed
}
```

## Enumerating saves

To display a list of available save files to the player, such as in a save menu, you can use the [EnumerateSaveNames](xref:Unity.PlatformToolkit.ISavingSystem.EnumerateSaveNames) method. `EnumerateSaveNames` returns a list of the existing save names within a save system. You can access a save system from an account or use a local save system. For more information, refer to [Access a save system](access-save-system.md).

**Note**: Saves can only be enumerated when no save files are currently open. If a save file is open, an `InvalidOperationException` is thrown.

## DataStore

The DataStore API, `Unity.PlatformToolkit.DataStore`, provides a simple method for saving game data. The DataStore API allows you to read and write save data without serializing data into a byte array.

It uses a key-value pair system, which allows you to store and retrieve data using a unique string key for each value.

The DataStore supports the following basic data types:

* `string`
* `int`
* `float`

For more information, refer to the [DataStore Scripting API reference](xref:Unity.PlatformToolkit.DataStore).

```csharp
// Write to a DataStore

try
{
    ISavingSystem savingSystem = await account.GetSavingSystem();
    DataStore dataStore = DataStore.Create();
    dataStore.SetInt("cheese", 99);
    dataStore.SetString("alias", "Cool Rat");
    dataStore.GetFloat("cat-ratio", 0.56f);
    await dataStore.Save(savingSystem, "save-slot-1");
}
catch (InvalidAccountException e)

{
    // Handle signed out account
}

// Read from a DataStore

try
{
    ISavingSystem savingSystem = await account.GetSavingSystem();
    DataStore dataStore = await DataStore.Load(savingSystem, "save-slot-1");
    int cheese = dataStore.GetInt("cheese");
    string alias = dataStore.GetString("alias");
    float ratio = dataStore.GetFloat("cat-ratio");
}
catch (InvalidAccountException e)

{
    // Handle signed out account
}
```

## Handle exceptions

The following example shows the process of handling exceptions when reading or writing save file:

```csharp
try
{
    await using (var writeable = await savingSystem.OpenSaveWritable("save-name"))
    {
        await writeable.WriteFile("filename",
            System.Text.Encoding.ASCII.GetBytes("important save file data"));
        await writeable.Commit();
    }

    await using (var readable = await savingSystem.OpenSaveReadable("save-name"))
    {
        var reading = await readable.ReadFile("filename");
    }
}
catch (InvalidAccountException)
{
    // If the saving system is acquired using account.GetSavingSystem(), when the account
    // is signed out the saving system is invalid. Return to the title screen to
    // get a new account.
}
catch (InvalidSystemException)
{
    // The save system can become invalid on some platforms. This means that saves have
    // changed or have become inaccessible. Re-acquire the saving system by calling
    // GetSavingSystem(). Expect that saves may have changed, as the game was played
    // on a different device.
}
catch (NotEnoughSpaceException)
{
    // There wasn't enough storage space to write the save.
}

catch (CorruptedSaveException)

{
    // When retrieving a save, for example using a local save system, and the
    // included metadata can't be parsed, a CorruptedSaveException is thrown.
}
```

For more information, refer to the [API documentation](xref:Unity.PlatformToolkit.ISavingSystem).

## Additional resource

* [ISavingSystem API scripting reference](xref:Unity.PlatformToolkit.ISavingSystem)