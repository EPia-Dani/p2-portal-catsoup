
using UnityEngine;
using System.Collections.Generic;

namespace RefractionCubes
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserEmitter : MonoBehaviour
    {
        public LineRenderer line;
        public float maxDistance = 128f;
        public int maxReflections = 128;
        public bool checkForIntruders = false;

        [Header("State")] private readonly List<Vector3> _linePoints = new();
        private PlayerManager _playerManager;

        private void Update()
        {
            _linePoints.Clear();
            _linePoints.Add(transform.position);
            ShootLaser(transform.position, transform.forward, maxReflections);
            line.positionCount = _linePoints.Count;
            line.SetPositions(_linePoints.ToArray());
        }

        void ShootLaser(Vector3 position, Vector3 direction, int reflectionsRemaining)
        {
            if (reflectionsRemaining <= 0) return;

            reflectionsRemaining--;
            direction = direction.normalized;
            if (!Physics.Raycast(position, direction, out RaycastHit hit, maxDistance))
            {
                _linePoints.Add(position+direction*maxDistance);
                return;
            }
            _linePoints.Add(hit.point);
            var target = hit.collider.gameObject;
            if(checkForIntruders && IsObjectIntruder(target))
            {
                _playerManager?.OnPlayerDeath();
                Debug.Log("Object is not a refraction cube: " + target.name);
                return;
            }

            if (IsObjectMirror(target))
            {
                var reflectDir = Vector3.Reflect(direction, hit.normal);
                ShootLaser(hit.point, reflectDir, reflectionsRemaining);
            }
        }
        bool IsObjectMirror(GameObject obj)
        {
            if(!IsRefractionCube(obj)) return false;
            return obj.CompareTag("Interactable");
        }
        bool IsObjectIntruder(GameObject obj)
        {
            return obj.CompareTag("Player");
        }
        
        bool IsRefractionCube(GameObject obj)
        {
            var cube = obj.GetComponent<InteractableObject>() ?? obj.GetComponentInParent<InteractableObject>();
            if (cube == null) return false;
            return cube.isRefractionCube;
        }
    }
}