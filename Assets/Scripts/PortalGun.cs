using UnityEngine;
using Input;

/// <summary>
/// Portal gun that places portals with Input System actions.
/// </summary>
public class PortalGun : MonoBehaviour
{
    [SerializeField] private PortalRenderer[] portals = new PortalRenderer[2];
    [SerializeField] private LayerMask shootMask = ~0;
    [SerializeField] private float shootDistance = 1000f;

    private PlayerInput controls;

    private void Awake()
    {
        controls = new PlayerInput();
    }

    private void OnEnable()
    {
        if (controls != null) controls.Enable();
    }

    private void OnDisable()
    {
        if (controls != null) controls.Disable();
    }

    private void OnDestroy()
    {
        if (controls != null) { controls.Dispose(); controls = null; }
    }

    private void Update()
    {
        if (controls == null) return;

        if (controls.Player.ShootBlue.WasPerformedThisFrame())
        {
            FirePortal(0);
        }

        if (controls.Player.ShootOrange.WasPerformedThisFrame())
        {
            FirePortal(1);
        }
    }

    private void FirePortal(int index)
    {
        Camera cam = CameraManager.MainCamera;
        if (cam == null || index < 0 || index >= portals.Length) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore))
        {
            // Place the portal
            if (portals[index] == null)
            {
                Debug.LogWarning($"Portal {index} not assigned!");
                return;
            }

            Transform portalTransform = portals[index].transform;
            portalTransform.position = hit.point + hit.normal * 0.02f;
            Vector3 forward = -hit.normal;
            Vector3 up = Vector3.Cross(forward, Vector3.ProjectOnPlane(cam.transform.right, forward)).normalized;
            portalTransform.rotation = Quaternion.LookRotation(forward, up);
            
            portals[index].gameObject.SetActive(true);
        }
    }
}
