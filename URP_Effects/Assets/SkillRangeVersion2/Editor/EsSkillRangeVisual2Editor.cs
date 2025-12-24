using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static EsSkillRangeVersion2.EsSkillRangeVisual2;

namespace EsSkillRangeVersion2
{
    [CustomEditor(typeof(EsSkillRangeVisual2))]

    public class EsSkillRangeVisual2Editor : Editor
    {
        private SerializedProperty mSkillRangeType;
        private SerializedProperty mFlowProgress;
        private SerializedProperty mCloseEffectProgress;
        private SerializedProperty mActionTime;
        private SerializedProperty mCloseEffectTime;
        // Fan
        private SerializedProperty mAngle;
        private SerializedProperty mRadius;
        private SerializedProperty mEnableSide;

        // Rectangle
        private SerializedProperty mWidth;
        private SerializedProperty mLength;
        private SerializedProperty mVerticalScale;
        private SerializedProperty mHorizontalScale;

        private void OnEnable()
		{
            mSkillRangeType = serializedObject.FindProperty("SkillType");
            mFlowProgress = serializedObject.FindProperty("FlowProgress");
            mActionTime = serializedObject.FindProperty("ActionTime");
            mCloseEffectTime = serializedObject.FindProperty("CloseEffectTime");
            // Fan
            mAngle = serializedObject.FindProperty("Angle");
            mRadius = serializedObject.FindProperty("Radius");
            mEnableSide = serializedObject.FindProperty("EnableSide");
            // Rectangle
            mWidth = serializedObject.FindProperty("Width");
            mLength = serializedObject.FindProperty("Length");
            mVerticalScale = serializedObject.FindProperty("VerticalScale");
            mHorizontalScale = serializedObject.FindProperty("HorizontalScale");
            mCloseEffectTime = serializedObject.FindProperty("CloseEffectTime");
            mCloseEffectProgress = serializedObject.FindProperty("CloseEffectProgress");
        }

        public override void OnInspectorGUI()
		{
            EsSkillRangeVisual2 skillRangeVisual = target as EsSkillRangeVisual2;

            if (!Application.isPlaying)
            {
                EditorGUILayout.PropertyField(mFlowProgress, new GUIContent("进度"));
                EditorGUILayout.PropertyField(mCloseEffectProgress, new GUIContent("结束效果进度"));
            }

            EditorGUILayout.PropertyField(mSkillRangeType, new GUIContent("类型"));
            EditorGUILayout.PropertyField(mActionTime, new GUIContent("持续时间"));
            EditorGUILayout.PropertyField(mCloseEffectTime, new GUIContent("结束效果持续时间"));

            if ((EsSkillRangeType)mSkillRangeType.enumValueIndex == EsSkillRangeType.Fan)
			{
                EditorGUILayout.PropertyField(mAngle, new GUIContent("角度"));
                EditorGUILayout.PropertyField(mRadius, new GUIContent("半径"));
                mEnableSide.boolValue = EditorGUILayout.Toggle(new GUIContent("开启侧边"), mEnableSide.boolValue);
            }
            if ((EsSkillRangeType)mSkillRangeType.enumValueIndex == EsSkillRangeType.Rectangle)
			{
                EditorGUILayout.PropertyField(mWidth, new GUIContent("宽度"));
                mWidth.floatValue = Mathf.Max(0.01f,mWidth.floatValue);
                EditorGUILayout.PropertyField(mLength, new GUIContent("高度"));
                mLength.floatValue = Mathf.Max(0.01f, mLength.floatValue);
                mVerticalScale.boolValue = EditorGUILayout.Toggle(new GUIContent("竖向进度"), mVerticalScale.boolValue);

                mHorizontalScale.boolValue = EditorGUILayout.Toggle(new GUIContent("横向进度"), mHorizontalScale.boolValue);
            }
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/特效/技能预警框版本2", false, 0)]
        private static void CreateSkillRange()
        {
            EsSkillRangeVisual2.CreateSkillRange();
        }
    }
}

