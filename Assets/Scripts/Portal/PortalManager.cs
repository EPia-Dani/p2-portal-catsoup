using Portal;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
	[SerializeField] private PortalRenderer[] portalPrefabs = new PortalRenderer[2];

	public Collider[] portalSurfaces = new Collider[2];
	public Vector3[] portalNormals = new Vector3[2];
	public Vector3[] portalCenters = new Vector3[2];

	public void PlacePortal(int index, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float wallOffset)
	{
		PortalRenderer portal = portalPrefabs[index];
		PortalRenderer otherPortal = portalPrefabs[1 - index];
		
		portal.SetVisible(true);
		portal.transform.SetPositionAndRotation(position + normal * wallOffset, Quaternion.LookRotation(-normal, up));
		
		portalSurfaces[index] = surface;
		portalNormals[index] = normal;
		portalCenters[index] = position;

		portal.InvalidateCachedTransform();
		otherPortal.InvalidateCachedTransform();
		portal.PlayAppear();
		
		TryStartOpening();
	}

	private void TryStartOpening()
	{
		if (!portalPrefabs[0] || !portalPrefabs[1]) return;
		if (!portalPrefabs[0].gameObject.activeInHierarchy || !portalPrefabs[1].gameObject.activeInHierarchy) return;
		portalPrefabs[0].StartOpening();
		portalPrefabs[1].StartOpening();
	}
	
	
}
