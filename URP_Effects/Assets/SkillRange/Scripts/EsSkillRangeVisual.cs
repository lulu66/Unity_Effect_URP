using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace EsEffect
{
    public class EsSkillRangeVisual : MonoBehaviour
    {
        public enum SkillRangeType
        {
            Circle,
            Rectangle
        }

        public SkillRangeType Type = SkillRangeType.Circle;

        // 进度
        [Range(0, 1)] public float Progress;
        // 内圈大小(用于圆环预警圈)
        [Range(0, 1)] public float StartProgress;
        // 预警圈的角度范围
        [Range(0, 360)] public int Angle = 60;
        public float Radius = 5;
        // 使用边缘
        public bool EnableEdge;
        // 边缘宽度(系数，最小1度，最大10度)（侧边边缘位置会覆盖一块给定角度的范围）
        [Range(0.0f, 1f)] public float EdgeAngle;
        // 侧边边缘间隙宽度(角度表示)
        [Range(0f, 20f)] public float AngleDiff;
        // 弧边边缘间隙的位置（用比例表示，0：弧边最边缘处；1：中心处）
        [Range(0f, 1f)] public float RadiusDiff;

        // 弧边边缘间隙的宽度
        [Range(0f, 1f)] public float GapWidth;

        // ------添加外边缘轮廓（类型为Circle时生效）------
        public Transform LeftCircleSide;
        public Transform RightCircleSide;
        [ColorUsage(true, true)]
        public Color CircleSideColor = Color.white;
        public Texture2D CircleSideTexture;
        public float CircleSideRadius = 43f;
        public bool UseCircleSide;
        public float LeftAngle
        {
            get
            {
                return 180 + transform.eulerAngles.y - Angle * 0.5f;
            }
        }
        public float RightAngle
        {
            get
            {
                return 180 + transform.eulerAngles.y + Angle * 0.5f;
            }
        }

        private MeshRenderer leftCircleSideMr;
        private MeshRenderer rightCircleSideMr;
        // -------------------------------------------

        // ------------ SkillRangeType == Rectangle相关参数-----------------
        public float Length = 5;
        public float Width = 2;
        [FormerlySerializedAs("SliceTopWidth")] public float SliceSideWidth = 1f;
        [FormerlySerializedAs("SliceLeftWidth")] public float SliceBottomWidth;
        [FormerlySerializedAs("SliceRightWidth")] public float SliceTopWidth = 2.25f;
        public bool CustomTopWidth = false;
        // ---------------------------------------------------------------

        // 效果时间
        public float ActionTime;
        // 闪烁时间(结束前开始倒数)
        public float FlushTime;
        private float currentTime;

        private readonly List<Vector3> mVertices = new List<Vector3>();
        private readonly List<int> mIndices = new List<int>();
        private readonly List<Vector2> mTexcoords = new List<Vector2>();

        //边缘贴图用到的2u
        private readonly List<Vector2> mTexcoords2 = new List<Vector2>();
        private readonly List<Color> mColors = new List<Color>();

        private Material skillRangeMaterial;
        private const int MinAngle = 5;

        private MeshFilter mMeshFilter;

        // 记录当前参数，避免频繁刷mesh
        private float curEdgeAngle;
        private Color curmEdgeColor;
        private int curAngle;
        private SkillRangeType curType;
        private bool curCustomTopWidth;
        private float curRadius;
        private float curLength;
        private float curWidth;
        private float cursliceBottomWidth;
        private float cursliceSideWidth;
        private float cursliceTopWidth;
        private float curAngleDiff;
        private float curRadiusDiff;
        private float curGapWidth;
        private readonly Color mEdgeColor = Color.white;

        private Vector3 sideScale = Vector3.one;
        private static readonly int mFlowType = Shader.PropertyToID("_FlowType");
        private static readonly int mFlowProgress = Shader.PropertyToID("_FlowProgress");
        private static readonly int mStartFlowProgress = Shader.PropertyToID("_StartFlowProgress");
        private static readonly int mMaxInnerAngle = Shader.PropertyToID("_MaxInnerAngle");
        private static readonly int mOuterRingOffset = Shader.PropertyToID("OuterRingOffset");
        private static readonly int mOuterRingWidth = Shader.PropertyToID("OuterRingWidth");
        private static readonly int mEnableEdge = Shader.PropertyToID("_EnableEdge");
        private static readonly string mShinng = "_SHINNG";

        void Start()
        {
            Refresh();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                currentTime = Progress * ActionTime;
                Progress = 0;
                Refresh(false);
                UpdateCircleSide();
            }
        }
#endif

        private void OnDestroy()
        {
            if (skillRangeMaterial != null)
                UnityEngine.Object.Destroy(skillRangeMaterial);
            if (mMeshFilter != null)
                UnityEngine.Object.Destroy(mMeshFilter.mesh);
        }

        // Update is called once per frame
        public void Update()
        {
            if (Progress <= 1)
            {
                currentTime += Time.deltaTime;
                setShaderParameter(false);
            }

            UpdateCircleSide();
        }

        public void Refresh(bool resetProgress = true)
        {
            //编辑器中替换材质球以后获取新的材质球
#if UNITY_EDITOR
            skillRangeMaterial = getSkillRangeMaterial();
#endif
            if (skillRangeMaterial == null)
            {
                skillRangeMaterial = getSkillRangeMaterial();
                if (skillRangeMaterial == null)
                {
                    return;
                }
            }
            if (mMeshFilter == null)
            {
                mMeshFilter = GetComponent<MeshFilter>();
                if (mMeshFilter == null)
                {
                    mMeshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (resetProgress)
            {
                currentTime = 0;
                Progress = -1;
            }
            // 内圈的大小
            skillRangeMaterial.SetFloat(mStartFlowProgress, StartProgress);
            // 预警圈的类型
            skillRangeMaterial.SetFloat(mFlowType, (int)Type);
            // 关闭闪烁关键字
            skillRangeMaterial.DisableKeyword(mShinng);
            setShaderParameter(true);

            // 优化：参数不变，不重新生成mesh
            if (isSamePara())
            {
                return;
            }

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
                if (skillRangeMesh == null)
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
            skillRangeMesh.name = "SkillRangeVisual";
            genRangeMesh();

        }

        /// <summary>
        /// 创建一个初始的预警圈
        /// </summary>
        public static void CreateSkillRange()
        {
            var gameObjectNew = new GameObject { name = "SkillRange" };
            gameObjectNew.AddComponent<MeshFilter>();
            gameObjectNew.AddComponent<MeshRenderer>();
            gameObjectNew.AddComponent<EsSkillRangeVisual>();
        }

        /// <summary>
        /// 获取预警圈的材质
        /// </summary>
        /// <returns></returns>
        private Material getSkillRangeMaterial()
        {
            MeshRenderer rengeRenderer = this.GetComponent<MeshRenderer>();
            if (rengeRenderer == null)
            {
                Debug.LogError("GameObject上找不到MeshRenderer");
                return null;
            }
#if UNITY_EDITOR
            skillRangeMaterial = Application.isPlaying ? rengeRenderer.material : rengeRenderer.sharedMaterial;
#else
                    skillRangeMaterial = rengeRenderer.material;
#endif
            if (skillRangeMaterial == null)
            {
                Debug.LogError("MeshRenderer上找不到Material");
                return null;
            }

            return skillRangeMaterial;
        }

        /// <summary>
        /// 更新预警动画进度、开关闪烁关键字、更新环形预警圈边缘相关参数
        /// </summary>
        /// <param name="refresh"></param>
        private void setShaderParameter(bool refresh)
        {
            if (ActionTime > 0)
            {
                // 更新进度
                Progress = Mathf.Clamp01(currentTime / ActionTime);
                skillRangeMaterial.SetFloat(mFlowProgress, Progress);
                // 闪烁开始时间>当前时间，开启闪烁关键字
                if (FlushTime > 0)
                {
                    float startTime = ActionTime - FlushTime;
                    startTime = startTime < 0 ? 0 : startTime;
                    if (currentTime > startTime)
                    {
                        skillRangeMaterial.EnableKeyword(mShinng);
                    }
                }
            }

            if (Type == SkillRangeType.Circle)
            {
                setCircleTypeParameter();
            }
        }

        /// <summary>
        /// 设置环形预警圈相关参数（弧边间隙、侧边间隙）
        /// </summary>
        private void setCircleTypeParameter()
        {
            if (EnableEdge)
            {
                // 除去两侧间隙角度范围后，剩下的内部的角度范围
                skillRangeMaterial.SetFloat(mMaxInnerAngle, Angle - AngleDiff);
                // 环的偏移(0~Radius)
                skillRangeMaterial.SetFloat(mOuterRingOffset, Radius - Radius * RadiusDiff);
                // 环的宽度
                skillRangeMaterial.SetFloat(mOuterRingWidth, Radius * GapWidth);
                // 开启边缘的关键字
                skillRangeMaterial.SetFloat(mEnableEdge, 1);
                skillRangeMaterial.EnableKeyword("ENABLE_EDGE");
            }
            else
            {
                // 不开启边缘，内部角度范围 = 整个角度范围
                skillRangeMaterial.SetFloat(mMaxInnerAngle, Angle);
                // 关闭边缘的关键字
                skillRangeMaterial.SetFloat(mEnableEdge, 0);
                skillRangeMaterial.DisableKeyword("ENABLE_EDGE");
            }
        }

        /// <summary>
        /// 生成预警圈mesh
        /// </summary>
        private void genRangeMesh()
        {
            // mesh的顶点属性：顶点坐标、两组uv、顶点色、顶点索引
            mVertices.Clear();
            mTexcoords.Clear();
            mTexcoords2.Clear();
            mColors.Clear();
            mIndices.Clear();
            mMeshFilter.sharedMesh.Clear();

            //compute secondary center pos
            // 生成不同类型的mesh
            if (Type == SkillRangeType.Circle)
            {
                genSectorRangeMesh();
            }

            else
            {
                genRectangleRangeMesh();
            }

            // 为mesh设置顶点属性，如果预警圈类型是环形，则添加顶点色和uv1
            mMeshFilter.sharedMesh.SetVertices(mVertices);
            mMeshFilter.sharedMesh.SetIndices(mIndices.ToArray(), MeshTopology.Triangles, 0);
            mMeshFilter.sharedMesh.SetUVs(0, mTexcoords);
            if (Type == SkillRangeType.Circle)
            {
                mMeshFilter.sharedMesh.SetUVs(1, mTexcoords2);
                mMeshFilter.sharedMesh.SetColors(mColors);
            }

            saveCurrentParameter();
        }

        /// <summary>
        /// 生成扇形mesh
        /// </summary>
        private void genSectorRangeMesh()
        {
            // 如果开启了使用边缘，强制边缘的角度为0.5度
            if (EnableEdge && EdgeAngle <= 0)
                EdgeAngle = 0.0f;
            else if (!EnableEdge) // 没开启使用边缘，边缘的角度为0度
                EdgeAngle = 0f;

            // 添加中心点的位置、uv0、uv1坐标和顶点色
            mVertices.Add(new Vector3(0, 0, 0));
            mTexcoords.Add(new Vector2(0.5f, 0.5f));
            mTexcoords2.Add(new Vector2(0.5f, 0.5f));
            mColors.Add(Color.black);

            // 添加边缘点(其中一个侧边点):位置、uv0、uv1和顶点色
            mVertices.Add(new Vector3(0, 0, 0)); //edge vertex
            mColors.Add(Color.black);
            mTexcoords.Add(new Vector2(0.5f, 0.5f));
            mTexcoords2.Add(new Vector2(0f, 0f));

            // 预警圈角度范围<5度，就不创建mesh了
            if (Angle < MinAngle)
                return;
            // 预警圈角度范围保持为5度的倍数
            Angle -= (Angle % MinAngle);

            // 预警圈的最大角度为：正常角度范围 + 边缘角度范围(1~10度)
            float maxAngle = Angle + 2 * MinAngle * EdgeAngle;
            float currentAngle = 0;

            // 预警圈角度的组数(5度为1组)，用来生成mesh的顶点
            int nums = Angle / MinAngle;
            //add extra edge mesh
            // 如果开启边缘了，要增加两组，侧边一边一组
            if (EnableEdge && EdgeAngle > 0.0f)
                nums += 2;

            for (int i = 0; i <= nums; ++i)
            {
                // 角度范围应该的起始和结束应该是(-0.5 * maxAngle * 0.5f ~ 0.5 * maxAngle * 0.5f)吧
                float angleNow = currentAngle - maxAngle * 0.5f;

                // 暂时按照单位圆（半径为1）计算弧边的顶点的坐标(x,z)
                float x = Mathf.Sin(angleNow * Mathf.Deg2Rad);
                float z = Mathf.Cos(angleNow * Mathf.Deg2Rad);

                // 坐标点(-1~1)转换为纹理坐标(0~1)
                mTexcoords.Add(new Vector2(0.5f * x + 0.5f, 0.5f * z + 0.5f));

                // uv1的纹理坐标都是0，估计是不用吧
                mTexcoords2.Add(new Vector2(0f, 0f));

                // 考虑半径的实际顶点坐标
                mVertices.Add(Radius * new Vector3(x, 0, z));

                // 顶点色都是黑色，估计也不用
                mColors.Add(Color.black);
                //subdivide edge mesh
                // 如果开启了边缘，那么边缘组就是第一组和最后一组，此时的步进角度为边缘的角度大小(MinAngle * 系数)，其余内部组正常步进角度
                if (EnableEdge && EdgeAngle > 0.0f)
                    if ((i == 0 || i == nums - 1))
                        currentAngle += MinAngle * EdgeAngle;
                    else
                        currentAngle += MinAngle;
                else // 如果没有开启边缘，则正常步进角度(5度)
                    currentAngle += MinAngle;
            }

            // 顶点数组的前两个顶点都在中心点的(0,0,0)，此处构造的三角形索引都是一组(按上面角度的分组)一个三角形
            for (int i = 2; i < mVertices.Count - 1; i++)
            {
                mIndices.Add(1);
                mIndices.Add(i);
                mIndices.Add(i + 1);
            }

            if (EnableEdge && EdgeAngle > 0.0f)
            {
                // compose rectangle edge mesh
                // 添加两个边缘的三角形索引，实际上就两条线
                mIndices.Add(0);
                mIndices.Add(2);
                mIndices.Add(1);

                mIndices.Add(0);
                mIndices.Add(1);
                mIndices.Add(mVertices.Count - 1);

                //Mark edge
                // 两个侧边的顶点色为白色
                mColors[0] = mEdgeColor;
                mColors[1] = mEdgeColor;
                mColors[2] = mEdgeColor;
                mColors[3] = mEdgeColor;
                mColors[mVertices.Count - 1] = mEdgeColor;
                mColors[mVertices.Count - 2] = mEdgeColor;

                //Set edge uv
                //侧边1三角形顶点uv1的坐标
                mTexcoords2[0] = new Vector2(0, 0f);
                mTexcoords2[2] = new Vector2(1f, 1f);
                mTexcoords2[3] = new Vector2(1f, 0);

                //侧边2三角形顶点uv1的坐标
                mTexcoords2[mVertices.Count - 1] = new Vector2(1f, 1f);
                mTexcoords2[mVertices.Count - 2] = new Vector2(1f, 0);
                mTexcoords2[1] = new Vector2(0, 0);

            }
        }

        /// <summary>
        /// 生成矩形预警圈
        /// </summary>
        private void genRectangleRangeMesh()
        {
            //9-slice scaling
            SliceBottomWidth = Length / 3f;
            if (!CustomTopWidth)
            {
                SliceTopWidth = Length / 3f;
            }
            float cornerSideWidth = SliceSideWidth / Width;
            float cornerBottomWidth = SliceBottomWidth / Length;
            float cornerTopWidth = SliceTopWidth / Length;

            for (int k = 0; k < 16; ++k)
            {
                mVertices.Add(new Vector3(0, 0, 0));
                mTexcoords.Add(new Vector2(0, 0));
            }

            float[] xPValues = new float[4];
            float[] yPValues = new float[4];

            xPValues[0] = 0.0f;
            yPValues[0] = 0.0f;
            xPValues[1] = cornerSideWidth;
            yPValues[1] = cornerBottomWidth;
            xPValues[2] = 1.0f - cornerSideWidth;
            yPValues[2] = 1.0f - cornerTopWidth;
            xPValues[3] = 1.0f;
            yPValues[3] = 1.0f;

            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    float xP = xPValues[x];
                    float yP = yPValues[y];

                    int index = getIndex(x, y);

                    mVertices[index] = new Vector3(
                        Mathf.Lerp(-1.0f, 1.0f, xP) * Width * 0.5f,
                        0.0f,
                        Mathf.Lerp(0.0f, 2.0f, yP) * Length * 0.5f); //assure start from 0 position
                    mTexcoords[index] = new Vector2(y / 3.0f, x / 3.0f);
                }
            }

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    mIndices.Add(getIndex(x + 1, y));
                    mIndices.Add(getIndex(x, y));
                    mIndices.Add(getIndex(x + 1, y + 1));
                    mIndices.Add(getIndex(x, y));
                    mIndices.Add(getIndex(x, y + 1));
                    mIndices.Add(getIndex(x + 1, y + 1));
                }
            }
        }

        /// <summary>
        /// 记录当前参数，避免频繁刷mesh
        /// </summary>
        private void saveCurrentParameter()
        {
            curEdgeAngle = EdgeAngle;
            curmEdgeColor = mEdgeColor;
            curAngle = Angle;
            curRadius = Radius;
            curLength = Length;
            curWidth = Width;
            curType = Type;
            curCustomTopWidth = CustomTopWidth;
            cursliceSideWidth = SliceSideWidth;
            cursliceBottomWidth = SliceBottomWidth;
            cursliceTopWidth = SliceTopWidth;
            curAngleDiff = AngleDiff;
            curRadiusDiff = RadiusDiff;
            curGapWidth = GapWidth;
        }

        //transform numbers to index
        private static int getIndex(int x, int y)
        {
            return (y * 4) + x;
        }

        /// <summary>
        /// 判断当前参数是否改变，改变返回false
        /// </summary>
        /// <returns></returns>
        private bool isSamePara()
        {
            return curType == Type &&
                   curCustomTopWidth == CustomTopWidth &&
                   isFloatEqual(cursliceSideWidth, SliceSideWidth)
                   && isFloatEqual(cursliceBottomWidth, SliceBottomWidth) &&
                   isFloatEqual(cursliceTopWidth, SliceTopWidth) &&
                   isFloatEqual(curEdgeAngle, EdgeAngle) && curmEdgeColor == mEdgeColor && curAngle == Angle &&
                   isFloatEqual(curRadius, Radius)
                   && isFloatEqual(curLength, Length) && isFloatEqual(curWidth, Width) &&
                   isFloatEqual(curAngleDiff, AngleDiff) && isFloatEqual(curRadiusDiff, RadiusDiff)
                   && isFloatEqual(curGapWidth, GapWidth);
        }

        private bool isFloatEqual(float a, float b)
        {
            return a.CompareTo(b) == 0;
        }

        /// <summary>
        /// 更新外边缘轮廓的位置、缩放、旋转和开关
        /// </summary>
        private void UpdateCircleSide()
        {
            if (LeftCircleSide == null || RightCircleSide == null)
            {
                return;
            }
            if (leftCircleSideMr == null || rightCircleSideMr == null)
            {
                leftCircleSideMr = LeftCircleSide.GetComponent<MeshRenderer>();
                rightCircleSideMr = RightCircleSide.GetComponent<MeshRenderer>();
            }

            if (!UseCircleSide && (LeftCircleSide.gameObject.activeSelf || RightCircleSide.gameObject.activeSelf))
            {
                LeftCircleSide.gameObject.SetActive(false);
                RightCircleSide.gameObject.SetActive(false);
                return;
            }
            if ((!LeftCircleSide.gameObject.activeSelf || !RightCircleSide.gameObject.activeSelf))
            {
                LeftCircleSide.gameObject.SetActive(true);
                RightCircleSide.gameObject.SetActive(true);
            }
            // left side
            var original = LeftCircleSide.localEulerAngles;
            LeftCircleSide.localEulerAngles = new Vector3(original.x, LeftAngle - transform.eulerAngles.y, original.z);
            sideScale.x = 1;
            LeftCircleSide.localScale = Radius * CircleSideRadius * sideScale;
            // right side
            original = LeftCircleSide.localEulerAngles;
            RightCircleSide.localEulerAngles = new Vector3(original.x, RightAngle - transform.eulerAngles.y, original.z);
            sideScale.x = -1;
            RightCircleSide.localScale = Radius * CircleSideRadius * sideScale;

            //角度为360的时候隐藏边界
            if (Angle == 360 || Angle == 0)
            {
                leftCircleSideMr.enabled = false;
                rightCircleSideMr.enabled = false;
            }
            else if (!leftCircleSideMr.enabled || !leftCircleSideMr.enabled)
            {
                leftCircleSideMr.enabled = true;
                rightCircleSideMr.enabled = true;
            }

#if UNITY_EDITOR
            var sideMat = LeftCircleSide.GetComponent<MeshRenderer>().sharedMaterial;
            sideMat.SetColor("_Color", CircleSideColor);
            sideMat.SetTexture("_MainTex", CircleSideTexture);
#endif

        }

    }
}