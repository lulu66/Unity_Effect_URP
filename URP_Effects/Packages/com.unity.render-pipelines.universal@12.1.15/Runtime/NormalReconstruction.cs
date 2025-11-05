namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Util class for normal reconstruction.
    /// </summary>
    public static class NormalReconstruction
    {
        private static readonly int s_NormalReconstructionMatrixID = Shader.PropertyToID("_NormalReconstructionMatrix");
        private static Matrix4x4[] s_NormalReconstructionMatrix = new Matrix4x4[2];

        /// <summary>
        /// Setup properties needed for normal reconstruction from depth using shader functions in NormalReconstruction.hlsl
        /// </summary>
        /// <param name="cmd">Command Buffer used for properties setup.</param>
        /// <param name="cameraData">CameraData containing camera matrices information.</param>
        public static void SetupProperties(CommandBuffer cmd, in CameraData cameraData)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            int eyeCount = cameraData.xr.enabled && cameraData.xr.singlePassEnabled ? 2 : 1;
#else
            int eyeCount = 1;
#endif
            for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
            {
                // 重建视图空间的位置和法线
                Matrix4x4 view = cameraData.GetViewMatrix(eyeIndex);
                Matrix4x4 proj = cameraData.GetProjectionMatrix(eyeIndex);
                s_NormalReconstructionMatrix[eyeIndex] = proj * view;

                // camera view space without translation, used by SSAO.hlsl ReconstructViewPos() to calculate view vector.
                Matrix4x4 cview = view;
                // 移出平移分量，只保留旋转,这表示相机被移动到原点，但是朝向不变
                cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                Matrix4x4 cviewProj = proj * cview;
                Matrix4x4 cviewProjInv = cviewProj.inverse;

                // 裁剪空间->视图空间，计算逆矩阵
                s_NormalReconstructionMatrix[eyeIndex] = cviewProjInv;
            }

            cmd.SetGlobalMatrixArray(s_NormalReconstructionMatrixID, s_NormalReconstructionMatrix);
        }
    }
}
