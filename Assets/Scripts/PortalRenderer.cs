using UnityEngine;

public class PortalRenderer : MonoBehaviour {
	[SerializeField] public PortalRenderer pair;
	[SerializeField] private Camera cam;
	[SerializeField] private int recursionLimit = 5;

	[SerializeField] private MeshRenderer portalMeshRenderer;
	private MaterialPropertyBlock propertyBlock;
	private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");

	private Camera mainCam;

	private void Awake() {
		mainCam = Camera.main;
		if (portalMeshRenderer == null) {
			portalMeshRenderer = GetComponentInChildren<MeshRenderer>(true);
		}
		propertyBlock = new MaterialPropertyBlock();
	}

	public void SetCircleRadius(float radius) {
		if (portalMeshRenderer == null) return;
		portalMeshRenderer.GetPropertyBlock(propertyBlock);
		propertyBlock.SetFloat(CircleRadiusId, radius);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);
	}

	private void LateUpdate() {
		// Store camera positions and rotations for each recursion level
		Vector3[] renderPositions = new Vector3[recursionLimit];
		Quaternion[] renderRotations = new Quaternion[recursionLimit];

		// Start with the main camera's transform
		Matrix4x4 localToWorldMatrix = mainCam.transform.localToWorldMatrix;
		
		// Calculate transformations for each recursion level
		for (int i = 0; i < recursionLimit; i++) {
			// Build portal camera transformation matrix through both portals
			localToWorldMatrix = pair.transform.localToWorldMatrix // Transform to pair portal's space
			                     * Matrix4x4.Scale(new Vector3(-1f, 1f, -1f)) // Flip to face player
			                     * transform.worldToLocalMatrix // Transform through this portal
			                     * localToWorldMatrix; // From previous camera position

			// Store in reverse order (furthest recursion first)
			int renderOrderIndex = recursionLimit - i - 1;
			renderPositions[renderOrderIndex] = localToWorldMatrix.GetPosition();
			renderRotations[renderOrderIndex] = Quaternion.LookRotation(
				localToWorldMatrix.GetColumn(2), 
				localToWorldMatrix.GetColumn(1)
			);
		}

		// Render from furthest to nearest recursion
		for (int i = 0; i < recursionLimit; i++) {
			// Set portal camera position and rotation
			cam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);

			// Transform portal plane to camera space for oblique projection
			Vector3 normal = -cam.worldToCameraMatrix.MultiplyVector(pair.transform.forward).normalized;

			// Create clip plane and apply oblique projection
			Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, 
				-Vector3.Dot(cam.worldToCameraMatrix.MultiplyPoint(pair.transform.position), normal));
			
			cam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);
			cam.Render();
		}
	}
}