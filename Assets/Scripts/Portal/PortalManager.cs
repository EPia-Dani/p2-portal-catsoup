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
		public Vector3 right;
		public Vector3 up;
		public Vector3 worldCenter;
	}

	[SerializeField] private PortalRenderer[] portalPrefabs = new PortalRenderer[2];

	private PortalState[] portalStates = new PortalState[2];

	[Header("Performance Settings")]
	/// <summary>
	/// Centralized performance settings for all portals
	/// </summary>
	[SerializeField] private int textureWidth = 1024;
	[SerializeField] private int recursionLimit = 2;
	[SerializeField] private int frameSkipInterval = 1;
	[SerializeField] private float clipPlaneOffset = 0.01f;

	[Header("Animation Settings")]
	/// <summary>
	/// Centralized animation settings for all portals
	/// </summary>
	[SerializeField] private float portalOpenDuration = 1f;
	[SerializeField] private AnimationCurve portalOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	[SerializeField] private float portalAppearDuration = 0.3f;
	[SerializeField] private float portalTargetRadius = 0.4f;
	[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	[SerializeField] private float openThreshold = 0.8f;

	private void Awake()
	{
		// Apply centralized performance settings to all portals
		ApplyPerformanceSettings();
	}

	/// <summary>
	/// Applies centralized settings to all portals
	/// </summary>
	private void ApplyPerformanceSettings()
	{
		// Initialize all portals with centralized settings (use for loop for better performance)
		for (int i = 0; i < portalPrefabs.Length; i++)
		{
			PortalRenderer portal = portalPrefabs[i];
			if (portal != null)
			{
				// Performance settings
				portal.SetRecursionLimit(recursionLimit);
				portal.SetFrameSkipInterval(frameSkipInterval);
				portal.SetTextureWidth(textureWidth);
				portal.SetClipPlaneOffset(clipPlaneOffset);
				
				// Animation settings
				portal.SetAnimationSettings(
					portalOpenDuration,
					portalOpenCurve,
					portalAppearDuration,
					portalTargetRadius,
					portalAppearCurve,
					openThreshold);
			}
		}

		// Both portals render on same frames (no staggering)
		// This ensures both show current data, eliminating frame latency
		if (portalPrefabs[0] != null) portalPrefabs[0].SetRenderOffset(0);
		if (portalPrefabs[1] != null) portalPrefabs[1].SetRenderOffset(0);
	}

	/// <summary>
	/// Updates performance settings at runtime
	/// </summary>
	public void SetPerformanceSettings(int newTextureWidth, int newRecursionLimit, int newFrameSkipInterval, float newClipPlaneOffset)
	{
		textureWidth = Mathf.Max(64, newTextureWidth);
		recursionLimit = Mathf.Max(1, newRecursionLimit);
		frameSkipInterval = Mathf.Max(1, newFrameSkipInterval);
		clipPlaneOffset = Mathf.Max(0.001f, newClipPlaneOffset);
		ApplyPerformanceSettings();
	}

	/// <summary>
	/// Updates animation settings at runtime
	/// </summary>
	public void SetAnimationSettings(
		float newOpenDuration,
		AnimationCurve newOpenCurve,
		float newAppearDuration,
		float newTargetRadius,
		AnimationCurve newAppearCurve,
		float newOpenThreshold)
	{
		portalOpenDuration = Mathf.Max(0.1f, newOpenDuration);
		portalOpenCurve = newOpenCurve;
		portalAppearDuration = Mathf.Max(0.1f, newAppearDuration);
		portalTargetRadius = Mathf.Max(0.1f, newTargetRadius);
		portalAppearCurve = newAppearCurve;
		openThreshold = Mathf.Clamp01(newOpenThreshold);
		ApplyPerformanceSettings();
	}

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
		portalStates[index].right = right;
		portalStates[index].up = up;
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
		
		bool portal0Opening = portalStates[0].renderer.IsOpening || portalStates[0].renderer.IsFullyOpen;
		bool portal1Opening = portalStates[1].renderer.IsOpening || portalStates[1].renderer.IsFullyOpen;
		
		if (!portal0Opening && !portalStates[0].renderer.IsFullyOpen)
			portalStates[0].renderer.StartOpening();
		
		if (!portal1Opening && !portalStates[1].renderer.IsFullyOpen)
			portalStates[1].renderer.StartOpening();
	}
	
	
}
