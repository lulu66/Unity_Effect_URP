using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Jobs;

namespace UnityEngine.Rendering.Universal
{
    internal class DecalEntityIndexer
    {
        public struct DecalEntityItem
        {
            public int chunkIndex;
            public int arrayIndex;
            public int version;
        }

        private List<DecalEntityItem> m_Entities = new List<DecalEntityItem>();

        // 用一个队列来记录m_Entities列表中已经没有有效元素的下标，以便于循环利用列表，节省空间
        private Queue<int> m_FreeIndices = new Queue<int>();

        public bool IsValid(DecalEntity decalEntity)
        {
            if (m_Entities.Count <= decalEntity.index)
                return false;

            return m_Entities[decalEntity.index].version == decalEntity.version;
        }

        /// <summary>
        /// 添加一个decal，添加在列表哪个位置由队列中存储的下标决定，会优先存放在队列记录的下标位置处,如果队列已经无元素，则在列表末端添加decal
        /// </summary>
        /// <param name="arrayIndex">在chunck列表每个小chunck中的数组的下标</param>
        /// <param name="chunkIndex">在chunck列表中的下标</param>
        /// <returns></returns>
        public DecalEntity CreateDecalEntity(int arrayIndex, int chunkIndex)
        {
            // Reuse
            if (m_FreeIndices.Count != 0)
            {
                // 找到一个无效数据位置
                int entityIndex = m_FreeIndices.Dequeue();
                int newVersion = m_Entities[entityIndex].version + 1;

                // 填充一个新的decal
                m_Entities[entityIndex] = new DecalEntityItem()
                {
                    arrayIndex = arrayIndex,
                    chunkIndex = chunkIndex,
                    version = newVersion,
                };
                // 返回decal在列表中的位置下标
                return new DecalEntity()
                {
                    index = entityIndex,
                    version = newVersion,
                };
            }

            // Create new one
            // 如果队列已经清空，说明列表中被填满了，此时从列表末尾填充decal数据
            {
                int entityIndex = m_Entities.Count;
                int version = 1;

                m_Entities.Add(new DecalEntityItem()
                {
                    arrayIndex = arrayIndex,
                    chunkIndex = chunkIndex,
                    version = version,
                });


                return new DecalEntity()
                {
                    index = entityIndex,
                    version = version,
                };
            }
        }

        /// <summary>
        /// 销毁一个decal，会把decal的索引入栈，并标记为已过时,以便于之后再利用这个下标位置填充decal数据
        /// </summary>
        /// <param name="decalEntity"></param>
        public void DestroyDecalEntity(DecalEntity decalEntity)
        {
            Assert.IsTrue(IsValid(decalEntity));
            m_FreeIndices.Enqueue(decalEntity.index);

            // Update version that everything that points to it will have outdated version
            var item = m_Entities[decalEntity.index];
            item.version++;
            m_Entities[decalEntity.index] = item;
        }

        /// <summary>
        /// 根据decalEntity中的下标位置返回一个decalEntityItem数据
        /// </summary>
        /// <param name="decalEntity"></param>
        /// <returns></returns>
        public DecalEntityItem GetItem(DecalEntity decalEntity)
        {
            Assert.IsTrue(IsValid(decalEntity));
            return m_Entities[decalEntity.index];
        }

        /// <summary>
        /// 更新decal在entityChunck列表中的位置
        /// </summary>
        public void UpdateIndex(DecalEntity decalEntity, int newArrayIndex)
        {
            Assert.IsTrue(IsValid(decalEntity));
            var item = m_Entities[decalEntity.index];
            item.arrayIndex = newArrayIndex;
            item.version = decalEntity.version;
            m_Entities[decalEntity.index] = item;
        }

        /// <summary>
        /// 批量更新decal的chunck位置下标
        /// </summary>
        /// <param name="remaper"></param>        
        public void RemapChunkIndices(List<int> remaper)
        {
            for (int i = 0; i < m_Entities.Count; ++i)
            {
                int newChunkIndex = remaper[m_Entities[i].chunkIndex];
                var item = m_Entities[i];
                item.chunkIndex = newChunkIndex;
                m_Entities[i] = item;
            }
        }

        /// <summary>
        /// 清理entity数组
        /// </summary>
        public void Clear()
        {
            m_Entities.Clear();
            m_FreeIndices.Clear();
        }
    }

    // 作为在DecalEntityManager和DecalEntityIndexer之间传递下标位置信息的媒介结构体
    internal struct DecalEntity
    {
        public int index;
        public int version;
    }

