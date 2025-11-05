using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
	public class FXAARenderFeature : ScriptableRendererFeature
	{
		FXAAPass fxaaPass;
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if(fxaaPass != null)
			{
				if(fxaaPass.Setup())
				{
					renderer.EnqueuePass(fxaaPass);
				}
			}
		}

		public override void Create()
		{
			if(fxaaPass == null)
			{
				fxaaPass = new FXAAPass(RenderPassEvent.BeforeRenderingPostProcessing);
			}
		}
	}
}

