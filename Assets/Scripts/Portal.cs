using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Portal - camera transform calculation and rendering.
/// </summary>
public class Portal : MonoBehaviour {
	[Header("Portal Link")] [Tooltip("El otro portal al que este está conectado")] [SerializeField]
	public Portal linkedPortal;

	[Header("Rendering Setup")]
	[Tooltip("La cámara que renderiza lo que se ve a través de ESTE portal (debe tener su propia RenderTexture)")]
	[SerializeField]
	private Camera portalCamera;

	[Tooltip(
		"La RenderTexture a la que renderiza esta cámara (debe estar asignada también en portalCamera.targetTexture)")]
	[SerializeField]
	private RenderTexture renderTexture;

	[Tooltip("El material de ESTE portal que muestra la vista (opcional, solo para referencia)")] [SerializeField]
	private Material portalMaterial;

	[Header("Settings")] [SerializeField] private LayerMask cullingMask = ~0;

	static readonly Matrix4x4 Yaw180 = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

	private Camera _mainCamera;


	private void Awake() {_mainCamera = Camera.main;}

	private void LateUpdate() {
		Matrix4x4 m = linkedPortal.transform.localToWorldMatrix
		              * Yaw180
		              * transform.worldToLocalMatrix
		              * _mainCamera.transform.localToWorldMatrix;

		// Extract position and rotation from the matrix
		Vector3 pos = m.MultiplyPoint3x4(Vector3.zero);
		Vector3 fwd = m.MultiplyVector(Vector3.forward);
		Vector3 up = m.MultiplyVector(Vector3.up);
		portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

		// Copy camera properties from main camera
		portalCamera.usePhysicalProperties = _mainCamera.usePhysicalProperties;
		portalCamera.sensorSize = _mainCamera.sensorSize;
		portalCamera.focalLength = _mainCamera.focalLength;
		portalCamera.gateFit = _mainCamera.gateFit;
		portalCamera.lensShift = _mainCamera.lensShift;

		portalCamera.orthographic = _mainCamera.orthographic;
		portalCamera.orthographicSize = _mainCamera.orthographicSize;
		portalCamera.aspect = _mainCamera.aspect;

		// Set up oblique projection for proper clipping at the portal plane
		SetupObliqueProjection(_mainCamera);
		portalCamera.Render();
	}


	private void SetupObliqueProjection(Camera mainCamera) {
		// Get the portal plane in world space
		// The plane is defined by the linked portal's position and its forward direction
		Vector3 planePos = linkedPortal.transform.position;
		Vector3 planeNormal = linkedPortal.transform.forward;
		// Transform plane to camera space
		Vector3 camSpacePos = portalCamera.worldToCameraMatrix.MultiplyPoint(planePos);
		Vector3 camSpaceNormal = portalCamera.worldToCameraMatrix.MultiplyVector(planeNormal).normalized;
		// Ensure the normal points away from the camera
		float dot = Vector3.Dot(camSpaceNormal, Vector3.forward);
		if (dot > 0) {
			camSpaceNormal = -camSpaceNormal;
		}

		// Create clip plane in camera space (plane equation: ax + by + cz + d = 0)
		Vector4 clipPlane = new Vector4(
			camSpaceNormal.x,
			camSpaceNormal.y,
			camSpaceNormal.z,
			-Vector3.Dot(camSpacePos, camSpaceNormal)
		);
		// Calculate oblique projection matrix
		Matrix4x4 projection = mainCamera.projectionMatrix;
		Matrix4x4 obliqueProjection = CalculateObliqueMatrix(projection, clipPlane);

		portalCamera.projectionMatrix = obliqueProjection;
		portalCamera.nonJitteredProjectionMatrix = obliqueProjection;
	}


	private Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane) {
		Vector4 q = projection.inverse * new Vector4(
			Mathf.Sign(clipPlane.x),
			Mathf.Sign(clipPlane.y),
			1.0f,
			1.0f
		);
		Vector4 c = clipPlane * (2.0f / Vector4.Dot(clipPlane, q));

		// Replace the third row of the projection matrix
		projection[2] = c.x - projection[3];
		projection[6] = c.y - projection[7];
		projection[10] = c.z - projection[11];
		projection[14] = c.w - projection[15];

		return projection;
	}

	public void PlaceOn(RaycastHit hit){
	transform.position = hit.point + hit.normal * 0.02f;
	Vector3 f = -hit.normal, u = Vector3.Normalize(Vector3.Cross(f, Vector3.ProjectOnPlane(Camera.main.transform.right, f)));
	transform.rotation = Quaternion.LookRotation(f, u);
}
}