    /// <summary>
    /// Contains <see cref="DecalEntity"/> and shared material.
    /// </summary>
    internal class DecalEntityChunk : DecalChunk
    {
        public Material material;

        // decal实体列表（只存储其在总列表的索引）
        public NativeArray<DecalEntity> decalEntities;
        // 贴花对应的projector列表
        public DecalProjector[] decalProjectors;
        // 贴花对应的projector transform 列表
        public TransformAccessArray transformAccessArray;

        public override void Push()
        {
            count++;
        }

        public override void RemoveAtSwapBack(int entityIndex)
        {
            RemoveAtSwapBack(ref decalEntities, entityIndex, count);
            RemoveAtSwapBack(ref decalProjectors, entityIndex, count);
            transformAccessArray.RemoveAtSwapBack(entityIndex);
            count--;
        }

        public override void SetCapacity(int newCapacity)
        {
            decalEntities.ResizeArray(newCapacity);
            ResizeNativeArray(ref transformAccessArray, decalProjectors, newCapacity);
            ArrayExtensions.ResizeArray(ref decalProjectors, newCapacity);
            capacity = newCapacity;
        }

        public override void Dispose()
        {
            if (capacity == 0)
                return;

            decalEntities.Dispose();
            transformAccessArray.Dispose();
            decalProjectors = null;
            count = 0;
            capacity = 0;
        }
    }

    /// <summary>
    /// Manages lifetime between <see cref="DecalProjector"></see> and <see cref="DecalEntity"/>.
    /// Contains all <see cref="DecalChunk"/>.
    /// </summary>
    internal class DecalEntityManager : IDisposable
    {
        public List<DecalEntityChunk> entityChunks = new List<DecalEntityChunk>();
        public List<DecalCachedChunk> cachedChunks = new List<DecalCachedChunk>();
        public List<DecalCulledChunk> culledChunks = new List<DecalCulledChunk>();
        public List<DecalDrawCallChunk> drawCallChunks = new List<DecalDrawCallChunk>();

        // 计数器，用来记录chunk的个数
        public int chunkCount;

        private ProfilingSampler m_AddDecalSampler;
        private ProfilingSampler m_ResizeChunks;
        private ProfilingSampler m_SortChunks;

        private DecalEntityIndexer m_DecalEntityIndexer = new DecalEntityIndexer();

        // 记录decal使用的材质和它在chunck列表中的位置下标
        private Dictionary<Material, int> m_MaterialToChunkIndex = new Dictionary<Material, int>();

        private struct CombinedChunks
        {
            public DecalEntityChunk entityChunk;
            public DecalCachedChunk cachedChunk;
            public DecalCulledChunk culledChunk;
            public DecalDrawCallChunk drawCallChunk;
            public int previousChunkIndex;
            public bool valid;
        }
        private List<CombinedChunks> m_CombinedChunks = new List<CombinedChunks>();
        private List<int> m_CombinedChunkRemmap = new List<int>();

        private Material m_ErrorMaterial;
        public Material errorMaterial
        {
            get
            {
                if (m_ErrorMaterial == null)
                    m_ErrorMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/InternalErrorShader"));
                return m_ErrorMaterial;
            }
        }

