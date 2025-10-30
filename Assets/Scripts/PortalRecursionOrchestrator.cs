using UnityEngine;

/// <summary>
/// Standalone portal recursion orchestrator (no dependency on PortalRenderer).
/// - Takes two portal surface transforms and their surface renderers
/// - Drives two internal portal cameras via RenderTextures
/// - Computes correct recursive camera transforms for arbitrary portal orientations
/// - Applies oblique near-plane clipping against the destination portal per level
/// - Optional UV flips to correct handedness
/// Attach to any GameObject and assign A/B portal surfaces. If PortalRenderer exists on those objects, it is disabled to avoid conflicts.
/// </summary>
public class PortalRecursionOrchestrator : MonoBehaviour
{
	[Header("Portal surfaces (Transform whose forward points out of the portal)")]
	[SerializeField] private Transform portalA;
	[SerializeField] private Transform portalB;

	[Header("Overrides (optional)")]
	[SerializeField] private Camera camA;
	[SerializeField] private Camera camB;
	[SerializeField] private Renderer surfaceA;
	[SerializeField] private Renderer surfaceB;
	[SerializeField] private Camera mainCam;

	[Header("Recursion renders per frame")]
	[SerializeField] private int recursion = 4;

	[Header("Optional UV flips (fix mirrored look)")]
	[SerializeField] private bool flipU_A = false;
	[SerializeField] private bool flipV_A = false;
	[SerializeField] private bool flipU_B = true;
	[SerializeField] private bool flipV_B = false;

	[Header("Advanced (orientation tuning)")]
	[SerializeField] private Vector3 mirrorScale = new Vector3(-1f, 1f, -1f);
	[SerializeField] private bool invertClipNormalA = false;
	[SerializeField] private bool invertClipNormalB = false;
	[SerializeField] private float clipPlaneOffset = 0.01f;

	private RenderTexture rtA;
	private RenderTexture rtB;

	private void Awake()
	{
		// If PortalRenderer exists on provided objects, disable to avoid logic conflicts
		DisableIfExists(portalA);
		DisableIfExists(portalB);

		// Auto-detect children if not set
		if (!camA && portalA) camA = portalA.GetComponentInChildren<Camera>(true);
		if (!camB && portalB) camB = portalB.GetComponentInChildren<Camera>(true);
		if (!surfaceA && portalA) surfaceA = portalA.GetComponentInChildren<MeshRenderer>(true);
		if (!surfaceB && portalB) surfaceB = portalB.GetComponentInChildren<MeshRenderer>(true);

		// Validate
		if (!camA || !camB || !surfaceA || !surfaceB)
		{
			Debug.LogWarning("PortalRecursionOrchestrator: Missing cameras or surfaces. Assign fields or ensure portals have child camera and mesh renderer.");
			return;
		}

		// Configure cameras for manual rendering in builds
		ConfigureCamera(camA);
		ConfigureCamera(camB);

		// Allocate RTs and cross-wire
		rtA = NewRT(Screen.width, Screen.height);
		rtB = NewRT(Screen.width, Screen.height);
		camA.targetTexture = rtA;
		camB.targetTexture = rtB;
		surfaceA.sharedMaterial.mainTexture = rtB; // A shows B
		surfaceB.sharedMaterial.mainTexture = rtA; // B shows A

		// UV flips
		ApplyUVFlip(surfaceA, flipU_A, flipV_A);
		ApplyUVFlip(surfaceB, flipU_B, flipV_B);
	}

	private void OnDestroy()
	{
		if (rtA) { if (camA) camA.targetTexture = null; rtA.Release(); Destroy(rtA); }
		if (rtB) { if (camB) camB.targetTexture = null; rtB.Release(); Destroy(rtB); }
	}

