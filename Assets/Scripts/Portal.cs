using UnityEngine;

public class Portal : MonoBehaviour {
	[SerializeField] public Portal pair;
	[SerializeField] private Camera cam;

	private void LateUpdate() {
		// Build portal camera transformation matrix
		Matrix4x4 m = pair.transform.localToWorldMatrix // Transform portal to world space from pair's local space
		              * Matrix4x4.Scale(new Vector3(-1f, 1f, -1f)) // Flip the portal to face player
		              * transform.worldToLocalMatrix 
		              * CameraManager.MainCamera.transform.localToWorldMatrix;
		// Set portal camera position and rotation from matrix
		cam.transform.SetPositionAndRotation(m.GetPosition(),
			Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
		// Transform portal plane to camera space for oblique projection 
		Vector3 pos = cam.worldToCameraMatrix.MultiplyPoint(pair.transform.position);
		Vector3 normal = -cam.worldToCameraMatrix.MultiplyVector(pair.transform.forward).normalized;
		// Create clip plane and apply oblique projection
		Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(pos, normal));
		cam.projectionMatrix = CameraManager.MainCamera.CalculateObliqueMatrix(clipPlane);
		cam.Render();
	}
}