        private Mesh m_DecalProjectorMesh;
        public Mesh decalProjectorMesh
        {
            get
            {
                if (m_DecalProjectorMesh == null)
                    m_DecalProjectorMesh = CoreUtils.CreateCubeMesh(new Vector4(-0.5f, -0.5f, -0.5f, 1.0f), new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                return m_DecalProjectorMesh;
            }
        }

        public DecalEntityManager()
        {
            m_AddDecalSampler = new ProfilingSampler("DecalEntityManager.CreateDecalEntity");
            m_ResizeChunks = new ProfilingSampler("DecalEntityManager.ResizeChunks");
            m_SortChunks = new ProfilingSampler("DecalEntityManager.SortChunks");
        }

        /// <summary>
        /// 返回指定的decal是否有效
        /// </summary>
        /// <param name="decalEntity"></param>
        /// <returns></returns>
        public bool IsValid(DecalEntity decalEntity)
        {
            return m_DecalEntityIndexer.IsValid(decalEntity);
        }

        /// <summary>
        /// 根据decal projector提供的信息，创建一个decal，并添加到chunck列表中，更新chunck的数据
        /// </summary>
        /// <param name="decalProjector"></param>
        /// <returns></returns>
        public DecalEntity CreateDecalEntity(DecalProjector decalProjector)
        {
            // 获取贴花材质
            var material = decalProjector.material;
            if (material == null)
                material = errorMaterial;

            using (new ProfilingScope(null, m_AddDecalSampler))
            {
                // 根据材质拿到decal在chunck列表中的下标
                int chunkIndex = CreateChunkIndex(material);
                // 添加decal的位置，就是在末尾添加
                int entityIndex = entityChunks[chunkIndex].count;

                // 创建一个decal
                DecalEntity entity = m_DecalEntityIndexer.CreateDecalEntity(entityIndex, chunkIndex);

                DecalEntityChunk entityChunk = entityChunks[chunkIndex];
                DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];
                DecalCulledChunk culledChunk = culledChunks[chunkIndex];
                DecalDrawCallChunk drawCallChunk = drawCallChunks[chunkIndex];

                // Make sure we have space to add new entity
                // 判断容量是否够，不够就更新列表的容量
                if (entityChunks[chunkIndex].capacity == entityChunks[chunkIndex].count)
                {
                    using (new ProfilingScope(null, m_ResizeChunks))
                    {
                        int newCapacity = entityChunks[chunkIndex].capacity + entityChunks[chunkIndex].capacity;
                        newCapacity = math.max(8, newCapacity);

                        entityChunk.SetCapacity(newCapacity);
                        cachedChunk.SetCapacity(newCapacity);
                        culledChunk.SetCapacity(newCapacity);
                        drawCallChunk.SetCapacity(newCapacity);
                    }
                }

                // 更新chunck中decal的数量
                entityChunk.Push();
                cachedChunk.Push();
                culledChunk.Push();
                drawCallChunk.Push();

                // 更新entity chunck数据
                entityChunk.decalProjectors[entityIndex] = decalProjector;
                entityChunk.decalEntities[entityIndex] = entity;
                entityChunk.transformAccessArray.Add(decalProjector.transform);

                // 更新decal entity的渲染数据
                UpdateDecalEntityData(entity, decalProjector);

                return entity;
            }
        }

        /// <summary>
        /// 根据材质查找chunck index,若没有找到则创建新的chunck加入列表
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        private int CreateChunkIndex(Material material)
        {
            if (!m_MaterialToChunkIndex.TryGetValue(material, out int chunkIndex))
            {
                var propertyBlock = new MaterialPropertyBlock();

                // In order instanced and non instanced rendering to work with _NormalToWorld
                // We need to make sure array is created with maximum size
                propertyBlock.SetMatrixArray("_NormalToWorld", new Matrix4x4[250]);

                entityChunks.Add(new DecalEntityChunk() { material = material });
                cachedChunks.Add(new DecalCachedChunk()
                {
                    propertyBlock = propertyBlock,
                });

                culledChunks.Add(new DecalCulledChunk());
                drawCallChunks.Add(new DecalDrawCallChunk() { subCallCounts = new NativeArray<int>(1, Allocator.Persistent) });

                m_CombinedChunks.Add(new CombinedChunks());
                m_CombinedChunkRemmap.Add(0);

                m_MaterialToChunkIndex.Add(material, chunkCount);
                return chunkCount++;
            }

            return chunkIndex;
        }

        /// <summary>
        /// 更新decal渲染相关的数据
        /// </summary>
        /// <param name="decalEntity"></param>
        /// <param name="decalProjector"></param>
        public void UpdateDecalEntityData(DecalEntity decalEntity, DecalProjector decalProjector)
        {
            var decalItem = m_DecalEntityIndexer.GetItem(decalEntity);

            // 拿到当前decal的信息：在chunck列表的下标；在chunck内数组的下标
            int chunkIndex = decalItem.chunkIndex;
            int arrayIndex = decalItem.arrayIndex;

            DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];

            // 更新decal的缩放和偏移
            cachedChunk.sizeOffsets[arrayIndex] = Matrix4x4.Translate(decalProjector.decalOffset) * Matrix4x4.Scale(decalProjector.decalSize);

            float drawDistance = decalProjector.drawDistance;
            float fadeScale = decalProjector.fadeScale;
            float startAngleFade = decalProjector.startAngleFade;
            float endAngleFade = decalProjector.endAngleFade;
            Vector4 uvScaleBias = decalProjector.uvScaleBias;
            int layerMask = decalProjector.gameObject.layer;
            ulong sceneLayerMask = decalProjector.gameObject.sceneCullingMask;
            float fadeFactor = decalProjector.fadeFactor;

