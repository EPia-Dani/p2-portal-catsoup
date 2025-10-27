using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages seamless teleportation through portals by cloning objects while they traverse.
/// </summary>
public class PortalTraveller : MonoBehaviour
{
	[Header("Clone Settings")]
	[Tooltip("Objetos que no deben ser clonados (ej: cámaras, lights)")]
	[SerializeField] private bool shouldClone = true;

	// Reference to the clone created when traversing
	private GameObject clone;
	private Portal currentPortal;
	private bool isCloneActive = false;

	// Track which side of portal we're on
	private float lastDistanceToPortal;
	private bool hasStartedTeleport = false;

	private Rigidbody rb;
	private CharacterController characterController;
	private FPSController fpsController;
	private List<Renderer> originalRenderers = new List<Renderer>();
	private List<Renderer> cloneRenderers = new List<Renderer>();

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		characterController = GetComponent<CharacterController>();
		fpsController = GetComponent<FPSController>();
		CacheRenderers();
	}

	private void CacheRenderers()
	{
		originalRenderers.Clear();
		originalRenderers.AddRange(GetComponentsInChildren<Renderer>());
	}

	/// <summary>
	/// Called when entering a portal trigger zone
	/// </summary>
	public void EnterPortal(Portal portal)
	{
		if (!shouldClone || !portal.linkedPortal) return;

		currentPortal = portal;
		lastDistanceToPortal = GetSignedDistanceToPortal(portal);
		hasStartedTeleport = false;

		// Create the clone
		CreateClone(portal);
	}

	/// <summary>
	/// Called while inside a portal trigger zone
	/// </summary>
	public void UpdateTraveller(Portal portal)
	{
		if (!isCloneActive || !clone) return;

		float currentDistance = GetSignedDistanceToPortal(portal);

		// Check if we've crossed through the portal plane
		if (!hasStartedTeleport && Mathf.Sign(currentDistance) != Mathf.Sign(lastDistanceToPortal))
		{
			hasStartedTeleport = true;
		}

		// Update clone position to mirror this object
		UpdateClone(portal);

		// Update which side is visible based on distance to portal
		float threshold = 0.01f;
		bool originalSideVisible = currentDistance > -threshold;
		bool cloneSideVisible = currentDistance < threshold;

		SetRenderersEnabled(originalRenderers, originalSideVisible);
		SetRenderersEnabled(cloneRenderers, cloneSideVisible);

		// Teleport as soon as we cross the portal plane to avoid collisions on the other side
		// Use a very small threshold to ensure we've actually crossed
		if (hasStartedTeleport && currentDistance < -0.05f)
		{
			Debug.Log($"[PortalTraveller] Teleporting through portal! Distance: {currentDistance}");
			CompleteTeleport(portal);
		}

		lastDistanceToPortal = currentDistance;
	}

	/// <summary>
	/// Called when exiting a portal trigger zone
	/// </summary>
	public void ExitPortal(Portal portal)
	{
		if (portal == currentPortal)
		{
			// If we exited without completing teleport, clean up clone
			if (isCloneActive && !hasStartedTeleport)
			{
				DestroyClone();
			}

			currentPortal = null;
		}
	}

	private void CreateClone(Portal portal)
	{
		if (isCloneActive) return;

		// Instantiate clone
		clone = Instantiate(gameObject);
		clone.name = gameObject.name + " (Portal Clone)";

		// Remove components from clone to prevent recursive cloning and physics issues
		var cloneTraveller = clone.GetComponent<PortalTraveller>();
		if (cloneTraveller)
		{
			Destroy(cloneTraveller);
		}

		// Disable clone's CharacterController to prevent physics conflicts
		var cloneCharController = clone.GetComponent<CharacterController>();
		if (cloneCharController)
		{
			cloneCharController.enabled = false;
		}

		// Disable clone's FPSController to prevent input/movement conflicts
		var cloneFPS = clone.GetComponent<FPSController>();
		if (cloneFPS)
		{
			cloneFPS.enabled = false;
		}

		// Make clone's rigidbody kinematic and disable collisions if it has one
		var cloneRb = clone.GetComponent<Rigidbody>();
		if (cloneRb)
		{
			cloneRb.isKinematic = true;
			cloneRb.detectCollisions = false;
		}

		// Disable all colliders on the clone to prevent physics glitches
		var cloneColliders = clone.GetComponentsInChildren<Collider>();
		foreach (var collider in cloneColliders)
		{
			collider.enabled = false;
		}

		// Cache clone renderers
		cloneRenderers.Clear();
		cloneRenderers.AddRange(clone.GetComponentsInChildren<Renderer>());

		// Sync clone state
		UpdateClone(portal);

		isCloneActive = true;
	}

	private void UpdateClone(Portal portal)
	{
		if (!clone || !portal.linkedPortal) return;

		// Transform matrix: from this portal to linked portal
		Matrix4x4 m = portal.linkedPortal.transform.localToWorldMatrix
		            * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f))
		            * portal.transform.worldToLocalMatrix;

		// Apply to clone - just update position/rotation
		// The clone is kinematic so we don't need to (and can't) set velocity
		Vector3 clonePos = m.MultiplyPoint3x4(transform.position);
		Quaternion cloneRot = m.rotation * transform.rotation;

		clone.transform.SetPositionAndRotation(clonePos, cloneRot);
	}

	private void CompleteTeleport(Portal portal)
	{
		if (!clone || !portal.linkedPortal) return;

		// Transform matrix for teleportation
		Matrix4x4 m = portal.linkedPortal.transform.localToWorldMatrix
		            * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f))
		            * portal.transform.worldToLocalMatrix;

		// Teleport position with offset forward from the exit portal
		Vector3 newPos = m.MultiplyPoint3x4(transform.position);
		
		// Add a significant offset in the forward direction of the exit portal to prevent wall clipping
		// This needs to be large enough to clear thick walls
		float offsetDistance = 2f; // Default offset for thick walls
		if (characterController)
		{
			// Use character height as a safe offset distance
			offsetDistance = characterController.height * 0.5f;
		}
		newPos += portal.linkedPortal.transform.forward * offsetDistance;
		
		// For CharacterController, we need to disable it temporarily to teleport
		bool wasControllerEnabled = false;
		if (characterController)
		{
			wasControllerEnabled = characterController.enabled;
			characterController.enabled = false;
		}

		// Handle rotation based on controller type
		if (fpsController)
		{
			// For FPS controller, we need to handle rotation specially
			// Calculate the new forward direction after portal transformation
			Vector3 newForward = m.MultiplyVector(transform.forward);
			Vector3 newUp = m.MultiplyVector(transform.up);
			Quaternion newRot = Quaternion.LookRotation(newForward, newUp);
			
			// Apply position
			transform.position = newPos;
			
			// Apply rotation to the root transform
			transform.rotation = newRot;
			
			// Transform velocity and update internal angles
			fpsController.TransformVelocity(m);
		}
		else
		{
			// For regular objects with rigidbody
			Quaternion newRot = m.rotation * transform.rotation;
			transform.SetPositionAndRotation(newPos, newRot);
			
			// Transform velocity for Rigidbody
			if (rb)
			{
				rb.linearVelocity = m.MultiplyVector(rb.linearVelocity);
				rb.angularVelocity = m.MultiplyVector(rb.angularVelocity);
			}
		}

		// Re-enable CharacterController
		if (characterController)
		{
			characterController.enabled = wasControllerEnabled;
		}

		// Re-enable all original renderers
		SetRenderersEnabled(originalRenderers, true);

		// Clean up clone
		DestroyClone();

		currentPortal = null;
		hasStartedTeleport = false;
	}

	private void DestroyClone()
	{
		if (clone)
		{
			Destroy(clone);
			clone = null;
		}
		cloneRenderers.Clear();
		isCloneActive = false;
	}

	private float GetSignedDistanceToPortal(Portal portal)
	{
		// Distance from portal plane (positive = in front, negative = behind)
		Vector3 toObject = transform.position - portal.transform.position;
		return Vector3.Dot(toObject, portal.transform.forward);
	}

	private float GetTeleportThreshold()
	{
		// Calculate appropriate threshold based on object size
		float threshold = 0.5f; // Default
		
		if (characterController)
		{
			// For character controllers, use height/2 as threshold
			threshold = characterController.height * 0.5f;
		}
		else
		{
			// For other objects, try to get bounds
			var collider = GetComponent<Collider>();
			if (collider)
			{
				threshold = collider.bounds.extents.magnitude;
			}
		}
		
		return Mathf.Max(0.3f, threshold); // Minimum threshold of 0.3
	}

	private void SetRenderersEnabled(List<Renderer> renderers, bool enabled)
	{
		foreach (var renderer in renderers)
		{
			if (renderer)
			{
				renderer.enabled = enabled;
			}
		}
	}

	private void OnDestroy()
	{
		DestroyClone();
	}
}

