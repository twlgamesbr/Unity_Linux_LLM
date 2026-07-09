using UnityEngine;

internal class SceneWithNestedLayoutElementsLoadScript : MonoBehaviour
{
    public bool isStartCalled { get; private set; }

    protected void Start()
    {
        isStartCalled = true;
    }
}
