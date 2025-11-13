using UnityEngine;

/// <summary>
/// Component that defines physics properties for surfaces.
/// Can make surfaces bouncy, slippery, or destructive.
/// Bouncy and sliding affect all objects via PhysicsMaterial.
/// Destructive surfaces only affect interactables (cubes, radios, etc).
/// </summary>
[RequireComponent(typeof(Collider))]
public class SurfacePhysics : MonoBehaviour
{
    public enum SurfaceType
    {
        Normal,      // Standard physics
        Bouncy,      // High bounce coefficient
        Sliding,     // Low friction (slippery)
        Destructive  // Destroys cubes on contact
    }

    [Header("Surface Type")]
    [Tooltip("Type of surface physics behavior")]
    public SurfaceType surfaceType = SurfaceType.Normal;

    [Header("Bouncy Surface Settings")]
    [Tooltip("Bounce coefficient (0 = no bounce, 1 = perfect bounce, >1 = super bounce)")]
    [Range(0f, 2f)]
    public float bounceCoefficient = 0.8f;

    [Tooltip("Bounce combine mode")]
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Maximum;

    [Header("Sliding Surface Settings")]
    [Tooltip("Friction coefficient (0 = no friction, 1 = high friction)")]
    [Range(0f, 1f)]
    public float frictionCoefficient = 0.1f;

    [Tooltip("Friction combine mode")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Minimum;

    [Header("Destructive Surface Settings")]
    [Tooltip("Minimum impact velocity required to destroy cube (0 = any contact destroys)")]
    public float minDestroyVelocity = 0.1f;

    [Tooltip("Destroy cubes even if they're being held")]
    public bool destroyHeldCubes = false;

    [Header("Interactable Filter")]
    [Tooltip("Which types of interactables this surface affects")]
    public bool affectRadios = true;
    
    [Tooltip("Affect simple interactables (cubes, etc.)")]
    public bool affectSimpleInteractables = true;
    
    [Tooltip("Affect other types of interactables")]
    public bool affectOtherInteractables = true;

    [Tooltip("Particle effect to spawn when cube is destroyed (optional)")]
    public GameObject destroyEffectPrefab;

    [Tooltip("Sound effect to play when cube is destroyed (optional)")]
    public AudioClip destroySound;

    private PhysicsMaterial _physicMaterial;
    private Collider _collider;
    private AudioSource _audioSource;

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            Debug.LogError($"[SurfacePhysics] {gameObject.name} requires a Collider component!");
            enabled = false;
            return;
        }

        // Create or get PhysicMaterial
        _physicMaterial = _collider.material;
        if (_physicMaterial == null)
        {
            _physicMaterial = new PhysicsMaterial($"{gameObject.name}_PhysicsMaterial");
            _collider.material = _physicMaterial;
        }

