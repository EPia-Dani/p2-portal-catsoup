using UnityEngine;
using System.Collections.Generic;

namespace Infrastructure
{
    [DisallowMultipleComponent]
    public class BreakableObject : MonoBehaviour
    {
        [Header("Fracture")]
        [Tooltip("Optional: assign a prefab containing pre-fractured pieces (children with Rigidbody+Collider).")]
        public GameObject fracturedPrefab;

        [Tooltip("If no fracturedPrefab is assigned, this many primitive fragments will be generated at runtime.")]
        public int runtimeFragmentCount = 12;

        [Tooltip("Radius used for the explosion force applied to fragments (meters).")]
        public float explosionRadius = 1.5f;

        [Tooltip("Base explosion force applied to fragments. Multiplied by optional forceMultiplier passed to Break().")]
        public float explosionForce = 6f;

        [Tooltip("Upwards modifier for AddExplosionForce to give a little lift.")]
        public float upwardModifier = 0.5f;

        [Tooltip("If true, fragments will inherit the incoming object's linear velocity (multiplied by inheritVelocityMultiplier).")]
        public bool inheritVelocity = true;

        [Tooltip("Multiplier applied to the incoming velocity when inheriting.")]
        public float inheritVelocityMultiplier = 1f;

        [Header("Runtime fragment settings")]
        [Tooltip("Min/max local scale (per-axis) for generated primitive fragments.")]
        public Vector3 minFragmentScale = new Vector3(0.12f, 0.12f, 0.12f);
        public Vector3 maxFragmentScale = new Vector3(0.4f, 0.4f, 0.4f);

        [Tooltip("How long generated fragments live before being destroyed (seconds). Set <=0 to never auto-destroy).")]
        public float fragmentLifetime = 8f;

        [Tooltip("Material to apply to generated fragments. Optional.")]
        public Material fragmentMaterial;

        [Header("Debug")]
        public bool debugLogs = false;
        
        public void Break(Vector3 impactPoint, Vector3 incomingVelocity, float forceMultiplier = 1f)
        {
            if (debugLogs) Debug.Log($"BreakableObject: Breaking '{gameObject.name}' at {impactPoint} (incomingVel={incomingVelocity.magnitude:F2})");

            if (fracturedPrefab != null)
            {
                // Instantiate fractured prefab at the same transform
                var inst = Instantiate(fracturedPrefab, transform.position, transform.rotation, null);
                inst.transform.localScale = transform.lossyScale; // best-effort scale application

                // Ensure any particle systems in the fractured prefab appear white (user requested white break particles)
                var psChildren = inst.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in psChildren)
                {
                    var main = ps.main;
                    main.startColor = Color.white;
                }

                // Apply explosion forces to all rigidbodies under the prefab
                var rbs = inst.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in rbs)
                {
                    if (rb == null) continue;
                    rb.AddExplosionForce(explosionForce * forceMultiplier, impactPoint, explosionRadius, upwardModifier, ForceMode.Impulse);
                    if (inheritVelocity)
                    {
                        AddVelocitySafe(rb, incomingVelocity * inheritVelocityMultiplier);
                    }
                    if (fragmentLifetime > 0f)
                    {
                        Destroy(rb.gameObject, fragmentLifetime);
                    }
                }

                // Destroy original intact object
                Destroy(gameObject);
                return;
            }

            // Fallback: simple runtime fracture using primitive fragments
            GenerateRuntimeFragments(impactPoint, incomingVelocity, forceMultiplier);

            // Destroy original intact object
            Destroy(gameObject);
        }

        void GenerateRuntimeFragments(Vector3 impactPoint, Vector3 incomingVelocity, float forceMultiplier)
        {
            // Try to get a Renderer bounds for distribution; if none, use small area around transform
            Bounds bounds = new Bounds(transform.position, Vector3.one * 0.5f);
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                bounds = rend.bounds;
            }

            List<GameObject> spawned = new List<GameObject>(runtimeFragmentCount);

            for (int i = 0; i < runtimeFragmentCount; i++)
            {
                // Create primitive fragment - use cube for stability
                GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.name = gameObject.name + "_frag_" + i;

                // Position: random point within bounds (a little jitter)
                Vector3 localPos = new Vector3(
                    Random.Range(-0.4f, 0.4f),
                    Random.Range(-0.4f, 0.4f),
                    Random.Range(-0.4f, 0.4f)
                );
                Vector3 worldPos = bounds.center + Vector3.Scale(localPos, bounds.extents);
                frag.transform.position = worldPos;

                // Random scale
                frag.transform.localScale = new Vector3(
                    Random.Range(minFragmentScale.x, maxFragmentScale.x),
                    Random.Range(minFragmentScale.y, maxFragmentScale.y),
                    Random.Range(minFragmentScale.z, maxFragmentScale.z)
                );

                // Rotation
                frag.transform.rotation = Random.rotation;

                // Rigidbody
                var rb = frag.AddComponent<Rigidbody>();
                rb.mass = 0.5f;

                // Collider already added by CreatePrimitive; leave it enabled

                // Optional material
                var mr = frag.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (fragmentMaterial != null)
                    {
                        mr.material = fragmentMaterial;
                        // force white color on fragment material so break fragments are white
                        try { mr.material.color = Color.white; } catch { }
                    }
                    else
                    {
                        // create a simple white material for visibility
                        var mat = new Material(Shader.Find("Standard"));
                        mat.color = Color.white;
                        mr.material = mat;
                    }
                }

                // Apply explosion
                rb.AddExplosionForce(explosionForce * forceMultiplier, impactPoint, explosionRadius, upwardModifier, ForceMode.Impulse);

                if (inheritVelocity)
                {
                    AddVelocitySafe(rb, incomingVelocity * inheritVelocityMultiplier);
                }

                // Track for cleanup
                spawned.Add(frag);

                // Schedule fragment destruction
                if (fragmentLifetime > 0f)
                {
                    Destroy(frag, fragmentLifetime);
                }
            }

            if (debugLogs) Debug.Log($"BreakableObject: Spawned {spawned.Count} runtime fragments for '{gameObject.name}'.");
        }

        // Helper to add velocity in a way compatible with different Unity versions
        void AddVelocitySafe(Rigidbody rb, Vector3 deltaVelocity)
        {
            if (rb == null) return;
            // Use the standard velocity property which is available across Unity versions
            rb.linearVelocity += deltaVelocity;
        }
    }
}
