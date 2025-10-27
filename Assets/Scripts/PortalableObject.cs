using UnityEngine;

/// <summary>
/// Marca un objeto como "portalable" - simplifica agregar la funcionalidad de portal a objetos.
/// Este script simplemente asegura que el objeto tenga un PortalTraveller.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PortalableObject : MonoBehaviour
{
	private void Awake()
	{
		// Asegurar que el objeto tenga un PortalTraveller
		if (!GetComponent<PortalTraveller>())
		{
			gameObject.AddComponent<PortalTraveller>();
		}
	}
}

