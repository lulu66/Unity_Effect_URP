using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering.Universal;

namespace Fluid2D
{
    public class Fluid2DManager : MonoBehaviour
    {
        // 流体模拟的材质
        public Material FluidSimulationMaterial;
        // 参与流体交互的target
        public Fluid2DTarget Target;

        //// 流体模拟需要用到的参数
        //public Vector4 Dissipation = Vector4.zero;   // 耗散(流体动能因粘性作用不可逆地转换为内能的过程)

        //[Range(0f,1f)]
        //public float Adhesion;          // 附着力（不是流体方程的标准力项，是流体与固体边界之间分子级别的吸引力，主要影响边界条件的计算）

        //[Range(0f, 1f)]
        //public float Pressure = 0.2f;          // 压力

        //[Range(0f, 1f)]
        //public float Viscosity;         // 粘度:对理想流体是不必要的，虽然对视觉质感起着比较大的作用

        //public Vector4 Boundary = new Vector4(0,0,1,1);        // 边界

        //[Range(0f, 1f)]
        //public float EdgeFallOff;       // 边界衰减


        private MeshRenderer fluidMr;
        private MeshFilter fluidFilter;
        private MaterialPropertyBlock propertyBlock;

        private Vector4 rect;

        private Fluid2DRenderFeature fluid2DFeature;

        private readonly int mainTexId = Shader.PropertyToID("_MainTex");
        private readonly int velocityId = Shader.PropertyToID("_Velocity");

        public static Fluid2DManager Instance
        {
            get;
            private set;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if(fluid2DFeature != null)
			{
                fluid2DFeature.Fluid2dPass.Release();
			}
        }
        private void OnEnable()
        {
            if (fluidMr == null)
            {
                fluidMr = GetComponent<MeshRenderer>();
            }
            fluidFilter = GetComponent<MeshFilter>();

            propertyBlock = new MaterialPropertyBlock();

            rect = new Vector4(0, 0, 1, 1);

            // 找到fluid 2d的render feature
            var pipelineAsset = UniversalRenderPipeline.asset;
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            UniversalRendererData rendererData = null;
            if (field != null)
            {
                var rendererDataList = field.GetValue(pipelineAsset) as ScriptableRendererData[];
                if (rendererDataList != null && rendererDataList.Length > 0)
                {
                    rendererData = rendererDataList[0] as UniversalRendererData;
                }
            }

            if(rendererData != null)
			{
                foreach (var rf in rendererData.rendererFeatures)
                {
                    if(rf.name == "Fluid2DRenderFeature")
					{
                        fluid2DFeature = rf as Fluid2DRenderFeature;
                    }
                }
            }
        }

        void LateUpdate()
        {
            if (FluidSimulationMaterial == null)
            {
                Debug.LogError("Fluid Simulation Material is null.");
                return;
            }

            // 更新交互，传递交互参数
            if (Target != null)
            {
                Target.Splat(transform, rect);
            }

        }

        // 更新流体平面的材质参数
        public void UpdateFluidPlaneMaterial(RenderTexture velocityRT, RenderTexture stateRT)
        {
            if (fluidMr == null || fluidMr.sharedMaterial == null) return;
            fluidMr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(mainTexId, stateRT);
            propertyBlock.SetTexture(velocityId, velocityRT);
            fluidMr.SetPropertyBlock(propertyBlock);

        }

    }

}
