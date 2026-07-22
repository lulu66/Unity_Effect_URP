using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fluid2D
{
    public class Fluid2DManager : MonoBehaviour
    {
        // ����ģ��Ĳ���
        public Material FluidSimulationMaterial;
        // �������彻����target
        public Fluid2DTarget Target;

        //// ����ģ����Ҫ�õ��Ĳ���
        //public Vector4 Dissipation = Vector4.zero;   // ��ɢ(���嶯����ճ�����ò������ת��Ϊ���ܵĹ���)

        //[Range(0f,1f)]
        //public float Adhesion;          // ���������������巽�̵ı�׼��������������߽�֮����Ӽ��������������ҪӰ��߽������ļ��㣩

        //[Range(0f, 1f)]
        //public float Pressure = 0.2f;          // ѹ��

        //[Range(0f, 1f)]
        //public float Viscosity;         // ճ��:�����������ǲ���Ҫ�ģ���Ȼ���Ӿ��ʸ����űȽϴ������

        //public Vector4 Boundary = new Vector4(0,0,1,1);        // �߽�

        //[Range(0f, 1f)]
        //public float EdgeFallOff;       // �߽�˥��


        private MeshRenderer fluidMr;
        private MeshFilter fluidFilter;
        private MaterialPropertyBlock propertyBlock;

        private Vector4 rect;

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

        private void OnEnable()
        {
            if (fluidMr == null)
            {
                fluidMr = GetComponent<MeshRenderer>();
            }
            fluidFilter = GetComponent<MeshFilter>();

            propertyBlock = new MaterialPropertyBlock();

            rect = new Vector4(0, 0, 1, 1);
        }

        void LateUpdate()
        {
            if (FluidSimulationMaterial == null)
            {
                Debug.LogError("Fluid Simulation Material is null.");
                return;
            }

            // ���½��������ݽ�������
            if (Target != null)
            {
                Target.Splat(transform, rect);
            }

        }

        // ��������ƽ��Ĳ��ʲ���
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
