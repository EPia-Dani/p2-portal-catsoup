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
                Instantiate(impactEffect, contact.point, Quaternion.LookRotation(contact.normal));
            }

            // try to apply damage via SendMessage to avoid a hard dependency on a specific Health type
            collision.collider.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            Destroy(gameObject);
        }
    }
}
