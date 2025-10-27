using System.Collections.Generic;
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

	// Track objects currently traversing this portal
	private List<PortalTraveller> travellers = new List<PortalTraveller>();

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

		// Update all objects currently traversing this portal
		UpdateTravellers();

		// Render this portal's view
		if (renderTexture && renderTexture.IsCreated())
		{
			portalCamera.Render();
		}
	}

	private void UpdateTravellers()
	{
		for (int i = travellers.Count - 1; i >= 0; i--)
		{
			if (travellers[i] == null)
			{
				travellers.RemoveAt(i);
				continue;
			}

			travellers[i].UpdateTraveller(this);
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
		portalCamera.usePhysicalProperties = mainCamera.usePhysicalProperties;
		portalCamera.sensorSize            = mainCamera.sensorSize;
		portalCamera.focalLength           = mainCamera.focalLength;
		portalCamera.gateFit               = mainCamera.gateFit;
		portalCamera.lensShift             = mainCamera.lensShift;

		portalCamera.orthographic      = mainCamera.orthographic;
		portalCamera.orthographicSize  = mainCamera.orthographicSize;
		portalCamera.aspect            = mainCamera.aspect;

		// Set up oblique projection for proper clipping at the portal plane
		SetupObliqueProjection(mainCamera);
	}

	/// <summary>
	/// Sets up an oblique projection matrix that clips at the linked portal's plane.
	/// This prevents rendering objects behind the portal surface.
	/// </summary>
	private void SetupObliqueProjection(Camera mainCamera)
	{
		// Get the portal plane in world space
		// The plane is defined by the linked portal's position and its forward direction
		Vector3 planePos = linkedPortal.transform.position;
		Vector3 planeNormal = linkedPortal.transform.forward;

		// Transform plane to camera space
		Vector3 camSpacePos = portalCamera.worldToCameraMatrix.MultiplyPoint(planePos);
		Vector3 camSpaceNormal = portalCamera.worldToCameraMatrix.MultiplyVector(planeNormal).normalized;

		// Ensure the normal points away from the camera
		float dot = Vector3.Dot(camSpaceNormal, Vector3.forward);
		if (dot > 0)
		{
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

	/// <summary>
	/// Calculates an oblique projection matrix for a given clip plane.
	/// Based on: http://www.terathon.com/lengyel/Lengyel-Oblique.pdf
	/// </summary>
	private Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane)
	{
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

	public void PlaceOn(RaycastHit hit)
	{
		transform.position = hit.point + hit.normal * 0.02f;
		
		// Calcular orientación del portal basada en la normal de la superficie
		Vector3 portalForward = -hit.normal;
		Vector3 portalUp;
		
		// Si la superficie es casi horizontal (suelo/techo)
		if (Mathf.Abs(hit.normal.y) > 0.9f)
		{
			// En suelo/techo, usar la dirección forward de la cámara como referencia
			Camera cam = Camera.main;
			if (cam != null)
			{
				Vector3 camForward = cam.transform.forward;
				// Proyectar la dirección de la cámara en el plano de la superficie
				portalUp = Vector3.ProjectOnPlane(camForward, hit.normal).normalized;
				
				// Si la proyección es muy pequeña, usar otro vector
				if (portalUp.sqrMagnitude < 0.1f)
				{
					portalUp = Vector3.ProjectOnPlane(cam.transform.right, hit.normal).normalized;
				}
			}
			else
			{
				portalUp = Vector3.ProjectOnPlane(Vector3.forward, hit.normal).normalized;
			}
		}
		else
		{
			// En paredes verticales, mantener el portal vertical
			// Usar el vector up mundial pero proyectado en el plano de la pared
			portalUp = Vector3.ProjectOnPlane(Vector3.up, hit.normal).normalized;
			
			// Si el resultado es cero (pared perfectamente horizontal, caso raro), usar un fallback
			if (portalUp.sqrMagnitude < 0.1f)
			{
				portalUp = Vector3.ProjectOnPlane(Vector3.forward, hit.normal).normalized;
			}
		}
		
		transform.rotation = Quaternion.LookRotation(portalForward, portalUp);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!linkedPortal) return;

		// Try to get PortalTraveller component
		PortalTraveller traveller = other.GetComponent<PortalTraveller>();
		if (!traveller)
		{
			// If no PortalTraveller, try to add one automatically to Rigidbodies
			Rigidbody rb = other.attachedRigidbody;
			if (rb)
			{
				traveller = rb.gameObject.AddComponent<PortalTraveller>();
			}
		}

		if (traveller && !travellers.Contains(traveller))
		{
			travellers.Add(traveller);
			traveller.EnterPortal(this);
		}
	}

	private void OnTriggerStay(Collider other)
	{
		// Ensure traveller stays registered while in trigger
		PortalTraveller traveller = other.GetComponent<PortalTraveller>();
		if (traveller && !travellers.Contains(traveller))
		{
			travellers.Add(traveller);
			traveller.EnterPortal(this);
		}
	}

	private void OnTriggerExit(Collider other)
	{
		PortalTraveller traveller = other.GetComponent<PortalTraveller>();
		if (traveller)
		{
			traveller.ExitPortal(this);
			travellers.Remove(traveller);
		}
	}
}
