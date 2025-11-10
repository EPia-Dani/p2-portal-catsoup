using UnityEngine;

namespace Enemy
{
    public class Turret : MonoBehaviour
    {
        [Header("References")]
        public Transform head;               // part that rotates to aim (defaults to this.transform if null)
        public Transform firePoint;          // where projectiles spawn
        public GameObject projectilePrefab;  // prefab with Rigidbody and collision handling

        [Header("Detection")]
        public float detectionRadius = 10f;
        public LayerMask targetMask = ~0;    // which layers are considered targets
        public LayerMask obstacleMask = 0;   // layers that block line-of-sight (e.g. Default)
        public bool requireLineOfSight = true;
        public float loseTargetDelay = 0.2f; // small grace before losing a target

        [Header("Aiming & Firing")]
        public float rotationSpeed = 5f;     // how fast the head rotates
        public float fireRate = 1f;          // shots per second
        public float projectileSpeed = 25f;

        [Header("Debug")]
        public bool logFiring = true;        // enable to see debug logs when firing
        public bool allowManualFire; // press ManualFireKey in Play to call FireOnce()
        public KeyCode manualFireKey = KeyCode.F;
        public bool verboseDiagnostics; // when true, prints detailed search/hit info

        // Re-usable buffer for OverlapSphereNonAlloc to avoid GC allocations
        Collider[] overlapResults = new Collider[32];

        Transform currentTarget;
        float fireTimer;
        float loseTimer;

        // cache turret colliders to ignore collisions with spawned projectiles
        Collider[] turretColliders;

        void Reset()
        {
            // sensible defaults when added from editor
            head = transform;
        }

        void Start()
        {
            if (head == null) head = transform;
            fireTimer = 0f; // allow immediate firing on acquire

            turretColliders = GetComponentsInChildren<Collider>(true);

            if (firePoint == null)
                Debug.LogWarning($"Turret '{name}': firePoint is not assigned. Assign an empty child Transform as the muzzle.", this);

            if (projectilePrefab == null)
                Debug.LogWarning($"Turret '{name}': projectilePrefab is not assigned. Assign a prefab with a Mesh/Renderer, Collider and Rigidbody.", this);

            if ((int)targetMask == 0)
                Debug.LogWarning($"Turret '{name}': targetMask is zero (no layers selected). Make sure the intended targets are on a layer included by the mask.", this);
        }

        void Update()
        {
            AcquireAndTrack();
            HandleFiring();

            // debug line to current target
            if (currentTarget != null && firePoint != null)
            {
                Debug.DrawLine(firePoint.position, currentTarget.position, Color.red);
            }

            // manual-fire debug: helpful to confirm projectiles spawn/behave
            if (allowManualFire && UnityEngine.Input.GetKeyDown(manualFireKey))
            {
                if (logFiring) Debug.Log($"Turret '{name}': manual fire requested.", this);
                FireOnce();
            }
        }

        void AcquireAndTrack()
        {
            if (currentTarget == null)
            {
                FindTarget();
            }
            else
            {
                // validate target still valid
                if (!IsTargetValid(currentTarget))
                {
                    loseTimer += Time.deltaTime;
                    if (loseTimer >= loseTargetDelay)
                    {
                        if (logFiring) Debug.Log($"Turret '{name}': lost target {currentTarget.name}.", this);
                        currentTarget = null;
                        loseTimer = 0f;
                    }
                }
                else
                {
                    loseTimer = 0f;
                    AimAt(currentTarget.position);
                }
            }
        }

        void FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, overlapResults, targetMask);
            if (logFiring && verboseDiagnostics) Debug.Log($"Turret '{name}': OverlapSphere found {count} colliders.");
            float bestDist = Mathf.Infinity;
            Transform best = null;

