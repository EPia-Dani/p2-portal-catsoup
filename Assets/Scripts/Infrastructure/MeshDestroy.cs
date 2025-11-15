using UnityEngine;

namespace Infrastructure
{
    /// <summary>
    /// Attach this to a wall (Collider) to break objects that hit it.
    /// If the colliding object (or one of its parents) has a `BreakableObject` component,
    /// this component will call `Break(...)` on it. If there is no fractured prefab assigned it will fall back to a runtime fragment generator.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshDestroy : MonoBehaviour
    {
        [Tooltip("If true the wall collider is treated as a trigger and uses OnTriggerEnter. Otherwise OnCollisionEnter is used.")]
        public bool useTrigger = true;

        [Tooltip("Minimum impact speed required to break the object. Set to 0 to break on any contact.")]
        public float breakVelocityThreshold = 1f;

        [Tooltip("Optional tag filter. Leave empty to affect any object that has a BreakableObject component.")]
        public string targetTag = "";

        [Tooltip("If true, only objects that have a BreakableObject component (on self or parent) will be broken.")]
        public bool requireBreakableComponent = true;

        [Header("Debug")]
        public bool debugLogs = false;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = useTrigger;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!useTrigger) return;
            HandleImpact(other, other.attachedRigidbody, other.ClosestPoint(transform.position));
        }

        void OnCollisionEnter(Collision collision)
        {
            if (useTrigger) return;
            // use the first contact point if available
            Vector3 contactPoint = collision.contacts != null && collision.contacts.Length > 0
                ? collision.contacts[0].point
                : collision.collider.ClosestPoint(transform.position);
            HandleImpact(collision.collider, collision.rigidbody, contactPoint);
        }

        void HandleImpact(Collider other, Rigidbody otherRb, Vector3 impactPoint)
        {
            if (other == null) return;

            if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
            {
                if (debugLogs) Debug.Log($"MeshDestroy: Collider '{other.name}' ignored due to tag filter.");
                return;
            }

            // Check velocity threshold (try Rigidbody.linearVelocity magnitude if available, otherwise fall back to velocity)
            float incomingSpeed = 0f;
            if (otherRb != null)
            {
                try
                {
                    incomingSpeed = otherRb.linearVelocity.magnitude;
                }
                catch
                {
                    incomingSpeed = otherRb.linearVelocity.magnitude;
                }
            }

            if (incomingSpeed < breakVelocityThreshold)
            {
                if (breakVelocityThreshold > 0f && debugLogs)
                    Debug.Log($"MeshDestroy: Ignored '{other.name}' due to speed {incomingSpeed:F2} < threshold {breakVelocityThreshold:F2}.");
                return;
            }

            // Find BreakableObject component on the hit collider or its parents
            var breakable = other.GetComponentInParent<BreakableObject>();
            if (breakable == null)
            {
                if (requireBreakableComponent)
                {
                    if (debugLogs) Debug.Log($"MeshDestroy: No BreakableObject found on '{other.name}' (or parents) - skipping.");
                    return;
                }
                else
                {
                    if (debugLogs) Debug.Log($"MeshDestroy: No BreakableObject found for '{other.name}', but requireBreakableComponent==false. Nothing to call.");
                    return;
                }
            }

            if (debugLogs) Debug.Log($"MeshDestroy: Breaking object '{breakable.gameObject.name}' at {impactPoint} (incoming speed={incomingSpeed:F2}).");
            // Call Break on the breakable object and pass impact info
            Vector3 incomingVelocity = Vector3.zero;
            if (otherRb != null)
            {
                try
                {
                    incomingVelocity = otherRb.linearVelocity;
                }
                catch
                {
                    incomingVelocity = otherRb.linearVelocity;
                }
            }
            breakable.Break(impactPoint, incomingVelocity);
        }
    }
}
