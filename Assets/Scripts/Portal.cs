using UnityEngine;

/// <summary>
/// Portal - camera transform calculation and rendering.
/// </summary>
public class Portal : MonoBehaviour
{
	[Header("Portal Link")]
	[Tooltip("El otro portal al que este está conectado")]
	[SerializeField] public Portal linkedPortal;

	[Header("Rendering Setup")]
	[Tooltip("La cámara que renderiza lo que se ve a través de ESTE portal (debe tener su propia RenderTexture)")]
	[SerializeField] private Camera portalCamera;
	
	[Tooltip("La RenderTexture a la que renderiza esta cámara (debe estar asignada también en portalCamera.targetTexture)")]
	[SerializeField] private RenderTexture renderTexture;
	
	[Tooltip("El material de ESTE portal que muestra la vista (opcional, solo para referencia)")]
	[SerializeField] private Material portalMaterial;

	[Header("Settings")]
	[SerializeField] private LayerMask cullingMask = ~0;

	static readonly Matrix4x4 Yaw180 = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

	private void Start()
	{
		// Asegurar que la cámara use la RenderTexture correcta
		if (portalCamera && renderTexture)
		{
			portalCamera.targetTexture = renderTexture;
		}
	}

	private void LateUpdate()
	{
		var main = Camera.main;
		if (!main || !linkedPortal || !portalCamera) return;

		// Update THIS portal's camera to show the view from the LINKED portal
		UpdatePortalCamera(main);

		// Render this portal's view
		if (renderTexture && renderTexture.IsCreated())
		{
			portalCamera.Render();
		}
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		// Sincronizar la RenderTexture con la cámara cuando cambies valores en el editor
		if (portalCamera && renderTexture)
		{
			portalCamera.targetTexture = renderTexture;
		}
	}
#endif

	/// <summary>
	/// Positions this portal's camera at the linked portal, mirroring the main camera's relative position
	/// </summary>
	private void UpdatePortalCamera(Camera mainCamera)
	{
		// Transform chain: 
		// 1. Get main camera position relative to THIS portal
		// 2. Flip 180 degrees (portal reversal)
		// 3. Apply that relative transform from the LINKED portal's position
		Matrix4x4 m = linkedPortal.transform.localToWorldMatrix
		            * Yaw180
		            * transform.worldToLocalMatrix
		            * mainCamera.transform.localToWorldMatrix;

		// Extract position and rotation from the matrix
		Vector3 pos = m.MultiplyPoint3x4(Vector3.zero);
		Vector3 fwd = m.MultiplyVector(Vector3.forward);
		Vector3 up  = m.MultiplyVector(Vector3.up);
		portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

		// Copy camera properties from main camera
		portalCamera.projectionMatrix            = mainCamera.projectionMatrix;
		portalCamera.nonJitteredProjectionMatrix = mainCamera.nonJitteredProjectionMatrix;

		portalCamera.usePhysicalProperties = mainCamera.usePhysicalProperties;
		portalCamera.sensorSize            = mainCamera.sensorSize;
		portalCamera.focalLength           = mainCamera.focalLength;
		portalCamera.gateFit               = mainCamera.gateFit;
		portalCamera.lensShift             = mainCamera.lensShift;

		portalCamera.orthographic      = mainCamera.orthographic;
		portalCamera.orthographicSize  = mainCamera.orthographicSize;
		portalCamera.aspect            = mainCamera.aspect;
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
