using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>按实例数复用合并用数组，避免每次重建都为 CombineMeshes 分配临时数组。</summary>
    class VoxelPlatformCombineBuffer
    {
        // 缓存上限按数组个数计，超过后归还的数组直接交给 GC，避免多种长度各留一份把内存撑大。
        private const int MaxCachedArrays = 8;
        // CombineMeshes 只接受长度等于实例数的数组，因此按长度分桶复用。
        private readonly Dictionary<int, Stack<CombineInstance[]>> buckets = new();
        private int cachedCount;

        /// <summary>取得长度等于实例数的数组，对应长度的桶为空时新建。</summary>
        internal CombineInstance[] Rent(int count)
        {
            if (!buckets.TryGetValue(count, out Stack<CombineInstance[]> bucket) || bucket.Count == 0)
                return new CombineInstance[count];
            cachedCount--;
            return bucket.Pop();
        }

        /// <summary>清掉数组内的网格引用后放回对应长度的桶。</summary>
        internal void Return(CombineInstance[] array)
        {
            if (array == null || array.Length == 0) return;
            // 数组里的网格引用会延长几何变体的生命周期，缓存前先清空。
            System.Array.Clear(array, 0, array.Length);
            if (cachedCount >= MaxCachedArrays) return;
            if (!buckets.TryGetValue(array.Length, out Stack<CombineInstance[]> bucket))
                buckets.Add(array.Length, bucket = new Stack<CombineInstance[]>());
            bucket.Push(array);
            cachedCount++;
        }

        /// <summary>丢弃全部缓存数组。</summary>
        internal void Clear()
        {
            buckets.Clear();
            cachedCount = 0;
        }
    }
}
