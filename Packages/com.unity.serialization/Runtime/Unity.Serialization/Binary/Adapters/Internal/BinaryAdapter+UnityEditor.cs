#if UNITY_EDITOR
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;

namespace Unity.Serialization.Binary
{
    unsafe partial class BinaryAdapter
        : IBinaryAdapter,
            IBinaryAdapter<UnityEngine.GUID>,
            IBinaryAdapter<UnityEditor.GlobalObjectId>
    {
        void IBinaryAdapter<UnityEngine.GUID>.Serialize(
            in BinarySerializationContext<UnityEngine.GUID> context,
            UnityEngine.GUID value
        )
        {
            context.Writer->AddNBC(value.ToString());
        }

        UnityEngine.GUID IBinaryAdapter<UnityEngine.GUID>.Deserialize(
            in BinaryDeserializationContext<UnityEngine.GUID> context
        )
        {
            context.Reader->ReadNextNBC(out var str);
            return UnityEngine.GUID.TryParse(str, out var value) ? value : default;
        }

        void IBinaryAdapter<UnityEditor.GlobalObjectId>.Serialize(
            in BinarySerializationContext<UnityEditor.GlobalObjectId> context,
            UnityEditor.GlobalObjectId value
        )
        {
            context.Writer->AddNBC(value.ToString());
        }

        UnityEditor.GlobalObjectId IBinaryAdapter<UnityEditor.GlobalObjectId>.Deserialize(
            in BinaryDeserializationContext<UnityEditor.GlobalObjectId> context
        )
        {
            context.Reader->ReadNextNBC(out var str);
            return UnityEditor.GlobalObjectId.TryParse(str, out var value) ? value : default;
        }
    }
}
#endif
