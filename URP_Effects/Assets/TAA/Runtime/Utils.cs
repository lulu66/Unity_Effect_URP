using UnityEngine;

namespace Naiwen.TAA
{
    public static class Utils
    {
        const int k_SampleCount = 8;

        public static int sampleIndex { get; private set; }

        // 使用了一种非常聪明的准随机序列方法来生成"随机"偏移，主要用于阴影采样或其他需要随机采样的图形学应用
        // 范围[-0.5, 0.5]，均匀分布在零点周围
        public static Vector2 GenerateRandomOffset()
        {
            // The variance between 0 and the actual halton sequence values reveals noticeable instability
            // in Unity's shadow maps, so we avoid index 0.
            // 哈尔顿序列(一种低差异序列)
            var offset = new Vector2(
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 2) - 0.5f,
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 3) - 0.5f
                );

            if (++sampleIndex >= k_SampleCount)
                sampleIndex = 0;

            return offset;
        }
        /// <summary>
        /// Gets a jittered orthographic projection matrix for a given camera.
        /// 生成带有抖动偏移的正交投影矩阵
        /// </summary>
        /// <param name="camera">The camera to build the orthographic matrix for</param>
        /// <param name="offset">The jitter offset</param>
        /// <returns>A jittered projection matrix</returns>
        public static Matrix4x4 GetJitteredOrthographicProjectionMatrix(Camera camera, Vector2 offset)
        {
            // 世界单位的摄象机垂直和水平方向的大小
            float vertical = camera.orthographicSize;
            float horizontal = vertical * camera.aspect;

            // 世界空间下的像素的偏移
            offset.x *= horizontal / (0.5f * camera.pixelWidth);
            offset.y *= vertical / (0.5f * camera.pixelHeight);

            // 计算抖动的视锥体边界
            // 原始边界：left = -horizontal; right = +horizontal; top = +vertical; bottom = -vertical
            float left = offset.x - horizontal;
            float right = offset.x + horizontal;
            float top = offset.y + vertical;
            float bottom = offset.y - vertical;

            // 生成正交投影矩阵
            return Matrix4x4.Ortho(left, right, bottom, top, camera.nearClipPlane, camera.farClipPlane);
        }

        /// <summary>
        /// Gets a jittered perspective projection matrix for a given camera.
        /// 生成带有抖动偏移的透视投影矩阵
        /// </summary>
        /// <param name="camera">The camera to build the projection matrix for</param>
        /// <param name="offset">The jitter offset</param>
        /// <returns>A jittered projection matrix</returns>
        public static Matrix4x4 GetJitteredPerspectiveProjectionMatrix(Camera camera, Vector2 offset)
        {
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;

            float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView) * near;
            float horizontal = vertical * camera.aspect;

            offset.x *= horizontal / (0.5f * camera.pixelWidth);
            offset.y *= vertical / (0.5f * camera.pixelHeight);

            var matrix = camera.projectionMatrix;

            // 此处修改矩阵，表示偏移量是与深度相关的。offset.x / horizontal：归一化偏移比例，这意味着在近裁剪面和远裁剪面的偏移量是不一样的，越远，偏移量会按比例放大
            // 视锥体被剪切
            matrix[0, 2] += offset.x / horizontal;
            matrix[1, 2] += offset.y / vertical;

            return matrix;
        }


    }


}