using Portal;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
	[Header("Portals")]
	[SerializeField] private PortalRenderer bluePortal;
	[SerializeField] private PortalRenderer orangePortal;

	[Header("Settings")]
	[SerializeField] private int textureWidth = 1024;
	[SerializeField] private int textureHeight = 1024;
	[SerializeField] private int recursionLimit = 2;
	[SerializeField] private int frameSkipInterval = 1;

	public Collider[] portalSurfaces = new Collider[2];
	public Vector3[] portalNormals = new Vector3[2];
	public Vector3[] portalCenters = new Vector3[2];

	private void Start() {
		ApplySettings();
	}

	private void OnValidate() {
		textureWidth = Mathf.Clamp(textureWidth, 256, 4096);
		textureHeight = Mathf.Clamp(textureHeight, 256, 4096);
		recursionLimit = Mathf.Max(1, recursionLimit);
		frameSkipInterval = Mathf.Max(1, frameSkipInterval);
	}

	private void ApplySettings() {
		PortalRenderer[] portals = { bluePortal, orangePortal };
		foreach (var portal in portals) {
			if (portal != null) {
				portal.ConfigurePortal(textureWidth, textureHeight, recursionLimit, frameSkipInterval);
			}
		}
	}

	public void PlacePortal(int index, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float wallOffset) {
		PortalRenderer portal = index == 0 ? bluePortal : orangePortal;
		if (portal == null) return;

		portal.SetVisible(true);
		portal.transform.SetPositionAndRotation(position + normal * wallOffset, Quaternion.LookRotation(-normal, up));

		portalSurfaces[index] = surface;
		portalNormals[index] = normal;
		portalCenters[index] = position;

		portal.PlayAppear();

		if (bluePortal != null && orangePortal != null &&
		    bluePortal.gameObject.activeInHierarchy && orangePortal.gameObject.activeInHierarchy) {
			bluePortal.StartOpening();
			orangePortal.StartOpening();
		}
	}
}
