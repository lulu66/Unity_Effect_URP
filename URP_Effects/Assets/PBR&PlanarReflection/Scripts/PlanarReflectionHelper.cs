using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PlanarReflectionHelper
{

    public static Vector4 GetPlaneExpression(Transform plane)
    {
        var normal = plane.up;
        var d = -Vector3.Dot(normal, plane.position);
        var pplane = new Vector4(normal.x, normal.y, normal.z, d);
        return pplane;
    }

    public static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 reflectionMat = Matrix4x4.identity;
        reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
        reflectionMat.m01 = (-2F * plane[0] * plane[1]);
        reflectionMat.m02 = (-2F * plane[0] * plane[2]);
        reflectionMat.m03 = (-2F * plane[3] * plane[0]);

        reflectionMat.m10 = (-2F * plane[1] * plane[0]);
        reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
        reflectionMat.m12 = (-2F * plane[1] * plane[2]);
        reflectionMat.m13 = (-2F * plane[3] * plane[1]);

        reflectionMat.m20 = (-2F * plane[2] * plane[0]);
        reflectionMat.m21 = (-2F * plane[2] * plane[1]);
        reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
        reflectionMat.m23 = (-2F * plane[3] * plane[2]);

        reflectionMat.m30 = 0F;
        reflectionMat.m31 = 0F;
        reflectionMat.m32 = 0F;
        reflectionMat.m33 = 1F;

        return reflectionMat;

    }


    //计算camera space plane
    public static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float clipPlanarOffset, float sideSign)
    {
        var offsetPos = pos + normal * clipPlanarOffset;
        var m = cam.worldToCameraMatrix;
        var cameraPosition = m.MultiplyPoint(offsetPos);
        var cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    public static Matrix4x4 CalculateObliqueMatrix(Vector4 plane, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
    {

		var viewSpacePlane = viewMatrix.inverse.transpose * plane;
		var clipSpaceFarPanelBoundPoint = new Vector4(Mathf.Sign(viewSpacePlane.x), Mathf.Sign(viewSpacePlane.y), 1, 1);
		var viewSpaceFarPanelBoundPoint = projectionMatrix.inverse * clipSpaceFarPanelBoundPoint;

		var m4 = new Vector4(projectionMatrix.m30, projectionMatrix.m31, projectionMatrix.m32, projectionMatrix.m33);
		var u = 2.0f / Vector4.Dot(viewSpaceFarPanelBoundPoint, viewSpacePlane);
		var newViewSpaceNearPlane = u * viewSpacePlane;

		var m3 = newViewSpaceNearPlane - m4;

		projectionMatrix.m20 = m3.x;
		projectionMatrix.m21 = m3.y;
		projectionMatrix.m22 = m3.z;
		projectionMatrix.m23 = m3.w;

		return projectionMatrix;

	}

    public static Matrix4x4 CalculateObliqueMatrix(Camera cam, Vector4 plane)
    {
        Vector4 Q_clip = new Vector4(Mathf.Sign(plane.x), Mathf.Sign(plane.y), 1f, 1f);
        Vector4 Q_view = cam.projectionMatrix.inverse.MultiplyPoint(Q_clip);

        Vector4 scaled_plane = plane * 2.0f / Vector4.Dot(plane, Q_view);
        Vector4 M3 = scaled_plane - cam.projectionMatrix.GetRow(3);

        Matrix4x4 new_M = cam.projectionMatrix;
        new_M.SetRow(2, M3);

        return new_M;
    }

    public static Matrix4x4 GetViewMatrix(Transform transform, Transform cameraTransform)
    {
        var reflectCamPosWS = GetReflectionCameraPos(transform, cameraTransform);
        var reflectCamRotWS = GetReflectionCameraRot(transform, cameraTransform);
        return Matrix4x4.TRS(reflectCamPosWS, reflectCamRotWS, new Vector3(1, -1, -1)).inverse;
    }

    public static Vector3 GetReflectionCameraPos(Transform transform, Transform cameraTransform)
    {
        // 将相机移转换到平面空间 plane space，再通过平面对称创建反射相机
        Vector3 camPosPS = transform.worldToLocalMatrix.MultiplyPoint(cameraTransform.position);
        Vector3 reflectCamPosPS = Vector3.Scale(camPosPS, new Vector3(1, -1, 1));  // 反射相机平面空间
        Vector3 reflectCamPosWS = transform.localToWorldMatrix.MultiplyPoint(reflectCamPosPS);  // 将反射相机转换到世界空间

        return reflectCamPosWS;
    }

    public static Quaternion GetReflectionCameraRot(Transform transform, Transform cameraTransform)
    {
        // 设置反射相机方向
        Vector3 camForwardPS = transform.worldToLocalMatrix.MultiplyVector(cameraTransform.forward);
        Vector3 reflectCamForwardPS = Vector3.Scale(camForwardPS, new Vector3(1, -1, 1));
        Vector3 reflectCamForwardWS = transform.localToWorldMatrix.MultiplyVector(reflectCamForwardPS);

        Vector3 camUpPS = transform.worldToLocalMatrix.MultiplyVector(cameraTransform.up);
        Vector3 reflectCamUpPS = Vector3.Scale(camUpPS, new Vector3(-1, 1, -1));
        Vector3 reflectCamUpWS = transform.localToWorldMatrix.MultiplyVector(reflectCamUpPS);
        var reflectCamRotWS = Quaternion.LookRotation(reflectCamForwardWS, reflectCamUpWS);

        return reflectCamRotWS;
    }
    public static Matrix4x4 GetViewMat(Transform transform, Vector3 oldPos, Quaternion oldRot)
    {
        var newPos = mirrorPos(transform, oldPos);

        var newRot = mirrorRot(transform, oldRot);

        return Matrix4x4.TRS(newPos, newRot, new Vector3(-1, 1, -1)).inverse;
    }


    public static Quaternion mirrorRot(Transform transform, Quaternion cam)
    {
        var up = transform.up;
        var reflect = Vector3.Reflect(cam * Vector3.forward, up);
        var reflectup = Vector3.Reflect(cam * Vector3.up, up);

        return Quaternion.LookRotation(reflect, reflectup);
    }

    public static Vector3 mirrorPos(Transform transform, Vector3 oldPos)
    {
        var normal = transform.up;
        var d = -Vector3.Dot(normal, transform.position);

        return oldPos - 2 * (Vector3.Dot(oldPos, normal) + d) * normal;
    }
}
