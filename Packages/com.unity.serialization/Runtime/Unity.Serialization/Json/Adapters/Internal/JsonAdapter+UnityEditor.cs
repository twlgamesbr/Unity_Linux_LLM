#if UNITY_EDITOR
namespace Unity.Serialization.Json
{
    partial class JsonAdapter : IJsonAdapter, IJsonAdapter<UnityEngine.GUID>, IJsonAdapter<UnityEditor.GlobalObjectId>
    {
        void IJsonAdapter<UnityEngine.GUID>.Serialize(
            in JsonSerializationContext<UnityEngine.GUID> context,
            UnityEngine.GUID value
        ) => context.Writer.WriteValue(value.ToString());

        UnityEngine.GUID IJsonAdapter<UnityEngine.GUID>.Deserialize(
            in JsonDeserializationContext<UnityEngine.GUID> context
        ) => UnityEngine.GUID.TryParse(context.SerializedValue.ToString(), out var value) ? value : default;

        void IJsonAdapter<UnityEditor.GlobalObjectId>.Serialize(
            in JsonSerializationContext<UnityEditor.GlobalObjectId> context,
            UnityEditor.GlobalObjectId value
        ) => context.Writer.WriteValue(value.ToString());

        UnityEditor.GlobalObjectId IJsonAdapter<UnityEditor.GlobalObjectId>.Deserialize(
            in JsonDeserializationContext<UnityEditor.GlobalObjectId> context
        ) => UnityEditor.GlobalObjectId.TryParse(context.SerializedValue.ToString(), out var value) ? value : default;
    }
}
#endif
