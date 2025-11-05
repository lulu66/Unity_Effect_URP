using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class FXAAPerformanceTest : MonoBehaviour
{
	public Volume GlobalVolume;
	private FXAAComponent m_FXAA;
	private FXAAComponent.FXAAMethod m_Method = FXAAComponent.FXAAMethod.EasyFXAA;
	private FXAAComponent.FXAAQuality m_Quality = FXAAComponent.FXAAQuality.Balance;

	public void Start()
	{
		if(GlobalVolume != null)
		{
			GlobalVolume.profile.TryGet<FXAAComponent>(out m_FXAA);
		}
		if (m_FXAA == null)
		{
			Debug.LogError($"fxaa is null.");
		}
			//m_FXAA = VolumeManager.instance.stack.GetComponent<FXAAComponent>();
	}

	public void ClickWithParams(FXAAComponent.FXAAMethod method, FXAAComponent.FXAAQuality quality)
	{
		m_FXAA.Enable.overrideState = true;
		m_FXAA.Enable.value = true;
		m_FXAA.Method.overrideState = true;
		m_FXAA.Method.value = method;
		m_FXAA.Quality.overrideState = true;
		m_FXAA.Quality.value = quality;
	}

	public void ClickHighPerformance()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.EasyFXAA, FXAAComponent.FXAAQuality.HighPerformance);
	}

	public void ClickPerformance()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.EasyFXAA, FXAAComponent.FXAAQuality.Performance);
	}

	public void ClickBalance()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.EasyFXAA, FXAAComponent.FXAAQuality.Balance);
	}

	public void ClickQuality()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.EasyFXAA, FXAAComponent.FXAAQuality.Quality);
	}

	public void ClickHighQuality()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.EasyFXAA, FXAAComponent.FXAAQuality.HighQuality);
	}

	public void ClickFS()
	{
		ClickWithParams(FXAAComponent.FXAAMethod.FS, FXAAComponent.FXAAQuality.Balance);
	}

	public void ClickDisable()
	{
		m_FXAA.Enable.overrideState = true;
		m_FXAA.Enable.value = false;
	}
}
