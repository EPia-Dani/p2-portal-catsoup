using UnityEngine;

/// <summary>
/// Provides static access to the main camera.
/// More efficient than Camera.main which uses FindGameObjectWithTag internally.
/// </summary>
public static class CameraManager
{
    private static Camera _mainCamera;

    /// <summary>
    /// Gets the main camera. Caches the result for performance.
    /// </summary>
    public static Camera MainCamera
    {
        get
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }
            return _mainCamera;
        }
    }

    /// <summary>
    /// Manually set the main camera if needed (e.g., during scene transitions).
    /// </summary>
    public static void SetMainCamera(Camera camera)
    {
        _mainCamera = camera;
    }

    /// <summary>
    /// Clear the cached camera reference (e.g., when changing scenes).
    /// </summary>
    public static void Clear()
    {
        _mainCamera = null;
    }
}

