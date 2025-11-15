using UnityEngine;
using System.Collections.Generic;
using Interact;

namespace RefractionCubes
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserEmitter : MonoBehaviour
    {
        public LineRenderer line;
        public float maxDistance = 128f;
        public int maxReflections = 128;
        public bool checkForIntruders = true;
        [Tooltip("If true, raycast will hit trigger colliders as well")]
        public bool includeTriggerColliders = true;

        [Header("State")] 
        private readonly List<Vector3> _linePoints = new();

        private void Awake()
        {
            // Ensure the line renderer is assigned
            if (line == null)
                line = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (line == null)
                return; // safety guard

            _linePoints.Clear();
            _linePoints.Add(transform.position);

            ShootLaser(transform.position, transform.forward, maxReflections);

            line.positionCount = _linePoints.Count;
            line.SetPositions(_linePoints.ToArray());
        }

        private void ShootLaser(Vector3 position, Vector3 direction, int reflectionsRemaining)
        {
            if (reflectionsRemaining <= 0)
                return;

            reflectionsRemaining--;
            direction = direction.normalized;

            var qti = includeTriggerColliders ? 
                QueryTriggerInteraction.Collide : 
                QueryTriggerInteraction.Ignore;

            // Perform the raycast
            if (!Physics.Raycast(
                    position, 
                    direction, 
                    out RaycastHit hit, 
                    maxDistance, 
                    Physics.DefaultRaycastLayers, 
                    qti))
            {
                _linePoints.Add(position + direction * maxDistance);
                // Visual debug: draw the final segment in the Scene view for one frame
                Debug.DrawLine(position, position + direction * maxDistance, Color.red, 0.1f);
                return;
            }

            _linePoints.Add(hit.point);
            // Visual debug: draw the hit segment in the Scene view for one frame
            Debug.DrawLine(position, hit.point, Color.red, 0.1f);

            var hitCollider = hit.collider;
            if (hitCollider == null)
                return; 

            var refraction = hitCollider.GetComponentInParent<RefractionCube>();
            if (refraction != null)
            {
                Debug.Log("Refraction cube detected: " + refraction.gameObject.name);

                Vector3 localExit = refraction.refractionDirection;
                Vector3 exitDirection;

                if (localExit.sqrMagnitude <= Mathf.Epsilon)
                {
                    exitDirection = Vector3.Reflect(direction, hit.normal).normalized;
                }
                else
                {
                    exitDirection = refraction.transform.TransformDirection(localExit.normalized);
                }

                const float exitOffset = 0.06f;
                Vector3 exitOrigin = refraction.transform.position + exitDirection * exitOffset;

                if (Vector3.Distance(exitOrigin, hit.point) < 0.01f)
                {
                    exitOrigin = hit.point + exitDirection * exitOffset;
                }

                ShootLaser(exitOrigin, exitDirection, reflectionsRemaining);
                return;
            }

            if (checkForIntruders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    // Prefer to find the FPSController on the hit object (or its parents)
                    var fps = hitCollider.GetComponentInParent<FPSController>();
                    if (fps != null)
                    {
                        fps.KillFromExternal();
                        Debug.Log("Player hit by laser via FPSController: " + fps.gameObject.name);
                        return;
                    }

                    // Fallback: try PlayerManager singleton
                    var pm = PlayerManager.Instance;
                    if (pm != null)
                    {
                        pm.OnPlayerDeath();
                        Debug.Log("Player hit by laser via PlayerManager singleton.");
                        return;
                    }

                    Debug.LogWarning("Laser hit object tagged Player but no FPSController or PlayerManager found.");
                    return;
                }

                if (hitCollider.CompareTag("Button"))
                {
                    // Detailed logging to help debug why buttons aren't triggered
                    Debug.Log($"Laser hit collider '{hitCollider.gameObject.name}' with tag '{hitCollider.gameObject.tag}'");

                    // First try parent chain, then try children as a fallback (some button setups put the ScriptableTrigger on a child)
                    var button = hitCollider.GetComponentInParent<ScriptableTrigger>();
                    if (button == null)
                    {
                        button = hitCollider.GetComponentInChildren<ScriptableTrigger>();
                        if (button != null)
                        {
                            Debug.Log($"Found ScriptableTrigger on child of '{hitCollider.gameObject.name}' -> invoking onEnter on '{button.gameObject.name}'");
                        }
                    }

                    if (button != null)
                    {
                        try
                        {
                            button.onEnter.Invoke();
                            Debug.Log("Button hit by laser: " + button.gameObject.name);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"Failed to invoke onEnter on ScriptableTrigger '{button.gameObject.name}': {ex}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Laser hit '{hitCollider.gameObject.name}' tagged Button but no ScriptableTrigger found in parents or children.");
                    }
                }
                else
                {
                    // Fallback: if object isn't tagged Button but a ScriptableTrigger exists on parent/children, invoke it.
                    var strayTrigger = hitCollider.GetComponentInParent<ScriptableTrigger>() ?? hitCollider.GetComponentInChildren<ScriptableTrigger>();
                    if (strayTrigger != null)
                    {
                        Debug.LogWarning($"Laser hit '{hitCollider.gameObject.name}' which is NOT tagged 'Button', but a ScriptableTrigger was found on '{strayTrigger.gameObject.name}'. Invoking onEnter as fallback.");
                        try
                        {
                            strayTrigger.onEnter.Invoke();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"Failed to invoke onEnter on fallback ScriptableTrigger '{strayTrigger.gameObject.name}': {ex}");
                        }
                    }
                }
            }
        }
    }
}
