using Unity.Entities;

namespace Unity.Rendering
{
    [MaterialProperty("unity_MatrixPreviousMI", 4 * 4 * 3)]
    // TODO: Remove this component completely after verifying that the previous inverse is
    // not needed by HDRP.
    internal struct BuiltinMaterialPropertyUnity_MatrixPreviousMI_Tag : IComponentData
    {
    }
}
