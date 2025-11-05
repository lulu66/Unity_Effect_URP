using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    internal abstract class DecalChunk : IDisposable
    {
        // decal的个数
        public int count { get; protected set; }

        // decal的容量
        public int capacity { get; protected set; }

        public JobHandle currentJobHandle { get; set; }

        public virtual void Push() { count++; }
        public abstract void RemoveAtSwapBack(int index);
        public abstract void SetCapacity(int capacity);

        public virtual void Dispose() { }

        /// <summary>
        /// 更新Projector transform array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="decalProjectors"></param>
        /// <param name="capacity"></param>
        protected void ResizeNativeArray(ref TransformAccessArray array, DecalProjector[] decalProjectors, int capacity)
        {
            var newArray = new TransformAccessArray(capacity);
            if (array.isCreated)
            {
                for (int i = 0; i < array.length; ++i)
                    newArray.Add(decalProjectors[i].transform);
                array.Dispose();
            }
            array = newArray;
        }

        protected void RemoveAtSwapBack<T>(ref NativeArray<T> array, int index, int count) where T : struct
        {
            array[index] = array[count - 1];
        }

        protected void RemoveAtSwapBack<T>(ref T[] array, int index, int count)
        {
            array[index] = array[count - 1];
        }
    }
}
