---
uid: wait-for-next-ui-frame
---

# Wait for the next UI Frame

Waiting frames with `yield return null` during automated tests can often be unstable as there is no guarantee that every subsystem has updated within that frame.

`UIFrameWaiter` is a helper designed to mitigate this issue.

UIFrameWaiter will loop until the root Panel's updaters and the scheduler of a Panel have completed their update loops at least once.
If this condition is not reached within a timeout of 1 second, an exception is thrown.

UIFrameWaiter can be called on an `EditorWindow`, `VisualElement`, or `Panel`.

In the context of an EditorPanel, some updaters are only called if there is a Repaint. When UIFrameWaiter is called on an EditorPanel that is not dirty, it will only include a subset of the updaters in its check for a full update loop.


``` csharp
// Example UI for an EditorWindow that contains a Button.
EditorWindow window = ScriptableObject.CreateInstance<MyEditorWindow>();
Button button = window.rootVisualElement.Query<Button>(name: "MyButton");

// Click a button that results in some UI action
// that needs to be waited for in MyEditorWindow.
yield return button.SimulateClick();

// Wait for a full UI loop.
yield return button.UIFrameWaiter();

or

yield return window.UIFrameWaiter();

or

yield return window.rootVisualElement.UIFrameWaiter();

or

yield return window.rootVisualElement.panel.UIFrameWaiter();

// In this case, all the above calls are equivalent
// because every calling element shares the same root Panel.
```

You can specify a custom enumerator function to be executed during UIFrameWaiter. The default is null, which results in a `yield return null`.

UIFrameWaiter can be used in conjunction with `SetFrameWaiter` to ensure that UIFrameWaiter is executed as part of helper function calls. See [Changing the wait function](#changing-the-wait-function) for more documentation around `SetFrameWaiter`.

``` csharp
// Example UI for an EditorWindow that contains a Button.
EditorWindow window = ScriptableObject.CreateInstance<MyEditorWindow>();
Button button = window.rootVisualElement.Query<Button>(name: "MyButton");
BaseVisualElementPanel panel = window.rootVisualElement.elementPanel;

// Set the FrameWaiter to be executed during helper calls.
EventHelpers.SetFrameWaiter(() => panel.UIFrameWaiter());

or

// Set the FrameWaiter to be executed during helper calls.
// This example provides a custom waiter function
// which will be executed during each loop performed by UIFrameWaiter.
EventHelpers.SetFrameWaiter(() => panel.UIFrameWaiter(MyCustomEnumerator));

// UIFrameWaiter will now be executed during the SimulateClick helper call.
yield return button.SimulateClick();
```

UIFrameWaiter is unfortunately not a magic fix. While it does ensure full loops of the updaters and scheduler, there are often other things at play that determine how a UI updates.

If multiple UI Frames are required for a particular scenario, `Repeat` can be used to execute any enumerator multiple times more easily.

``` csharp
// Fetch a UIBuilder window.
Builder builderWindow = ScriptableObject.CreateInstance<Builder>();
BaseVisualElementPanel panel = builderWindow.rootVisualElement.elementPanel;

// Do something that updates UI.
...

// Repeat UIFrameWaiter 2 times.
yield return Repeat(() => panel.UIFrameWaiter(), 2);

or

yield return Repeat(() => panel.UIFrameWaiter(MyCustomEnumerator), 2);
```

## Additional resources