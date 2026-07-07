using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System;
using NUnit.Framework;

namespace Unity.Numerics.Memory.Tests
{
    public unsafe class TestMemoryManager
    {
        // Job randomly either allocates to the heap or removes from the heap
        [BurstCompile(CompileSynchronously = true)]
        [GenerateTestsForBurstCompatibility]
        struct AllocOrFreeJob : IJob
        {
            public MemoryManager heap;
            public int maxAllocSize;
            public int numAllocs;

            public void Execute()
            {
                using var allocs = new NativeList<IntPtr>(numAllocs, Allocator.Temp);
                var r = new Random.Random(1);

                for (int i = 0; i < numAllocs; i++)
                {
                    if (allocs.IsEmpty || r.NextGaussian() > 0)
                    {
                        // allocate
                        var ptr = heap.Allocate<char>(r.NextUniformInt(8, maxAllocSize + 1));
                        allocs.Add((IntPtr)ptr);
                    }
                    else
                    {
                        // free
                        var idx = r.NextUniformInt(0, allocs.Length);
                        var ptr = (char*)allocs[idx];
                        heap.Free(ptr);
                        allocs.RemoveAt(idx);
                    }
                }

                for (int i = 0; i < allocs.Length; ++i)
                {
                    var ptr = (char*)allocs[i];
                    heap.Free(ptr);
                }
            }
        }

        // Note: MemoryManager is not thread safe. Don't ScheduleParallel
        [Test]
        public void TestMemoryManager_AllocOrFree()
        {
            int numAllocs = 256;
            int maxAllocSize = 512;
            Assert.DoesNotThrow(() =>
            {
                using var heap = MemoryManager.Create(numAllocs * maxAllocSize, Allocator.Temp);
                new AllocOrFreeJob()
                {
                    heap = heap,
                    numAllocs = numAllocs,
                    maxAllocSize = maxAllocSize
                }.Run();

                Assert.IsTrue(heap.info->activeAllocations == 0);
            });
        }

        [BurstCompile(CompileSynchronously = true)]
        [GenerateTestsForBurstCompatibility]
        struct AllocJob : IJob
        {
            public MemoryManager heap;
            public int numAllocs;
            public int maxAllocSize;

            public void Execute()
            {
                using var allocs = new NativeList<IntPtr>(numAllocs, Allocator.Temp);
                var r = new Random.Random(1);

                for (int i = 0; i < numAllocs; i++)
                {
                    // allocate
                    var size = r.NextUniformInt(8, maxAllocSize + 1);
                    var ptr = heap.Allocate<char>(size);
                    allocs.Add((IntPtr)ptr);
                    if (ptr == null) break;
                }

                for (int i = 0; i < allocs.Length; ++i)
                {
                    var ptr = (char*)allocs[i];
                    heap.Free(ptr);
                }
            }
        }

        // Note: MemoryManager is not thread safe. Don't ScheduleParallel
        [Test]
        public void TestMemoryManager_Alloc()
        {
            int numAllocs = 1024;
            int maxAllocSize = 128;
            Assert.DoesNotThrow(() =>
            {
                using var heap = MemoryManager.Create(numAllocs * maxAllocSize * 2, Allocator.Temp);
                new AllocJob
                {
                    heap = heap,
                    numAllocs = numAllocs,
                    maxAllocSize = maxAllocSize
                }.Run();

                Assert.IsTrue(heap.info->activeAllocations == 0);
            });
        }

        [Test]
        public void TestMemoryManager_HeapAllocErrorRaisedWhenInsufficientMemory()
        {
            int numAllocs = 128;
            int maxAllocSize = 16;

            using var heap = MemoryManager.Create(numAllocs * maxAllocSize, Allocator.Temp);
            using var allocs = new NativeList<IntPtr>(numAllocs, Allocator.Temp);
            Assert.Throws<OutOfMemoryException>(() =>
            {
                for (int i = 0; i < numAllocs; i++)
                {
                    var size = 32;
                    var ptr = heap.Allocate<char>(size);
                    allocs.Add((IntPtr)ptr);
                }
            });

            // Should get an Exception and then we need to dispose of what was successfully allocated
            for (int i = 0; i < allocs.Length; ++i)
            {
                var ptr = (char*)allocs[i];
                heap.Free(ptr);
            }
        }
    }
}
