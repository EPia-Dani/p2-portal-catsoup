using UnityEngine;

[DisallowMultipleComponent]
public class Portal : MonoBehaviour {
	[SerializeField] public Portal linkedPortal;
	[SerializeField] private Camera portalCamera;
	[SerializeField] private RenderTexture renderTexture;
	[SerializeField] private Material portalMaterial;
	[SerializeField] private Camera mainCamera;

	private void LateUpdate() {
		// Build portal camera transformation matrix
		Matrix4x4 m = linkedPortal.transform.localToWorldMatrix
		              * Matrix4x4.Scale(new Vector3(-1f, 1f, -1f))
		              * transform.worldToLocalMatrix
		              * mainCamera.transform.localToWorldMatrix;
		
		// Set portal camera position and rotation from matrix
		portalCamera.transform.SetPositionAndRotation(m.GetPosition(), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));

		// Transform portal plane to camera space for oblique projection 
		Matrix4x4 worldToCam = portalCamera.worldToCameraMatrix;
		Transform linkedTransform = linkedPortal.transform;
		Vector3 camSpacePos = worldToCam.MultiplyPoint(linkedTransform.position);
		Vector3 camSpaceNormal = worldToCam.MultiplyVector(linkedTransform.forward).normalized;
		camSpaceNormal *= -Mathf.Sign(Mathf.Max(Vector3.Dot(camSpaceNormal, Vector3.forward), 0.001f));

		// Create clip plane and apply oblique projection
		Vector4 clipPlane = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, -Vector3.Dot(camSpacePos, camSpaceNormal));
		portalCamera.projectionMatrix = portalCamera.nonJitteredProjectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
		portalCamera.Render();
	}

	public void PlaceOn(RaycastHit hit) {
		transform.position = hit.point + hit.normal * 0.02f;
		Vector3 f = -hit.normal;	
		Vector3 u = Vector3.Cross(f, Vector3.ProjectOnPlane(mainCamera.transform.right, f)).normalized;	
		transform.rotation = Quaternion.LookRotation(f, u);
	}
}