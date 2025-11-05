using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fluid2D
{
	[System.Serializable]
	public class Settings
	{
		public int Resolution = 256;
		[Header("流体物理参数")]
		public Vector4 Dissipation = Vector4.zero;   // 耗散(流体动能因粘性作用不可逆地转换为内能的过程)

		//[Range(0f, 1f)]
		//public float Adhesion;          // 附着力（不是流体方程的标准力项，是流体与固体边界之间分子级别的吸引力，主要影响边界条件的计算）

		[Range(0f, 1f)]
		public float Pressure = 0.2f;          // 压力

		//[Range(0f, 1f)]
		//public float Viscosity;         // 粘度:对理想流体是不必要的，虽然对视觉质感起着比较大的作用

		//public Vector4 Boundary = new Vector4(0, 0, 1, 1);        // 边界


		public Vector2 EdgeFallOff = Vector2.zero;       // 边界衰减

		public Vector4 WarpMode = new Vector4(0,0,1,1);

	}
	public class Fluid2DRenderFeature : ScriptableRendererFeature
	{

		public Settings settings;
		private Fluid2DPass fluid2dPass;
		public Fluid2DPass Fluid2dPass
		{
			get
			{
				return fluid2dPass;
			}
		}
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if(fluid2dPass != null)
			{
				fluid2dPass.Setup(settings);
				renderer.EnqueuePass(fluid2dPass);
			}
		}

		public override void Create()
		{
			if(fluid2dPass == null)
			{
				fluid2dPass = new Fluid2DPass();
				fluid2dPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
			}
		}
	}

	public class Fluid2DPass : ScriptableRenderPass
	{
		private Settings settings;

		private RenderTexture velocityA;
		private RenderTexture velocityB;
		private RenderTexture stateA;
		private RenderTexture stateB;

		private RenderTargetIdentifier source;

		private List<Vector4> positionAndSizes = new List<Vector4>();
		private List<Vector4> linearVels = new List<Vector4>();
		private List<float> angularVels = new List<float>();

		public void Setup(Settings settings)
		{
			this.settings = settings;

			// 1. 创建4个rt，用于更新流体状态
			CreateFluidRT();
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (Fluid2DManager.Instance == null) return;

			var cmd = CommandBufferPool.Get("Fluid 2D Simulation");
			cmd.Clear();

			// A. 更新流体效果平面的rt
			Fluid2DManager.Instance.UpdateFluidPlaneMaterial(velocityA, stateA);

			// B. 更新流体交互
			positionAndSizes = Fluid2DManager.Instance.Target.SplatPositionSizes;
			linearVels = Fluid2DManager.Instance.Target.SplatLinearVelocities;
			angularVels = Fluid2DManager.Instance.Target.SplatAnularVelocities;
			var splatMats = Fluid2DManager.Instance.Target.SplatInstanceMats;
			var densityTexture = Fluid2DManager.Instance.Target.DensityTexture;
			//Debug.LogError($"position and size 1: {positionAndSizes[0]}");
			//Debug.LogError($"position and size 2: {positionAndSizes[1]}");

			for (int i=0; i<positionAndSizes.Count; i++)
			{
				splatMats[i].SetVector("_SplatTransform", positionAndSizes[i]);
				// 更新状态
				cmd.Blit(densityTexture, stateA, splatMats[i], 0);

				splatMats[i].SetVector("_LinearVelicoty", linearVels[i]);
				splatMats[i].SetFloat("_AngularVelocity", angularVels[i]);
				// 更新速度
				cmd.Blit(densityTexture, velocityA, splatMats[i], 1);
			}

			// 更新流体模拟
			var simulationMaterial = Fluid2DManager.Instance.FluidSimulationMaterial;
			simulationMaterial.SetFloat("_Pressure", settings.Pressure);
			simulationMaterial.SetVector("_WrapMode", settings.WarpMode);
			simulationMaterial.SetVector("_Dissipation", settings.Dissipation);
			simulationMaterial.SetVector("_EdgeFalloff", settings.EdgeFallOff);

			// C. 更新平流状态
			velocityA.filterMode = FilterMode.Point;
			stateA.filterMode = FilterMode.Point;
			simulationMaterial.SetFloat("_DeltaTime", Time.deltaTime);
			simulationMaterial.SetTexture("_Velocity", velocityA);
			cmd.Blit(stateA, stateB, simulationMaterial, 0);

			// D. 更新平流速度
			cmd.Blit(velocityA, velocityB, simulationMaterial, 1);

			// E. 更新流体耗散（考虑流体的衰减）
			cmd.Blit(stateB, stateA, simulationMaterial, 2);

			// F.更新流体速度的散度
			cmd.Blit(velocityB, velocityA, simulationMaterial, 5);

			// G. 更新流体压力
			cmd.Blit(velocityA, velocityB, simulationMaterial, 6);
			cmd.Blit(velocityB, velocityA, simulationMaterial, 6);
			cmd.Blit(velocityA, velocityB);

			// H. 计算流体无散速度
			cmd.Blit(velocityB, velocityA, simulationMaterial, 7);

			velocityA.filterMode = FilterMode.Bilinear;
			stateA.filterMode = FilterMode.Bilinear;

			source = renderingData.cameraData.renderer.cameraColorTarget;
			CoreUtils.SetRenderTarget(cmd, source);
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		public void Release()
		{
			ReleaseFluidRT();
			positionAndSizes.Clear();
			linearVels.Clear();
			angularVels.Clear();
	}
		private void CreateFluidRT()
		{
			if(velocityA == null)
			{
				velocityA = new RenderTexture(settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				velocityA.name = "Velocity A";
				velocityA.filterMode = FilterMode.Point;
			}
			if (velocityB == null)
			{
				velocityB = new RenderTexture(settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				velocityB.name = "Velocity B";
				velocityB.filterMode = FilterMode.Point;

			}
			if (stateA == null)
			{
				stateA = new RenderTexture(settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				stateA.name = "State A";
				stateA.filterMode = FilterMode.Point;

			}
			if (stateB == null)
			{
				stateB = new RenderTexture(settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
				stateB.name = "State B";
				stateB.filterMode = FilterMode.Point;

			}

		}

		private void ReleaseFluidRT()
		{
			if (velocityA != null)
			{
#if UNITY_EDITOR
				UnityEngine.GameObject.DestroyImmediate(velocityA);
#else
			UnityEngine.GameObject.Destroy(velocityA);
#endif
				velocityA = null;
			}
			if (velocityB != null)
			{
#if UNITY_EDITOR
				UnityEngine.Object.DestroyImmediate(velocityB);
#else
			UnityEngine.GameObject.Destroy(velocityB);
#endif
				velocityB = null;
			}
			if (stateA != null)
			{
#if UNITY_EDITOR
				UnityEngine.Object.DestroyImmediate(stateA);
#else
			UnityEngine.GameObject.Destroy(stateA);
#endif
				stateA = null;
			}
			if (stateB != null)
			{
#if UNITY_EDITOR
				UnityEngine.Object.DestroyImmediate(stateB);
#else
			UnityEngine.GameObject.Destroy(stateB);
#endif
				stateB = null;
			}
		}

	}


}
