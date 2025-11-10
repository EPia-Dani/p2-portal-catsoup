
using UnityEngine;

public class PortalTraveller : MonoBehaviour {

	// Track the previous offset from portal for crossing detection (like in Portals project)
	public Vector3 previousOffsetFromPortal { get; set; }

	[Header("Portal Settings")]
	[Tooltip("Minimum horizontal velocity when exiting from non-vertical portal (floor/ceiling) to vertical portal (wall) to prevent getting stuck")]
	public float minPortalExitVelocity = 3f;

	public virtual void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot, float scaleRatio = 1f) {
		transform.position = pos;
		transform.rotation = rot;
		
		// Scale the traveler based on portal size difference
		if (Mathf.Abs(scaleRatio - 1f) > 0.001f) {
			transform.localScale *= scaleRatio;
		}
	}

	/// <summary>
	/// Applies minimum exit velocity when traveling from non-vertical portal to vertical portal.
	/// Override this method if you need custom velocity handling, but call base.ApplyMinimumExitVelocity
	/// to get the standard behavior for non-vertical to vertical portal transitions.
	/// </summary>
	/// <param name="fromPortal">Source portal transform</param>
	/// <param name="toPortal">Destination portal transform</param>
	/// <param name="currentVelocity">Current velocity after teleport transformation</param>
	/// <returns>Modified velocity with minimum exit force applied if needed</returns>
	public virtual Vector3 ApplyMinimumExitVelocity(Transform fromPortal, Transform toPortal, Vector3 currentVelocity)
	{
		// Determine if the source portal is 'non-vertical' (e.g. on the floor/ceiling/cliff)
		float fromPortalUpDot = Mathf.Abs(Vector3.Dot(fromPortal.forward.normalized, Vector3.up));
		const float nonVerticalThreshold = 0.5f;
		bool fromPortalIsNonVertical = fromPortalUpDot > nonVerticalThreshold;
		
		// Determine if the destination portal is vertical (wall)
		float toPortalUpDot = Mathf.Abs(Vector3.Dot(toPortal.forward.normalized, Vector3.up));
		bool toPortalIsVertical = toPortalUpDot <= nonVerticalThreshold;

		// Only apply minimum velocity when going from non-vertical to vertical portal
		if (!fromPortalIsNonVertical || !toPortalIsVertical)
		{
			return currentVelocity;
		}

		// Calculate horizontal velocity component
		Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
		float horizontalSpeed = horizontalVelocity.magnitude;

		// If horizontal speed is less than minimum, apply minimum exit velocity
		if (horizontalSpeed < minPortalExitVelocity)
		{
			// Calculate exit direction (forward direction of destination portal)
			Vector3 exitDirection = toPortal.forward;
			Vector3 horizontalExitDirection = new Vector3(exitDirection.x, 0, exitDirection.z);
			
			if (horizontalExitDirection.sqrMagnitude > 0.001f)
			{
				horizontalExitDirection.Normalize();
				
				// Set minimum horizontal velocity in exit direction
				horizontalVelocity = horizontalExitDirection * minPortalExitVelocity;
				
				// Return velocity with minimum horizontal component (preserve vertical)
				return new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
			}
		}
		else
		{
			// Object already has sufficient horizontal velocity, but ensure it's moving
			// in the exit direction (or at least not opposing it)
			Vector3 horizontalExitDirection = new Vector3(toPortal.forward.x, 0, toPortal.forward.z);
			if (horizontalExitDirection.sqrMagnitude > 0.001f)
			{
				horizontalExitDirection.Normalize();
				float alignment = Vector3.Dot(horizontalVelocity.normalized, horizontalExitDirection);
				
				// If velocity is opposing exit direction, add boost in exit direction
				if (alignment < 0.5f)
				{
					// Add boost in exit direction to help object escape
					Vector3 exitBoost = horizontalExitDirection * minPortalExitVelocity * 0.5f;
					return new Vector3(
						currentVelocity.x + exitBoost.x,
						currentVelocity.y,
						currentVelocity.z + exitBoost.z
					);
				}
			}
		}

		return currentVelocity;
	}

}