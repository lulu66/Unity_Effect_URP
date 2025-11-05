using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 外力与流体的交互
// 需要考虑外物的旋转，缩放，线速度，角速度
public class FluidTarget : MonoBehaviour
{
	public Material SplatMat;
	public Vector2 Scale = Vector2.one;
	//public bool ScaleWithDistance;
	public float MaxRelativeVelocity = 10f;
	// 速度大小的缩放
	public Vector3 VelocityScale = Vector3.one;
	// 速度权重
	[Range(0f, 1f)]
	public float VelocityWeight = 1f;
	// 速度图
	public Texture2D velocityTexture;

	public float MaxRelativeAngularVelocity = 12;

	public float AngularVelocityScale = 1;

	//外力
	public Vector3 Force;

	// 外部的旋转力
	public float Torque;

	// 密度权重
	[Range(0f, 1f)]
	public float DensityWeight = 1f;

	// 密度图
	public Texture2D DensityTexture;


	// splat权重
	public Color SplatWeight;

	// 噪声图
	public Texture2D NoiseTexture;

	// 速度噪声
	[Range(0f, 1f)]
	public float VelocityNoise = 0;
	public float VelocityNoiseOffset = 0;
	public float VelocityNoiseTilling = 1;
	// 密度噪声
	[Range(0f,1f)]
	public float DensityNoise = 0;
	public float DensityNoiseOffset = 0;
	public float DensityNoiseTilling = 1;

	// 外力施加的方式,通过混合模式体现
	public BlendMode SrcBlend = BlendMode.SrcAlpha;
	public BlendMode DstBlend = BlendMode.OneMinusSrcAlpha;
	public BlendOp BlendOperation = BlendOp.Add;

	private Quaternion oldRotation;
	private Vector3 oldPosition;

	private Vector4 velocityNoiseParam;
	private Vector4 densityNoiseParam;
	private Vector4 splatWeight;
	// 外物的线速度
	public Vector3 velocity
	{
		get
		{
			return (transform.position - oldPosition) / Time.deltaTime;
		}
	}

	// 外物的角速度
	public Vector3 angularVelocity
	{
		get
		{
			Quaternion rotationDelta = transform.rotation * Quaternion.Inverse(oldRotation);
			return new Vector3(rotationDelta.x, rotationDelta.y, rotationDelta.z) * 2.0f / Time.deltaTime;
		}
	}

	// 将外物对流体的影响更新到流体中
	// 1.外物的位置和大小，使用参数position和scale

	public void Splat(FluidContainer container, RenderTexture velocityA, RenderTexture stateA, Vector4 rect)
	{
		if (SplatMat == null) return;

		// uv空间的相对速度（外物相对于流体容器，此处假设流体容器静止不动）
		Vector3 relativeVelocity = container.WorldVectorToUVSpace(velocity, rect);
		//对速度进行限制
		float speed = relativeVelocity.magnitude;
		if (speed > 0.001f)
		{
			relativeVelocity /= speed;
			relativeVelocity *= Mathf.Min(speed, MaxRelativeVelocity);
		}

		// 外物的线速度
		Vector4 vel = Vector3.Scale(relativeVelocity, VelocityScale) + Force;
		vel.w = VelocityWeight;
		if (float.IsNaN(vel.x)) vel.x = 0;
		if (float.IsNaN(vel.y)) vel.y = 0;
		if (float.IsNaN(vel.z)) vel.z = 0;

		// 外物的角速度
		float relativeAngularVel = container.WorldVectorToUVSpace(angularVelocity, rect).z;
		relativeAngularVel = Mathf.Clamp(relativeAngularVel, -MaxRelativeAngularVelocity, MaxRelativeAngularVelocity) * AngularVelocityScale;
		relativeAngularVel += Torque;
		if (float.IsNaN(relativeAngularVel)) relativeAngularVel = 0;

		// 参数传递,外力对流体的影响方式
		SplatMat.SetInt("_SrcBlend", (int)SrcBlend);
		SplatMat.SetInt("_DstBlend", (int)DstBlend);
		SplatMat.SetInt("_BlendOp", (int)BlendOperation);
		SplatMat.SetTexture("_Noise", NoiseTexture);

		velocityNoiseParam = new Vector4(VelocityNoise, VelocityNoiseOffset, VelocityNoiseTilling, 0);
		densityNoiseParam = new Vector4(DensityNoise, DensityNoiseOffset, DensityNoiseTilling, 0);
		splatWeight = new Vector4(SplatWeight.r, SplatWeight.g, SplatWeight.b, SplatWeight.a * DensityWeight);

		// 默认：就splat一次

		// 计算投影到流体的position和size
		var lossyScale = transform.lossyScale;
		float maxScale = Mathf.Max(lossyScale.x, Mathf.Max(lossyScale.y, lossyScale.z));
		Vector2 projectSize = Scale * maxScale;
		Vector3 targetPos = transform.position;
		var projection = container.ProjectTarget(targetPos, projectSize);
		SplatMat.SetVector("_SplatTransform", projection);  // 传递投影位置和大小
		//Debug.LogError($"splat position and size : {projection}");
		// 计算外物的旋转，固定旋转，暂不考虑
		//SplatMat.SetFloat("_SplatRotation",0);

		//状态相关量
		SplatMat.SetVector("_SplatWeight", splatWeight);        // splat的权重
		SplatMat.SetVector("_DensityNoiseParams", densityNoiseParam); // 密度图的tillingOffset
		Graphics.Blit(DensityTexture, stateA, SplatMat, 0); //更新密度

		//// 速度相关量
		SplatMat.SetVector("_VelocityNoiseParams", velocityNoiseParam);
		SplatMat.SetFloat("_AngularVelocity", relativeAngularVel);
		//Debug.LogError($"angular velocity :{relativeAngularVel}");
		SplatMat.SetVector("_LinearVelicoty", vel);
		SplatMat.SetTexture("_Velocity", velocityTexture);
		Graphics.Blit(DensityTexture, velocityA, SplatMat, 1); //更新速度

		// 更新物体的位置和旋转
		{
			oldRotation = transform.rotation;
			oldPosition = transform.position;

		}
	}

	private void Start()
	{
		// 做一些初始化
	}

	private void OnEnable()
	{
		oldPosition = transform.position;
		oldRotation = transform.rotation;

	}
	//private void LateUpdate()
	//{
	//	oldRotation = transform.rotation;
	//	oldPosition = transform.position;
	//}
}