        // Setup audio source if destroy sound is provided
        if (destroySound != null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f; // 3D sound
            }
        }

        // Apply physics material settings based on surface type
        ApplyPhysicsMaterial();
    }

    void OnValidate()
    {
        // Update physics material when values change in editor
        if (Application.isPlaying && _physicMaterial != null)
        {
            ApplyPhysicsMaterial();
        }
    }

    void ApplyPhysicsMaterial()
    {
        if (_physicMaterial == null) return;

        switch (surfaceType)
        {
            case SurfaceType.Bouncy:
                _physicMaterial.bounciness = bounceCoefficient;
                _physicMaterial.bounceCombine = bounceCombine;
                _physicMaterial.staticFriction = 0.6f; // Normal friction
                _physicMaterial.dynamicFriction = 0.6f;
                _physicMaterial.frictionCombine = PhysicsMaterialCombine.Average;
                break;

            case SurfaceType.Sliding:
                _physicMaterial.bounciness = 0f; // No bounce
                _physicMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
                _physicMaterial.staticFriction = frictionCoefficient;
                _physicMaterial.dynamicFriction = frictionCoefficient;
                _physicMaterial.frictionCombine = frictionCombine;
                break;

            case SurfaceType.Destructive:
                // Destructive surfaces can still have physics properties
                _physicMaterial.bounciness = 0f;
                _physicMaterial.staticFriction = 0.6f;
                _physicMaterial.dynamicFriction = 0.6f;
                break;

            case SurfaceType.Normal:
            default:
                // Default Unity physics material values
                _physicMaterial.bounciness = 0f;
                _physicMaterial.staticFriction = 0.6f;
                _physicMaterial.dynamicFriction = 0.6f;
                _physicMaterial.bounceCombine = PhysicsMaterialCombine.Average;
                _physicMaterial.frictionCombine = PhysicsMaterialCombine.Average;
                break;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only handle destructive surfaces in collision
        if (surfaceType != SurfaceType.Destructive) return;

        GameObject otherObject = collision.gameObject;
        
        // Only affect interactables (cubes, radios, etc.)
        var interactable = otherObject.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            return;
        }

        // Check if this interactable type is filtered
        if (!ShouldAffectInteractable(interactable))
        {
            return;
        }

        // Check if cube is being held
        if (interactable.IsHeld && !destroyHeldCubes)
        {
            return; // Don't destroy held cubes unless explicitly allowed
        }

        // Check impact velocity
        float impactVelocity = collision.relativeVelocity.magnitude;
        if (impactVelocity < minDestroyVelocity)
        {
            return; // Impact too weak
        }

        // Destroy the interactable
        DestroyCube(otherObject, collision.contacts[0].point);
    }

    void OnTriggerEnter(Collider other)
    {
        // Only handle destructive surfaces in trigger
        if (surfaceType != SurfaceType.Destructive) return;

        GameObject otherObject = other.gameObject;
        
        // Only affect interactables (cubes, radios, etc.)
        var interactable = otherObject.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            return;
        }

        // Check if this interactable type is filtered
        if (!ShouldAffectInteractable(interactable))
        {
            return;
        }

        // Check if cube is being held
        if (interactable.IsHeld && !destroyHeldCubes)
        {
            return; // Don't destroy held cubes unless explicitly allowed
        }

        // For triggers, check velocity from Rigidbody if available
        if (minDestroyVelocity > 0f)
        {
            Rigidbody rb = otherObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float velocity = rb.linearVelocity.magnitude;
                if (velocity < minDestroyVelocity)
                {
                    return; // Velocity too low
                }
            }
            else
            {
                // No rigidbody, can't check velocity - skip if minDestroyVelocity > 0
                return;
            }
        }

        // Destroy the interactable (use the collider's position as contact point)
        DestroyCube(otherObject, other.bounds.center);
    }

    /// <summary>
    /// Destroys a cube with optional effects
    /// </summary>
    void DestroyCube(GameObject cube, Vector3 contactPoint)
    {
        Debug.Log($"[SurfacePhysics] Destroying cube {cube.name} on {gameObject.name}");

        // Check if this is a Radio and notify it before destroying
        var radio = cube.GetComponent<Radio>();
        if (radio != null)
        {
            radio.OnDestroyed();
        }

        // Play sound effect
        if (_audioSource != null && destroySound != null)
        {
            _audioSource.PlayOneShot(destroySound);
        }

        // Spawn particle effect
        if (destroyEffectPrefab != null)
        {
            GameObject effect = Instantiate(destroyEffectPrefab, contactPoint, Quaternion.identity);
            // Auto-destroy effect after 5 seconds if it doesn't destroy itself
            Destroy(effect, 5f);
        }

        // Destroy the cube
        Destroy(cube);
    }

    /// <summary>
    /// Checks if this surface should affect the given interactable based on filter settings
    /// </summary>
    bool ShouldAffectInteractable(InteractableObject interactable)
    {
        InteractableObject.InteractableType type = interactable.GetInteractableType();
        
        switch (type)
        {
            case InteractableObject.InteractableType.Radio:
                return affectRadios;
            case InteractableObject.InteractableType.SimpleInteractable:
                return affectSimpleInteractables;
            case InteractableObject.InteractableType.Other:
                return affectOtherInteractables;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the surface type (useful for other scripts)
    /// </summary>
    public SurfaceType GetSurfaceType()
    {
        return surfaceType;
    }
}
