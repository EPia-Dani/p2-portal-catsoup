using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	/// <summary>
	/// Mirrors a traveller's renderers through a portal. No physics or scripts are copied.
	/// </summary>
	public class PortalTravellerProxy : MonoBehaviour {
		PortalTraveller traveller;
		PortalRenderer fromPortal;
		PortalRenderer toPortal;
		PortalViewChain viewChain;

	readonly List<Renderer> clonedRenderers = new List<Renderer>();
	static readonly Matrix4x4 MirrorMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));

		public static PortalTravellerProxy Create(PortalTraveller traveller, PortalRenderer from, PortalRenderer to, PortalViewChain chain) {
			if (!traveller || !from || !to) return null;

			GameObject go = new GameObject($"{traveller.gameObject.name}_Proxy");
			go.transform.SetParent(to.transform, worldPositionStays: true);
			var proxy = go.AddComponent<PortalTravellerProxy>();
			proxy.Initialize(traveller, from, to, chain);
			return proxy;
		}

		void Initialize(PortalTraveller source, PortalRenderer from, PortalRenderer to, PortalViewChain chain) {
			traveller = source;
			fromPortal = from;
			toPortal = to;
			viewChain = chain;

			CloneRenderers();
			UpdateProxyTransform();
		}

		void CloneRenderers() {
			clonedRenderers.Clear();

			var renderers = traveller.GetComponentsInChildren<Renderer>();
			foreach (var renderer in renderers) {
				if (!renderer) continue;

				var clone = Instantiate(renderer, transform);
				clone.transform.localPosition = renderer.transform.localPosition;
				clone.transform.localRotation = renderer.transform.localRotation;
				clone.transform.localScale = renderer.transform.localScale;

				clonedRenderers.Add(clone);
			}
		}

		void LateUpdate() {
			if (!traveller || !fromPortal || !toPortal) {
				Dispose();
				return;
			}

			UpdateProxyTransform();
		}

		void UpdateProxyTransform() {
			if (!traveller || !fromPortal || !toPortal) return;

			float scaleRatio = toPortal.PortalScale / Mathf.Max(fromPortal.PortalScale, 0.0001f);
			Matrix4x4 teleportMatrix = ComputeTeleportMatrix(scaleRatio);

			Vector3 position = teleportMatrix.MultiplyPoint3x4(traveller.transform.position);
			Quaternion rotation = teleportMatrix.rotation * traveller.transform.rotation;

			transform.position = position;
			transform.rotation = rotation;
			transform.localScale = traveller.transform.localScale * scaleRatio;
		}

		Matrix4x4 ComputeTeleportMatrix(float scaleRatio) {
			Matrix4x4 localToWorld = toPortal.transform.localToWorldMatrix;
			Matrix4x4 worldToLocal = fromPortal.transform.worldToLocalMatrix;

			Matrix4x4 mirror = MirrorMatrix;

			Matrix4x4 scale = Matrix4x4.identity;
			if (Mathf.Abs(scaleRatio - 1f) > 0.0001f) {
				scale = Matrix4x4.Scale(Vector3.one * scaleRatio);
			}

			return localToWorld * scale * mirror * worldToLocal;
		}

		public void Dispose() {
			CleanupRenderers();

			if (gameObject) {
				Destroy(gameObject);
			}
		}

		void OnDestroy() {
			CleanupRenderers();
		}

		void CleanupRenderers() {
			foreach (var renderer in clonedRenderers) {
				if (renderer) Destroy(renderer.gameObject);
			}
			clonedRenderers.Clear();
		}
	}
}

