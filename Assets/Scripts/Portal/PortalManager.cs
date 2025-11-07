using UnityEngine;

namespace Portal {
	public class PortalManager : MonoBehaviour
	{
	[Header("Portals")]
	[SerializeField] private PortalRenderer bluePortal;
	[SerializeField] private PortalRenderer orangePortal;
	[SerializeField] private Transform bluePortalMesh;
	[SerializeField] private Transform orangePortalMesh;

	[Header("Settings")]
	[SerializeField] private int textureWidth = 1024;
	[SerializeField] private int textureHeight = 1024;
	[SerializeField] private int recursionLimit = 2;
	[SerializeField] private int frameSkipInterval = 1;

	public Collider[] portalSurfaces = new Collider[2];
	public Vector3[] portalNormals = new Vector3[2];
	public Vector3[] portalCenters = new Vector3[2];
	public Vector3[] portalRights = new Vector3[2];
	public Vector3[] portalUps = new Vector3[2];
	public float[] portalScaleMultipliers = new float[2] { 1f, 1f };

	private PortalAnimator _blueAnimator;
	private PortalAnimator _orangeAnimator;
	private Vector3 bluePortalBaseScale = Vector3.one;
	private Vector3 orangePortalBaseScale = Vector3.one;
	private Vector3 bluePortalMeshBaseScale = Vector3.one;
	private Vector3 orangePortalMeshBaseScale = Vector3.one;

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
			// Store the base scale of the blue portal (from prefab/default)
			bluePortalBaseScale = bluePortal.transform.localScale;
			if (bluePortalMesh) bluePortalMeshBaseScale = bluePortalMesh.localScale;
		}
		if (orangePortal) {
			_orangeAnimator = orangePortal.GetComponent<PortalAnimator>();
			if (_orangeAnimator == null) _orangeAnimator = orangePortal.GetComponentInChildren<PortalAnimator>();
			orangePortal.IsReadyToRender = false;
			// Store the base scale of the orange portal (from prefab/default)
			orangePortalBaseScale = orangePortal.transform.localScale;
			if (orangePortalMesh) orangePortalMeshBaseScale = orangePortalMesh.localScale;
		}

		// Link portals as pairs (like in Portals project)
		if (bluePortal && orangePortal) {
			bluePortal.pair = orangePortal;
			orangePortal.pair = bluePortal;
		}
	}

    private void Start() {
        // Load persisted settings if available before applying
        if (PlayerPrefs.HasKey("PortalRecursion")) {
            recursionLimit = Mathf.Max(1, PlayerPrefs.GetInt("PortalRecursion"));
        }
        if (PlayerPrefs.HasKey("PortalFrameSkip")) {
            frameSkipInterval = Mathf.Max(1, PlayerPrefs.GetInt("PortalFrameSkip"));
        }
        ApplySettings();
    }

        private void OnValidate() {
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

        public void SetRecursionLimit(int value) {
            recursionLimit = Mathf.Max(1, value);
            PlayerPrefs.SetInt("PortalRecursion", recursionLimit);
            ApplySettings();
        }

        public void SetFrameSkipInterval(int value) {
            frameSkipInterval = Mathf.Max(1, value);
            PlayerPrefs.SetInt("PortalFrameSkip", frameSkipInterval);
            ApplySettings();
        }

	public void PlacePortal(int index, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float wallOffset, float scale = 1f) {
		PortalRenderer portal = index == 0 ? bluePortal : orangePortal;
		Vector3 baseScale = index == 0 ? bluePortalBaseScale : orangePortalBaseScale;
		Transform portalMesh = index == 0 ? bluePortalMesh : orangePortalMesh;
		Vector3 meshBaseScale = index == 0 ? bluePortalMeshBaseScale : orangePortalMeshBaseScale;
		if (portal == null) return;

		portal.SetVisible(true);
		// Place portal at exact same Z position as wall - z-fighting handled by shader depth bias
		portal.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-normal, up));
		portal.transform.localScale = baseScale;

		// Scale only the portal mesh (X/Z) while preserving Y scale
		if (portalMesh) {
			portalMesh.gameObject.SetActive(true);
			portalMesh.localScale = new Vector3(meshBaseScale.x * scale, meshBaseScale.y, meshBaseScale.z * scale);
		}
		portalScaleMultipliers[index] = scale;

		// Set the wall collider so player can pass through
		portal.SetWallCollider(surface);

		portalSurfaces[index] = surface;
		portalNormals[index] = normal;
		portalCenters[index] = position;
		portalRights[index] = portal.transform.right;
		portalUps[index] = portal.transform.up;


		UpdatePortalVisualStates();
	}

	/// <summary>
	/// Removes a portal by hiding it and clearing its data.
	/// </summary>
	public void RemovePortal(int index) {
		if (index < 0 || index > 1) return;
		
		PortalRenderer portal = index == 0 ? bluePortal : orangePortal;
		PortalAnimator animator = index == 0 ? _blueAnimator : _orangeAnimator;
		Transform portalMesh = index == 0 ? bluePortalMesh : orangePortalMesh;
		if (portal != null) {
			portal.SetVisible(false);
			portal.IsReadyToRender = false;
		}
		
		if (animator != null) {
			animator.HideImmediate();
		}
		
		if (portalMesh != null) {
			portalMesh.gameObject.SetActive(false);
		}
		
		// Clear portal data
		if (portalSurfaces != null && index < portalSurfaces.Length) {
			portalSurfaces[index] = null;
		}
		if (portalNormals != null && index < portalNormals.Length) {
			portalNormals[index] = Vector3.zero;
		}
		if (portalCenters != null && index < portalCenters.Length) {
			portalCenters[index] = Vector3.zero;
		}
		if (portalRights != null && index < portalRights.Length) {
			portalRights[index] = Vector3.zero;
		}
		if (portalUps != null && index < portalUps.Length) {
			portalUps[index] = Vector3.zero;
		}
		if (portalScaleMultipliers != null && index < portalScaleMultipliers.Length) {
			portalScaleMultipliers[index] = 1f;
		}

		UpdatePortalVisualStates();
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

	void UpdatePortalVisualStates() {
		bool bluePlaced = portalSurfaces.Length > 0 && portalSurfaces[0] != null;
		bool orangePlaced = portalSurfaces.Length > 1 && portalSurfaces[1] != null;
		bool bothPlaced = bluePlaced && orangePlaced;

		ApplyPortalState(0, bluePlaced, bothPlaced);
		ApplyPortalState(1, orangePlaced, bothPlaced);
	}

	void ApplyPortalState(int index, bool placed, bool bothPlaced) {
		PortalRenderer portal = index == 0 ? bluePortal : orangePortal;
		PortalAnimator animator = index == 0 ? _blueAnimator : _orangeAnimator;
		Transform portalMesh = index == 0 ? bluePortalMesh : orangePortalMesh;

		if (portalMesh != null) {
			portalMesh.gameObject.SetActive(placed);
		}

		if (portal != null) {
			bool shouldRender = placed && bothPlaced;
			portal.SetVisible(shouldRender);
			if (!shouldRender) {
				portal.IsReadyToRender = false;
			}
		}

		if (animator != null) {
			animator.HideImmediate();
			if (placed) {
				animator.PlayAppear();
				if (bothPlaced) {
					animator.StartOpening();
				}
			}
		}
	}
	}
}
