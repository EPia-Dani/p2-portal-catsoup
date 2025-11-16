using UnityEngine;

namespace Enemy
{
    public class Turret : MonoBehaviour
    {
        [Header("References")]
        public Transform head;
        public Transform firePoint;
        public LineRenderer beamRenderer;

        [Header("Settings")]
        public float detectionRadius = 10f;
        public float fireRate = 1f;
        public float beamDuration = 0.1f;
        public float maxRange = 50f;
        public LayerMask obstacleMask = -1;

        private FPSController player;
        private float nextFireTime;
        private float beamEndTime;
        private bool beamActive;

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
            beamActive = false;
        }

        void Update()
        {
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
                // Get player's target position (camera/head if available, otherwise center)
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
                    beamEndTime = Time.time + beamDuration;
                    beamActive = true;
                }
            }

            // Handle beam visibility
            if (beamActive)
            {
                if (Time.time >= beamEndTime)
                {
                    beamActive = false;
                    if (beamRenderer != null)
                        beamRenderer.enabled = false;
                }
                else if (beamRenderer != null && firePoint != null)
                {
                    UpdateBeam();
                }
            }
        }

        void Fire()
        {
            if (beamRenderer == null || firePoint == null || player == null)
                return;

            beamRenderer.enabled = true;
        }

        Vector3 GetPlayerTargetPosition()
        {
            if (player == null) return Vector3.zero;
            
            // Try to get camera position (head level)
            Camera playerCam = Camera.main;
            if (playerCam != null && playerCam.transform != null)
            {
                return playerCam.transform.position;
            }
            
            // Fallback: use player position with offset for head height
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                // Add half the controller height to get approximate head position
                return player.transform.position + Vector3.up * (controller.height * 0.5f);
            }
            
            // Last resort: just use player position
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
                }
            }
            else if (distanceToPlayer > maxRange)
            {
                // Player beyond max range, show beam up to max range
                endPos = startPos + direction * maxRange;
            }

            beamRenderer.SetPosition(0, startPos);
            beamRenderer.SetPosition(1, endPos);
        }
    }
}
