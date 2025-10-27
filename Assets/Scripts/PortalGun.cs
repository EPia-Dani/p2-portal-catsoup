using System.Collections;
using UnityEngine;
using Input;

public class PortalGun : MonoBehaviour
{
    [SerializeField] private PortalRenderer[] portals = new PortalRenderer[2];
    [SerializeField] private LayerMask shootMask = ~0;
    [SerializeField] private float shootDistance = 1000f;

    // Portal appearance
    [SerializeField] private float portalAppearDuration = 0.3f;
    [SerializeField] private float portalTargetRadius = 0.4f;
    [SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Placement clamp
    [SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f); // half width/height on the wall (world units)
    [SerializeField] private float wallOffset = 0.02f;                   // avoid z-fighting
    [SerializeField] private float surfaceSkin = 0.01f;                  // margin from edges

    private PlayerInput _controls;

    private void Awake() { _controls = new PlayerInput(); }
    private void OnEnable() { _controls?.Enable(); }
    private void OnDisable() { _controls?.Disable(); }
    private void OnDestroy() { _controls?.Dispose(); _controls = null; }

    private void Update()
    {
        if (_controls.Player.ShootBlue.WasPerformedThisFrame()) FirePortal(0);
        if (_controls.Player.ShootOrange.WasPerformedThisFrame()) FirePortal(1);
    }

    private void FirePortal(int index)
    {
        var cam = Camera.main;
        if (!cam || index < 0 || index >= portals.Length) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;

        // Compute orientation from surface normal
        Vector3 forward = -hit.normal;
        Vector3 up = Vector3.Cross(forward, Vector3.ProjectOnPlane(cam.transform.right, forward)).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, up);

        // Clamp center on the hit surface
        Vector3 pos;
        bool ok;

        if (hit.collider is BoxCollider box)
        {
            ok = TryClampOnBoxFace(box, hit, portalHalfSize, surfaceSkin, out pos);
        }
        else
        {
            ok = TryClampOnBoundsFallback(hit.collider, hit, portalHalfSize, surfaceSkin, out pos);
        }

        if (!ok) return; // no room

        pos += hit.normal * wallOffset;

        var t = portals[index].transform;
        t.SetPositionAndRotation(pos, rot);
        StartCoroutine(AppearPortal(portals[index]));
    }

    // ---- Helpers ----

    // Accurate for BoxCollider walls. Clamps on the face that was hit.
    private static bool TryClampOnBoxFace(BoxCollider box, RaycastHit hit, Vector2 halfSize, float skin, out Vector3 posW)
    {
        var tr = box.transform;

        // World half-lengths of the box
        Vector3 half = Vector3.Scale(box.size * 0.5f, tr.lossyScale);
        float hx = Mathf.Abs(half.x);
        float hy = Mathf.Abs(half.y);
        float hz = Mathf.Abs(half.z);

        // Pick the face aligned with hit normal
        Vector3 n = hit.normal.normalized;
        float dx = Mathf.Abs(Vector3.Dot(n, tr.right));
        float dy = Mathf.Abs(Vector3.Dot(n, tr.up));
        float dz = Mathf.Abs(Vector3.Dot(n, tr.forward));

        Vector3 faceN, uDir, vDir;
        float uMax, vMax, faceHalf;

        Vector3 c = tr.TransformPoint(box.center);

        if (dx >= dy && dx >= dz)
        {
            faceN = Mathf.Sign(Vector3.Dot(n, tr.right)) * tr.right;
            uDir = tr.up;    uMax = hy;
            vDir = tr.forward; vMax = hz;
            faceHalf = hx;
        }
        else if (dy >= dz)
        {
            faceN = Mathf.Sign(Vector3.Dot(n, tr.up)) * tr.up;
            uDir = tr.right;  uMax = hx;
            vDir = tr.forward; vMax = hz;
            faceHalf = hy;
        }
        else
        {
            faceN = Mathf.Sign(Vector3.Dot(n, tr.forward)) * tr.forward;
            uDir = tr.right;  uMax = hx;
            vDir = tr.up;     vMax = hy;
            faceHalf = hz;
        }

        // Check fit
        if (uMax <= halfSize.x + skin || vMax <= halfSize.y + skin)
        {
            posW = hit.point;
            return false;
        }

        // Face center in world
        Vector3 faceCenter = c + faceN * faceHalf;

        // Offset from face center
        Vector3 d = hit.point - faceCenter;

        float u = Mathf.Clamp(Vector3.Dot(d, uDir.normalized), -(uMax - halfSize.x - skin), (uMax - halfSize.x - skin));
        float v = Mathf.Clamp(Vector3.Dot(d, vDir.normalized), -(vMax - halfSize.y - skin), (vMax - halfSize.y - skin));

        posW = faceCenter + uDir.normalized * u + vDir.normalized * v;
        return true;
    }

    // Generic fallback using collider.bounds projected to the hit plane. Conservative.
    private static bool TryClampOnBoundsFallback(Collider col, RaycastHit hit, Vector2 halfSize, float skin, out Vector3 posW)
    {
        Bounds b = col.bounds;
        Vector3 n = hit.normal.normalized;

        // Build orthonormal basis on the plane
        Vector3 uDir = Vector3.Cross(n, Mathf.Abs(Vector3.Dot(n, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 vDir = Vector3.Cross(n, uDir);

        // Half-lengths along u/v from AABB extents
        Vector3 e = b.extents;
        float uMax = Mathf.Abs(e.x * uDir.x) + Mathf.Abs(e.y * uDir.y) + Mathf.Abs(e.z * uDir.z);
        float vMax = Mathf.Abs(e.x * vDir.x) + Mathf.Abs(e.y * vDir.y) + Mathf.Abs(e.z * vDir.z);

        if (uMax <= halfSize.x + skin || vMax <= halfSize.y + skin)
        {
            posW = hit.point;
            return false;
        }

        // Use plane through the AABB center projected to the hit plane normal
        Vector3 faceCenter = b.center + n * Vector3.Dot(hit.point - b.center, n);

        Vector3 d = hit.point - faceCenter;
        float u = Mathf.Clamp(Vector3.Dot(d, uDir), -(uMax - halfSize.x - skin), (uMax - halfSize.x - skin));
        float v = Mathf.Clamp(Vector3.Dot(d, vDir), -(vMax - halfSize.y - skin), (vMax - halfSize.y - skin));

        posW = faceCenter + uDir * u + vDir * v;
        return true;
    }

    private IEnumerator AppearPortal(PortalRenderer portal)
    {
        portal.gameObject.SetActive(true);
        portal.SetCircleRadius(0f);
        float elapsed = 0f;
        while (elapsed < portalAppearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / portalAppearDuration);
            float curvedT = portalAppearCurve != null ? portalAppearCurve.Evaluate(t) : t;
            float radius = Mathf.LerpUnclamped(0f, portalTargetRadius, curvedT);
            portal.SetCircleRadius(radius);
            yield return null;
        }
        portal.SetCircleRadius(portalTargetRadius);
    }
}
