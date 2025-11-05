using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UnityEngine.Rendering.Universal.Internal
{
    //NOTE: This class is meant to be removed when RTHandles get implemented in urp
    internal sealed class RenderTargetBufferSystem
    {
        struct SwapBuffer
        {
            public RenderTargetHandle rt;
            public int name;
            public int msaa;
        }
        SwapBuffer m_A, m_B;
        static bool m_AisBackBuffer = true;

        static RenderTextureDescriptor m_Desc;
        FilterMode m_FilterMode;
        bool m_AllowMSAA = true;
        bool m_RTisAllocated = false;

        SwapBuffer backBuffer { get { return m_AisBackBuffer ? m_A : m_B; } }
        SwapBuffer frontBuffer { get { return m_AisBackBuffer ? m_B : m_A; } }

        /// <summary>
        /// 初始化bufferA和bufferB的标记
        /// </summary>
        /// <param name="name"></param>
        public RenderTargetBufferSystem(string name)
        {
            m_A.name = Shader.PropertyToID(name + "A");
            m_B.name = Shader.PropertyToID(name + "B");
            m_A.rt.Init(name + "A");
            m_B.rt.Init(name + "B");
        }

        /// <summary>
        /// 返回back buffer
        /// </summary>
        /// <returns></returns>
        public RenderTargetHandle GetBackBuffer()
        {
            return backBuffer.rt;
        }

        /// <summary>
        /// 返回back buffer
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public RenderTargetHandle GetBackBuffer(CommandBuffer cmd)
        {
            // 如果没分配buffer,先分配buffer
            if (!m_RTisAllocated)
                Initialize(cmd);

            // 返回back buffer
            return backBuffer.rt;
        }

        /// <summary>
        /// 释放现有front buffer后根据msaa的设置重新创建front buffer,并返回;如果front buffer是bufferB,则不让bufferB拥有深度，只让bufferA拥有深度
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public RenderTargetHandle GetFrontBuffer(CommandBuffer cmd)
        {
            // 如果没有分配buffer，先分配buffer
            if (!m_RTisAllocated)
                Initialize(cmd);

            // 管线的MSAA采样数
            int pipelineMSAA = m_Desc.msaaSamples;
            // buffer的MSAA采样数
            int bufferMSAA = frontBuffer.msaa;

            // 如果允许开启MSAA且buffer和管线的MSAA采样数不一样
            if (m_AllowMSAA && bufferMSAA != pipelineMSAA)
            {
                //We don't want a depth buffer on B buffer
                // 修改创建buffer的描述：如果bufferA当前作为back buffer,那么创建的front buffer不要深度
                var desc = m_Desc;
                if (m_AisBackBuffer)
                    desc.depthBufferBits = 0;

                // 重新创建front buffer
                cmd.ReleaseTemporaryRT(frontBuffer.name);
                cmd.GetTemporaryRT(frontBuffer.name, desc, m_FilterMode);

                // 如果bufferA当前是back buffer,那bufferB的msaa数和描述的msaa采样数一致，否则bufferA和描述的msaa采样数一致
                if (m_AisBackBuffer)
                    m_B.msaa = desc.msaaSamples;
                else m_A.msaa = desc.msaaSamples;
            }
            else if (!m_AllowMSAA && bufferMSAA > 1) // 如果不允许开启MSAA且buffer的MSAA采样数>1
            {
                //We don't want a depth buffer on B buffer
                // 修改创建buffer的描述：1)禁用msaa;2)如果bufferA当前作为back buffer,那么创建的front buffer不要深度
                var desc = m_Desc;
                desc.msaaSamples = 1;
                if (m_AisBackBuffer)
                    desc.depthBufferBits = 0;

                // 重新创建front buffer
                cmd.ReleaseTemporaryRT(frontBuffer.name);
                cmd.GetTemporaryRT(frontBuffer.name, desc, m_FilterMode);

                // 如果bufferA当前是back buffer,那bufferB禁用msaa，否则bufferA禁用msaa
                if (m_AisBackBuffer)
                    m_B.msaa = desc.msaaSamples;
                else m_A.msaa = desc.msaaSamples;
            }

            return frontBuffer.rt;
        }
        /// <summary>
        /// 交换front和back buffer的标记
        /// </summary>
        public void Swap()
        {
            m_AisBackBuffer = !m_AisBackBuffer;
        }

        /// <summary>
        /// 分配bufferA和bufferB
        /// 将buffer标记为已分配
        /// </summary>
        /// <param name="cmd"></param>
        void Initialize(CommandBuffer cmd)
        {
            m_A.msaa = m_Desc.msaaSamples;
            m_B.msaa = m_Desc.msaaSamples;

            cmd.GetTemporaryRT(m_A.name, m_Desc, m_FilterMode);
            var descB = m_Desc;
            //descB.depthBufferBits = 0;
            cmd.GetTemporaryRT(m_B.name, descB, m_FilterMode);

            m_RTisAllocated = true;
        }

        /// <summary>
        /// 释放bufferA & bufferB;
        /// 让bufferA重置为back buffer;
        /// 默认允许MSAA；
        /// </summary>
        /// <param name="cmd"></param>
        public void Clear(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(m_A.name);
            cmd.ReleaseTemporaryRT(m_B.name);

            m_AisBackBuffer = true;
            m_AllowMSAA = true;
        }

        /// <summary>
        /// 通过参数传递设置当前的swap buffer的格式和过滤模式,并创建用于交换的buffer
        /// </summary>
        public void SetCameraSettings(CommandBuffer cmd, RenderTextureDescriptor desc, FilterMode filterMode)
        {
            Clear(cmd); //SetCameraSettings is called when new stack starts rendering. Make sure the targets are updated to use the new descriptor.

            m_Desc = desc;
            m_FilterMode = filterMode;

            Initialize(cmd);
        }
        /// <summary>
        /// 通过参数传递设置当前的swap buffer的格式和过滤模式,但不创建buffer
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="filterMode"></param>
        public void SetCameraSettings(RenderTextureDescriptor desc, FilterMode filterMode)
        {
            m_Desc = desc;
            m_FilterMode = filterMode;
        }

        /// <summary>
        /// 返回bufferA
        /// </summary>
        /// <returns></returns>
        public RenderTargetHandle GetBufferA()
        {
            return m_A.rt;
        }

        /// <summary>
        /// 是否启用MSAA
        /// </summary>
        /// <param name="enable"></param>
        public void EnableMSAA(bool enable)
        {
            m_AllowMSAA = enable;
        }
    }
}
