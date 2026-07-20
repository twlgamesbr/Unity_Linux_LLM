using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.Rendering
{
    internal static class MemoryUtilities
    {
        public static unsafe T* Malloc<T>(int count, Allocator allocator)
            where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), allocator);
        }

        public static unsafe void Free<T>(T* p, Allocator allocator)
            where T : unmanaged
        {
            UnsafeUtility.Free(p, allocator);
        }
    }
}
