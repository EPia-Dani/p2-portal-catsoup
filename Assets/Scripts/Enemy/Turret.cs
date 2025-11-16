using UnityEngine;
using FMODUnity;

namespace Enemy
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Turret : InteractableObject
    {
        [Header("References")]
        public Transform head;
        public Transform firePoint;
        public LineRenderer beamRenderer;

        [Header("Settings")]
        public float detectionRadius = 10f;
        public float fireRate = 1f;
        public int burstFlashCount = 4;
        public float flashDuration = 0.025f;
        public float flashGap = 0.010f;
        public float maxRange = 50f;
        public LayerMask obstacleMask = -1;

        [Header("Audio")]
        [Tooltip("FMOD event to play when turret shoots")]
        public EventReference shootSound;

        public FPSController player;
        private float nextFireTime;
        private float nextFlashTime;
        private int currentFlash;
        private bool isFiringBurst;
        private bool isBeamOn;
        private bool isDisabled;

        // Movement detection
        [Header("Movement Disable")]
        [Tooltip("Transform to watch for movement. Defaults to the turret base (this transform). If head == this transform, rotation will be ignored.")]
        public Transform movementReference;
        [Tooltip("Distance in meters the base must move to consider it 'moved' and disable the turret.")]
        public float disableDistanceThreshold = 0.05f;
        [Tooltip("Rotation in degrees the base must rotate to consider it 'moved' and disable the turret. Ignored when head == this transform.")]
        public float disableRotationThreshold = 5f;
        [Tooltip("How often to check for movement (seconds)")]
        public float disableCheckInterval = 0.1f;
        [Tooltip("Grace time after Start before movement checks activate (seconds), avoids disabling while the turret first aims.")]
        public float startupGraceSeconds = 0.5f;
        [Tooltip("Enable verbose debug logging for movement detection")]
        public bool debugMovement = false;


        private Transform _moveRef;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float nextMovementCheck;
        private bool ignoreRotationForMovement;
        private float startTime;
        private bool hasDealtDamageThisBurst = false;

        void Start()
        {
            player = FindFirstObjectByType<FPSController>();
            
            if (head == null)
                head = transform;

            if (firePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(head);
                fp.transform.localPosition = Vector3.zero;
                firePoint = fp.transform;
            }

            if (beamRenderer != null)
                beamRenderer.enabled = false;

            nextFireTime = Time.time;
            nextFlashTime = 0f;
            currentFlash = 0;
            isFiringBurst = false;
            isBeamOn = false;
            isDisabled = false;

            // Initialize movement detection
            _moveRef = movementReference != null ? movementReference : transform;
            ignoreRotationForMovement = (head == transform); // If aiming rotates the root, ignore rotation for movement detection
            lastPosition = _moveRef.position;
            lastRotation = _moveRef.rotation;
            nextMovementCheck = Time.time + disableCheckInterval;
            startTime = Time.time;
            
            // Check if we have a Rigidbody and warn if it's kinematic
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic && debugMovement)
            {
                Debug.LogWarning($"[Turret] {gameObject.name} has a kinematic Rigidbody. Movement detection will work, but physics-based movement won't affect it.");
            }
            
            if (debugMovement)
            {
                Debug.Log($"[Turret] {gameObject.name} movement detection initialized:" +
                          $"\n  - Movement Reference: {_moveRef.name}" +
                          $"\n  - Ignore Rotation: {ignoreRotationForMovement}" +
                          $"\n  - Distance Threshold: {disableDistanceThreshold}m" +
                          $"\n  - Rotation Threshold: {disableRotationThreshold}°" +
                          $"\n  - Grace Period: {startupGraceSeconds}s" +
                          $"\n  - Has Rigidbody: {rb != null}" +
                          $"\n  - Is Kinematic: {(rb != null ? rb.isKinematic.ToString() : "N/A")}" );
            }
        }

        /// <summary>
        /// Called when turret is picked up - permanently disable all turret functionality
        /// </summary>
        public override void OnPickedUp(PlayerPickup holder)
        {
            Debug.Log($"[Turret] OnPickedUp called on {gameObject.name}! Setting isDisabled = true");

            // Disable all turret functionality
            isDisabled = true;

            // Disable beam renderer since turret won't shoot anymore
            if (beamRenderer != null)
            {
                beamRenderer.enabled = false;
                Debug.Log($"[Turret] Disabled beam renderer");
            }

            // Call base implementation for interactable object functionality
            base.OnPickedUp(holder);

            Debug.Log($"[Turret] {gameObject.name} picked up - turret functionality permanently disabled. isDisabled = {isDisabled}");
        }

        void LateUpdate()
        {
            // Movement disable checks (after a small grace period)
            if (!isDisabled)
            {
                if (Time.time - startTime < startupGraceSeconds)
                {
                    // Update baseline during grace period to avoid false positives
                    lastPosition = _moveRef.position;
                    lastRotation = _moveRef.rotation;
                }
                else if (Time.time >= nextMovementCheck)
                {
                    nextMovementCheck = Time.time + disableCheckInterval;

                    float distanceMoved = Vector3.Distance(_moveRef.position, lastPosition);
                    float angleRotated = ignoreRotationForMovement ? 0f : Quaternion.Angle(_moveRef.rotation, lastRotation);

                    if (debugMovement && (distanceMoved > 0.001f || angleRotated > 0.1f))
                    {
                        Debug.Log($"[Turret] {gameObject.name} movement check:" +
                                  $"\n  - Distance moved: {distanceMoved:F4}m (threshold: {disableDistanceThreshold}m)" +
                                  $"\n  - Angle rotated: {angleRotated:F2}° (threshold: {disableRotationThreshold}°)" +
                                  $"\n  - Will disable: {(distanceMoved > disableDistanceThreshold || angleRotated > disableRotationThreshold)}");
                    }

                    // If turret base has moved or rotated significantly, disable it
                    if (distanceMoved > disableDistanceThreshold || angleRotated > disableRotationThreshold)
                    {
                        Debug.Log($"[Turret] {gameObject.name} moved {distanceMoved:F3}m or rotated {angleRotated:F1}° - disabling turret!");
                        isDisabled = true;

                        // Disable beam immediately
                        if (beamRenderer != null)
                        {
                            beamRenderer.enabled = false;
                        }

                        // Early out of update when disabled
                        return;
                    }

                    // Update last known position/rotation
                    lastPosition = _moveRef.position;
                    lastRotation = _moveRef.rotation;
                }
            }

            // Skip all turret logic if disabled (picked up or moved)
            if (isDisabled)
            {
                return;
            }

            if (player == null)
            {
                player = FindFirstObjectByType<FPSController>();
                if (player == null)
                    return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            // Check if player is in range
            if (distanceToPlayer <= detectionRadius)
            {
                // Get player's target position (body center/torso)
                Vector3 playerTargetPos = GetPlayerTargetPosition();
                
                // Aim at player
                if (head != null)
                {
                    Vector3 direction = (playerTargetPos - head.position).normalized;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        head.rotation = Quaternion.Slerp(head.rotation, targetRotation, Time.deltaTime * 5f);
                    }
                }

                // Fire logic
                if (Time.time >= nextFireTime)
                {
                    Fire();
                    nextFireTime = Time.time + (1f / fireRate);
                }
            }

            // Handle flash burst
            if (isFiringBurst)
            {
                if (Time.time >= nextFlashTime)
                {
                    if (!isBeamOn && currentFlash < burstFlashCount)
                    {
                        // Turn beam ON for next flash
                        isBeamOn = true;
                        if (beamRenderer != null)
                        {
                            beamRenderer.enabled = true;
                            UpdateBeam();
                        }
                        nextFlashTime = Time.time + flashDuration;
                    }
                    else if (isBeamOn)
                    {
                        // Turn beam OFF
                        isBeamOn = false;
                        if (beamRenderer != null)
                            beamRenderer.enabled = false;

                        currentFlash++;

                        // Set gap time, or end burst if last flash
                        if (currentFlash < burstFlashCount)
                        {
                            nextFlashTime = Time.time + flashGap;
                        }
                        else
                        {
                            // Burst complete
                            isFiringBurst = false;
                            currentFlash = 0;
                            hasDealtDamageThisBurst = false; // Safety: ensure reset
                        }
                    }
                }
                else if (beamRenderer != null && beamRenderer.enabled && isBeamOn)
                {
                    // Keep beam updated during flash
                    UpdateBeam();
                }
            }
        }

        void Fire()
        {
            if (beamRenderer == null || firePoint == null || player == null)
                return;

            // Play shoot sound
            if (!shootSound.IsNull)
            {
                RuntimeManager.PlayOneShot(shootSound, firePoint.position);
            }

            // Start flash burst
            isFiringBurst = true;
            currentFlash = 0;
            isBeamOn = false;
            nextFlashTime = Time.time; // Start immediately
            hasDealtDamageThisBurst = false; // Reset damage flag
        }

        Vector3 GetPlayerTargetPosition()
        {
            if (player == null) return Vector3.zero;
            
            // Aim at player's torso/chest area (below camera level)
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                // Aim lower - around chest/torso level, well below the camera
                return player.transform.position + Vector3.up * (controller.height * 0.1f);
            }
            
            // Fallback: use player position
            return player.transform.position;
        }

        void UpdateBeam()
        {
            if (beamRenderer == null || firePoint == null || player == null)
                return;

            Vector3 startPos = firePoint.position;
            Vector3 targetPos = GetPlayerTargetPosition();
            Vector3 direction = (targetPos - startPos).normalized;
            float distanceToPlayer = Vector3.Distance(startPos, targetPos);
            Vector3 endPos = targetPos;
            bool hitPlayer = false;

            // Raycast to check for obstacles between turret and player
            // Only use hit point if it's closer than the player (something is blocking)
            if (Physics.Raycast(startPos, direction, out RaycastHit hit, Mathf.Min(distanceToPlayer, maxRange), obstacleMask))
            {
                float hitDistance = Vector3.Distance(startPos, hit.point);
                
                // Only use the hit point if it's actually blocking (closer than player)
                // This prevents hitting the ground or player's collider incorrectly
                if (hitDistance < distanceToPlayer - 0.1f) // Small buffer to avoid floating point issues
                {
                    endPos = hit.point;
                }
                else
                {
                    // Hit point is at or beyond player, so target player directly
                    endPos = targetPos;
                    hitPlayer = true;
                }
            }
            else if (distanceToPlayer > maxRange)
            {
                // Player beyond max range, show beam up to max range
                endPos = startPos + direction * maxRange;
            }
            else
            {
                // Target player center directly - beam is hitting player
                endPos = targetPos;
                hitPlayer = true;
            }

            beamRenderer.SetPosition(0, startPos);
            beamRenderer.SetPosition(1, endPos);

            // Deal damage to player when beam is actively hitting them during a flash
            // Only damage once per burst to avoid multiple hits
            if (hitPlayer && isBeamOn && isFiringBurst && !hasDealtDamageThisBurst)
            {
                // Damage player - FPSController has invulnerability built in
                player.TakeDamage(1f);
                hasDealtDamageThisBurst = true; // Mark that we've dealt damage this burst
            }
        }
    }
}
