using Portal;
using UnityEngine;

/// <summary>
/// Manages portal state, lifecycle, and coordinates portal placement.
/// Responsibilities: State tracking, activation/deactivation, opening logic.
/// </summary>
public class PortalManager : MonoBehaviour
{
	public struct PortalState
	{
		public PortalRenderer renderer;
		public Collider surface;
		public Vector3 normal;

		public Vector3 worldCenter;
	}

	[SerializeField] private PortalRenderer[] portalPrefabs = new PortalRenderer[2];

	private PortalState[] portalStates = new PortalState[2];




	/// <summary>
	/// Places a portal at the specified position and orientation.
	/// Handles state storage and triggers animations.
	/// </summary>
	public void PlacePortal(int index, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float wallOffset)
	{
		PortalRenderer portal = portalPrefabs[index];
		PortalRenderer otherPortal = portalPrefabs[1 - index];
		
		portal.SetVisible(true);
		
		// Set transform
		portal.transform.SetPositionAndRotation(position + normal * wallOffset, Quaternion.LookRotation(-normal, up));
		
		// Store state
		portalStates[index].renderer = portal;
		portalStates[index].surface = surface;
		portalStates[index].normal = normal;
		portalStates[index].worldCenter = position;

		// Invalidate cached transform data for both portals
		portal.InvalidateCachedTransform();
		otherPortal.InvalidateCachedTransform();

		// Play appearance animation
		portal.PlayAppear();
		
		// Try to start opening both portals if both are placed
		TryStartOpening();
	}

	/// <summary>
	/// Gets the state of a portal, or null if not placed.
	/// </summary>
	public PortalState? GetPortalState(int index)
	{
		if (index < 0 || index >= 2 || !portalStates[index].surface)
			return null;
		return portalStates[index];
	}

	/// <summary>
	/// Starts opening both portals if both are placed.
	/// </summary>
	private void TryStartOpening()
	{
		if (!portalStates[0].renderer || !portalStates[1].renderer) return;
		if (!portalStates[0].renderer.gameObject.activeInHierarchy || !portalStates[1].renderer.gameObject.activeInHierarchy) return;
		
			portalStates[0].renderer.StartOpening();
		
			portalStates[1].renderer.StartOpening();
	}
	
	
}
