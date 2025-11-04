using UnityEngine;

namespace Portal {
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

	private PortalAnimator _blueAnimator;
	private PortalAnimator _orangeAnimator;

	/// <summary>
	/// Gets the blue portal renderer.
	/// </summary>
	public PortalRenderer BluePortal => bluePortal;

	/// <summary>
	/// Gets the orange portal renderer.
	/// </summary>
	public PortalRenderer OrangePortal => orangePortal;

	private void Awake() {
		if (bluePortal) {
			_blueAnimator = bluePortal.GetComponent<PortalAnimator>();
			if (_blueAnimator == null) _blueAnimator = bluePortal.GetComponentInChildren<PortalAnimator>();
			bluePortal.IsReadyToRender = false;
		}
		if (orangePortal) {
			_orangeAnimator = orangePortal.GetComponent<PortalAnimator>();
			if (_orangeAnimator == null) _orangeAnimator = orangePortal.GetComponentInChildren<PortalAnimator>();
			orangePortal.IsReadyToRender = false;
		}

		// Link portals as pairs (like in Portals project)
		if (bluePortal && orangePortal) {
			bluePortal.pair = orangePortal;
			orangePortal.pair = bluePortal;
			Debug.Log("Portals linked as pairs");
		}
	}

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
		PortalAnimator animator = index == 0 ? _blueAnimator : _orangeAnimator;
		if (portal == null) return;

		portal.SetVisible(true);
		// Place portal at exact same Z position as wall - z-fighting handled by shader depth bias
		portal.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-normal, up));
		
		// Set the wall collider so player can pass through
		portal.SetWallCollider(surface);

		portalSurfaces[index] = surface;
		portalNormals[index] = normal;
		portalCenters[index] = position;

	

		if (animator != null) animator.PlayAppear();

		if (bluePortal != null && orangePortal != null &&
		    bluePortal.gameObject.activeInHierarchy && orangePortal.gameObject.activeInHierarchy &&
		    _blueAnimator != null && _orangeAnimator != null) {
			_blueAnimator.StartOpening();
			_orangeAnimator.StartOpening();
		}
	}


	private void Update() {
		UpdateRenderReadiness();
	}

	private void UpdateRenderReadiness() {
		if (bluePortal != null && _blueAnimator != null) {
			bool ready = _blueAnimator.IsOpening || _blueAnimator.IsFullyOpen;
			bluePortal.IsReadyToRender = ready;
		}

		if (orangePortal != null && _orangeAnimator != null) {
			bool ready = _orangeAnimator.IsOpening || _orangeAnimator.IsFullyOpen;
			orangePortal.IsReadyToRender = ready;
		}
	}
	}
}
