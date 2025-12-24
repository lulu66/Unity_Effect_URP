using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class EsSkillRangeVisual2ShaderGUI : ShaderGUI
{

	MaterialProperty srcBlendProp, dstBlendProp;
	MaterialProperty colorProp;
	MaterialProperty mainTexProp;
	MaterialProperty progressTexProp;
	MaterialProperty progressColorProp;
	MaterialProperty fanSideTexProp;
	MaterialProperty fanSideOffsetProp;
	MaterialProperty fanSideColorProp;

	// 结束效果
	MaterialProperty closeEffectTexProp;
	MaterialProperty closeEffectColorProp;
	MaterialProperty maskTexProp;

	string[] unityBlendModeNames = System.Enum.GetNames(typeof(UnityEngine.Rendering.BlendMode));
	Material targetMaterial;
	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		FindProperties(properties);

		targetMaterial = materialEditor.target as Material;

		srcBlendProp.floatValue = EditorGUILayout.Popup("混合源", (int)srcBlendProp.floatValue, unityBlendModeNames);
		dstBlendProp.floatValue = EditorGUILayout.Popup("混合目标", (int)dstBlendProp.floatValue, unityBlendModeNames);

		materialEditor.TexturePropertySingleLine(new GUIContent("主贴图"), mainTexProp, colorProp);
		//materialEditor.TextureScaleOffsetProperty(mainTexProp);

		materialEditor.TexturePropertySingleLine(new GUIContent("进度条贴图"), progressTexProp, progressColorProp);
		//materialEditor.TextureScaleOffsetProperty(progressTexProp);

		if (targetMaterial.IsKeywordEnabled(EsSkillRangeVersion2.EsSkillRangeVisual2.ShaderKeywords.ENABLE_FAN_EDGE))
		{
			materialEditor.TexturePropertySingleLine(new GUIContent("扇形边界贴图"), fanSideTexProp, fanSideColorProp);
			materialEditor.ShaderProperty(fanSideOffsetProp, new GUIContent("边界偏移"));
		}
		EditorGUILayout.LabelField("结束效果", EditorStyles.boldLabel);
		materialEditor.TexturePropertySingleLine(new GUIContent("噪声贴图"), closeEffectTexProp, closeEffectColorProp);
		materialEditor.TextureScaleOffsetProperty(closeEffectTexProp);
		materialEditor.TexturePropertySingleLine(new GUIContent("噪声Mask贴图"), maskTexProp);


	}

	private void FindProperties(MaterialProperty[] properties)
	{
		// 混合模式
		srcBlendProp = FindProperty("_SrcBlend", properties);
		dstBlendProp = FindProperty("_DstBlend", properties);

		colorProp = FindProperty("_Color", properties);
		mainTexProp = FindProperty("_MainTex", properties);
		progressTexProp = FindProperty("_ProgressTex", properties);
		progressColorProp = FindProperty("_ProgressColor", properties);
		fanSideTexProp = FindProperty("_FanSideTex", properties);
		fanSideColorProp = FindProperty("_FanSideColor", properties);
		fanSideOffsetProp = FindProperty("_FanSideOffset", properties);

		// 结束效果
		closeEffectTexProp = FindProperty("_CloseNoiseTex", properties);
		closeEffectColorProp = FindProperty("_CloseColor", properties);
		maskTexProp = FindProperty("_MaskTex", properties);

	}
}
