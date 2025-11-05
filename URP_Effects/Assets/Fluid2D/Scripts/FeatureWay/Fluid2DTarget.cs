using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fluid2D
{
    public class Fluid2DTarget : MonoBehaviour
    {
        public Material SplatMat;
        public Transform[] FootTransforms;
        // 交互物的大小缩放
        [Range(0.01f, 0.1f)]
        public float Scale = 0.05f;

        [Header("线速度相关")]
        [Range(0f, 20f)]
        public float MaxRelativeVelocity = 10f;
        public Vector3 VelocityScale = Vector3.one;
        [Range(0f, 1f)]
        public float VelocityWeight = 1f;
        public Texture2D velocityTexture;

        [Header("旋转速度相关")]
        [Range(0f, 20f)]
        public float MaxRelativeAngularVelocity = 10f;
        [Range(0f, 20f)]
        public float AngularVelocityScale = 1;

        [Header("外力")]
        public Vector3 Force;
        public float Torque;

        [Header("密度相关")]
        [Range(0f, 1f)]
        public float DensityWeight = 1f;
        public Texture2D DensityTexture;
        public Color SplatWeight;

        //[Header("噪声相关")]
        //public Texture2D NoiseTexture;
        //[Range(0f, 1f)]
        //public float VelocityNoise = 0;
        //[Range(-1f, 1f)]
        //public float VelocityNoiseOffset = 0;
        //[Range(0f, 10f)]
        //public float VelocityNoiseTilling = 1;
        //[Range(0f, 1f)]
        //public float DensityNoise = 0;
        //[Range(-1f, 1f)]
        //public float DensityNoiseOffset = 0;
        //[Range(0f, 10f)]
        //public float DensityNoiseTilling = 1;

        private List<Quaternion> oldRotation;
        private List<Vector3> oldPosition;

        private List<Vector3> velocities;
        private List<Vector3> angularVelocities;

        private List<Vector4> linearVelsInUV;
        private List<Vector4> angularVelsInUV;
        private List<float> relativeAngularVels;

        private Vector4 velocityNoiseParam = Vector4.zero;
        private Vector4 densityNoiseParam = Vector4.zero;
        private Vector4 splatWeight = Vector4.zero;

        private Vector4 projectionTarget = Vector4.zero;
        private Vector3 fluidPlaneSize;
        private List<Vector4> PositionAndSize;

        private Vector4 tempParams = Vector4.zero;

        public List<Material> SplatInstanceMats;
        // 交互物的位置和大小
        public List<Vector4> SplatPositionSizes
        {
            get
            {
                return PositionAndSize;
            }
        }

        public List<Vector4> SplatLinearVelocities
        {
            get
            {
                return linearVelsInUV;
            }
        }

        public List<float> SplatAnularVelocities
        {
            get
            {
                return relativeAngularVels;
            }
        }
        // 线速度
        public void CalVelocity()
        {
            for (int i = 0; i < velocities.Count; i++)
            {
                velocities[i] = (FootTransforms[i].position - oldPosition[i]) / Time.deltaTime;
            }
        }

        // 角速度
        public void CalAngularVelocity()
        {
            for (int i = 0; i < angularVelocities.Count; i++)
            {
                Quaternion rotationDelta = FootTransforms[i].rotation * Quaternion.Inverse(oldRotation[i]);
                angularVelocities[i] = new Vector3(rotationDelta.x, rotationDelta.y, rotationDelta.z) * 2.0f / Time.deltaTime;
            }
        }

        private void OnEnable()
        {

            if(oldPosition == null)
			{
                oldPosition = new List<Vector3>(FootTransforms.Length);

            }
            if (oldRotation == null)
            {
                oldRotation = new List<Quaternion>(FootTransforms.Length);

            }
            if (PositionAndSize == null)
            {
                PositionAndSize = new List<Vector4>(FootTransforms.Length);

            }
            if (velocities == null)
            {
                velocities = new List<Vector3>(FootTransforms.Length);

            }
            if (angularVelocities == null)
            {
                angularVelocities = new List<Vector3>(FootTransforms.Length);

            }
            if (relativeAngularVels == null)
            {
                relativeAngularVels = new List<float>(FootTransforms.Length);

            }
            if (linearVelsInUV == null)
            {
                linearVelsInUV = new List<Vector4>(FootTransforms.Length);

            }
            if (angularVelsInUV == null)
            {
                angularVelsInUV = new List<Vector4>(FootTransforms.Length);

            }
            if (SplatInstanceMats == null)
            {
                SplatInstanceMats = new List<Material>(FootTransforms.Length);

            }

            for (int i = 0; i < FootTransforms.Length; i++)
            {
                oldPosition.Add(FootTransforms[i].position);
                oldRotation.Add(FootTransforms[i].rotation);

                PositionAndSize.Add(Vector4.zero);
                velocities.Add(Vector3.zero);
                angularVelocities.Add(Vector3.zero);
                relativeAngularVels.Add(0);
                linearVelsInUV.Add(Vector4.zero);
                angularVelsInUV.Add(Vector4.zero);

                SplatInstanceMats.Add(new Material(SplatMat));
            }

        }

		private void OnDisable()
		{
            oldPosition.Clear();
            oldRotation.Clear();
            PositionAndSize.Clear();
            velocities.Clear();
            angularVelocities.Clear();
            relativeAngularVels.Clear();
            linearVelsInUV.Clear();
            angularVelsInUV.Clear();

            if(SplatInstanceMats != null)
			{
                foreach(var mat in SplatInstanceMats)
				{
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(mat);
#else
			        Destroy(mat);
#endif
                }
                SplatInstanceMats.Clear();
                SplatInstanceMats = null;
            }
		}

		public void Splat(Transform fluidPlaneTransform, Vector4 rect)
        {
            if (SplatMat == null) return;

            // 计算uv空间的线速度
            CalVelocity();
            WorldVectorToUVSpace(fluidPlaneTransform, velocities, linearVelsInUV, rect);

            for (int i = 0; i < linearVelsInUV.Count; i++)
            {
                float speed = linearVelsInUV[i].magnitude;
                if (speed > 0.001f)
                {
                    linearVelsInUV[i] /= speed;
                    linearVelsInUV[i] *= Mathf.Min(speed, MaxRelativeVelocity);
                }
                tempParams = Vector3.Scale(linearVelsInUV[i], VelocityScale) + Force;
                tempParams.w = VelocityWeight;
                if (float.IsNaN(tempParams.x)) tempParams.x = 0;
                if (float.IsNaN(tempParams.y)) tempParams.y = 0;
                if (float.IsNaN(tempParams.z)) tempParams.z = 0;
                linearVelsInUV[i] = tempParams;

            }

            // 计算uv空间的角速度
            CalAngularVelocity();
            WorldVectorToUVSpace(fluidPlaneTransform, angularVelocities, angularVelsInUV, rect);
            for (int i = 0; i < angularVelsInUV.Count; i++)
            {
                relativeAngularVels[i] = angularVelsInUV[i].z;
                relativeAngularVels[i] = Mathf.Clamp(relativeAngularVels[i], -MaxRelativeAngularVelocity, MaxRelativeAngularVelocity) * AngularVelocityScale;
                relativeAngularVels[i] += Torque;
                if (float.IsNaN(relativeAngularVels[i])) relativeAngularVels[i] = 0;

            }

            //SplatMat.SetTexture("_Noise", NoiseTexture);
            //velocityNoiseParam.x = VelocityNoise;
            //velocityNoiseParam.y = VelocityNoiseOffset;
            //velocityNoiseParam.z = VelocityNoiseTilling;

            //densityNoiseParam.x = DensityNoise;// = new Vector4(DensityNoise, DensityNoiseOffset, DensityNoiseTilling, 0);
            //densityNoiseParam.y = DensityNoiseOffset;
            //densityNoiseParam.z = DensityNoiseTilling;

            splatWeight.x = SplatWeight.r;
            splatWeight.y = SplatWeight.g;
            splatWeight.z = SplatWeight.b;
            splatWeight.w = SplatWeight.a * DensityWeight;

            // 更新交互物投影到流体平面的position和size
            for (int i = 0; i < FootTransforms.Length; i++)
            {
                var lossyScale = FootTransforms[i].lossyScale;
                float maxScale = Mathf.Max(lossyScale.x, Mathf.Max(lossyScale.y, lossyScale.z));
                float projectSize = Scale * maxScale;
                Vector3 targetPos = FootTransforms[i].position;
                PositionAndSize[i] = ProjectTarget(fluidPlaneTransform, targetPos, projectSize);
            }

            // 更新交互物的rotation（略，无自旋转）

            // 更新位移和旋转
            for (int i = 0; i < FootTransforms.Length; i++)
            {
                oldPosition[i] = FootTransforms[i].position;
                oldRotation[i] = FootTransforms[i].rotation;
            }

            // 需要传递的公共参数
            //SplatMat.SetTexture("_Noise", NoiseTexture);
            SplatMat.SetVector("_SplatWeight", splatWeight);
            //SplatMat.SetVector("_DensityNoiseParams", densityNoiseParam); // 密度图的tillingOffset
            //SplatMat.SetVector("_VelocityNoiseParams", velocityNoiseParam);
            SplatMat.SetTexture("_Velocity", velocityTexture);      // 额外的速度图
        }


        private void WorldVectorToUVSpace(Transform fluidPlaneTransform, List<Vector3> vector, List<Vector4> targets, Vector4 uvRect)
        {
            fluidPlaneSize = fluidPlaneTransform.localScale;

            for (int i = 0; i < targets.Count; i++)
            {
                if (fluidPlaneSize.x <= 0.001 || fluidPlaneSize.y <= 0.001)
                {
                    targets[i] = Vector3.zero;
                }

                var v = fluidPlaneTransform.InverseTransformVector(vector[i]);

                //v.x *= uvRect.z / fluidPlaneSize.x;
                //v.y *= uvRect.w / fluidPlaneSize.y;

                v.x *= uvRect.z;
                v.y *= uvRect.w;

                targets[i] = v;

            }

        }

        private Vector4 ProjectTarget(Transform fluidPlaneTransform, Vector3 targetPos, float projectSize)
        {

            // 投影外物的位置到流体平面上：原则上要将外物的位置通过射线检测投影的流体平面上，但是这里做一个简化，因为平面是水平的，那么外物在流体平面的投影其实可以简单的将y=plane.y;
            var targetPoint = targetPos;
            targetPoint.y = fluidPlaneTransform.position.y;

            // 将外物位置转换到流体平面的局部坐标系当中
            //var localTargetPos = fluidPlaneTransform.InverseTransformPoint(targetPoint) / (Vector2)fluidPlaneSize;
            var localTargetPos = fluidPlaneTransform.InverseTransformPoint(targetPoint);

            // 这里原本是创建另外一条从流体平面底下到目标位置右侧的一条射线，然后通过与平面交点，求得一个相交位置，并转换到流体平面局部坐标系中，和目标位置的投影位置计算一个距离来作为一个合适的投影大小的缩放值
            // 这里简化掉，直接通过参数调节scale即可

            projectionTarget.x = localTargetPos.x;
            projectionTarget.y = localTargetPos.y;
            projectionTarget.z = projectSize;
            projectionTarget.w = projectSize;

            return projectionTarget;

        }
    }

}
