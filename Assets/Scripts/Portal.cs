using UnityEngine;

/// <summary>
/// Portal - camera transform calculation and rendering.
/// </summary>
[DisallowMultipleComponent]
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
		if (linkedPortal == null || portalCamera == null || _mainCamera == null) return;
		Matrix4x4 m = BuildPortalCameraMatrix();

		// Extract position and rotation directly from matrix columns
		Vector3 pos = m.GetColumn(3);
		Quaternion rot = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
		portalCamera.transform.SetPositionAndRotation(pos, rot);

		// Set up oblique projection for proper clipping at the portal plane
		SetupObliqueProjection();
		portalCamera.Render();
	}
	private Matrix4x4 BuildPortalCameraMatrix() {
		return linkedPortal.transform.localToWorldMatrix
		       * Yaw180
		       * transform.worldToLocalMatrix
		       * _mainCamera.transform.localToWorldMatrix;
	}

	private void SetupObliqueProjection() {
		// Get the portal plane in world space
		Transform linkedTransform = linkedPortal.transform;
		
		// Transform plane to camera space
		Matrix4x4 worldToCam = portalCamera.worldToCameraMatrix;
		Vector3 camSpacePos = worldToCam.MultiplyPoint(linkedTransform.position);
		Vector3 camSpaceNormal = worldToCam.MultiplyVector(linkedTransform.forward).normalized;
		
		// Ensure the normal points away from the camera (flip if pointing toward camera)
		if (Vector3.Dot(camSpaceNormal, Vector3.forward) > 0) {
			camSpaceNormal = -camSpaceNormal;
		}

		// Create clip plane in camera space (plane equation: ax + by + cz + d = 0)
		Vector4 clipPlane = new Vector4(
			camSpaceNormal.x,
			camSpaceNormal.y,
			camSpaceNormal.z,
			-Vector3.Dot(camSpacePos, camSpaceNormal)
		);
		
		// Calculate and apply oblique projection matrix
		Matrix4x4 obliqueProjection = CalculateObliqueMatrix(_mainCamera.projectionMatrix, clipPlane);
		portalCamera.projectionMatrix = obliqueProjection;
		portalCamera.nonJitteredProjectionMatrix = obliqueProjection;
	}

	private static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane) {
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
		Vector3 f = -hit.normal;
		Vector3 u = Vector3.Normalize(Vector3.Cross(f, Vector3.ProjectOnPlane(Camera.main.transform.right, f)));
		transform.rotation = Quaternion.LookRotation(f, u);
	}
}