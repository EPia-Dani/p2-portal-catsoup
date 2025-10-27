using System.Collections;
using UnityEngine;
using Input;

public class PortalGun : MonoBehaviour
{
    [SerializeField] private PortalRenderer[] portals = new PortalRenderer[2];
    [SerializeField] private LayerMask shootMask = ~0;
    [SerializeField] private float shootDistance = 1000f;
    [SerializeField] private Camera shootCamera;

    [SerializeField] private float portalAppearDuration = 0.3f;
    [SerializeField] private float portalTargetRadius = 0.4f;
    [SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f);
    [SerializeField] private float wallOffset = 0.02f;
    [SerializeField] private float surfaceSkin = 0.01f;

    private PlayerInput _controls;

    private void Awake() { _controls = new PlayerInput(); shootCamera ??= Camera.main; }
    private void OnEnable() { _controls?.Enable(); shootCamera ??= Camera.main; }
    private void OnDisable() { _controls?.Disable(); }
    private void OnDestroy() { _controls?.Dispose(); _controls = null; }

    private void Update()
    {
        if (_controls.Player.ShootBlue.WasPerformedThisFrame()) FirePortal(0);
        if (_controls.Player.ShootOrange.WasPerformedThisFrame()) FirePortal(1);
    }

    private void FirePortal(int index)
    {
        var cam = shootCamera ? shootCamera : (shootCamera = Camera.main);
        if (!cam || index < 0 || index >= portals.Length) return;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;

        Vector3 forward = -hit.normal;
        Vector3 up = Vector3.Cross(forward, Vector3.ProjectOnPlane(cam.transform.right, forward)).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, up);

        Vector3 pos;
        bool ok = hit.collider is BoxCollider box
            ? TryClampOnBoxFace(box, hit, portalHalfSize, surfaceSkin, out pos)
            : TryClampOnBoundsFallback(hit.collider, hit, portalHalfSize, surfaceSkin, out pos);
        if (!ok) return;
        pos += hit.normal * wallOffset;

        var portal = portals[index];
        if (!portal) return;
        var t = portal.transform;
        t.SetPositionAndRotation(pos, rot);
        StartCoroutine(AppearPortal(portal));
    }

    private static bool TryClampOnBoxFace(BoxCollider box, RaycastHit hit, Vector2 halfSize, float skin, out Vector3 posW)
    {
        var tr = box.transform;
        Vector3 half = Vector3.Scale(box.size * 0.5f, tr.lossyScale);
        float hx = Mathf.Abs(half.x);
        float hy = Mathf.Abs(half.y);
        float hz = Mathf.Abs(half.z);
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
        if (uMax <= halfSize.x + skin || vMax <= halfSize.y + skin)
        {
            posW = hit.point;
            return false;
        }
        Vector3 faceCenter = c + faceN * faceHalf;
        Vector3 d = hit.point - faceCenter;
        Vector3 uN = uDir.normalized;
        Vector3 vN = vDir.normalized;
        float uLimit = (uMax - halfSize.x - skin);
        float vLimit = (vMax - halfSize.y - skin);
        float u = Mathf.Clamp(Vector3.Dot(d, uN), -uLimit, uLimit);
        float v = Mathf.Clamp(Vector3.Dot(d, vN), -vLimit, vLimit);
        posW = faceCenter + uN * u + vN * v;
        return true;
    }

    private static bool TryClampOnBoundsFallback(Collider col, RaycastHit hit, Vector2 halfSize, float skin, out Vector3 posW)
    {
        Bounds b = col.bounds;
        Vector3 n = hit.normal.normalized;
        Vector3 uDir = Vector3.Cross(n, Mathf.Abs(Vector3.Dot(n, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 vDir = Vector3.Cross(n, uDir);
        Vector3 e = b.extents;
        float uMax = Mathf.Abs(e.x * uDir.x) + Mathf.Abs(e.y * uDir.y) + Mathf.Abs(e.z * uDir.z);
        float vMax = Mathf.Abs(e.x * vDir.x) + Mathf.Abs(e.y * vDir.y) + Mathf.Abs(e.z * vDir.z);
        if (uMax <= halfSize.x + skin || vMax <= halfSize.y + skin)
        {
            posW = hit.point;
            return false;
        }
        Vector3 faceCenter = b.center + n * Vector3.Dot(hit.point - b.center, n);
        Vector3 d = hit.point - faceCenter;
        float uLimit = (uMax - halfSize.x - skin);
        float vLimit = (vMax - halfSize.y - skin);
        float u = Mathf.Clamp(Vector3.Dot(d, uDir), -uLimit, uLimit);
        float v = Mathf.Clamp(Vector3.Dot(d, vDir), -vLimit, vLimit);
        posW = faceCenter + uDir * u + vDir * v;
        return true;
    }

    private IEnumerator AppearPortal(PortalRenderer portal)
    {
        portal.gameObject.SetActive(true);
        portal.SetCircleRadius(0f);
        float elapsed = 0f;
        float invDur = portalAppearDuration > 0f ? 1f / portalAppearDuration : 0f;
        bool hasCurve = portalAppearCurve != null;
        float targetRadius = portalTargetRadius;
        while (elapsed < portalAppearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed * invDur);
            float curvedT = hasCurve ? portalAppearCurve.Evaluate(t) : t;
            float radius = Mathf.LerpUnclamped(0f, targetRadius, curvedT);
            portal.SetCircleRadius(radius);
            yield return null;
        }
        portal.SetCircleRadius(targetRadius);
    }
}
