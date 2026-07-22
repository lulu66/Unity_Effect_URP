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
		[Header("������������")]
		public Vector4 Dissipation = Vector4.zero;   // ��ɢ(���嶯����ճ�����ò������ת��Ϊ���ܵĹ���)

		//[Range(0f, 1f)]
		//public float Adhesion;          // ���������������巽�̵ı�׼��������������߽�֮����Ӽ��������������ҪӰ��߽������ļ��㣩

		[Range(0f, 1f)]
		public float Pressure = 0.2f;          // ѹ��

		//[Range(0f, 1f)]
		//public float Viscosity;         // ճ��:�����������ǲ���Ҫ�ģ���Ȼ���Ӿ��ʸ����űȽϴ������

		//public Vector4 Boundary = new Vector4(0, 0, 1, 1);        // �߽�


		public Vector2 EdgeFallOff = Vector2.zero;       // �߽�˥��

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

		protected override void Dispose(bool disposing)
		{
			if (disposing && fluid2dPass != null)
			{
				fluid2dPass.Release();
				fluid2dPass = null;
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

			// 1. ����4��rt�����ڸ�������״̬
			CreateFluidRT();
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (Fluid2DManager.Instance == null) return;

			var cmd = CommandBufferPool.Get("Fluid 2D Simulation");
			cmd.Clear();

			// A. ��������Ч��ƽ���rt
			Fluid2DManager.Instance.UpdateFluidPlaneMaterial(velocityA, stateA);

			// B. �������彻��
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
				// ����״̬
				cmd.Blit(densityTexture, stateA, splatMats[i], 0);

				splatMats[i].SetVector("_LinearVelicoty", linearVels[i]);
				splatMats[i].SetFloat("_AngularVelocity", angularVels[i]);
				// �����ٶ�
				cmd.Blit(densityTexture, velocityA, splatMats[i], 1);
			}

			// ��������ģ��
			var simulationMaterial = Fluid2DManager.Instance.FluidSimulationMaterial;
			simulationMaterial.SetFloat("_Pressure", settings.Pressure);
			simulationMaterial.SetVector("_WrapMode", settings.WarpMode);
			simulationMaterial.SetVector("_Dissipation", settings.Dissipation);
			simulationMaterial.SetVector("_EdgeFalloff", settings.EdgeFallOff);

			// C. ����ƽ��״̬
			velocityA.filterMode = FilterMode.Point;
			stateA.filterMode = FilterMode.Point;
			simulationMaterial.SetFloat("_DeltaTime", Time.deltaTime);
			simulationMaterial.SetTexture("_Velocity", velocityA);
			cmd.Blit(stateA, stateB, simulationMaterial, 0);

			// D. ����ƽ���ٶ�
			cmd.Blit(velocityA, velocityB, simulationMaterial, 1);

			// E. ���������ɢ�����������˥����
			cmd.Blit(stateB, stateA, simulationMaterial, 2);

			// F.���������ٶȵ�ɢ��
			cmd.Blit(velocityB, velocityA, simulationMaterial, 5);

			// G. ��������ѹ��
			cmd.Blit(velocityA, velocityB, simulationMaterial, 6);
			cmd.Blit(velocityB, velocityA, simulationMaterial, 6);
			cmd.Blit(velocityA, velocityB);

			// H. ����������ɢ�ٶ�
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