	private void LateUpdate()
	{
		if (!camA || !camB || !portalA || !portalB) return;
		if (!mainCam) mainCam = Camera.main;
		if (!mainCam) return;

		// Build transform stacks exactly like PortalRenderer
		var stackA = BuildMatrices(mainCam.transform.localToWorldMatrix, portalA, portalB);
		var stackB = BuildMatrices(mainCam.transform.localToWorldMatrix, portalB, portalA);

		// Pop and render alternating (B then A) to mirror original order
		while (stackA.Count > 0 || stackB.Count > 0)
		{
			if (stackB.Count > 0)
			{
				var m = stackB.Pop();
				RenderLevel(camB, m, portalA.forward, portalA.position);
			}
			if (stackA.Count > 0)
			{
				var m = stackA.Pop();
				RenderLevel(camA, m, portalB.forward, portalB.position);
			}
		}
	}

	// ----- helpers -----
	private static void ConfigureCamera(Camera c)
	{
		c.allowMSAA = false;
		c.useOcclusionCulling = false;
		c.enabled = false; // manual rendering
		c.clearFlags = CameraClearFlags.Color;
		c.backgroundColor = Color.black;
		if (c.nearClipPlane < 0.01f) c.nearClipPlane = 0.02f;
	}

private Matrix4x4 ScaleMirror()
	{
	return Matrix4x4.Scale(mirrorScale);
	}

	private System.Collections.Generic.Stack<Matrix4x4> BuildMatrices(Matrix4x4 start, Transform srcPortal, Transform dstPortal)
	{
		var s = new System.Collections.Generic.Stack<Matrix4x4>(Mathf.Max(1, recursion));
		var scale = ScaleMirror();
		var current = start;
		for (int i = 0; i < Mathf.Max(1, recursion); i++)
		{
			current = dstPortal.localToWorldMatrix * scale * srcPortal.worldToLocalMatrix * current;
			s.Push(current);
		}
		return s;
	}

	private void RenderLevel(Camera c, Matrix4x4 localToWorld, Vector3 dstForward, Vector3 dstPosition)
	{
		// Extract TRS from matrix (match PortalRenderer usage)
		Vector3 pos = new Vector3(localToWorld.m03, localToWorld.m13, localToWorld.m23);
		Vector3 forward = new Vector3(localToWorld.m02, localToWorld.m12, localToWorld.m22);
		Vector3 up = new Vector3(localToWorld.m01, localToWorld.m11, localToWorld.m21);
		c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, up));

		// Oblique clip using destination portal plane
		var w2c = c.worldToCameraMatrix;
		bool invert = (c == camA ? invertClipNormalB : invertClipNormalA); // camA clips against B plane, camB against A
		Vector3 worldNormal = invert ? -dstForward : dstForward;
		Vector3 worldPoint = dstPosition + worldNormal * clipPlaneOffset;
		Vector3 normal = -w2c.MultiplyVector(worldNormal).normalized;
		Vector4 clipPlane = new Vector4(
			normal.x, normal.y, normal.z,
			-Vector3.Dot(w2c.MultiplyPoint(worldPoint), normal)
		);
		c.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);
		c.Render();
	}

	private static RenderTexture NewRT(int w, int h)
	{
		var rt = new RenderTexture(Mathf.Max(256, w), Mathf.Max(256, h), 24, RenderTextureFormat.ARGB32)
		{ antiAliasing = 1, useMipMap = false, autoGenerateMips = false,
		  wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear};
		rt.Create();
		return rt;
	}

	private static void ApplyUVFlip(Renderer r, bool flipU, bool flipV)
	{
		if (!r) return;
		var scale = new Vector2(flipU ? -1f : 1f, flipV ? -1f : 1f);
		var offset = new Vector2(flipU ? 1f : 0f, flipV ? 1f : 0f);
		r.sharedMaterial.SetTextureScale("_MainTex", scale);
		r.sharedMaterial.SetTextureOffset("_MainTex", offset);
	}

	private static void DisableIfExists(Transform t)
	{
		if (!t) return;
		var pr = t.GetComponent<PortalRenderer>();
		if (pr) pr.enabled = false;
	}
}


