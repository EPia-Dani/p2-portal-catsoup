using Input;
using UnityEngine;
using UI;

namespace Portal {
	public class PortalGun : MonoBehaviour {
		[SerializeField] LayerMask shootMask = ~0;
		[SerializeField] float shootDistance = 1000f;
		[SerializeField] Camera shootCamera;
		[SerializeField] PortalManager portalManager;
		[SerializeField] Vector2 portalHalfSize = new Vector2(0.45f, 0.45f);
		[SerializeField] float clampSkin = 0.01f;

		[Header("Portal Resizing")]
		[SerializeField] float minPortalSize = 0.2f;
		[SerializeField] float maxPortalSize = 1f;
		[SerializeField] float resizeSpeed = 0.5f;

		[Header("Portal Bounds Visualization")]
		[SerializeField] bool showPlacementBounds = true;
		[SerializeField] Color boundsColor = new Color(1f, 1f, 0f, 0.8f);
		[SerializeField] float boundsOffset = 0.001f;
		[SerializeField] int ellipseSegments = 64;

		[SerializeField] LineRenderer boundsRenderer;

		PlayerInput _input;
		PortalSizeController _sizeController;
		PortalPlacementCalculator _placementCalculator;
		PortalOverlapGuard _overlapGuard;
		Crosshair _crosshair;

		void OnValidate() {
			if (!showPlacementBounds) showPlacementBounds = true;
			if (boundsColor.maxColorComponent < 0.01f) boundsColor = new Color(1f, 1f, 0f, 0.8f);
		}

		void Start() {
			if (!shootCamera) shootCamera = Camera.main;
			if (!portalManager) portalManager = GetComponent<PortalManager>();
			_input = InputManager.PlayerInput;

			_sizeController = new PortalSizeController(portalHalfSize, minPortalSize, maxPortalSize, resizeSpeed);
			_placementCalculator = new PortalPlacementCalculator(shootCamera, shootMask, shootDistance, clampSkin, _sizeController);
			_overlapGuard = new PortalOverlapGuard(portalManager, _sizeController, clampSkin);
			InitializeBoundsRenderer();
			
			#if UNITY_2023_1_OR_NEWER
			_crosshair = FindFirstObjectByType<Crosshair>();
			#else
			_crosshair = FindObjectOfType<Crosshair>();
			#endif
		}

		void Update() {
			HandlePortalResize();

			// First raycast to check what we're looking at (all surfaces)
			Ray ray = shootCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
			if (!Physics.Raycast(ray, out RaycastHit hit, shootDistance, ~0, QueryTriggerInteraction.Ignore)) {
				Debug.Log("[PortalGun] No raycast hit");
				HideBoundsRenderer();
				UpdateCrosshair(false, false);
				return;
			}

			// Debug what we hit
			Debug.Log($"[PortalGun] Raycast hit: {hit.collider?.name}, Tag: {hit.collider?.tag}, Layer: {LayerMask.LayerToName(hit.collider?.gameObject.layer ?? 0)}");

			// Check if it's a portal wall - if not, show empty
			if (hit.collider == null || !hit.collider.CompareTag("Portal Wall")) {
				Debug.Log($"[PortalGun] Not a portal wall - tag check failed. Tag is: '{hit.collider?.tag}'");
				HideBoundsRenderer();
				UpdateCrosshair(false, false);
				return;
			}

			Debug.Log("[PortalGun] Portal wall detected!");

			// It's a portal wall - get placement info using shootMask
			if (!_placementCalculator.TryCalculate(out PortalPlacement placement) || !placement.IsValid) {
				HideBoundsRenderer();
				UpdateCrosshair(false, false);
				return;
			}

			if (showPlacementBounds) {
				UpdateBoundsVisualization(placement);
			} else {
				HideBoundsRenderer();
			}

			// Check overlap with existing portals
			Vector2 localPos = placement.LocalPosition;
			bool overlappingBlue = IsOverlappingPortal(PortalId.Blue, placement, localPos);
			bool overlappingOrange = IsOverlappingPortal(PortalId.Orange, placement, localPos);

			// Update crosshair: if overlapping blue, hide orange; if overlapping orange, hide blue; otherwise show both
			bool canPlaceBlue = !overlappingBlue;
			bool canPlaceOrange = !overlappingOrange;
			UpdateCrosshair(canPlaceBlue, canPlaceOrange);

			if (_input.Player.ShootBlue.WasPerformedThisFrame()) {
				TryPlacePortal(PortalId.Blue, placement);
			}

			if (_input.Player.ShootOrange.WasPerformedThisFrame()) {
				TryPlacePortal(PortalId.Orange, placement);
			}
		}