            // 更新decal的绘制距离和衰减
            cachedChunk.drawDistances[arrayIndex] = new Vector2(drawDistance, fadeScale);
            // In the shader to remap from cosine -1 to 1 to new range 0..1  (with 0 - 0 degree and 1 - 180 degree)
            // we do 1.0 - (dot() * 0.5 + 0.5) => 0.5 * (1 - dot())
            // we actually square that to get smoother result => x = (0.5 - 0.5 * dot())^2
            // Do a remap in the shader. 1.0 - saturate((x - start) / (end - start))
            // After simplification => saturate(a + b * dot() * (dot() - 2.0))
            // a = 1.0 - (0.25 - start) / (end - start), y = - 0.25 / (end - start)

            // 更新decal的角度衰减
            if (startAngleFade == 180.0f) // angle fade is disabled
            {
                cachedChunk.angleFades[arrayIndex] = new Vector2(0.0f, 0.0f);
            }
            else
            {
                float angleStart = startAngleFade / 180.0f;
                float angleEnd = endAngleFade / 180.0f;
                var range = Mathf.Max(0.0001f, angleEnd - angleStart);
                cachedChunk.angleFades[arrayIndex] = new Vector2(1.0f - (0.25f - angleStart) / range, -0.25f / range);
            }

            // 更新decal的其它数据
            cachedChunk.uvScaleBias[arrayIndex] = uvScaleBias;
            cachedChunk.layerMasks[arrayIndex] = layerMask;
            cachedChunk.sceneLayerMasks[arrayIndex] = sceneLayerMask;
            cachedChunk.fadeFactors[arrayIndex] = fadeFactor;
            cachedChunk.scaleModes[arrayIndex] = decalProjector.scaleMode;

            cachedChunk.positions[arrayIndex] = decalProjector.transform.position;
            cachedChunk.rotation[arrayIndex] = decalProjector.transform.rotation;
            cachedChunk.scales[arrayIndex] = decalProjector.transform.lossyScale;
            cachedChunk.dirty[arrayIndex] = true;
        }

        /// <summary>
        /// 删除一个decal
        /// </summary>
        /// <param name="decalEntity"></param>
        public void DestroyDecalEntity(DecalEntity decalEntity)
        {
            if (!m_DecalEntityIndexer.IsValid(decalEntity))
                return;

            var decalItem = m_DecalEntityIndexer.GetItem(decalEntity);
            m_DecalEntityIndexer.DestroyDecalEntity(decalEntity);

            int chunkIndex = decalItem.chunkIndex;
            int arrayIndex = decalItem.arrayIndex;

            DecalEntityChunk entityChunk = entityChunks[chunkIndex];
            DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];
            DecalCulledChunk culledChunk = culledChunks[chunkIndex];
            DecalDrawCallChunk drawCallChunk = drawCallChunks[chunkIndex];

            int lastArrayIndex = entityChunk.count - 1;
            if (arrayIndex != lastArrayIndex)
                m_DecalEntityIndexer.UpdateIndex(entityChunk.decalEntities[lastArrayIndex], arrayIndex);

