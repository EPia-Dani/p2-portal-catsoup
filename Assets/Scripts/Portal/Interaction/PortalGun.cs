using Input;
using UnityEngine;

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
		}

		void Update() {
			HandlePortalResize();

			if (!_placementCalculator.TryCalculate(out PortalPlacement placement)) {
				HideBoundsRenderer();
				return;
			}

			if (showPlacementBounds) {
				UpdateBoundsVisualization(placement);
			} else {
				HideBoundsRenderer();
			}

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
	}
}
