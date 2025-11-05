using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanarReflectionPass : ScriptableRenderPass
{
	public LayerMask layerMask;
	public float clipPlanarOffset;
	public float planeOffset;
	public bool drawSkeyBox;
 
	private RenderTargetIdentifier source;

	private readonly int planarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTexture");
	private  readonly int worldSpaceCameraPosId = Shader.PropertyToID("_WorldSpaceCameraPos");

	private readonly List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();
	private FilteringSettings filterSettings;

	private Transform targetPlane;
	public void SetUp()
	{
		if(targetPlane == null)
		{
			var go = GameObject.Find("Ground");
			if(go != null)
			{
				targetPlane = go.transform;
			}
		}

		shaderTagIdList.Clear();
		shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
		shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
		shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));

		filterSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
	}

	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	{
		if (targetPlane == null) return;
		var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
		var drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);

		source = renderingData.cameraData.renderer.cameraColorTarget;
		var cmd = CommandBufferPool.Get("Planar Reflection");
		ref var cameraData = ref renderingData.cameraData;
		var camera = cameraData.camera;
		var cameraTransform = camera.transform;

		DrawPlanarReflection(context, cameraData, targetPlane, cmd, drawingSettings, drawSkeyBox);

		var viewMatrix = camera.worldToCameraMatrix;
		RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix, cameraData.GetGPUProjectionMatrix(), false);
		cmd.SetGlobalVector(worldSpaceCameraPosId, new Vector4(cameraTransform.position.x, cameraTransform.position.y, cameraTransform.position.z, 0));
		CoreUtils.SetRenderTarget(cmd, source);
		cmd.SetInvertCulling(false);
		context.ExecuteCommandBuffer(cmd);
		CommandBufferPool.Release(cmd);
		camera.ResetCullingMatrix();
	}

	public void DrawPlanarReflection(ScriptableRenderContext context, CameraData cameraData, Transform targetPlane, CommandBuffer cmd, DrawingSettings drawingSettings, bool drawSkyBox)
	{
		var camera = cameraData.camera;
		var reflectionTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, RenderTextureFormat.Default);

		var cameraTransform = camera.transform;
		var plane = PlanarReflectionHelper.GetPlaneExpression(targetPlane);
		//相机反射
		var reflectionPos = PlanarReflectionHelper.GetReflectionCameraPos(targetPlane, cameraTransform);
		var reflectionRot = PlanarReflectionHelper.GetReflectionCameraRot(targetPlane, cameraTransform);
		//相机反射矩阵
		var reflectionViewMatrix = Matrix4x4.TRS(reflectionPos, reflectionRot, new Vector3(1, -1, -1)).inverse;

		//平截头体计算
		var projectionMatrix = camera.projectionMatrix;
		projectionMatrix = PlanarReflectionHelper.CalculateObliqueMatrix(plane, reflectionViewMatrix, projectionMatrix);
		projectionMatrix[8] *= -1;
		projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true);

		camera.TryGetCullingParameters(out var cullingParameters);

		cullingParameters.cullingMatrix = projectionMatrix * reflectionViewMatrix;

		var cullingResults = context.Cull(ref cullingParameters);

		cmd.SetGlobalVector(worldSpaceCameraPosId, new Vector4(reflectionPos.x, reflectionPos.y, reflectionPos.z, 0));

		RenderingUtils.SetViewAndProjectionMatrices(cmd, reflectionViewMatrix, projectionMatrix, true);

		cmd.SetGlobalTexture(planarReflectionTextureId, reflectionTexture);

		CoreUtils.SetRenderTarget(cmd, reflectionTexture, ClearFlag.All, clearColor);

		//镜像后绕序会反掉,需要反转绕序
		cmd.SetInvertCulling(true);
		context.ExecuteCommandBuffer(cmd);

		cmd.Clear();

		context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);

		if (drawSkyBox)
			context.DrawSkybox(cameraData.camera);

		RenderTexture.ReleaseTemporary(reflectionTexture);

	}

}
