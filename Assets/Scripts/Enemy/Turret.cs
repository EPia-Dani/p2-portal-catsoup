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
        public float acquireDelay = 0.5f;    // delay between seeing a target and locking/firing (portal turret style)

        [Header("Aiming & Firing")]
        public float rotationSpeed = 5f;     // how fast the head rotates
        public float fireRate = 1f;          // legacy continuous fire rate (kept for compatibility)
        public float projectileSpeed = 25f;

        [Header("Burst Fire (Portal-style)")]
        public int shotsPerBurst = 3;        // how many shots in a burst
        public float burstShotInterval = 0.1f; // time between shots inside a burst
        public float burstCooldown = 2f;    // cooldown between bursts

        [Header("Audio/Visual (optional)")]
        public AudioSource audioSource;
        public AudioClip wakeClip;           // played when turret first notices target
        public AudioClip acquireClip;        // played when target is acquired/locked
        public AudioClip shootClip;          // optional per-shot sound
        public AudioClip lostClip;           // played when turret loses target
        public ParticleSystem muzzleFlash;   // optional muzzle flash on firing
        public Animator animator;            // optional animator to trigger wake/sleep/shoot states

        [Header("Debug")]
        public bool logFiring = true;        // enable to see debug logs when firing
        public bool allowManualFire; // press ManualFireKey in Play to call FireOnce()
        public KeyCode manualFireKey = KeyCode.F;
        public bool verboseDiagnostics; // when true, prints detailed search/hit info

        // Re-usable buffer for OverlapSphereNonAlloc to avoid GC allocations
        Collider[] overlapResults = new Collider[32];

        Transform currentTarget;
        float fireTimer; // used for burst cooldown or legacy continuous mode
        float loseTimer;

        // burst state
        int shotsRemainingInBurst = 0;
        float shotTimer = 0f; // timer between shots inside burst
        float acquireTimer = 0f; // timer used while acquiring

        // simple internal state machine
        enum State { Idle, Acquiring, Tracking, Firing, Cooldown }
        State state = State.Idle;

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

            if (targetMask.value == 0)
                Debug.LogWarning($"Turret '{name}': targetMask is zero (no layers selected). Make sure the intended targets are on a layer included by the mask.", this);

            // set idle animator state if available
            if (animator != null)
            {
                animator.Play("Idle");
            }
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
            switch (state)
            {
                case State.Idle:
                    // try to find a target
                    if (currentTarget == null)
                        FindTarget();
                    // if a target was found by FindTarget it will have moved us to Acquiring
                    break;

                case State.Acquiring:
                    if (currentTarget == null)
                    {
                        state = State.Idle;
                        acquireTimer = 0f;
                        break;
                    }

                    // Aim while acquiring
                    AimAt(currentTarget.position);
                    acquireTimer -= Time.deltaTime;
                    if (acquireTimer <= 0f)
                    {
                        // lock on and enter tracking/firing
                        state = State.Tracking;
                        shotsRemainingInBurst = shotsPerBurst;
                        if (audioSource != null && acquireClip != null) audioSource.PlayOneShot(acquireClip);
                        if (animator != null) animator.SetTrigger("Locked");
                        if (logFiring) Debug.Log($"Turret '{name}': locked on {currentTarget.name} and ready to fire.", this);
                    }
                    break;

                case State.Tracking:
                    if (currentTarget == null)
                    {
                        state = State.Idle;
                        break;
                    }

                    if (!IsTargetValid(currentTarget))
                    {
                        loseTimer += Time.deltaTime;
                        if (loseTimer >= loseTargetDelay)
                        {
                            if (logFiring) Debug.Log($"Turret '{name}': lost target {currentTarget.name}.", this);
                            PlayClipSafe(lostClip);
                            currentTarget = null;
                            loseTimer = 0f;
                            state = State.Idle;
                        }
                    }
                    else
                    {
                        loseTimer = 0f;
                        AimAt(currentTarget.position);
                        // start firing immediately when tracking
                        state = State.Firing;
                        shotTimer = 0f; // allow immediate shot in burst
                        if (animator != null) animator.SetTrigger("Wake");
                    }
                    break;

                case State.Firing:
                    if (currentTarget == null)
                    {
                        state = State.Idle;
                        break;
                    }

                    if (!IsTargetValid(currentTarget))
                    {
                        loseTimer += Time.deltaTime;
                        if (loseTimer >= loseTargetDelay)
                        {
                            if (logFiring) Debug.Log($"Turret '{name}': lost target {currentTarget.name}.", this);
                            PlayClipSafe(lostClip);
                            currentTarget = null;
                            loseTimer = 0f;
                            state = State.Idle;
                        }
                        break;
                    }

                    // keep aiming while firing
                    AimAt(currentTarget.position);
                    break;

                case State.Cooldown:
                    // wait for cooldown between bursts
                    fireTimer -= Time.deltaTime;
                    if (fireTimer <= 0f)
                    {
                        fireTimer = 0f;
                        // after cooldown, go back to Tracking so Acquire/lock behaves consistently
                        shotsRemainingInBurst = shotsPerBurst;
                        state = State.Tracking;
                    }
                    break;
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
                // start acquiring (small delay before firing, like the portal turret)
                acquireTimer = acquireDelay;
                state = State.Acquiring;
                // reset firing timers
                fireTimer = 0f;
                shotsRemainingInBurst = shotsPerBurst;
                if (logFiring) Debug.Log($"Turret '{name}': acquired target {currentTarget.name} (entering acquiring state).", this);
                PlayClipSafe(wakeClip);
                if (animator != null) animator.SetTrigger("Wake");
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
            // state-driven firing: handle burst logic
            if (state != State.Firing) return;
            if (currentTarget == null) return;
            if (projectilePrefab == null || firePoint == null) return;

            // if there are shots remaining in the current burst, handle shot timing
            if (shotsRemainingInBurst > 0)
            {
                shotTimer -= Time.deltaTime;
                if (shotTimer <= 0f)
                {
                    // fire one shot
                    FireOnce();
                    shotsRemainingInBurst--;
                    shotTimer = burstShotInterval;

                    if (muzzleFlash != null) muzzleFlash.Play();
                    PlayClipSafe(shootClip);
                    if (animator != null) animator.SetTrigger("Shoot");
                }
            }
            else
            {
                // burst finished - enter cooldown state
                state = State.Cooldown;
                fireTimer = burstCooldown;
                if (logFiring) Debug.Log($"Turret '{name}': burst finished, entering cooldown for {burstCooldown} seconds.", this);
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
                if (logFiring) Debug.LogWarning($"Turret '{name}' projectile has no Rigidbody - adding one at runtime so it moves in builds.", projectileInstance);
                // add a Rigidbody so the projectile can move. This helps when prefabs were missing the component in some builds.
                rb = projectileInstance.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (rb == null)
            {
                if (logFiring) Debug.LogWarning($"Turret '{name}': no Rigidbody available on projectile; it won't move.", projectileInstance);
            }
            else
            {
                if (projectileSpeed <= 0f)
                {
                    Debug.LogWarning($"Turret '{name}': projectileSpeed is {projectileSpeed}. Projectile will not move; set a positive speed.", this);
                }
                else
                {
                    // set velocity directly for deterministic behaviour in builds
                    rb.linearVelocity = firePoint.forward * projectileSpeed;
                    if (logFiring) Debug.Log($"Turret '{name}': set projectile velocity to {rb.linearVelocity} (rb name: '{rb.name}').", projectileInstance);
                }
            }
        }

        // Helper that plays a clip using the assigned AudioSource if available, otherwise falls back to PlayClipAtPoint
        void PlayClipSafe(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                Vector3 pos = firePoint != null ? firePoint.position : transform.position;
                AudioSource.PlayClipAtPoint(clip, pos);
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