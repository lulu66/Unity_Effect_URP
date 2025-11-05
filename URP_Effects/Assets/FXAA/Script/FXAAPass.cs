using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
	public class FXAAPass : ScriptableRenderPass
	{
		ProfilingSampler m_ProfilingSampler;

		private FXAAComponent m_FXAA;
		private Material m_EasyFxaaMaterial = null;
		private Material m_FsFxaaMaterial = null;
		private RenderTargetHandle m_FXAART;
		private RenderTargetIdentifier m_Source;

		private FXAAComponent.FXAAMethod method = FXAAComponent.FXAAMethod.EasyFXAA;
		// easy fxaa
		private int m_QualityIndex = -1;
		private Vector3[] QUALITY_PRESETS = null;

		private Shader easyFxaaShader;
		private Shader fsFxaaShader;

		static class ShaderConstants
		{
			public static readonly int _QualitySettings = Shader.PropertyToID("_QualitySettings");
			public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
			public static readonly int _MainTex_TexelSize = Shader.PropertyToID("_MainTex_TexelSize");
		}

		public FXAAPass(RenderPassEvent evt)
		{
			m_ProfilingSampler = new ProfilingSampler("FXAA");
			m_FXAART.Init("FXAA");
			QUALITY_PRESETS = new Vector3[5];
			QUALITY_PRESETS[(int)FXAAComponent.FXAAQuality.HighPerformance] = new Vector3(0.0f, 0.333f, 0.0833f);
			QUALITY_PRESETS[(int)FXAAComponent.FXAAQuality.Performance] = new Vector3(0.25f, 0.25f, 0.0833f);
			QUALITY_PRESETS[(int)FXAAComponent.FXAAQuality.Balance] = new Vector3(0.75f, 0.166f, 0.0833f);
			QUALITY_PRESETS[(int)FXAAComponent.FXAAQuality.Quality] = new Vector3(1.0f, 0.125f, 0.0625f);
			QUALITY_PRESETS[(int)FXAAComponent.FXAAQuality.HighQuality] = new Vector3(1.0f, 0.063f, 0.0312f);

			easyFxaaShader = Shader.Find("Hidden/Anti-Aliasing/FXAA_HLSL");
			fsFxaaShader = Shader.Find("Hidden/Anti-Aliasing/FXAA_FS");

			if(m_FsFxaaMaterial == null)
			{
				m_FsFxaaMaterial = new Material(fsFxaaShader);
			}
			if (m_EasyFxaaMaterial == null)
			{
				m_EasyFxaaMaterial = new Material(easyFxaaShader);
			}
			base.renderPassEvent = evt;

		}

		public bool Setup()
		{
			if (m_FXAA == null)
				m_FXAA = VolumeManager.instance.stack.GetComponent<FXAAComponent>();

			if (m_FXAA == null || !m_FXAA.IsActive())
				return false;

			if (m_EasyFxaaMaterial == null || m_FsFxaaMaterial == null) return false;

			method = m_FXAA.Method.value;

			return true;
		}
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			m_Source = renderingData.cameraData.renderer.cameraColorTarget;
			var cmd = CommandBufferPool.Get();
			cmd.Clear();
			using (new ProfilingScope(cmd, m_ProfilingSampler))
			{
				var desc = renderingData.cameraData.cameraTargetDescriptor;
				desc.depthBufferBits = 0;

				cmd.SetGlobalVector(ShaderConstants._MainTex_TexelSize, new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height));

				cmd.GetTemporaryRT(m_FXAART.id, desc, FilterMode.Bilinear);
				cmd.SetRenderTarget(m_FXAART.id);
				if(method == FXAAComponent.FXAAMethod.EasyFXAA)
				{
					int qualityIndex = (int)m_FXAA.Quality.value;
					if (m_QualityIndex != qualityIndex)
					{
						cmd.SetGlobalVector(ShaderConstants._QualitySettings, QUALITY_PRESETS[qualityIndex]);
						m_QualityIndex = qualityIndex;
					}
					cmd.Blit(m_Source, m_FXAART.id, m_EasyFxaaMaterial, 0);
				}
				else
				{
					cmd.Blit(m_Source, m_FXAART.id, m_FsFxaaMaterial, 0);
				}


				cmd.Blit(m_FXAART.id, m_Source);
				cmd.SetRenderTarget(m_Source);
				cmd.ReleaseTemporaryRT(m_FXAART.id);
			}
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		public void Cleanup()
		{
			if(m_EasyFxaaMaterial != null)
			{
				UnityEngine.GameObject.Destroy(m_EasyFxaaMaterial);
			}
			if (m_FsFxaaMaterial != null)
			{
				UnityEngine.GameObject.Destroy(m_FsFxaaMaterial);
			}
		}
	}
}

