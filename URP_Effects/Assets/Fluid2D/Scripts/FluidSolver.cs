using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FluidSolver : MonoBehaviour
{
    // 流体网格分辨率
    public int Resolution = 1024;
    public RenderTexture VelocityA;
    public RenderTexture VelocityB;
    public RenderTexture StateA;
    public RenderTexture StateB;

    public FluidContainer Container;
    public Material SimulationMaterial;

    private Vector4 rect;
    private Vector4 warpMode = new Vector4(0,0,1,1); //采样模式,默认(0,0,1,1)
    private void InitRenderTextures()
	{
        VelocityA = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        VelocityA.name = "Velocity A";

        VelocityB = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        VelocityB.name = "Velocity B";

        StateA = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        StateA.name = "State A";

        StateB = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        StateB.name = "State B";

        VelocityA.filterMode = FilterMode.Point;
        VelocityB.filterMode = FilterMode.Point;
        StateA.filterMode = FilterMode.Point;
        StateB.filterMode = FilterMode.Point;
    }

    private void DeinitRenderTextures()
	{
        if(VelocityA != null)
		{
#if UNITY_EDITOR
			DestroyImmediate(VelocityA);
#else
			Destroy(VelocityA);
#endif
			VelocityA = null;
        }
        if(VelocityB != null)
		{
#if UNITY_EDITOR
			DestroyImmediate(VelocityB);
#else
			Destroy(VelocityB);
#endif
			VelocityB = null;
        }
        if(StateA != null)
		{
#if UNITY_EDITOR
			DestroyImmediate(StateA);
#else
			Destroy(StateA);
#endif
			StateA = null;
        }
        if(StateB != null)
		{
#if UNITY_EDITOR
			DestroyImmediate(StateB);
#else
			Destroy(StateB);
#endif
			StateB = null;
        }
    }

	private void OnEnable()
	{
        InitRenderTextures();

        rect = new Vector4(0,0,1,1);
	}

	private void OnDisable()
	{
        DeinitRenderTextures();
	}

	private void OnDestroy()
	{
        DeinitRenderTextures();
    }
	void Start()
    {
        
    }

	private void LateUpdate()
	{
        UpdateFluidSolver(Time.deltaTime);

    }

    private void UpdateFluidSolver(float timeStep)
	{
        if(Container == null)
		{
            Debug.LogError("Container is null");
            return;
		}
        if(SimulationMaterial == null)
		{
            Debug.LogError("simulation material is null");
            return;
        }
        // 更新容器大小
        // 传递流体模拟后的velocity和state buffer
        //Container.UpdateContainerSize();
        Container.UpdateMaterial(VelocityA, StateA);

		// 计算对流体产生影响的外物对流体的影响,更到到velocity和state buffer中
		for (int i = 0; i < Container.Targets.Length; i++)
		{
			Container.Targets[i].Splat(Container, VelocityA, StateA, rect);
		}
		// 首先考虑流体容器的速度和加速度，这里流体容器静止，因此不考虑
		// 然后考虑容器整体受到的外力，包括重力，这里仍不考虑容器本身的外力
		// 最后考虑容器的速度和位移，这里也不考虑

		// 传递流体物理参数到模拟材质
		SimulationMaterial.SetFloat("_Pressure", Container.Pressure);
		//SimulationMaterial.SetFloat("_Viscosity", Container.Viscosity);
		//SimulationMaterial.SetFloat("_VortConf", Container.Turbulence);
		//SimulationMaterial.SetFloat("_Adhesion", Container.Adhesion);
		//SimulationMaterial.SetVector("_ExternalForce", Vector3.zero);
		// 纹理的包装模式
		SimulationMaterial.SetVector("_WrapMode", warpMode);
		// 耗散和衰减相关参数
		SimulationMaterial.SetVector("_Dissipation", Container.Dissipation);
		SimulationMaterial.SetVector("_EdgeFalloff", Container.EdgeFallOff);
		//SimulationMaterial.SetVector("_Offsets", Vector4.zero);  // offset指流体容器本身产生的位移，如果流体容器不动，此处就没有位移，可以忽略

		// 流体模拟

		// 1.更新平流的状态 stateA -> stateB, stateA的四个通道都是density
		VelocityA.filterMode = FilterMode.Point;
		StateA.filterMode = FilterMode.Point;
		SimulationMaterial.SetFloat("_DeltaTime", timeStep);
		SimulationMaterial.SetTexture("_Velocity", VelocityA);
		//SimulationMaterial.SetTexture("_State",StateB);
		Graphics.Blit(StateA, StateB, SimulationMaterial, 0);

		// 2.速度的平流 velocityA -> velocityB
		Graphics.Blit(VelocityA, VelocityB, SimulationMaterial, 1);

		// 3.耗散(考虑流体的衰减) stateB -> stateA
		Graphics.Blit(StateB, StateA, SimulationMaterial, 2);

		// 4.速度场的旋度：涡量，量化流体旋转运动的强度,用于后续计算涡量约束，可简化;存储于velocityA的b通道， velocityB -> velocityA
		//Graphics.Blit(VelocityB, VelocityA, SimulationMaterial, 3);

		// 5. 密度梯度:表示密度场在空间中的变化率，应用：模拟浮力、表面张力、界面捕捉、可压缩流体；此处应该是用来计算normal的
		// stateA -> stateB RG通道存放normal,BA通道不变,为密度
		// 假定为均匀流体，省略密度梯度的计算
		//Graphics.Blit(StateA, StateB, SimulationMaterial, 4);

		// 5.更新外力对整个流体的影响（这里忽略，暂且认为没有其它外力对流体产生影响）
		//StateB.filterMode = FilterMode.Bilinear;
		{
			// 计算重力、表面张力，流体的整体外力，浮力,涡量约束等对速度的影响
			// 速度考虑了normal map对速度的影响，做一个映射
		}
		//StateB.filterMode = FilterMode.Point;

		// 6. 计算速度的散度 velocityA -> velocityB, 速度的B通道为速度的散度
		// 物理意义：1）速度的散度=0，为不可压缩流体；2）非0的速度散度是计算压力泊松方程中投影法的关键步骤；
		Graphics.Blit(VelocityB, VelocityA, SimulationMaterial, 5);

		// 7. 计算压力，使用一次Jaccobi迭代 velocityB -> velocityA, 压力值存放于A通道
		Graphics.Blit(VelocityA, VelocityB, SimulationMaterial, 6);
		Graphics.Blit(VelocityB, VelocityA, SimulationMaterial, 6);
		Graphics.Blit(VelocityA, VelocityB);

		// 8. 计算无散速度
		Graphics.Blit(VelocityB, VelocityA, SimulationMaterial, 7);

		VelocityA.filterMode = FilterMode.Bilinear;
		StateA.filterMode = FilterMode.Bilinear;
	}
}
