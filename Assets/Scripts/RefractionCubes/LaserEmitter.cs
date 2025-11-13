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
        public bool checkForIntruders = false;
        [Tooltip("If true, raycast will hit trigger colliders as well")]
        public bool includeTriggerColliders = false;

        [Header("State")] private readonly List<Vector3> _linePoints = new();
        private PlayerManager _playerManager;

        private void Update()
        {
            _linePoints.Clear();
            _linePoints.Add(transform.position);
            ShootLaser(transform.position, transform.forward, maxReflections);
            line.positionCount = _linePoints.Count;
            line.SetPositions(_linePoints.ToArray());
        }

        void ShootLaser(Vector3 position, Vector3 direction, int reflectionsRemaining)
        {
            if (reflectionsRemaining <= 0) return;

            reflectionsRemaining--;
            direction = direction.normalized;

            var qti = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            if (!Physics.Raycast(position, direction, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers, qti))
            {
                _linePoints.Add(position + direction * maxDistance);
                return;
            }

            _linePoints.Add(hit.point);
            var hitCollider = hit.collider;

            // Prefer component-based detection (works if the tag is on a parent or the component is on a parent)
            var refraction = hitCollider.GetComponentInParent<RefractionCube>();
            if (refraction != null)
            {
                Debug.Log("Refraction cube detected: " + refraction.gameObject.name);

                // Convert the cube's configured refraction direction from local to world space.
                Vector3 localExit = refraction.refractionDirection;
                Vector3 exitDirection;

                if (localExit.sqrMagnitude <= Mathf.Epsilon)
                {
                    // Fallback: reflect off the hit normal if no exit direction configured
                    exitDirection = Vector3.Reflect(direction, hit.normal).normalized;
                }
                else
                {
                    exitDirection = refraction.transform.TransformDirection(localExit.normalized);
                }

                // Start the next ray slightly outside the cube to avoid immediately hitting the same collider
                const float exitOffset = 0.06f;
                Vector3 exitOrigin = refraction.transform.position + exitDirection * exitOffset;

                // If the exit origin accidentally is inside the collider (rare), move it along the exit direction from the hit point instead
                if (Vector3.Distance(exitOrigin, hit.point) < 0.01f)
                {
                    exitOrigin = hit.point + exitDirection * exitOffset;
                }

                ShootLaser(exitOrigin, exitDirection, reflectionsRemaining);
                return;
            }

            if (checkForIntruders)
            {
                // Try to detect player by component on the hit object or its parents
                var player = hitCollider.GetComponentInParent<PlayerManager>();
                if (player != null)
                {
                    player.OnPlayerDeath();
                    Debug.Log("Player hit by laser: " + player.gameObject.name);
                    return;
                }
            }

            // If we hit something else (non-refraction object), stop the laser at hit point.
        }
    }
}
