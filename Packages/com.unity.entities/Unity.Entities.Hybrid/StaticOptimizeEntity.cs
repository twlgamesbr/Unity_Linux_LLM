using UnityEngine;
using MonoBehaviour = UnityEngine.MonoBehaviour;

namespace Unity.Entities
{
    /// <summary>
    /// Authoring component that indicates that the hierarchy from this point should be considered static and can be optimized.
    /// </summary>
    /// <remarks>The `Static` component is added to all the entities in the hierarchy.</remarks>
    [DisallowMultipleComponent]
    public class StaticOptimizeEntity : MonoBehaviour
    {
    }
}
