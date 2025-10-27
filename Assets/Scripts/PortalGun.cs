using System.Collections;
using UnityEngine;
using Input;

/// <summary>
/// Portal gun that places portals with Input System actions.
/// </summary>
public class PortalGun : MonoBehaviour
{
    private static readonly int CircleRadius = Shader.PropertyToID("_CircleRadius");
    [SerializeField] private PortalRenderer[] portals = new PortalRenderer[2];
    [SerializeField] private LayerMask shootMask = ~0;
    [SerializeField] private float shootDistance = 1000f;
    [SerializeField] private float portalAppearDuration = 0.3f;
    [SerializeField] private float portalTargetRadius = 0.4f;
    [SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private PlayerInput _controls;

    private void Awake() {_controls = new PlayerInput();
}

    private void OnEnable(){ if (_controls != null) _controls.Enable();}

    private void OnDisable() { if (_controls != null) _controls.Disable();}

    private void OnDestroy(){ if (_controls != null) { _controls.Dispose(); _controls = null; }}

    private void Update()
    {
        if (_controls.Player.ShootBlue.WasPerformedThisFrame())
        {
            FirePortal(0);
        }

        if (_controls.Player.ShootOrange.WasPerformedThisFrame())
        {
            FirePortal(1);
        }
    }

    private void FirePortal(int index) {
        Camera cam = Camera.main;
        if (cam == null || index < 0 || index >= portals.Length) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore))
        {
            
            Transform portalTransform = portals[index].transform;
            portalTransform.position = hit.point + hit.normal * 0.02f;
            Vector3 forward = -hit.normal;
            Vector3 up = Vector3.Cross(forward, Vector3.ProjectOnPlane(cam.transform.right, forward)).normalized;
            portalTransform.rotation = Quaternion.LookRotation(forward, up);
            
            StartCoroutine(AppearPortal(portals[index]));
        }
    }

    //Create coroutine to make the portal appear from the center using it's shader using _circleRadius
    private IEnumerator AppearPortal(PortalRenderer portal) {
        portal.gameObject.SetActive(true);
        portal.SetCircleRadius(0f);
        float elapsed = 0f;
        while (elapsed < portalAppearDuration) {
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
