using UnityEngine;

/// <summary>
/// Portal - camera transform calculation and rendering.
/// Setup in editor: assign camera and RenderTexture targetTexture.
/// </summary>
public class Portal : MonoBehaviour
{
	[SerializeField] public Portal linkedPortal;
	[SerializeField] private Camera viewCamera;
	[SerializeField] private LayerMask cullingMask = ~0;

	static readonly Matrix4x4 Yaw180 = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

	private void LateUpdate()
	{
		var main = Camera.main;
		if (!main || !linkedPortal || !viewCamera) return;

		// Calculate view camera transform: B * Yaw180 * A^-1 * Main
		Matrix4x4 m = linkedPortal.transform.localToWorldMatrix
		            * Yaw180
		            * transform.worldToLocalMatrix
		            * main.transform.localToWorldMatrix;

		Vector3 pos = m.MultiplyPoint3x4(Vector3.zero);
		Vector3 fwd = m.MultiplyVector(Vector3.forward);
		Vector3 up  = m.MultiplyVector(Vector3.up);
		viewCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

		

		viewCamera.projectionMatrix            = main.projectionMatrix;
		viewCamera.nonJitteredProjectionMatrix = main.nonJitteredProjectionMatrix;

		viewCamera.usePhysicalProperties = main.usePhysicalProperties;
		viewCamera.sensorSize            = main.sensorSize;
		viewCamera.focalLength           = main.focalLength;
		viewCamera.gateFit               = main.gateFit;
		viewCamera.lensShift             = main.lensShift;

		viewCamera.orthographic      = main.orthographic;
		viewCamera.orthographicSize  = main.orthographicSize;
		viewCamera.aspect            = main.aspect; // o (float)rt.width/rt.height si usas RT


		// Render
		if (viewCamera.targetTexture && viewCamera.targetTexture.IsCreated())
		{
			viewCamera.Render();
		}
	}

	public void PlaceOn(RaycastHit hit)
	{
		transform.position = hit.point + hit.normal * 0.02f;
		transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!linkedPortal) return;

		Rigidbody rb = other.attachedRigidbody;
		if (!rb) return;

		// Mirror position across portals
		Vector3 relativePos = transform.InverseTransformPoint(rb.position);
		relativePos.z = -relativePos.z;
		rb.position = linkedPortal.transform.TransformPoint(relativePos);

		// Mirror velocity
		Vector3 relativeVel = transform.InverseTransformDirection(rb.linearVelocity);
		relativeVel.z = -relativeVel.z;
		rb.linearVelocity = linkedPortal.transform.TransformDirection(relativeVel);
	}
}
