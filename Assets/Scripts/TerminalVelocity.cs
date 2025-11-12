using UnityEngine;

/// <summary>
/// Limits the velocity of Rigidbody objects to prevent physics from breaking at high speeds.
/// Attach this component to any object with a Rigidbody that needs terminal velocity limiting.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TerminalVelocity : MonoBehaviour
{
    [Header("Terminal Velocity Settings")]
    [Tooltip("Maximum velocity magnitude. Objects exceeding this speed will be clamped.")]
    public float maxVelocity = 50f;
    
    [Tooltip("Maximum angular velocity magnitude. Prevents objects from spinning too fast.")]
    public float maxAngularVelocity = 50f;

    private Rigidbody _rigidbody;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            Debug.LogWarning($"[TerminalVelocity] No Rigidbody found on {gameObject.name}. Component disabled.");
            enabled = false;
        }
    }

    void FixedUpdate()
    {
        if (_rigidbody == null) return;

        // Clamp linear velocity
        if (_rigidbody.linearVelocity.magnitude > maxVelocity)
        {
            _rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * maxVelocity;
        }

        // Clamp angular velocity
        if (_rigidbody.angularVelocity.magnitude > maxAngularVelocity)
        {
            _rigidbody.angularVelocity = _rigidbody.angularVelocity.normalized * maxAngularVelocity;
        }
    }
}










