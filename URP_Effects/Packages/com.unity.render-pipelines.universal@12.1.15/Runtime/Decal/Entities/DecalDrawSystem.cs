using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Abstract class that render decals using <see cref="DecalDrawCallChunk"/>.
    /// Supports rendering with <see cref="CommandBuffer"/> and graphics draw calls.
    /// </summary>
    internal abstract class DecalDrawSystem
    {
        protected DecalEntityManager m_EntityManager;
        private Matrix4x4[] m_WorldToDecals;
        private Matrix4x4[] m_NormalToDecals;
        private ProfilingSampler m_Sampler;

        public Material overrideMaterial { get; set; }

        public DecalDrawSystem(string sampler, DecalEntityManager entityManager)
        {
            m_EntityManager = entityManager;

            m_WorldToDecals = new Matrix4x4[250];
            m_NormalToDecals = new Matrix4x4[250];

            m_Sampler = new ProfilingSampler(sampler);
        }

        public void Execute(CommandBuffer cmd)
        {
            using (new ProfilingScope(cmd, m_Sampler))
            {
                for (int i = 0; i < m_EntityManager.chunkCount; ++i)
                {
                    Execute(
                        cmd,
                        m_EntityManager.entityChunks[i],
                        m_EntityManager.cachedChunks[i],
                        m_EntityManager.drawCallChunks[i],
                        m_EntityManager.entityChunks[i].count);
                }
            }
        }

        protected virtual Material GetMaterial(DecalEntityChunk decalEntityChunk) => decalEntityChunk.material;

        protected abstract int GetPassIndex(DecalCachedChunk decalCachedChunk);

        private void Execute(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int count)
        {
            // 保证decal转换和包围盒的计算完毕
            decalCachedChunk.currentJobHandle.Complete();
            // 保证decal渲染数据准备完毕
            decalDrawCallChunk.currentJobHandle.Complete();

            // 渲染decal的材质
            Material material = GetMaterial(decalEntityChunk);

            // 渲染decal的shader pass id
            int passIndex = GetPassIndex(decalCachedChunk);

            if (count == 0 || passIndex == -1 || material == null)
                return;

            if (SystemInfo.supportsInstancing && material.enableInstancing)
            {
                DrawInstanced(cmd, decalEntityChunk, decalCachedChunk, decalDrawCallChunk, passIndex);
            }
            else
            {
                Draw(cmd, decalEntityChunk, decalCachedChunk, decalDrawCallChunk, passIndex);
            }
        }
        /// <summary>
        /// 逐个绘制decal
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="decalEntityChunk"></param>
        /// <param name="decalCachedChunk"></param>
        /// <param name="decalDrawCallChunk"></param>
        /// <param name="passIndex"></param>
        private void Draw(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int passIndex)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                for (int j = subCall.start; j < subCall.end; ++j)
                {
                    decalCachedChunk.propertyBlock.SetMatrix("_NormalToWorld", decalDrawCallChunk.normalToDecals[j]);
                    cmd.DrawMesh(mesh, decalDrawCallChunk.decalToWorlds[j], material, 0, passIndex, decalCachedChunk.propertyBlock);
                }
            }
        }

        /// <summary>
        /// 以实例方式绘制decal
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="decalEntityChunk"></param>
        /// <param name="decalCachedChunk"></param>
        /// <param name="decalDrawCallChunk"></param>
        /// <param name="passIndex"></param>
        private void DrawInstanced(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int passIndex)
        {
            // 获取mesh
            var mesh = m_EntityManager.decalProjectorMesh;

            // 获取材质
            var material = GetMaterial(decalEntityChunk);

            // ?
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            // 绘制批次数
            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                // Reinterpret : 用于重新解释 NativeArray 的内存布局，允许以不同的数据类型访问相同的内存块

                // 拷贝decal变换矩阵
                var decalToWorldSlice = decalDrawCallChunk.decalToWorlds.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(decalToWorldSlice, subCall.start, m_WorldToDecals, 0, subCall.count);

                // 拷贝decal normal变换矩阵
                var normalToWorldSlice = decalDrawCallChunk.normalToDecals.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(normalToWorldSlice, subCall.start, m_NormalToDecals, 0, subCall.count);

                decalCachedChunk.propertyBlock.SetMatrixArray("_NormalToWorld", m_NormalToDecals);
                cmd.DrawMeshInstanced(mesh, 0, material, passIndex, m_WorldToDecals, subCall.end - subCall.start, decalCachedChunk.propertyBlock);
            }
        }

        public void Execute(in CameraData cameraData)
        {
            using (new ProfilingScope(null, m_Sampler))
            {
                for (int i = 0; i < m_EntityManager.chunkCount; ++i)
                {
                    Execute(
                        cameraData,
                        m_EntityManager.entityChunks[i],
                        m_EntityManager.cachedChunks[i],
                        m_EntityManager.drawCallChunks[i],
                        m_EntityManager.entityChunks[i].count);
                }
            }
        }

        private void Execute(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int count)
        {
            // 保证decal转换和包围盒的计算完毕
            decalCachedChunk.currentJobHandle.Complete();
            // 保证decal渲染数据准备完毕
            decalDrawCallChunk.currentJobHandle.Complete();

            Material material = GetMaterial(decalEntityChunk);
            int passIndex = GetPassIndex(decalCachedChunk);

            if (count == 0 || passIndex == -1 || material == null)
                return;

            if (SystemInfo.supportsInstancing && material.enableInstancing)
            {
                DrawInstanced(cameraData, decalEntityChunk, decalCachedChunk, decalDrawCallChunk);
            }
            else
            {
                Draw(cameraData, decalEntityChunk, decalCachedChunk, decalDrawCallChunk);
            }
        }

        private void Draw(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                for (int j = subCall.start; j < subCall.end; ++j)
                {
                    decalCachedChunk.propertyBlock.SetMatrix("_NormalToWorld", decalDrawCallChunk.normalToDecals[j]);
                    Graphics.DrawMesh(mesh, decalCachedChunk.decalToWorlds[j], material, decalCachedChunk.layerMasks[j], cameraData.camera, 0, decalCachedChunk.propertyBlock);
                }
            }
        }

        private void DrawInstanced(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                var decalToWorldSlice = decalDrawCallChunk.decalToWorlds.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(decalToWorldSlice, subCall.start, m_WorldToDecals, 0, subCall.count);

                var normalToWorldSlice = decalDrawCallChunk.normalToDecals.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(normalToWorldSlice, subCall.start, m_NormalToDecals, 0, subCall.count);

                decalCachedChunk.propertyBlock.SetMatrixArray("_NormalToWorld", m_NormalToDecals);
                Graphics.DrawMeshInstanced(mesh, 0, material,
                    m_WorldToDecals, subCall.count, decalCachedChunk.propertyBlock, ShadowCastingMode.On, true, 0, cameraData.camera);
            }
        }
    }
}
