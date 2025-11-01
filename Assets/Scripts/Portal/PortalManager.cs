using Portal;
using UnityEngine;

/// <summary>
/// Centralized portal system management.
/// Handles portal placement, visibility, and provides a single point for settings.
/// </summary>
public class PortalManager : MonoBehaviour
{
	[Header("Portal References")]
	[SerializeField] private PortalRenderer[] portalPrefabs = new PortalRenderer[2];

	[Header("Rendering Settings")]
	[SerializeField] private int textureWidth = 1024;
	[SerializeField] private int textureHeight = 1024;
	[SerializeField] private int recursionLimit = 2;
	[SerializeField] private int frameSkipInterval = 1;

	[Header("Portal Data (Auto-filled)")]
	public Collider[] portalSurfaces = new Collider[2];
	public Vector3[] portalNormals = new Vector3[2];
	public Vector3[] portalCenters = new Vector3[2];

	private bool _settingsApplied = false;

	private void Start()
	{
		ApplySettingsToPortals();
	}

	private void OnValidate()
	{
		// Clamp values in inspector
		textureWidth = Mathf.Clamp(textureWidth, 256, 4096);
		textureHeight = Mathf.Clamp(textureHeight, 256, 4096);
		recursionLimit = Mathf.Max(1, recursionLimit);
		frameSkipInterval = Mathf.Max(1, frameSkipInterval);
	}

	/// <summary>
	/// Applies all settings from this manager to both portals.
	/// </summary>
	public void ApplySettingsToPortals()
	{
		if (_settingsApplied) return;

		for (int i = 0; i < portalPrefabs.Length; i++)
		{
			if (portalPrefabs[i] == null) continue;

			portalPrefabs[i].ConfigurePortal(textureWidth, textureHeight, recursionLimit, frameSkipInterval);
		}

		_settingsApplied = true;
	}

	/// <summary>
	/// Updates texture resolution and applies to all portals.
	/// </summary>
	public void SetTextureResolution(int width, int height)
	{
		textureWidth = Mathf.Clamp(width, 256, 4096);
		textureHeight = Mathf.Clamp(height, 256, 4096);

		for (int i = 0; i < portalPrefabs.Length; i++)
		{
			if (portalPrefabs[i] != null)
				portalPrefabs[i].GetComponent<PortalRenderView>().UpdateTextureResolution(textureWidth, textureHeight);
		}
	}

	/// <summary>
	/// Updates recursion limit and applies to all portals.
	/// </summary>
	public void SetRecursionLimit(int limit)
	{
		recursionLimit = Mathf.Max(1, limit);

		for (int i = 0; i < portalPrefabs.Length; i++)
		{
			if (portalPrefabs[i] != null)
				portalPrefabs[i].SetRecursionLimit(recursionLimit);
		}
	}

	/// <summary>
	/// Updates frame skip interval and applies to all portals.
	/// </summary>
	public void SetFrameSkipInterval(int interval)
	{
		frameSkipInterval = Mathf.Max(1, interval);

		for (int i = 0; i < portalPrefabs.Length; i++)
		{
			if (portalPrefabs[i] != null)
				portalPrefabs[i].SetFrameSkipInterval(frameSkipInterval);
		}
	}

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
		if (otherPortal != null)
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