		void HandlePortalResize() {
			if (_input == null || _input.Player.ResizePortal == null) return;

			Vector2 scrollDelta = _input.Player.ResizePortal.ReadValue<Vector2>();
			_sizeController?.TryUpdateScale(scrollDelta.y);
		}

		void TryPlacePortal(PortalId id, PortalPlacement placement) {
			if (portalManager == null || !placement.IsValid) return;

			Vector2 localPos = placement.LocalPosition;
			bool removeOther;
			if (!_overlapGuard.TryResolvePlacement(id, placement, localPos, out Vector2 resolvedPos, out removeOther)) {
				return;
			}

			if (removeOther) {
				portalManager.RemovePortal(id.Other());
			}

			Vector3 finalPosition = placement.GetWorldPosition(resolvedPos);
			portalManager.PlacePortal(id, finalPosition, placement.Normal, placement.Right, placement.Up, placement.Surface, _sizeController.CurrentScale);
		}

		void InitializeBoundsRenderer() {
			if (boundsRenderer == null) {
				boundsRenderer = GetComponentInChildren<LineRenderer>();
			}
			if (boundsRenderer == null) {
				boundsRenderer = GetComponent<LineRenderer>();
			}
			if (boundsRenderer == null) {
				boundsRenderer = gameObject.AddComponent<LineRenderer>();
			}

			if (boundsRenderer == null) return;
			boundsRenderer.useWorldSpace = true;
			boundsRenderer.loop = true;
			boundsRenderer.enabled = false;
		}

		void UpdateBoundsVisualization(PortalPlacement placement) {
			if (boundsRenderer == null || !placement.IsValid) return;

			int desiredPositions = ellipseSegments + 1;
			if (boundsRenderer.positionCount != desiredPositions) {
				boundsRenderer.positionCount = desiredPositions;
			}

			Vector3 center = placement.Position + placement.Normal * boundsOffset;
			Vector2 halfSize = _sizeController.CurrentHalfSize;
			for (int i = 0; i <= ellipseSegments; i++) {
				float angle = (Mathf.PI * 2f * i) / ellipseSegments;
				float x = halfSize.x * Mathf.Cos(angle);
				float y = halfSize.y * Mathf.Sin(angle);
				boundsRenderer.SetPosition(i, center + placement.Right * x + placement.Up * y);
			}

			boundsRenderer.enabled = true;
		}

		void HideBoundsRenderer() {
			if (boundsRenderer != null) {
				boundsRenderer.enabled = false;
			}
		}

		bool IsOverlappingPortal(PortalId id, PortalPlacement placement, Vector2 localPos) {
			if (portalManager == null) return false;
			
			PortalId otherId = id.Other();
			if (!portalManager.TryGetState(otherId, out PortalState otherState) || !otherState.IsPlaced) {
				return false; // Other portal not placed, no overlap
			}
			
			// Check if on same surface
			if (otherState.Surface != placement.Surface || Vector3.Dot(placement.Normal, otherState.Normal) < 0.99f) {
				return false; // Different surface, no overlap
			}
			
			// Check distance to other portal
			Vector3 otherOffset = otherState.Position - placement.SurfaceCenter;
			Vector2 otherLocalPos = new Vector2(Vector3.Dot(otherOffset, placement.Right), Vector3.Dot(otherOffset, placement.Up));
			
			float otherScale = otherState.Scale <= 0f ? 1f : otherState.Scale;
			Vector2 otherHalfSize = _sizeController.ResolveHalfSize(otherScale);
			Vector2 currentHalfSize = _sizeController.CurrentHalfSize;
			
			Vector2 minSeparation = new Vector2(
				Mathf.Max(currentHalfSize.x + otherHalfSize.x, 1e-3f),
				Mathf.Max(currentHalfSize.y + otherHalfSize.y, 1e-3f)
			);
			
			Vector2 dist = localPos - otherLocalPos;
			Vector2 distNormalized = new Vector2(
				dist.x / minSeparation.x,
				dist.y / minSeparation.y
			);
			
			// If too close, we're overlapping
			return distNormalized.magnitude < 1.05f;
		}

		void UpdateCrosshair(bool canPlaceBlue, bool canPlaceOrange) {
			if (_crosshair != null) {
				_crosshair.SetState(canPlaceBlue, canPlaceOrange);
			}
		}
	}
}
