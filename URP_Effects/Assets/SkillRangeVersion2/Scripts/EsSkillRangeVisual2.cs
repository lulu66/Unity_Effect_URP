using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EsSkillRangeVersion2
{
	public class EsSkillRangeVisual2 : MonoBehaviour
	{
		public enum EsSkillRangeType
		{
			Circle,
			Fan,
			Rectangle
		}

		public static class ShaderKeywords
		{
			public static string FAN_TYPE = "_TYPE_FAN";
			public static string RECTANGLE_TYPE = "_TYPE_RECTANGLE";
			public static string ENABLE_FAN_EDGE = "_ENABLE_FAN_EDGE";
			public static string RECT_HORIZONTAL_SCALE = "_RECT_HORIZONTAL_SCALE";
			public static string RECT_VERTICAL_SCALE = "_RECT_VERTICAL_SCALE";

		}

		public static class ShaderProperties
		{
			public static int FlowProgress = Shader.PropertyToID("_FlowProgress");
			public static int VerticalHeadFactor = Shader.PropertyToID("_VerticalHeadFactor");
			public static int CloseEffectProgress = Shader.PropertyToID("_CloseEffectProgress");

		}
		private static string SKILL_RANGE_NAME = "SkillRangeVisual";
		private static int MinAngle = 5;    // 扇形中一个段的角度

		public EsSkillRangeType SkillType = EsSkillRangeType.Fan;

		// ---------------- 通用 ----------------------------
		[Range(0, 1)]
		public float FlowProgress = 0;      // 预警圈填充进度
		[Range(0, 1)]
		public float CloseEffectProgress;	// 预警圈填充完成之后的结束效果进度

		public float CloseEffectTime = 0;	// 预警圈结束后的泛白时间
		// ------------------------------------------------

		// ------------------- 扇形 ---------------------

		[Range(0, 360)]
		public int Angle = 60;              // 预警圈的角度
		public float Radius = 5;            // 预警圈的半径
		public float EdgeWidth = 1;			// 预警圈侧边宽度
		public bool EnableSide = false;     // 预警圈是否开启侧边

		// ---------------------------------------------

		// ------------------- 矩形 ---------------------

		public float Length = 5;
		public float Width = 2;
		public bool HorizontalScale = false;
		public bool VerticalScale = true;
		[Range(0, 1)]
		public float VerticalLenFactor = 0.2f;	// 竖向渐进时，头部uv所占的比例
		// ---------------------------------------------

		public float ActionTime;

		private Material skillRangeMaterial;
		private MeshFilter mMeshFilter;

		private float currentTime;
		private float currentCloseEffectTime;
		// -------------------- 生成Mesh ----------------------
		private List<Vector3> mVertices = new List<Vector3>();
		private List<int> mIndices = new List<int>();
		private List<Vector2> mTexcoords = new List<Vector2>();
		private List<Vector2> mTexcoord1s = new List<Vector2>();    // 侧边uv(单独给一套是因为需要独立的uv缩放)
		private readonly List<Color> mColors = new List<Color>();   //标记侧边

		// ---------------------------------------------------

		// ------------------------ 记录当前参数，避免频繁创建Mesh ---------------
		private int curAngle;
		private EsSkillRangeType curType;
		private float curRadius;
		private bool curEnableSise;
		private float curRecWidth;
		private float curRecHeight;
		private bool curHorizontalScale;
		private bool curVerticalScale;
		// ------------------------------------------------------------------
		private void Start()
		{
			Refresh();
		}

		public void Update()
		{
			if (FlowProgress <=1)
			{
				currentTime += Time.deltaTime;
				SetFlowProgress();
			}
			if(FlowProgress >=1 && CloseEffectProgress <= 1)
			{
				currentCloseEffectTime += Time.deltaTime;
				SetCloseEffectProgress();
			}
		}

		private void OnDestroy()
		{
			if (skillRangeMaterial != null)
				UnityEngine.Object.Destroy(skillRangeMaterial);
			if (mMeshFilter != null)
				UnityEngine.Object.Destroy(mMeshFilter.mesh);
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (!Application.isPlaying)
			{
				currentTime = FlowProgress * ActionTime;
				currentCloseEffectTime = CloseEffectProgress * CloseEffectTime;
				FlowProgress = 0;
				CloseEffectProgress = 0;
				Refresh(false);
			}
		}

		public static void CreateSkillRange()
		{
			var gameObjectNew = new GameObject { name = "SkillRangeVersion2" };
			gameObjectNew.AddComponent<MeshFilter>();
			gameObjectNew.AddComponent<MeshRenderer>();
			gameObjectNew.AddComponent<EsSkillRangeVisual2>();
		}

#endif

		#region 接口

		/// <summary>
		/// 生成预警框mesh
		/// </summary>
		/// <param name="resetProgress"></param>
		/// <param name="customTime"></param>
		public void Refresh(bool resetProgress = true, float customTime = 0)
		{
			// 技能框Material
			skillRangeMaterial = GetSkillRangeMaterial();

			if (skillRangeMaterial == null)
			{
				return;
			}
			// 技能框meshFilter
			if (mMeshFilter == null)
			{
				mMeshFilter = GetComponent<MeshFilter>();
				if (mMeshFilter == null)
				{
					mMeshFilter = gameObject.AddComponent<MeshFilter>();
				}
			}
			// 重置进度条
			if (resetProgress)
			{
				currentTime = customTime;
				FlowProgress = -1;
				CloseEffectProgress = -1;
				currentCloseEffectTime = 0;
			}


			// 设置进度
			SetFlowProgress();
			SetCloseEffectProgress();
			// 优化：参数不改变不重新生成mesh
			if (IsSameShaderParams())
			{
				return;
			}

			// 选择技能框类型
			SetSkillRangeType(SkillType);

			// 生成预警圈mesh
			Mesh skillRangeMesh;
#if UNITY_EDITOR
			if (Application.isPlaying)
			{
				skillRangeMesh = mMeshFilter.mesh;
				if (skillRangeMesh == null)
				{
					skillRangeMesh = new Mesh();
					mMeshFilter.mesh = skillRangeMesh;
				}
				//skillRangeMesh.BindingTarget(gameObject);

			}
			else
			{
				skillRangeMesh = mMeshFilter.sharedMesh;
				if (skillRangeMesh == null || skillRangeMesh.name != SKILL_RANGE_NAME)
				{
					skillRangeMesh = new Mesh();
					mMeshFilter.sharedMesh = skillRangeMesh;
				}
			}
#else
        skillRangeMesh = mMeshFilter.mesh;
        if (skillRangeMesh == null)
        {
            skillRangeMesh = new Mesh();
            mMeshFilter.mesh = skillRangeMesh;
            skillRangeMesh.BindingTarget(gameObject);
        }
#endif
			skillRangeMesh.name = SKILL_RANGE_NAME;

			// 生成预警框mesh
			GenerateSkillRangeMesh();
		}

		#endregion

		#region 内部函数
		private Material GetSkillRangeMaterial()
		{
			MeshRenderer rengeRenderer = this.GetComponent<MeshRenderer>();
			if (rengeRenderer == null)
			{
				Debug.LogError("技能框没有MeshRenderer");
				return null;
			}
#if UNITY_EDITOR
			skillRangeMaterial = Application.isPlaying ? rengeRenderer.material : rengeRenderer.sharedMaterial;
#else
        skillRangeMaterial = rengeRenderer.material;
#endif
			if (skillRangeMaterial == null)
			{
				Debug.LogError("技能框上没有材质球");
				return null;
			}

			return skillRangeMaterial;
		}
		
		private void SetFlowProgress()
		{
			if (ActionTime > 0)
			{
				FlowProgress = Mathf.Clamp01(currentTime / ActionTime);
				skillRangeMaterial.SetFloat(ShaderProperties.FlowProgress, FlowProgress);
			}

		}

		private void SetCloseEffectProgress()
		{
			// 结束泛白效果
			if (FlowProgress >= 1 && CloseEffectTime>0)
			{
				CloseEffectProgress = Mathf.Clamp01(currentCloseEffectTime / CloseEffectTime);
				skillRangeMaterial.SetFloat(ShaderProperties.CloseEffectProgress, CloseEffectProgress);
			}
		}
		private void SetSkillRangeType(EsSkillRangeType SkillType)
		{
			skillRangeMaterial.DisableKeyword(ShaderKeywords.FAN_TYPE);
			skillRangeMaterial.DisableKeyword(ShaderKeywords.RECTANGLE_TYPE);
			skillRangeMaterial.DisableKeyword(ShaderKeywords.ENABLE_FAN_EDGE);
			skillRangeMaterial.DisableKeyword(ShaderKeywords.RECT_HORIZONTAL_SCALE);
			skillRangeMaterial.EnableKeyword(ShaderKeywords.RECT_VERTICAL_SCALE);
			switch (SkillType)
			{
				case EsSkillRangeType.Fan:
					{
						skillRangeMaterial.EnableKeyword(ShaderKeywords.FAN_TYPE);
						if (EnableSide)
						{
							skillRangeMaterial.EnableKeyword(ShaderKeywords.ENABLE_FAN_EDGE);
						}
						break;
					}
				case EsSkillRangeType.Rectangle:
					{
						skillRangeMaterial.EnableKeyword(ShaderKeywords.RECTANGLE_TYPE);
						if(HorizontalScale)
						{
							skillRangeMaterial.EnableKeyword(ShaderKeywords.RECT_HORIZONTAL_SCALE);
						}
						if(VerticalScale)
						{
							skillRangeMaterial.EnableKeyword(ShaderKeywords.RECT_VERTICAL_SCALE);
						}
						break;
					}
			}
		}

		private bool IsSameShaderParams()
		{
			return curType == SkillType && IsFloatEqual(curRadius, Radius) && curAngle == Angle && curEnableSise == EnableSide && IsFloatEqual(curRecWidth, Width) && IsFloatEqual(curRecHeight,Length) && curVerticalScale == VerticalScale && curHorizontalScale == HorizontalScale;
		}

		private void SaveCurrentParameter()
		{
			curAngle = Angle;
			curType = SkillType;
			curRadius = Radius;
			curRecWidth = Width;
			curRecHeight = Length;
			curEnableSise = EnableSide;
			curVerticalScale = VerticalScale;
			curHorizontalScale = HorizontalScale;
		}
		private void GenerateSkillRangeMesh()
		{
			mVertices.Clear();
			mTexcoords.Clear();
			mTexcoord1s.Clear();
			mIndices.Clear();
			mColors.Clear();
			mMeshFilter.sharedMesh.Clear();

			switch (SkillType)
			{
				case EsSkillRangeType.Fan:
					{
						GenerateFanMesh();
						break;
					}
				case EsSkillRangeType.Rectangle:
					{
						if(VerticalScale)
						{
							GenerateVerticalRectangleMesh();
						}
						if(HorizontalScale)
						{
							GenerateHorizontalRectangleMesh();
						}
						break;
					}
			}
			mMeshFilter.sharedMesh.SetVertices(mVertices);
			mMeshFilter.sharedMesh.SetIndices(mIndices.ToArray(), MeshTopology.Triangles, 0);
			mMeshFilter.sharedMesh.SetUVs(0, mTexcoords);
			if (SkillType == EsSkillRangeType.Fan)
			{
				mMeshFilter.sharedMesh.SetColors(mColors);
				mMeshFilter.sharedMesh.SetUVs(1, mTexcoord1s);
			}
			if(SkillType == EsSkillRangeType.Rectangle)
			{
				mMeshFilter.sharedMesh.SetColors(mColors);
			}
			SaveCurrentParameter();
		}

		private void GenerateFanMesh()
		{
			// 角度小于最小角度值就不生成mesh了
			if (Angle < MinAngle) return;

			// 添加中心点
			mVertices.Add(new Vector3(0, 0, 0));
			mTexcoords.Add(new Vector2(0.5f, 0.5f));
			mTexcoord1s.Add(Vector2.zero);
			mColors.Add(Color.black);

			// 预警圈角度范围保持为最小度数的倍数
			Angle -= (Angle % MinAngle);

			float maxAngle = Angle;
			float currentAngle = 0f;

			// 扇形的段数
			int nums = Angle / MinAngle;

			for (int i = 0; i <= nums; ++i)
			{
				// 计算角度顺序：-0.5 * maxAngle * 0.5f ~ 0.5 * maxAngle * 0.5f
				float angleNow = currentAngle - maxAngle * 0.5f;

				// 单位圆：角度转换为2D坐标, x，z的范围都是[-1,1]了
				float x = Mathf.Sin(angleNow * Mathf.Deg2Rad);
				float z = Mathf.Cos(angleNow * Mathf.Deg2Rad);

				// 坐标点(-1~1)转换为纹理坐标(0~1)
				mTexcoords.Add(new Vector2(0.5f * x + 0.5f, 0.5f * z + 0.5f));
				mTexcoord1s.Add(Vector2.zero);

				// 添加实际顶点坐标
				mVertices.Add(Radius * new Vector3(x, 0, z));
				mColors.Add(Color.black);

				currentAngle += MinAngle;
			}

			// 三角形索引:一段一段添加
			for (int i = 1; i < mVertices.Count - 1; i++)
			{
				mIndices.Add(0);
				mIndices.Add(i);
				mIndices.Add(i + 1);
			}

			if (EnableSide)
			{
				// 添加边界
				float rotation = maxAngle * 0.5f;
				float width = Radius*2;
				// 相当于左边界和有边界均顺时针旋转了180度才对~
				GenerateSideMesh(width, Radius, 180-rotation, 180+rotation, mVertices.Count);
			}

		}

		/// <summary>
		/// 基本逻辑：生成两个quad，作为两层效果
		/// </summary>
		private void GenerateRectangleMesh()
		{
			float halfWidth = Width * 0.5f;
			Vector3[] vertices = new Vector3[4]
			{
				new Vector3(-halfWidth, 0, -Length),	// 左下
				new Vector3(halfWidth, 0, -Length),	// 右下
				new Vector3(-halfWidth, 0, 0),	// 左中
				new Vector3(halfWidth, 0, 0),	// 右中
			};

			Vector2[] uvs = new Vector2[4]
			{
				new Vector2(1,1f),  // 左中
				new Vector2(0,1f),   // 右上
				new Vector2(1,0),  // 左下
				new Vector2(0,0),  // 右下
			};

			// 第一层
			for (int i = 0; i < vertices.Length; i++)
			{
				mVertices.Add(vertices[i]);
				mTexcoords.Add(uvs[i]);
				mColors.Add(Color.black);
			}

			// 第二层：渐进层，因为头部不能缩放，所以分成两段


			// 添加索引
			int indexStart = 0;
			for(int i=0; i<1; i++)
			{
				mIndices.Add(indexStart);
				mIndices.Add(indexStart + 2);
				mIndices.Add(indexStart + 1);

				mIndices.Add(indexStart + 1);
				mIndices.Add(indexStart + 2);
				mIndices.Add(indexStart + 3);

			}
		}

		private void GenerateVerticalRectangleMesh()
		{
			float halfWidth = Width * 0.5f;
			Vector3[] vertices = new Vector3[6]
			{
				new Vector3(-halfWidth, 0, -Length),	// 左下
				new Vector3(halfWidth, 0, -Length),	// 右下
				new Vector3(-halfWidth,0,-Length * (1-VerticalLenFactor)),	//左中
				new Vector3(halfWidth,0,-Length * (1-VerticalLenFactor)),	// 右中
				new Vector3(-halfWidth, 0, 0),	// 左上
				new Vector3(halfWidth, 0, 0),	// 右上
			};

			Vector2[] uvs = new Vector2[6]
			{
				new Vector2(1,1f),  // 左下
				new Vector2(0,1),   // 右下
				new Vector2(1,1-VerticalLenFactor),  // 左中
				new Vector2(0,1-VerticalLenFactor),  // 右中
				new Vector2(1,0),  // 左上
				new Vector2(0,0),  // 右上

			};

			// 第一层
			for (int i = 0; i < 2; i++)
			{
				mVertices.Add(vertices[i]);
				mTexcoords.Add(uvs[i]);
				mColors.Add(Color.black);
			}
			for (int i = 2; i < vertices.Length; i++)
			{
				mVertices.Add(vertices[i]);
				mTexcoords.Add(uvs[i]);
				mColors.Add(Color.white);
			}

			// 添加索引
			mIndices.Add(0);
			mIndices.Add(2);
			mIndices.Add(1);

			mIndices.Add(1);
			mIndices.Add(2);
			mIndices.Add(3);

			mIndices.Add(2);
			mIndices.Add(4);
			mIndices.Add(3);

			mIndices.Add(3);
			mIndices.Add(4);
			mIndices.Add(5);
		}

		private void GenerateHorizontalRectangleMesh()
		{
			float halfWidth = Width * 0.5f;
			Vector3[] vertices = new Vector3[4]
			{
				new Vector3(-halfWidth, 0, -Length),	// 左下
				new Vector3(halfWidth, 0, -Length),	// 右下
				new Vector3(-halfWidth, 0, 0),	// 左中
				new Vector3(halfWidth, 0, 0),	// 右中
			};

			Vector2[] uvs = new Vector2[4]
			{
				new Vector2(1,1f),  // 左中
				new Vector2(0,1f),   // 右上
				new Vector2(1,0),  // 左下
				new Vector2(0,0),  // 右下
			};

			// 第一层
			for (int i = 0; i < vertices.Length; i++)
			{
				mVertices.Add(vertices[i]);
				mTexcoords.Add(uvs[i]);
				mColors.Add(Color.black);
			}

			// 第二层：渐进层，因为头部不能缩放，所以分成两段


			// 添加索引
			int indexStart = 0;
			for (int i = 0; i < 1; i++)
			{
				mIndices.Add(indexStart);
				mIndices.Add(indexStart + 2);
				mIndices.Add(indexStart + 1);

				mIndices.Add(indexStart + 1);
				mIndices.Add(indexStart + 2);
				mIndices.Add(indexStart + 3);

			}
		}

		/// <summary>
		/// 侧边边缘用两个quad mesh实现
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="rotation"></param>
		private void GenerateSideMesh(float width, float height, float leftRotation, float rightRotation, int indexStart)
		{
			float halfWidth = width * 0.5f;

			float leftRad = leftRotation * Mathf.Deg2Rad;
			float rightRad = rightRotation * Mathf.Deg2Rad;

			// 根据拓展顶点的比例算需要的uv比例
			float heightFactor = 0.1f;
			float extendSideHeight = height* heightFactor;
			Vector3[] vertices = new Vector3[6]
			{
				new Vector3(-halfWidth, 0, -height),	// 左下
				new Vector3(halfWidth, 0, -height),	// 右下
				new Vector3(-halfWidth, 0, 0),	// 左中
				new Vector3(halfWidth, 0, 0),	// 右中
				new Vector3(-halfWidth, 0, extendSideHeight),	// 左上
				new Vector3(halfWidth, 0, extendSideHeight)		// 右上
			};
			Vector2[] leftuvs = new Vector2[6]
			{
				new Vector2(0,1),  // 右下
				new Vector2(1,1),  // 左下

				new Vector2(0,0.5f),   // 右上
				new Vector2(1,0.5f),  // 左中

				new Vector2(0,0.5f - 0.5f*heightFactor),  // 右下
				new Vector2(1,0.5f - 0.5f*heightFactor),  // 左下

			};
			Vector2[] rightuvs = new Vector2[6]
			{
				new Vector2(1,1),  // 左下
				new Vector2(0,1),  // 右下

				new Vector2(1,0.5f),  // 左中
				new Vector2(0,0.5f),   // 右上

				new Vector2(1,0.5f - 0.5f*heightFactor),  // 左下
				new Vector2(0,0.5f - 0.5f*heightFactor),  // 右下

			};
			// 左边界顶点
			for (int i = 0; i < vertices.Length; i++)
			{
				var vertex = vertices[i];

				float cos = Mathf.Cos(leftRad);
				float sin = Mathf.Sin(leftRad);

				vertex.x = vertices[i].x * cos - vertices[i].z * sin;
				vertex.z = vertices[i].x * sin + vertices[i].z * cos;

				mVertices.Add(vertex);
				mTexcoords.Add(Vector3.zero);
				mTexcoord1s.Add(leftuvs[i]);
				mColors.Add(Color.white);
			}

			AddQuadIndex(indexStart);

			// 右边界顶点
			indexStart = mVertices.Count;
			for (int i = 0; i < vertices.Length; i++)
			{
				var vertex = vertices[i];

				float cos = Mathf.Cos(rightRad);
				float sin = Mathf.Sin(rightRad);

				vertex.x = vertices[i].x * cos - vertices[i].z * sin;
				vertex.z = vertices[i].x * sin + vertices[i].z * cos;

				mVertices.Add(vertex);
				mTexcoords.Add(Vector3.zero);
				mTexcoord1s.Add(rightuvs[i]);
				mColors.Add(Color.white);
			}

			AddQuadIndex(indexStart);
		}

		private void AddQuadIndex(int indexStart)
		{
			mIndices.Add(indexStart);
			mIndices.Add(indexStart + 2);
			mIndices.Add(indexStart + 1);

			mIndices.Add(indexStart + 1);
			mIndices.Add(indexStart + 2);
			mIndices.Add(indexStart + 3);

			mIndices.Add(indexStart + 2);
			mIndices.Add(indexStart + 4);
			mIndices.Add(indexStart + 3);

			mIndices.Add(indexStart + 3);
			mIndices.Add(indexStart + 4);
			mIndices.Add(indexStart + 5);
		}

		private bool IsFloatEqual(float a, float b)
		{
			return a.CompareTo(b) == 0;
		}
		#endregion
	}

}
