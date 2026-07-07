---
uid: change-the-wait-function
---

# Change the wait function

In some cases, it may be useful to change the wait function that's executed during Dispatch or Simulate helper function calls.

The default wait function is equivalent to a `yield return null`.

Suppose we have the following functions that we wish to use during certain Dispatch or Simulate helper calls.

``` csharp
public IEnumerator TemporaryWaiter(int frames = 1)
{
    for (int i = 0; i < frames; i++)
    {
        yield return null;
    }
} 

public void TemporaryVoidWaiter()
{
    Debug.Log("Void waiter");
}
```

The waiter function that will be used can be set as follows:

``` csharp
// Add a Button to your UI or fetch a Button from your UI via a Query.
var button = new Button("MyButton");
var button = rootVisualElement.Query<Button>(name: "MyButton");

// Set the frame Waiter to TemporaryWaiter.
// We set the waiter using this syntax because it has input arguments.
EventHelpers.SetFrameWaiter(() => TemporaryWaiter(2));

// This call will wait 2 frames between every event sent.
yield return rootVisualElement.SimulateMouseMoveTo(button.worldBound.center);

// Set the frame waiter to TemporaryVoidWaiter ONLY within this using context.
// We can set the waiter using this syntax because
// it does not have any input arguments.
using (EventHelpers.SetFrameWaiter(TemporaryVoidWaiter))
{
    yield return button.SimulateClick();
    
    // The frame waiter within the using context will be disposed.
}

// At this stage, the waiter function is once again set to TemporaryWaiter.
```

## Additional resources