            for (int i = 0; i < count; i++)
            {
                var c = overlapResults[i];
                if (logFiring && verboseDiagnostics) Debug.Log($"Turret '{name}': hit[{i}] = '{(c==null?"<null>":c.name)}' (layer={LayerMask.LayerToName(c.gameObject.layer)}).", this);
                if (c == null) continue;
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < bestDist)
                {
                    if (requireLineOfSight && firePoint != null)
                    {
                        Vector3 dir = (c.transform.position - firePoint.position).normalized;
                        if (Physics.Raycast(firePoint.position, dir, out RaycastHit hit, detectionRadius, obstacleMask))
                        {
                            if (logFiring && verboseDiagnostics) Debug.Log($"Turret '{name}': Raycast hit '{hit.collider.name}' blocking '{c.name}'.", this);
                            // blocked by something else
                            if (hit.collider != c && !hit.collider.transform.IsChildOf(c.transform))
                                continue;
                        }
                        else if (logFiring && verboseDiagnostics)
                        {
                            Debug.Log($"Turret '{name}': Line of sight clear to '{c.name}'.", this);
                        }
                    }

                    bestDist = d;
                    best = c.transform;
                }
            }

            if (best != null)
            {
                currentTarget = best;
                // reset the fire timer so we shoot immediately when acquiring a new target
                fireTimer = 0f;
                if (logFiring) Debug.Log($"Turret '{name}': acquired target {currentTarget.name}.", this);
            }
            else
            {
                // optional debug when no target found (comment out if noisy)
                if (logFiring) Debug.Log($"Turret '{name}': no target in range.", this);
            }
        }

        bool IsTargetValid(Transform t)
        {
            if (t == null) return false;
            if (Vector3.Distance(transform.position, t.position) > detectionRadius) return false;

            if (requireLineOfSight && firePoint != null)
            {
                Vector3 dir = (t.position - firePoint.position).normalized;
                if (Physics.Raycast(firePoint.position, dir, out RaycastHit hit, detectionRadius, obstacleMask))
                {
                    if (hit.collider.transform != t && !hit.collider.transform.IsChildOf(t)) return false;
                }
            }

            return true;
        }

        void AimAt(Vector3 worldPos)
        {
            if (head == null) head = transform;
            Vector3 dir = worldPos - head.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion wanted = Quaternion.LookRotation(dir);
            head.rotation = Quaternion.Slerp(head.rotation, wanted, rotationSpeed * Time.deltaTime);
        }

        void HandleFiring()
        {
            if (currentTarget == null) return;
            if (projectilePrefab == null || firePoint == null) return;

            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                FireOnce();
                fireTimer = 1f / Mathf.Max(0.0001f, fireRate);
            }
        }

        void FireOnce()
        {
            if (projectilePrefab == null || firePoint == null)
            {
                if (logFiring) Debug.LogWarning($"Turret '{name}' attempted to fire but projectilePrefab or firePoint is missing.", this);
                return;
            }

            var projectileInstance = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            if (logFiring) Debug.Log($"Turret '{name}' fired a projectile at time {Time.time}.", this);

            // debug: log spawn position and name
            if (logFiring) Debug.Log($"Turret '{name}': spawned projectile '{projectileInstance.name}' at {projectileInstance.transform.position}.", projectileInstance);

            // If the projectile prefab has no Renderer (invisible collider-only prefab), create a small debug visual so it's visible in Play mode.
            var childRenderers = projectileInstance.GetComponentsInChildren<Renderer>(true);
            if (childRenderers == null || childRenderers.Length == 0)
            {
                if (logFiring) Debug.Log($"Turret '{name}': projectile '{projectileInstance.name}' has no Renderer; creating debug visual child.", projectileInstance);
                var debugVis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                // remove the collider on the debug visual so it doesn't affect physics
                var visCol = debugVis.GetComponent<Collider>();
                if (visCol != null) Destroy(visCol);
                debugVis.name = "_DebugProjectileVisual";
                debugVis.transform.SetParent(projectileInstance.transform, false);
                debugVis.transform.localPosition = Vector3.zero;
                debugVis.transform.localScale = Vector3.one * 0.2f;
                var mr = debugVis.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    // create a simple material instance so the color is visible
                    mr.material = new Material(Shader.Find("Standard"));
                    mr.material.color = Color.red;
                }
            }

            // prevent the projectile from immediately colliding with the turret
            if (turretColliders != null && turretColliders.Length > 0)
            {
                var projCols = projectileInstance.GetComponentsInChildren<Collider>(true);
                if (logFiring) Debug.Log($"Turret '{name}': projectile has {projCols.Length} colliders; turret has {turretColliders.Length} colliders.", projectileInstance);
                foreach (var pc in projCols)
                {
                    foreach (var tc in turretColliders)
                    {
                        if (pc != null && tc != null)
                        {
                            Physics.IgnoreCollision(pc, tc, true);
                            if (logFiring) Debug.Log($"Turret '{name}': Ignoring collision between projectile collider '{pc.name}' and turret collider '{tc.name}'.", this);
                        }
                    }
                }
            }

            // try to find a Rigidbody on the projectile or its children
            Rigidbody rb = projectileInstance.GetComponentInChildren<Rigidbody>();
            if (rb == null)
            {
                if (logFiring) Debug.LogWarning($"Turret '{name}' projectile has no Rigidbody - it won't move. Add a Rigidbody to the projectile prefab.", projectileInstance);
            }
            else
            {
                if (projectileSpeed <= 0f)
                {
                    Debug.LogWarning($"Turret '{name}': projectileSpeed is {projectileSpeed}. Projectile will not move; set a positive speed.", this);
                }
                else
                {
                    // use AddForce to avoid directly setting possibly-obsolete properties
                    rb.AddForce(firePoint.forward * projectileSpeed, ForceMode.VelocityChange);
                    if (logFiring) Debug.Log($"Turret '{name}': applied force {firePoint.forward * projectileSpeed} to projectile (rb name: '{rb.name}').", projectileInstance);
                }
            }
        }

        /// <summary>
        /// Public helper so you can trigger a single shot from other scripts or context menu.
        /// </summary>
        [ContextMenu("Test Fire")]
        public void TestFire()
        {
            FireOnce();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(firePoint.position, firePoint.position + (firePoint.forward * Mathf.Min(10f, detectionRadius)));
            }
        }

        public void SetEnabled(bool on) => enabled = on;
    }
}