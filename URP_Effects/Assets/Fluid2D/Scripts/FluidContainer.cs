using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FluidContainer : MonoBehaviour
{
	public Vector3 size;
	public FluidTarget[] Targets;

	// 创建plane网格
	public Vector2Int subDivision = Vector2Int.one;

	// 流体的各种物理参数
	public Vector4 Dissipation;		// 耗散(流体动能因粘性作用不可逆地转换为内能的过程)
	//public float Turbulence;		// 湍流
	public float Adhesion;          // 附着力（不是流体方程的标准力项，是流体与固体边界之间分子级别的吸引力，主要影响边界条件的计算）
	public float Pressure;          // 压力
	public float Viscosity;         // 粘度:对理想流体是不必要的，虽然对视觉质感起着比较大的作用
	public Vector4 Boundary;        // 边界
	public Vector4 EdgeFallOff;		// 边界衰减

	private MeshRenderer fluidMr;
	private MeshFilter fluidFilter;
	private MaterialPropertyBlock propertyBlock;

	private Mesh proceduralMesh;
	protected Vector3[] vertices;
	protected Vector3[] normals;
	protected Vector4[] tangents;
	protected Vector2[] uvs;
	protected int[] triangles;

	private Vector4 projectionTarget = Vector4.zero;

	private readonly int sizeId = Shader.PropertyToID("_ContainerSize");
	private readonly int mainTexId = Shader.PropertyToID("_MainTex");
	private readonly int velocityId = Shader.PropertyToID("_Velocity");

	private void OnEnable()
	{
		if(fluidMr == null)
		{
			fluidMr = GetComponent<MeshRenderer>();
		}
		fluidFilter = GetComponent<MeshFilter>();
		propertyBlock = new MaterialPropertyBlock();
		BluidPlaneMesh();
	}

	public Vector3 WorldVectorToUVSpace(Vector3 vector, Vector4 uvRect)
	{
		if(size.x <= 0.001 || size.y <= 0.001)
		{
			return Vector3.zero;
		}
		var v = transform.InverseTransformVector(vector);
		v.x *= uvRect.z / size.x;
		v.y *= uvRect.w / size.y;
		return v;
	}
	public void UpdateContainerSize()
	{
		Shader.SetGlobalVector(sizeId, size);
	}

	public void UpdateMaterial(RenderTexture velocityRT, RenderTexture stateRT)
	{
		if (fluidMr == null || fluidMr.sharedMaterial == null) return;
		fluidMr.GetPropertyBlock(propertyBlock);
		propertyBlock.SetTexture(mainTexId, stateRT);
		propertyBlock.SetTexture(velocityId, velocityRT);
		fluidMr.SetPropertyBlock(propertyBlock);
	}

	// 投影目标位置到流体平面局部坐标系中
	public Vector4 ProjectTarget(Vector3 targetPos, Vector2 projectSize)
	{
		// 投影外物的位置到流体平面上：原则上要将外物的位置通过射线检测投影的流体平面上，但是这里做一个简化，因为平面是水平的，那么外物在流体平面的投影其实可以简单的将y=plane.y;
		var targetPoint = targetPos;
		targetPoint.y = transform.position.y;

		// 将外物位置转换到流体平面的局部坐标系当中
		var localTargetPos = transform.InverseTransformPoint(targetPoint)/(Vector2)size;

		// 这里原本是创建另外一条从流体平面底下到目标位置右侧的一条射线，然后通过与平面交点，求得一个相交位置，并转换到流体平面局部坐标系中，和目标位置的投影位置计算一个距离来作为一个合适的投影大小的缩放值
		// 这里简化掉，直接通过参数调节scale即可

		projectionTarget.x = localTargetPos.x;
		projectionTarget.y = localTargetPos.y;
		projectionTarget.z = projectSize.x;
		projectionTarget.w = projectSize.y;

		return projectionTarget;

	}

	// 创建一个plane 作为流体的容器
	private void BluidPlaneMesh()
	{
		if(proceduralMesh == null)
		{
			proceduralMesh = new Mesh();
			proceduralMesh.name = "FluidPlane";
			if (fluidFilter != null)
			{
				fluidFilter.sharedMesh = proceduralMesh;
			}
		}

		proceduralMesh.Clear();

		subDivision.x = Mathf.Max(1, subDivision.x);
		subDivision.y = Mathf.Max(1, subDivision.y);
		Vector2 quadSize = new Vector2(1.0f / subDivision.x, 1.0f / subDivision.y);
		int vertexCount = (subDivision.x + 1) * (subDivision.y + 1);
		int triangleCount = subDivision.x * subDivision.y * 2;
		if (vertexCount > 65535)
			proceduralMesh.indexFormat = IndexFormat.UInt32;
		else
			proceduralMesh.indexFormat = IndexFormat.UInt16;

		vertices = new Vector3[vertexCount];
		normals = new Vector3[vertexCount];
		tangents = new Vector4[vertexCount];
		uvs = new Vector2[vertexCount];
		triangles = new int[triangleCount * 3];

		for (int y = 0; y < subDivision.y + 1; ++y)
		{
			for (int x = 0; x < subDivision.x + 1; ++x)
			{
				int v = y * (subDivision.x + 1) + x;
				vertices[v] = new Vector3((quadSize.x * x - 0.5f) * size.x, (quadSize.y * y - 0.5f) * size.y, 0);
				normals[v] = -Vector3.forward;
				tangents[v] = new Vector4(1, 0, 0, -1);
				uvs[v] = new Vector3(x / (float)subDivision.x, y / (float)subDivision.y);
			}
		}

		for (int y = 0; y < subDivision.y; ++y)
		{
			for (int x = 0; x < subDivision.x; ++x)
			{

				int face = (y * (subDivision.x + 1) + x);
				int t = (y * subDivision.x + x) * 6;

				triangles[t] = face + subDivision.x + 1;
				triangles[t + 1] = face + 1;
				triangles[t + 2] = face;

				triangles[t + 3] = face + subDivision.x + 2;
				triangles[t + 4] = face + 1;
				triangles[t + 5] = face + subDivision.x + 1;
			}
		}

		proceduralMesh.SetVertices(vertices);
		proceduralMesh.SetNormals(normals);
		proceduralMesh.SetTangents(tangents);
		proceduralMesh.SetUVs(0, uvs);
		proceduralMesh.SetIndices(triangles, MeshTopology.Triangles, 0);
		proceduralMesh.RecalculateNormals();
	}
}
