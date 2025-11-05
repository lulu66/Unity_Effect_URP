using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanarReflectionFeature : ScriptableRendererFeature
{

	[System.Serializable]
	public class PlanarReflectionSettings
	{
		public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
		public LayerMask reflectionLayerMask = -1;
		//public float clipPlaneOffset = 0;
		//public float planeOffset = 0;
		public bool drawSkeyBox = false;
	}

	public PlanarReflectionSettings Settings = new PlanarReflectionSettings();
	PlanarReflectionPass planarReflectionPass;

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		planarReflectionPass.SetUp();
		renderer.EnqueuePass(planarReflectionPass);
	}

	public override void Create()
	{
		planarReflectionPass = new PlanarReflectionPass();
		//planarReflectionPass.renderPassEvent = Settings.renderPassEvent;
		planarReflectionPass.layerMask = Settings.reflectionLayerMask;
		planarReflectionPass.clipPlanarOffset = 0;//Settings.clipPlaneOffset;
		planarReflectionPass.planeOffset = 0;//Settings.planeOffset;
		planarReflectionPass.drawSkeyBox = Settings.drawSkeyBox;
	}
}
