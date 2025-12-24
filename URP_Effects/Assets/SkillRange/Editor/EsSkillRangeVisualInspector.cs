using UnityEngine;
using UnityEditor;
using SkillRangeType = EsEffect.EsSkillRangeVisual.SkillRangeType;

namespace EsEffect
{
    [CustomEditor(typeof(EsSkillRangeVisual))]
    public class EsSkillRangeVisualInspector : Editor
    {
        // private SkillRangeType mSkillRangeType;
        private SerializedProperty mSkillRangeType;
        private SerializedProperty mProgress;
        private SerializedProperty mStartProgress;
        private SerializedProperty mActionTime;
        private SerializedProperty mFlushTime;
        private SerializedProperty mAngle;
        private SerializedProperty mRadius;
        private SerializedProperty mEnableEdge;
        private SerializedProperty mEdgeAngle;
        private SerializedProperty mRadiusDiff;
        private SerializedProperty mGapWidth;
        private SerializedProperty mAngleDiff;
        private SerializedProperty mLength;
        private SerializedProperty mWidth;
        private SerializedProperty mSliceSideWidth;
        private SerializedProperty mSliceTopWidth;
        private SerializedProperty mCustomTopWidth;

        private SerializedProperty mLeftCircleSide;
        private SerializedProperty mRightCircleSide;
        private SerializedProperty mUseCircleSide;
        private SerializedProperty mCircleSideColor;
        private SerializedProperty mCircleSideTexture;
        private SerializedProperty mCircleSideRadius;
        private void OnEnable()
        {
            mSkillRangeType = serializedObject.FindProperty("Type");
            mProgress = serializedObject.FindProperty("Progress");
            mStartProgress = serializedObject.FindProperty("StartProgress");
            mActionTime = serializedObject.FindProperty("ActionTime");
            mFlushTime = serializedObject.FindProperty("FlushTime");
            mAngle = serializedObject.FindProperty("Angle");
            mRadius = serializedObject.FindProperty("Radius");
            mEnableEdge = serializedObject.FindProperty("EnableEdge");
            mEdgeAngle = serializedObject.FindProperty("EdgeAngle");
            mRadiusDiff = serializedObject.FindProperty("RadiusDiff");
            mGapWidth = serializedObject.FindProperty("GapWidth");
            mAngleDiff = serializedObject.FindProperty("AngleDiff");
            mLength = serializedObject.FindProperty("Length");
            mWidth = serializedObject.FindProperty("Width");
            mSliceSideWidth = serializedObject.FindProperty("SliceSideWidth");
            mSliceTopWidth = serializedObject.FindProperty("SliceTopWidth");
            mCustomTopWidth = serializedObject.FindProperty("CustomTopWidth");

            mLeftCircleSide = serializedObject.FindProperty("LeftCircleSide");
            mRightCircleSide = serializedObject.FindProperty("RightCircleSide");
            mUseCircleSide = serializedObject.FindProperty("UseCircleSide");
            mCircleSideColor = serializedObject.FindProperty("CircleSideColor");
            mCircleSideTexture = serializedObject.FindProperty("CircleSideTexture");
            mCircleSideRadius = serializedObject.FindProperty("CircleSideRadius");
        }

        public override void OnInspectorGUI()
        {
            EsSkillRangeVisual skillRangeVisual = target as EsSkillRangeVisual;
            if (skillRangeVisual == null)
                return;
            EditorGUI.BeginChangeCheck();
            if (!Application.isPlaying)
            {
                EditorGUILayout.PropertyField(mProgress, new GUIContent("进度"));
                EditorGUILayout.PropertyField(mStartProgress, new GUIContent("内圈大小(用于圆环预警圈)"));
            }
            EditorGUILayout.PropertyField(mSkillRangeType, new GUIContent("类型"));
            EditorGUILayout.PropertyField(mActionTime, new GUIContent("时间"));
            EditorGUILayout.PropertyField(mFlushTime, new GUIContent("闪烁时间(结束前开始倒数)"));
            if ((SkillRangeType)mSkillRangeType.enumValueIndex == SkillRangeType.Circle)
            {
                EditorGUILayout.PropertyField(mAngle, new GUIContent("角度"));
                EditorGUILayout.PropertyField(mRadius, new GUIContent("半径"));
                EditorGUILayout.PropertyField(mEnableEdge, new GUIContent("使用边缘"));
                if (mEnableEdge.boolValue)
                {
                    EditorGUILayout.PropertyField(mEdgeAngle, new GUIContent("边缘宽度"));
                    EditorGUILayout.PropertyField(mRadiusDiff, new GUIContent("弧边边缘间隙位置"));
                    EditorGUILayout.PropertyField(mGapWidth, new GUIContent("弧边边缘间隙宽度"));
                    EditorGUILayout.PropertyField(mAngleDiff, new GUIContent("侧边边缘间隙宽度"));
                }

                EditorGUILayout.PropertyField(mUseCircleSide,new GUIContent("使用外轮廓样式"));
                if (mUseCircleSide.boolValue)
                {
                    EditorGUILayout.PropertyField(mLeftCircleSide, new GUIContent("左轮廓"));
                    EditorGUILayout.PropertyField(mRightCircleSide, new GUIContent("右轮廓"));
                    EditorGUILayout.PropertyField(mCircleSideColor, new GUIContent("轮廓颜色"));
                    EditorGUILayout.PropertyField(mCircleSideTexture, new GUIContent("轮廓纹理"));
                    EditorGUILayout.PropertyField(mCircleSideRadius, new GUIContent("轮廓半径"));


                }

            }
            else
            {
                EditorGUILayout.PropertyField(mLength, new GUIContent("长度"));
                EditorGUILayout.PropertyField(mWidth, new GUIContent("宽度"));
                EditorGUILayout.PropertyField(mSliceSideWidth, new GUIContent("两侧宽度"));
                EditorGUILayout.PropertyField(mCustomTopWidth, new GUIContent("自定义顶部宽度"));
                if (mCustomTopWidth.boolValue)
                {
                    EditorGUILayout.PropertyField(mSliceTopWidth, new GUIContent("顶部宽度"));
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/特效/技能预警框", false, 0)]
        private static void CreateSkillRange()
        {
            EsSkillRangeVisual.CreateSkillRange();
        }
    }
}