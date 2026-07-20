using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Scenes
{
    internal class AssetObjectManifest : ScriptableObject
    {
        public RuntimeGlobalObjectId[] GlobalObjectIds;
        public Object[] Objects;
    }

#if UNITY_EDITOR
    internal class AssetObjectManifestBuilder
    {
        public static unsafe void BuildManifest(GUID guid, AssetObjectManifest manifest)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid.ToString()));
            BuildManifest(objects, manifest);
        }

        public static unsafe void BuildManifest(Object[] objects, AssetObjectManifest manifest)
        {
            manifest.Objects = objects;
            manifest.GlobalObjectIds = new RuntimeGlobalObjectId[objects.Length];
            var globalobjectIds = new GlobalObjectId[objects.Length];

            GlobalObjectId.GetGlobalObjectIdsSlow(objects, globalobjectIds);

            fixed (GlobalObjectId* src = globalobjectIds)
            fixed (RuntimeGlobalObjectId* dst = manifest.GlobalObjectIds)
            {
                UnsafeUtility.MemCpy(dst, src, UnsafeUtility.SizeOf<RuntimeGlobalObjectId>() * objects.Length);
            }
        }
    }
#endif
}
