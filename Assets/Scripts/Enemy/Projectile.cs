using UnityEngine;

namespace Enemy
{
    public class Projectile : MonoBehaviour
    {
        public float lifeTime = 5f;
        public float damage = 10f;
        public GameObject impactEffect;

        void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        void OnCollisionEnter(Collision collision)
        {
            // spawn effect at contact point
            if (impactEffect != null && collision.contacts != null && collision.contacts.Length > 0)
            {
                var contact = collision.contacts[0];
                var inst = Instantiate(impactEffect, contact.point, Quaternion.LookRotation(contact.normal));
                // force any particle systems in the instantiated impact effect to be white
                var ps = inst.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var p in ps)
                {
                    var main = p.main;
                    main.startColor = Color.white;
                }
            }

            // try to apply damage via SendMessage to avoid a hard dependency on a specific Health type
            // First, try the collided object itself
            collision.collider.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            // Also attempt to find an FPSController on the object or its parents and call TakeDamage directly
            var fps = collision.collider.gameObject.GetComponentInParent<FPSController>();
            if (fps != null)
            {
                fps.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
