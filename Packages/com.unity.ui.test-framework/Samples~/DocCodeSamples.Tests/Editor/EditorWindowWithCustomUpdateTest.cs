using NUnit.Framework;
using UnityEditor.UIElements.TestFramework;

public class EditorWindowWithCustomUpdateTest : EditorWindowUITestFixture<EditorWindowWithCustomUpdate>
{
    [Test]
    public void ExplicitlyCallUpdateFunction_ForWindowWithCustomUpdate()
    {
        simulate.FrameUpdate();
        simulate.Click(window.Button);
        simulate.FrameUpdate();

        Assert.That(window.Button.text, Is.EqualTo("Button was clicked!"));

        // The label's text won't have updated since its logic
        // is tied to the window's update function.
        Assert.That(window.ButtonClickedCountLabel.text,
            Is.EqualTo("Times button was clicked: 0"));

        // Call the window's update function directly,
        // or wait for actual frames using coroutines.
        window.Update();

        Assert.That(window.ButtonClickedCountLabel.text,
            Is.EqualTo("Times button was clicked: 1"));
    }
}