            entityChunk.RemoveAtSwapBack(arrayIndex);
            cachedChunk.RemoveAtSwapBack(arrayIndex);
            culledChunk.RemoveAtSwapBack(arrayIndex);
            drawCallChunk.RemoveAtSwapBack(arrayIndex);
        }

        /// <summary>
        /// 构建combined chunck列表；构建material to chunck index列表；更新decal在chunck列表中的下标;
        /// </summary>
        public void Update()
        {
            using (new ProfilingScope(null, m_SortChunks))
            {
                // 检测材质的有效性
                for (int i = 0; i < chunkCount; ++i)
                {
                    if (entityChunks[i].material == null)
                        entityChunks[i].material = errorMaterial;
                }

                // Combine chunks into single array
                // 构建combine chunck列表
                for (int i = 0; i < chunkCount; ++i)
                {
                    m_CombinedChunks[i] = new CombinedChunks()
                    {
                        entityChunk = entityChunks[i],
                        cachedChunk = cachedChunks[i],
                        culledChunk = culledChunks[i],
                        drawCallChunk = drawCallChunks[i],
                        previousChunkIndex = i,
                        valid = entityChunks[i].count != 0,
                    };
                }


                // Sort
                // 对combined chunck列表进行排序：根据有效性/绘制顺序/材质hash值;销毁无效chunck的Job线程
                m_CombinedChunks.Sort((a, b) =>
                {
                    if (a.valid && !b.valid)
                        return -1;
                    if (!a.valid && b.valid)
                        return 1;

                    if (a.cachedChunk.drawOrder < b.cachedChunk.drawOrder)
                        return -1;
                    if (a.cachedChunk.drawOrder > b.cachedChunk.drawOrder)
                        return 1;
                    return a.entityChunk.material.GetHashCode().CompareTo(b.entityChunk.material.GetHashCode());
                });

                // Early out if nothing changed
                bool dirty = false;
                for (int i = 0; i < chunkCount; ++i)
                {
                    if (m_CombinedChunks[i].previousChunkIndex != i || !m_CombinedChunks[i].valid)
                    {
                        dirty = true;
                        break;
                    }
                }
                if (!dirty)
                    return;

                // Update chunks
                int count = 0;
                m_MaterialToChunkIndex.Clear();
                for (int i = 0; i < chunkCount; ++i)
                {
                    var combinedChunk = m_CombinedChunks[i];

                    // Destroy invalid chunk
                    // 销毁无效chunck的job线程
                    if (!m_CombinedChunks[i].valid)
                    {
                        combinedChunk.entityChunk.currentJobHandle.Complete();
                        combinedChunk.cachedChunk.currentJobHandle.Complete();
                        combinedChunk.culledChunk.currentJobHandle.Complete();
                        combinedChunk.drawCallChunk.currentJobHandle.Complete();

                        combinedChunk.entityChunk.Dispose();
                        combinedChunk.cachedChunk.Dispose();
                        combinedChunk.culledChunk.Dispose();
                        combinedChunk.drawCallChunk.Dispose();

                        continue;
                    }

                    // 对于有效的chunck线程，构建material to chunck index字典
                    entityChunks[i] = combinedChunk.entityChunk;
                    cachedChunks[i] = combinedChunk.cachedChunk;
                    culledChunks[i] = combinedChunk.culledChunk;
                    drawCallChunks[i] = combinedChunk.drawCallChunk;
                    if (!m_MaterialToChunkIndex.ContainsKey(entityChunks[i].material))
                        m_MaterialToChunkIndex.Add(entityChunks[i].material, i);
                    m_CombinedChunkRemmap[combinedChunk.previousChunkIndex] = i;
                    count++;
                }

                // In case some chunks where destroyed resize the arrays
                // 移出无效chunck
                if (chunkCount > count)
                {
                    entityChunks.RemoveRange(count, chunkCount - count);
                    cachedChunks.RemoveRange(count, chunkCount - count);
                    culledChunks.RemoveRange(count, chunkCount - count);
                    drawCallChunks.RemoveRange(count, chunkCount - count);
                    m_CombinedChunks.RemoveRange(count, chunkCount - count);
                    chunkCount = count;
                }

                // Remap entities chunk index with new sorted ones
                // 更新每个decal在chunck列表中的位置
                m_DecalEntityIndexer.RemapChunkIndices(m_CombinedChunkRemmap);
            }
        }

        /// <summary>
        /// 终止和销毁所有chunck，清空数据列表
        /// </summary>
        public void Dispose()
        {
            CoreUtils.Destroy(m_ErrorMaterial);
            CoreUtils.Destroy(m_DecalProjectorMesh);

            foreach (var entityChunk in entityChunks)
                entityChunk.currentJobHandle.Complete();
            foreach (var cachedChunk in cachedChunks)
                cachedChunk.currentJobHandle.Complete();
            foreach (var culledChunk in culledChunks)
                culledChunk.currentJobHandle.Complete();
            foreach (var drawCallChunk in drawCallChunks)
                drawCallChunk.currentJobHandle.Complete();

            foreach (var entityChunk in entityChunks)
                entityChunk.Dispose();
            foreach (var cachedChunk in cachedChunks)
                cachedChunk.Dispose();
            foreach (var culledChunk in culledChunks)
                culledChunk.Dispose();
            foreach (var drawCallChunk in drawCallChunks)
                drawCallChunk.Dispose();

            m_DecalEntityIndexer.Clear();
            m_MaterialToChunkIndex.Clear();
            entityChunks.Clear();
            cachedChunks.Clear();
            culledChunks.Clear();
            drawCallChunks.Clear();
            m_CombinedChunks.Clear();
            chunkCount = 0;
        }
    }
}
